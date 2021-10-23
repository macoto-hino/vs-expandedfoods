using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ExpandedFoods
{
    public class BlockBottleRack : Block
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityBottleRack bebottlerack = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBottleRack;
            if (bebottlerack != null) return bebottlerack.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}