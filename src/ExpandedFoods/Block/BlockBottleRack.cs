    using System;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;

    public class BlockBottleRack : Block
    {

        public MeshData GenMesh(ICoreClientAPI capi, string shapePath, ITexPositionSource texture, int slot, float rot, ITesselatorAPI tesselator = null)
        {
            var y = (float)(Math.Floor(slot / 4f) / 4f) - 0.25f;
            var x = (float)(slot % 4) / 4 - 0.374f;
            var shape = capi.Assets.TryGet(shapePath + ".json").ToObject<Shape>();
            tesselator.TesselateShape(shapePath, shape, out var mesh, texture, new Vec3f(this.Shape.rotateX, this.Shape.rotateY, this.Shape.rotateZ));
            if (slot >= 0)
            {
                mesh.Translate(x, y - 0.475f, -0.375f);
                mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 1.57f, 0f, rot);
            }
            return mesh;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityBottleRack bedc)
            { bedc.OnBreak(byPlayer, pos); }
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBottleRack bedc)
            { return bedc.OnInteract(byPlayer, blockSel); }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }