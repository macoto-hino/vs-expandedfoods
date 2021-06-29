using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedFoods
{
    public class BlockScrewPress : BlockLiquidContainerBase
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "screwPressHelp", () =>
            {
                List<ItemStack> squeezables = new List<ItemStack>();
                List<ItemStack> liquid = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj == null) continue;
                    if (obj.Attributes != null && obj.Attributes["squeezeInto"].Exists)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) squeezables.AddRange(stacks);
                    }

                    if (((obj is BlockBowl) && obj.LastCodePart() == "burned") || obj is BlockBucket)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) liquid.AddRange(stacks);
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-screwpress-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = squeezables.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-screwpress-shiftrightclick",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        Itemstacks = squeezables.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-screwpress-liquid",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = liquid.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-screwpress-crush",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sneak",
                        Itemstacks = null
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "expandedfoods:blockhelp-screwpress-remove",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCodes = new string[] {"sneak", "sprint"}
                    }
                };
            });
        }

        public override int GetContainerSlotId(IWorldAccessor world, BlockPos pos)
        {
            return 1;
        }

        public override int GetContainerSlotId(IWorldAccessor world, ItemStack containerStack)
        {
            if (!(containerStack?.Collectible is BlockScrewPress)) return 0;
            return 1;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { new ItemStack(this) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken();
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            BEScrewPress sp = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEScrewPress;
            if (sp == null) return false;

            if (singlePut && singleTake) { sp.dropLeftovers(); return true; }

            if (singleTake) { sp.usePress(byPlayer); return true; }

            if (!hotbarSlot.Empty && !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) { sp.TryAdd(hotbarSlot,singlePut? 5:1); return true; }

            if (!hotbarSlot.Empty && (hotbarSlot.Itemstack.Collectible is BlockBowl) && hotbarSlot.Itemstack.Collectible.LastCodePart() != "burned") return true;

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface) || (hotbarSlot.Itemstack.Collectible is BlockBowl)) return false;


            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            



            if (obj is ILiquidSink && !singlePut && !(obj is BlockBarrel))
            {
                ItemStack owncontentStack = GetContent(world, blockSel.Position);
                int moved = 0;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = TryPutContent(world, hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                }
                else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutContent(world, containerStack, owncontentStack, singleTake ? 1 : 9999);

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
                    TryTakeContent(world, blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    if (sp != null) sp.dropLeftovers();
                    return true;
                }
            }

            return true;
        }

        public override int TryPutContent(IWorldAccessor world, ItemStack containerStack, ItemStack contentStack, int desiredItems)
        {
            if (contentStack == null) return 0;

            ItemStack stack = GetContent(world, containerStack);

            int availItems = contentStack.StackSize;

            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);
                if (props == null || !props.Containable) return 0;


                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre);

                ItemStack placedstack = contentStack.Clone();
                placedstack.StackSize = GameMath.Min(availItems, desiredItems, placeableItems);
                SetContent(containerStack, placedstack);

                return Math.Min(desiredItems, placeableItems);
            }
            else
            {
                if (!stack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                WaterTightContainableProps props = GetContentProps(world, containerStack);

                float maxItems = sink.CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - stack.StackSize);
                placeableItems = Math.Min(placeableItems, contentStack.StackSize);
                
                stack.StackSize += Math.Min(placeableItems, desiredItems);

                return Math.Min(placeableItems, desiredItems);
            }
        }

        public override void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            // Don't fill when dropped as item in water
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            dsc.AppendLine(Lang.Get("expandedfoods:Press fruit and seeds to get juice and oil."));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BEScrewPress sp = world.BlockAccessor.GetBlockEntity(pos) as BEScrewPress;
            string liqInfo = base.GetPlacedBlockInfo(world, pos, forPlayer);
            if (sp == null) return liqInfo;
            StringBuilder dsc = new StringBuilder();
            if (sp.isSqueezing)
            {
                int squeezTime = (int)(sp.squeezeUntil - world.Calendar.TotalHours);
                if (squeezTime < 0) squeezTime = 0;
                dsc.AppendLine(Lang.Get("expandedfoods:Squeezing x{0} {1} for {2} hours", sp.inputSlot.StackSize, sp.inputSlot.Itemstack.GetName(), squeezTime));
                return dsc.ToString();
            }
            else
            {
                if (sp.inputSlot.StackSize > 0 && sp.outputSlot.StackSize == 0)
                {
                    dsc.AppendLine(Lang.Get("expandedfoods:x{0} {1} inserted", sp.inputSlot.StackSize, sp.inputSlot.Itemstack.GetName()));
                    dsc.AppendLine(liqInfo);
                    return dsc.ToString();
                }
                else if (sp.inputSlot.StackSize > 0 && sp.outputSlot.StackSize > 0)
                {
                    dsc.AppendLine(Lang.Get("expandedfoods:x{0} {1} leftover", sp.inputSlot.StackSize, sp.inputSlot.Itemstack.GetName()));
                    dsc.AppendLine(liqInfo);
                    return dsc.ToString();
                }
            }

            return liqInfo;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BEScrewPress bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEScrewPress;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                    bect.GenAnim();
                }
            }

            return val;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[0];
        }
    }
}
