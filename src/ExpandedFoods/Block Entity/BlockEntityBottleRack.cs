    using ExpandedFoods;    
    using System.Text;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;
    using Vintagestory.GameContent;
    using Vintagestory.API.Datastructures;
    using System.Diagnostics;

    public class BlockEntityBottleRack : BlockEntityDisplay
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
                // && this.inventory[index].Itemstack.Block.Code.Path.Contains("bottle"))
                if (colObj.Attributes != null)
                {
                    if (colObj.Code.Path.StartsWith("bottle-"))
                    {
                        if (this.TryPut(playerSlot, blockSel))
                        { return true; }
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


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            MeshData mesh;
            var shapeBase = "expandedfoods:shapes/";
            var shapePath = "";
            var rot = 0f;

            var block = this.Api.World.BlockAccessor.GetBlock(this.Pos) as BlockBottleRack;
            var texture = tesselator.GetTexSource(block);
            shapePath = "block/bottlerack";
            mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + shapePath, texture, -1, rot, tesselator);
            mesher.AddMeshData(mesh);

            if (this.inventory != null)
            {
                for (var i = 0; i <= 15; i++)
                {
                    if (!this.inventory[i].Empty)
                    {
                        var bblock = this.inventory[i].Itemstack.Block as BlockBottle;
                        var blockPath = this.inventory[i].Itemstack.Block.Code.Path;
                        var tempblock = this.Api.World.GetBlock(block.CodeWithPath(blockPath));
                        if (blockPath.Contains("-clay-"))
                        { shapePath = "block/bottle"; }
					    else if (!blockPath.Contains("clay"))
                        { shapePath = "block/glassbottleempty"; }
                        texture = ((ICoreClientAPI)this.Api).Tesselator.GetTexSource(tempblock);
                        //we can change shape and texture here, i.e.
                        //shapePath = "block/glassbottle";
                        //var tempblock = this.Api.World.GetBlock(block.CodeWithPath("glassbottle"));
                        //texture = ((ICoreClientAPI)this.Api).Tesselator.GetTexSource(tempblock);

                        if (block.LastCodePart() == "east")
                        { rot = 1.57f; }
                        else if (block.LastCodePart() == "south")
                        { rot = 3.14f; }
                        else if (block.LastCodePart() == "west")
                        { rot = 4.71f; }
                        mesh = block.GenMesh(this.Api as ICoreClientAPI, shapeBase + shapePath, texture, i, rot, tesselator);
                        mesher.AddMeshData(mesh);
                    }
                }
            }
            return true;
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            sb.AppendLine();
            if (forPlayer?.CurrentBlockSelection == null)
            { return; }
            var index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
            if (!this.inventory[index].Empty)
            {
                sb.AppendLine(this.inventory[index].Itemstack.GetName());
            }
        }
    }