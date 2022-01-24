using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedFoods
{
    public class BlockBottle : BlockBucket, IContainedMeshSource
    {
        public override float CapacityLitres => Attributes?["capacityLitres"]?.AsFloat(1f) ?? 1f;
        protected override string meshRefsCacheKey => "meshrefs";
        protected override AssetLocation emptyShapeLoc => new AssetLocationAndSource("expandedfoods:glassbottle.json"); // doesn't work... try the shape too..
        protected override AssetLocation contentShapeLoc => new AssetLocationAndSource("expandedfoods:glassbottle.json"); // doesn't work... try the shape too.

        public new MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = null;
            MeshData bucketmesh = null;


            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);

                BottleTextureSource contentSource = new BottleTextureSource(capi, contentStack, props.Texture, this);

                float level = contentStack.StackSize / props.ItemsPerLitre;

                if (level <= 0.25f)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottle-1.json").ToObject<Shape>();
                }
                else if (level <= 0.5f)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottle-2.json").ToObject<Shape>();
                }
                else if (level < 1)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottle-3.json").ToObject<Shape>();
                }
                else
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottle.json").ToObject<Shape>();
                }

                capi.Tesselator.TesselateShape("bucket", shape, out bucketmesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }


            return bucketmesh;
        }

        public MeshData GenMeshSideways(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = null;
            MeshData bucketmesh = null;


            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);

                BottleTextureSource contentSource = new BottleTextureSource(capi, contentStack, props.Texture, this);

                float level = contentStack.StackSize / props.ItemsPerLitre;

                if (level <= 0.25f)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottleracked-1.json").ToObject<Shape>();
                }
                else if (level <= 0.5f)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottleracked-2.json").ToObject<Shape>();
                }
                else if (level < 1)
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottleracked-3.json").ToObject<Shape>();
                }
                else
                {
                    shape = capi.Assets.TryGet("expandedfoods:shapes/block/glassbottle.json").ToObject<Shape>();
                }

                capi.Tesselator.TesselateShape("bucket", shape, out bucketmesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }

            return bucketmesh;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Code.Path.Contains("clay")) return;
            Dictionary<string, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue("bottleMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            }
            else
            {
                capi.ObjectCache["bottleMeshRefs"] = meshrefs = new Dictionary<string, MeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize, out meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                //meshdata.Rgba2 = null;


                meshrefs[contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize] = meshRef = capi.Render.UploadMesh(meshdata);

            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("bottleMeshRefs", out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("bottleMeshRefs");
            }
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null && !byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

            ItemStack content = GetContent(itemslot.Itemstack);

            if (content != null && content.Collectible.GetNutritionProperties(byEntity.World, content, byEntity as Entity) != null)
            {
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer player = null;
                        if (byEntity is EntityPlayer) player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                        byEntity.PlayEntitySound("drink", player);
                    }
                }, 500);

                byEntity.AnimManager?.StartAnimation("eat");

                handHandling = EnumHandHandling.PreventDefault;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            ItemStack content = GetContent(slot.Itemstack);
            if (content == null || content.Collectible.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity as Entity) == null || (blockSel != null && !byEntity.Controls.Sneak)) return false;



            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.X += byEntity.LocalEyePos.X;
            pos.Y += byEntity.LocalEyePos.Y - 0.4f;
            pos.Z += byEntity.LocalEyePos.Z;
            //pos.Y += byEntity.EyeHeight - 0.4f;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                byEntity.World.SpawnCubeParticles(pos, content, 0.3f, 4, 0.5f, (byEntity as EntityPlayer)?.Player);
            }


            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();

                tf.EnsureDefaultValues();

                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y = Math.Min(0.02f, GameMath.Sin(20 * secondsUsed) / 10);
                }

                tf.Translation.X -= Math.Min(1f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.05f, secondsUsed * 2);

                tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(80f, secondsUsed * 350);

                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                return secondsUsed <= 1f;
            }

            // Let the client decide when he is done eating
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            ItemStack content = GetContent(slot.Itemstack);
            FoodNutritionProperties nutriProps = content?.Collectible.GetNutritionProperties(byEntity.World, content, byEntity as Entity);

            if (byEntity.World is IServerWorldAccessor && nutriProps != null && secondsUsed >= 0.95f)
            {
                DummySlot dummy = new DummySlot(content);
                content.Collectible.OnHeldInteractStop(secondsUsed, dummy, byEntity, blockSel, entitySel);
                SetContent(slot.Itemstack, dummy.StackSize > 0 ? dummy.Itemstack : null);
                slot.MarkDirty();
                (byEntity as EntityPlayer)?.Player.InventoryManager.BroadcastHotbarSlot();
            }
        }

        public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
        {
            if (transType == EnumTransitionType.Perish) return Attributes["perishRate"].AsFloat(1);
            return Attributes["cureRate"].AsFloat(1);
        }

        public override float GetContainingTransitionModifierPlaced(IWorldAccessor world, BlockPos pos, EnumTransitionType transType)
        {
            if (transType == EnumTransitionType.Perish) return Attributes["perishRate"].AsFloat(1);
            return Attributes["cureRate"].AsFloat(1);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            ItemStack content = GetContent(inSlot.Itemstack);
            if (content != null)
            {
                DummySlot dummy = new DummySlot(content);
                dsc.AppendLine();
                dsc.AppendLine(Lang.Get("expandedfoods:Liquid Info:"));
                content.Collectible.GetHeldItemInfo(dummy, dsc, world, withDebugInfo);
            }
        }

        public void GetShelfInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            dsc.Append(GetHeldItemName(inSlot.Itemstack));
            dsc.Append(", ");
            ItemStack content = GetContent(inSlot.Itemstack);
            if (content != null)
            {
                DummyInventory dummyInv = new DummyInventory(api);
                DummySlot dummy = new DummySlot(content, dummyInv);
                dummyInv.OnAcquireTransitionSpeed = (transType, stack, mul) =>
                {
                    return mul * GetContainingTransitionModifierContained(world, inSlot, transType) * inSlot.Inventory.GetTransitionSpeedMul(transType, stack);
                };

                dsc.AppendLine(PerishableInfoCompactShelf(world.Api, dummy, 1, true));
            }
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
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

            return true;
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            if (!entityItem.Swimming || entityItem.World.Side != EnumAppSide.Server) return;

            ItemStack contents = GetContent(entityItem.Itemstack);
            if (contents != null && contents.Collectible.Code.Path == "rot")
            {
                entityItem.World.SpawnItemEntity(contents, entityItem.ServerPos.XYZ);
                SetContent(entityItem.Itemstack, null);
            }
        }

        public string PerishableInfoCompactShelf(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(Lang.GetIfExists("incontainer-item-" + contentSlot.Itemstack.Collectible.Code.Path) ?? contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    string comma = ", ";

                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:


                            if (transitionLevel > 0)
                            {
                                dsc.Append(comma + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:

                            if (transitionLevel > 0)
                            {
                                dsc.Append(comma + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }

            }

            return dsc.ToString();
        }
    }

    public class BlockEntityBottle : BlockEntityContainer
    {
        public override InventoryBase Inventory => inv;
        InventoryGeneric inv;
        public override string InventoryClassName => "bottle";

        public BlockEntityBottle()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        BlockBottle ownBlock;
        MeshData currentMesh;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);


            ownBlock = Block as BlockBottle;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        internal MeshData GenMesh()
        {
            if (ownBlock == null || ownBlock.Code.Path.Contains("clay")) return null;

            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            return mesh;
        }

        public ItemStack GetContent()
        {
            return inv[0].Itemstack;
        }


        internal void SetContent(ItemStack stack)
        {
            inv[0].Itemstack = stack;
            MarkDirty(true);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null || ownBlock.Code.Path.Contains("clay")) return false;
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, 0));
            return true;
        }

        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            float mul = base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
            mul *= ownBlock?.GetContainingTransitionModifierPlaced(Api.World, Pos, transType) ?? 1;
            return mul;
        }

    }


    public class BottleTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private ICoreClientAPI capi;

        TextureAtlasPosition contentTextPos;
        TextureAtlasPosition blockTextPos;
        TextureAtlasPosition corkTextPos;
        CompositeTexture contentTexture;

        public BottleTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block bottle)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "map");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(bottle, "glass");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "map" && corkTextPos != null) return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null) return blockTextPos;
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(capi, "contenttexture-" + contentTexture.ToString(), () =>
                    {
                        TextureAtlasPosition texPos;
                        int id = 0;

                        BitmapRef bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                            bmp.Dispose();
                        }

                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }

}