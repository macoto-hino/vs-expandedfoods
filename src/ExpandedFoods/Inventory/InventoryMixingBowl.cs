using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ExpandedFoods
{
    /// <summary>
    /// Inventory with one input slot and one output slot and six watertight slots
    /// </summary>
    public class InventoryMixingBowl : InventoryBase, ISlotProvider
    {
        ItemSlot[] slots;
        public ItemSlot[] Slots { get { return slots; } }
        BlockEntityMixingBowl machine;

        //xskills compatibility
        public IPlayer LastModifiedBy { get; protected set; }
        public IPlayer LastAccessedBy { get; protected set; }
        public string LoadedOwner { get; protected set; }

        public InventoryMixingBowl(string inventoryID, ICoreAPI api, BlockEntityMixingBowl bowl) : base(inventoryID, api)
        {
            // slot 0 = pot
            // slot 1 = output
            //slots 2-7 = ingredients
            machine = bowl;
            slots = GenEmptySlots(8);

            LastModifiedBy = null;
            LastAccessedBy = null;
            LoadedOwner = null;
        }


        public override int Count
        {
            get { return 8; }
        }

        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId < 0 || slotId >= Count) return null;
                return slots[slotId];
            }
            set
            {
                if (slotId < 0 || slotId >= Count) throw new ArgumentOutOfRangeException(nameof(slotId));
                if (value == null) throw new ArgumentNullException(nameof(value));
                slots[slotId] = value;
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            slots = SlotsFromTreeAttributes(tree, slots);

            if (Api != null)
            {
                for (int i = 2; i < this.Count; i++)
                {
                    this[i].MaxSlotStackSize = 6;
                    (this[i] as ItemSlotMixingBowl).Set(machine, i - 2);
                }
            }

            LoadedOwner = tree.GetString("owner");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(slots, tree);

            if (LastModifiedBy != null) tree.SetString("owner", LastModifiedBy.PlayerUID);
        }

        protected override ItemSlot NewSlot(int i)
        {
            if (i == 0) return new ItemSlotPotInput(this);
            if (i == 1) return new ItemSlotWatertight(this);
            return new ItemSlotMixingBowl(this, machine, i - 2);
        }

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return slots[1];
        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            ItemSlot goingTo = base.GetAutoPushIntoSlot(atBlockFace, fromSlot);

            return goingTo == slots[1] ? slots[0] : goingTo;
        }

        public override bool CanPlayerAccess(IPlayer player, EntityPos position)
        {
            bool result = base.CanPlayerAccess(player, position);
            if (!result) return result;
            LastAccessedBy = player;
            return result;
        }

        public override void OnItemSlotModified(ItemSlot slot)
        {
            base.OnItemSlotModified(slot);
            if (LastAccessedBy == null) return;
            if (slot == this[0] || slot == this[1]) return;
            LastModifiedBy = LastAccessedBy;
        }

        public override object ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            LastAccessedBy = op.ActingPlayer;
            return base.ActivateSlot(slotId, sourceSlot, ref op);
        }
    }

    public class ItemSlotMixingBowl : ItemSlot
    {
        BlockEntityMixingBowl machine;
        int stackNum;

        public ItemSlotMixingBowl(InventoryBase inventory, BlockEntityMixingBowl bowl, int itemNumber) : base(inventory)
        {
            MaxSlotStackSize = 6;
            machine = bowl;
            stackNum = itemNumber;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return base.CanTakeFrom(sourceSlot, priority) && locked(sourceSlot);
        }

        public override bool CanHold(ItemSlot sourceSlot)
        {
            return base.CanHold(sourceSlot) && locked(sourceSlot);
        }

        public override bool CanTake()
        {
            bool isLiquid = !Empty && itemstack.Collectible.IsLiquid();
            if (isLiquid) return false;

            return base.CanTake();
        }

        protected override void ActivateSlotLeftClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;

            if (bucketblock != null)
            {
                ItemStack bucketContents = bucketblock.GetContent(sourceSlot.Itemstack);
                bool stackable = !Empty && itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes) && StackSize < MaxSlotStackSize;

                if ((Empty || stackable) && bucketContents != null && !machine.invLocked)
                {
                    ItemStack bucketStack = sourceSlot.Itemstack;
                    ItemStack takenContent = bucketblock.TryTakeContent(bucketStack, op.ActingPlayer?.Entity?.Controls.Sneak == true ? MaxSlotStackSize - StackSize : 1);
                    sourceSlot.Itemstack = bucketStack;
                    takenContent.StackSize += StackSize;
                    this.itemstack = takenContent;
                    MarkDirty();
                    return;
                }

                return;
            }

            string contentItemCode = sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString();
            if (contentItemCode != null && !machine.invLocked)
            {
                ItemStack contentStack = new ItemStack(world.GetItem(AssetLocation.Create(contentItemCode, sourceSlot.Itemstack.Collectible.Code.Domain)));
                bool stackable = !Empty && itemstack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes);

                if ((Empty || stackable) && contentStack != null)
                {
                    if (stackable) this.itemstack.StackSize++;
                    else this.itemstack = contentStack;

                    MarkDirty();
                    ItemStack bowlStack = new ItemStack(world.GetBlock(AssetLocation.Create(sourceSlot.Itemstack.ItemAttributes["emptiedBlockCode"].AsString(), sourceSlot.Itemstack.Collectible.Code.Domain)));
                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = bowlStack;
                    }
                    else
                    {
                        sourceSlot.Itemstack.StackSize--;
                        if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(bowlStack))
                        {
                            world.SpawnItemEntity(bowlStack, op.ActingPlayer.Entity.Pos.XYZ);
                        }
                    }
                    sourceSlot.MarkDirty();
                }

                return;
            }

            if (sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true) return;

            base.ActivateSlotLeftClick(sourceSlot, ref op);
        }

        protected override void ActivateSlotRightClick(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            IWorldAccessor world = inventory.Api.World;
            BlockBucket bucketblock = sourceSlot.Itemstack?.Block as BlockBucket;

            if (bucketblock != null)
            {
                if (Empty) return;

                ItemStack bucketContents = bucketblock.GetContent(sourceSlot.Itemstack);

                if (bucketContents == null)
                {
                    TakeOut(bucketblock.TryPutLiquid(sourceSlot.Itemstack, Itemstack, 1));
                    MarkDirty();
                }
                else
                {
                    if (itemstack.Equals(world, bucketContents, GlobalConstants.IgnoredStackAttributes))
                    {
                        TakeOut(bucketblock.TryPutLiquid(sourceSlot.Itemstack, bucketblock.GetContent(sourceSlot.Itemstack), 1));
                        MarkDirty();
                        return;
                    }
                }

                return;
            }


            if (itemstack != null && sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true)
            {
                string outBlockCode = sourceSlot.Itemstack.ItemAttributes["contentItem2BlockCodes"][itemstack.Collectible.Code.ToShortString()].AsString();

                if (outBlockCode != null)
                {
                    ItemStack outBlockStack = new ItemStack(world.GetBlock(AssetLocation.Create(outBlockCode, sourceSlot.Itemstack.Collectible.Code.Domain)));

                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = outBlockStack;
                    }
                    else
                    {
                        sourceSlot.Itemstack.StackSize--;
                        if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(outBlockStack))
                        {
                            world.SpawnItemEntity(outBlockStack, op.ActingPlayer.Entity.Pos.XYZ);
                        }
                    }

                    sourceSlot.MarkDirty();
                    TakeOut(1);
                }

                return;
            }

            if (sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true || sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString() != null) return;

            base.ActivateSlotRightClick(sourceSlot, ref op);
        }

        bool locked(ItemSlot sourceSlot)
        {
            if (!machine.invLocked) return true;

            ItemStack stack = machine.lockedInv[stackNum];
            if (stack == null) return false;

            return stack.Equals(machine.Api.World, sourceSlot.Itemstack, GlobalConstants.IgnoredStackAttributes);
        }

        public void Set(BlockEntityMixingBowl bowl, int num)
        {
            machine = bowl;
            stackNum = num;
        }
    }

    public class ItemSlotPotInput : ItemSlot
    {

        public ItemSlotPotInput(InventoryBase inventory) : base(inventory)
        {
        }

        public override bool CanHold(ItemSlot slot)
        {
            return slot.Itemstack?.Collectible is BlockCookingContainer;
        }

        public override bool CanTake()
        {
            return true;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return sourceSlot.Itemstack?.Collectible is BlockCookingContainer && base.CanTakeFrom(sourceSlot, priority);
        }
    }
}
