using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;

namespace ExpandedFoods
{
    public class BlockSaucepan : BlockLiquidContainerBase
    {
        public override float CapacityLitres => Attributes?["capacityLitres"]?.AsFloat(5f) ?? 5f;


        static SimmerRecipe[] simmerRecipes;

        public bool isSealed;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (simmerRecipes == null)
            {
                simmerRecipes = Attributes["simmerRecipes"].AsObject<SimmerRecipe[]>();
                if (simmerRecipes != null)
                {
                    foreach(SimmerRecipe rec in simmerRecipes)
                    {
                        rec.Resolve(api.World, "saucepan");
                    }
                }
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            List<ItemStack> liquidContainerStacks = new List<ItemStack>();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj is BlockLiquidContainerTopOpened || obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                {
                    List<ItemStack> stacks = obj.GetHandBookStacks((ICoreClientAPI)api);
                    if (stacks != null) liquidContainerStacks.AddRange(stacks);
                }
            }

            return new WorldInteraction[]
                    {
                    new WorldInteraction()
                    {
                        ActionLangCode = "game:blockhelp-behavior-rightclickpickup",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = liquidContainerStacks.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-lid", // json lang file. 
                        HotKeyCodes = new string[] { "sneak", "sprint" },
                        MouseButton = EnumMouseButton.Right
                    }
            };
        }

        public override bool CanSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemStack inputStack, ItemStack outputStack)
        {
            if (outputStack != null || GetContent(inputStack) != null) return false;
            List<ItemStack> stacks = new List<ItemStack>();

            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) stacks.Add(slot.Itemstack);
            }

            if (stacks.Count <= 0) return false;
            else if (stacks.Count == 1)
            {
                //stacks[0].Collectible.CombustibleProps?.SmeltedStack?.Resolve(world, "saucepan");
                if (stacks[0].Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack == null || !stacks[0].Collectible.CombustibleProps.RequiresContainer) return false;
                return stacks[0].StackSize % stacks[0].Collectible.CombustibleProps.SmeltedRatio == 0;
            }
            else if (simmerRecipes != null)
            {
                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if (rec.Match(stacks) > 0) return true;
                }
            }
            return false;
        }

        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            if (!CanSmelt(world, cookingSlotsProvider, inputSlot.Itemstack, outputSlot.Itemstack)) return;

            List<ItemStack> contents = new List<ItemStack>();
            ItemStack product = null;

            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }

            if (contents.Count == 1)
            {
                //contents[0].Collectible.CombustibleProps.SmeltedStack.Resolve(world, "saucepan");

                product = contents[0].Collectible.CombustibleProps.SmeltedStack.ResolvedItemstack.Clone();

                product.StackSize *= (contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio);
            }
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if ((amount = rec.Match(contents)) > 0)
                    {
                        match = rec;
                        break;
                    }
                }

                if (match == null) return;

                product = match.Simmering.SmeltedStack.ResolvedItemstack.Clone();

                product.StackSize *= amount;
                
                if (product.Collectible is IExpandedFood)
                {
                    List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> input = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
                    List<ItemSlot> alreadyfound = new List<ItemSlot>();

                    foreach (CraftingRecipeIngredient ing in match.Ingredients)
                    {
                        foreach (ItemSlot slot in cookingSlotsProvider.Slots)
                        {
                            if (!alreadyfound.Contains(slot) && !slot.Empty && ing.SatisfiesAsIngredient(slot.Itemstack))
                            {
                                alreadyfound.Add(slot);
                                input.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(slot, ing));
                                break;
                            }
                        }
                    }
                    
                    (product.Collectible as IExpandedFood).OnCreatedByKneading(input, product);
                }
            }

            if (product == null) return;

            if (product.Collectible.Class == "ItemLiquidPortion" || product.Collectible is ItemExpandedLiquid || product.Collectible is ItemTransLiquid)
            {
                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }

                outputSlot.Itemstack = inputSlot.TakeOut(1);

                (outputSlot.Itemstack.Collectible as BlockLiquidContainerBase).TryPutLiquid(outputSlot.Itemstack, product, product.StackSize);

            }
            else
            {
                outputSlot.Itemstack = product;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }

            }
        }

        public override float GetMeltingDuration(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float dur = 0f;
            List<ItemStack> contents = new List<ItemStack>();
            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }
            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps != null) return contents[0].Collectible.CombustibleProps.MeltingDuration * contents[0].StackSize;
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if ((amount = rec.Match(contents)) > 0)
                    {
                        match = rec;
                        break;
                    }
                }

                if (match == null) return 0;

                return match.Simmering.MeltingDuration * amount;
            }

            return dur;
        }

        public override float GetMeltingPoint(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot)
        {
            float temp = 0f;
            List<ItemStack> contents = new List<ItemStack>();
            foreach (ItemSlot slot in cookingSlotsProvider.Slots)
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }
            if (contents.Count == 1 && contents[0].Collectible.CombustibleProps != null) return contents[0].Collectible.CombustibleProps.MeltingPoint;
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if ((amount = rec.Match(contents)) > 0)
                    {
                        match = rec;
                        break;
                    }
                }

                if (match == null) return 0;

                return match.Simmering.MeltingPoint;
            }

            return temp;
        }

        public override int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            var props = GetContainableProps(liquidStack);
            if (props == null) return 0;

            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres);
            int availItems = liquidStack.StackSize;

            ItemStack stack = GetContent(containerStack);
            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                if (!props.Containable) return 0;

                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre);

                ItemStack placedstack = liquidStack.Clone();
                placedstack.StackSize = GameMath.Min(availItems, desiredItems, placeableItems);
                SetContent(containerStack, placedstack);

                return Math.Min(desiredItems, placeableItems);
            }
            else
            {
                if (!stack.Equals(api.World, liquidStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                float maxItems = sink.CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize);

                stack.StackSize += Math.Min(placeableItems, desiredItems);

                return Math.Min(placeableItems, desiredItems);
            }
        }

        public static WaterTightContainableProps GetInContainerProps(ItemStack stack)
        {
            try
            {
                JsonObject obj = stack?.ItemAttributes?["waterTightContainerProps"];
                if (obj != null && obj.Exists) return obj.AsObject<WaterTightContainableProps>(null, stack.Collectible.Code.Domain);
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {

            BlockEntitySaucepan sp = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySaucepan;
            BlockPos pos = blockSel.Position;

            if (byPlayer.WorldData.EntityControls.Sneak && byPlayer.WorldData.EntityControls.Sprint)
            {
                if (sp != null && Attributes.IsTrue("canSeal"))
                {
                    world.PlaySoundAt(AssetLocation.Create(Attributes["lidSound"].AsString("sounds/block"), Code.Domain), pos.X + 0.5f, pos.Y + 0.5f, pos.Z + 0.5f, byPlayer);
                    sp.isSealed = !sp.isSealed;
                    sp.RedoMesh();
                    sp.MarkDirty(true);
                }

                return true;
            }

            if (sp?.isSealed == true) return false;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource && !singleTake)
            {
                int moved = TryPutLiquid(blockSel.Position, (obj as ILiquidSource).GetContent(hotbarSlot.Itemstack), singlePut ? 1 : 9999);

                if (moved > 0)
                {
                    (obj as ILiquidSource).TryTakeContent(hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    return true;
                }
            }

            if (obj is ILiquidSink && !singlePut)
            {
                ItemStack owncontentStack = GetContent(blockSel.Position);
                int moved = 0;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = (obj as ILiquidSink).TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutLiquid(containerStack, owncontentStack, singleTake ? 1 : 9999);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (itemslot.Itemstack?.Attributes.GetBool("isSealed") == true) return;

            if (blockSel == null || byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                return;
            }

            // Prevent placing on normal use
            handHandling = EnumHandHandling.PreventDefaultAction;


            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs = null;
            bool isSealed = itemstack.Attributes.GetBool("isSealed");

            object obj;
            if (capi.ObjectCache.TryGetValue((Variant["metal"]) + "MeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache[(Variant["metal"]) + "MeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            int hashcode = GetSaucepanHashCode(capi.World, contentStack, isSealed);

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                MeshData meshdata = GenRightMesh(capi, contentStack, null, isSealed);
                //meshdata.Rgba2 = null;


                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(meshdata);

            }

            renderinfo.ModelRef = meshRef;
        }

        public string GetOutputText(IWorldAccessor world, InventorySmelting inv)
        {
            List<ItemStack> contents = new List<ItemStack>();
            ItemStack product = null;

            foreach (ItemSlot slot in new ItemSlot[] { inv[3], inv[4], inv[5], inv[6]})
            {
                if (!slot.Empty) contents.Add(slot.Itemstack);
            }

            if (contents.Count == 1)
            {
                product = contents[0].Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                if (product == null) return null;

                return Lang.Get("firepit-gui-willcreate", contents[0].StackSize / contents[0].Collectible.CombustibleProps.SmeltedRatio, product.GetName());
            }
            else if (simmerRecipes != null && contents.Count > 1)
            {
                SimmerRecipe match = null;
                int amount = 0;

                foreach (SimmerRecipe rec in simmerRecipes)
                {
                    if ((amount = rec.Match(contents)) > 0)
                    {
                        match = rec;
                        break;
                    }
                }

                if (match == null) return null;

                product = match.Simmering.SmeltedStack.ResolvedItemstack;

                if (product == null) return null;

                return Lang.Get("firepit-gui-willcreate", amount, product.GetName());
            }

            return null;
        }

        public MeshData GenRightMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null, bool isSealed = false)
        {
            Shape shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/" + (isSealed && Attributes.IsTrue("canSeal") ? "lid" : "empty") + ".json").ToObject<Shape>();
            MeshData bucketmesh;
            capi.Tesselator.TesselateShape(this, shape, out bucketmesh);

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);

                ContainerTextureSource contentSource = new ContainerTextureSource(capi, contentStack, props.Texture);

                MeshData contentMesh;

                if (props.Texture == null) return null;

                //shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents.json").ToObject<Shape>();
   
                float maxLevel = Attributes["maxFillLevel"].AsFloat();
                float fullness = contentStack.StackSize / (props.ItemsPerLitre * CapacityLitres);

                #region Normal Cauldron

                if (maxLevel is 13f)
                {
                    if (fullness <= 0.2f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.4f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.4f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.6f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.6f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.8f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.8f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 1f + ".json").ToObject<Shape>();
                    }
                }

                #endregion  Normal Cauldron

                #region Small Cauldron

                if (maxLevel is 8f)
                {
                    if (fullness <= 0.2f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.1f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.4f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.6f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.3f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 0.8f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.4f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.5f + ".json").ToObject<Shape>();
                    }
                }

                #endregion Small Cauldron

                #region Saucepan

                if (maxLevel is 2f)
                {
                    if (fullness <= 0.5f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.1f + ".json").ToObject<Shape>();
                    }
                    else if (fullness <= 1f % maxLevel)
                    {
                        shape = capi.Assets.TryGet("expandedfoods:shapes/block/" + FirstCodePart() + "/contents" + "-" + 0.2f + ".json").ToObject<Shape>();
                    }
                }

                #endregion Saucepan


                capi.Tesselator.TesselateShape("saucepan", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

                if (props.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }

                for (int i = 0; i < contentMesh.Flags.Length; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }

                bucketmesh.AddMeshData(contentMesh);

                // Water flags
                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only

                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
            }


            return bucketmesh;
        }

        public int GetSaucepanHashCode(IClientWorldAccessor world, ItemStack contentStack, bool isSealed)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            if (isSealed) s += "sealed";
            return s.GetHashCode();
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack drop =  base.OnPickBlock(world, pos);

            BlockEntitySaucepan sp = world.BlockAccessor.GetBlockEntity(pos) as BlockEntitySaucepan;

            if (sp != null)
            {
                drop.Attributes.SetBool("isSealed", sp.isSealed);
            }

            return drop;
        }
    }

    public class SimmerRecipe
    {
        public CraftingRecipeIngredient[] Ingredients;

        public CombustibleProperties Simmering;

        public bool Resolve(IWorldAccessor world, string debug)
        {
            bool result = true;

            foreach (CraftingRecipeIngredient ing in Ingredients)
            {
                result &= ing.Resolve(world, debug);
            }

            result &= Simmering.SmeltedStack.Resolve(world, debug);

            return result;
        }

        public int Match(List<ItemStack> Inputs)
        {
            if (Inputs.Count != Ingredients.Length) return 0;
            List<CraftingRecipeIngredient> matched = new List<CraftingRecipeIngredient>();
            int amount = -1;

            foreach (ItemStack input in Inputs)
            {
                CraftingRecipeIngredient match = null;

                foreach (CraftingRecipeIngredient ing in Ingredients)
                {
                    if ((ing.ResolvedItemstack == null && !ing.IsWildCard) || matched.Contains(ing) || !ing.SatisfiesAsIngredient(input)) continue;
                    match = ing;
                    break;
                }

                if (match == null || input.StackSize % match.Quantity != 0 || (input.StackSize / match.Quantity) % Simmering.SmeltedRatio != 0) return 0;

                int maxAmount = (input.StackSize / match.Quantity) / Simmering.SmeltedRatio;

                if (amount == -1) amount = maxAmount;
                else if (maxAmount != amount) return 0;

                if (amount == 0) return amount;

                matched.Add(match);


            }

            return amount;
        }
    }
}
