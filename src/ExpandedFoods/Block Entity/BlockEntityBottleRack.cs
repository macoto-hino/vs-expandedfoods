using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
//using System.Diagnostics;

namespace ExpandedFoods
{
    public class BlockEntityBottleRack : BlockEntityShelf
    {
        private readonly int maxSlots = 16;
        public override string InventoryClassName => "bottlerack";
        protected InventoryGeneric inventory;

        public override InventoryBase Inventory => this.inventory;

        public BlockEntityBottleRack()
        {
            this.inventory = new InventoryGeneric(this.maxSlots, null, null);
            this.meshes = new MeshData[this.maxSlots];
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            var playerSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (playerSlot.Empty)
            {
                if (this.TryTake(byPlayer, blockSel))
                { return true; }
                return false;
            }
            else
            {
                var colObj = playerSlot.Itemstack.Collectible;
                if (colObj.Attributes != null)
                {
                    if (colObj.Code.Path.StartsWith("bottle-"))
                    {
                        if (this.TryPut(playerSlot, blockSel))
                        {
                            var sound = this.Block?.Sounds?.Place;
                            this.Api.World.PlaySoundAt(sound ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                            return true; 
                        }
                    }
                    return false;
                }
            }
            return false;
        }

        internal void OnBreak(IPlayer byPlayer, BlockPos pos)
        {
            for (var index = 15; index >= 0; index--)
            {
                if (!this.inventory[index].Empty)
                {
                    var stack = this.inventory[index].TakeOut(1);
                    if (stack.StackSize > 0)
                    { this.Api.World.SpawnItemEntity(stack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5)); }
                    this.MarkDirty(true);
                }
            }
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;
            if (this.inventory[index].Empty)
            {
                var moved = slot.TryPutInto(this.Api.World, this.inventory[index]);
                if (moved > 0)
                {
                    this.updateMesh(index);
                    this.MarkDirty(true);
                }
                return moved > 0;
            }
            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            var index = blockSel.SelectionBoxIndex;
            if (!this.inventory[index].Empty)
            {
                var stack = this.inventory[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    var sound = stack.Block?.Sounds?.Place;
                    this.Api.World.PlaySoundAt(sound ?? new AssetLocation("game:sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }
                if (stack.StackSize > 0)
                {
                    this.Api.World.SpawnItemEntity(stack, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                this.updateMesh(index);
                this.MarkDirty(true);
                return true;
            }
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);
            if (this.Api != null)
            {
                if (this.Api.Side == EnumAppSide.Client)
                { this.Api.World.BlockAccessor.MarkBlockDirty(this.Pos); }
            }
        }

        public MeshData TransformBottleMesh(MeshData mesh, int slot, string type, string direction)
        {
            var rot = 0f; //north in radians
            switch (direction)
            {
                case "east": rot = 1.57f; break;
                case "south": rot = 3.14f; break;
                case "west": rot = 4.71f; break;
            }
            double col = slot % 4;
            var x = (float)col / 4 - 0.38f;
            var y = (float)(Math.Floor(slot / 4f) / 4f) - 0.3f;
            mesh.Translate(x, y - 0.5f, -0.42f);
            if (type == "bottlerackcorner")
            {
                if (col == 1 || col == 2)
                { mesh.Translate(0f, 0.2f, 0.2f); }
            }
            
            mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 1.57f, 0f, rot);
            mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.99f, 0.99f, 0.99f);
            if (type == "bottlerackcorner")
            { 
                if ( col == 1 || col == 2) 
                {
                    mesh.Translate(0f, 0.2f, 0f);
                    mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 0f, -0.785f, 0f); 
                }
                else if (col == 3) //far right column
                { mesh.Rotate(new Vec3f(0.5f, y, 0.5f), 0f, -1.57f, 0f); }
            }
            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh;
            var shapeBase = "expandedfoods:shapes/";
            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos) as BlockBottleRack;
            mesh = capi.TesselatorManager.GetDefaultBlockMesh(block); //add bottle rack
            mesher.AddMeshData(mesh);
            for (var i = 0; i <= 15; i++)
            {
                if (!this.inventory[i].Empty)
                {
                    var blockPath = this.inventory[i].Itemstack.Block.Code.Path;
                    if (blockPath.Contains("-clay-"))
                    {
                        var bottleBlock = this.Api.World.GetBlock(block.CodeWithPath(blockPath));
                        var texture = ((ICoreClientAPI)this.Api).Tesselator.GetTexSource(bottleBlock);
                        mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + "block/bottle", texture, tesselator);
                        mesh = TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                        mesher.AddMeshData(mesh);
                    }
                    else
                    {
                        ItemStack content = (this.inventory[i].Itemstack.Collectible as BlockBottle).GetContent(this.inventory[i].Itemstack);
                        if (content != null) //glass bottle with contents
                        {
                            mesh = (this.inventory[i].Itemstack.Collectible as BlockBottle).GenMeshSideways(Api as ICoreClientAPI, content, null);
                            mesh = TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                            mesher.AddMeshData(mesh);
                        }
                        else //glass bottle
                        {
                            var bottleBlock = this.inventory[i].Itemstack.Block as BlockBottle;
                            var texture = tesselator.GetTexSource(bottleBlock);
                            mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + "block/glassbottleempty", texture, tesselator);
                            mesh = TransformBottleMesh(mesh, i, block.FirstCodePart(), block.LastCodePart());
                            mesher.AddMeshData(mesh);
                        }
                    }
                }
            }
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            sb.AppendLine(Lang.Get("Suitable spot for liquid storage."));
            sb.AppendLine();
            if (forPlayer?.CurrentBlockSelection == null)
            { return; }
            var index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
            if (!this.inventory[index].Empty)
            {
                var blockPath = this.inventory[index].Itemstack.Block.Code.Path;
                if (!blockPath.Contains("-clay-"))
                {
                    (this.inventory[index].Itemstack.Collectible as BlockBottle).GetShelfInfo(this.Inventory[index], sb, Api.World);
                }
                else
                {
                    sb.AppendLine(this.inventory[index].Itemstack.GetName());
                }
            }
        }
    }
}