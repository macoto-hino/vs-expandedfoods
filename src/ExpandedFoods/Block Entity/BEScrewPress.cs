using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedFoods
{
    class BEScrewPress : BlockEntityContainer
    {
        public override string InventoryClassName => "screwpress";

        public override InventoryBase Inventory => inv;
        internal InventoryGeneric inv;

        public ItemSlot inputSlot { get { return inv[0]; } }
        public ItemSlot outputSlot { get { return inv[1]; } }
        public int maxCap = 40;
        public bool isSqueezing;
        public double squeezeUntil;
        bool loaded = false;

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        public float MeshAngle { get; set; }
        ICoreClientAPI capi;



        public BEScrewPress()
        {
            inv = new InventoryGeneric(2, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(OnGameTick, 100);
            if (api.Side != EnumAppSide.Client) return;
            capi = api as ICoreClientAPI;

            if (Api.Side == EnumAppSide.Client && loaded)
                InitAnim();

        }
        
        private void InitAnim()
        {
            animUtil?.InitializeAnimator("screwpress", 
                new Vec3f(0, MeshAngle * GameMath.RAD2DEG, 0), 
                capi.Assets.TryGet(AssetLocation.Create("expandedfoods:shapes/block/screwpress.json")).ToObject<Shape>());
        }

        public void OnGameTick(float dt)
        {
            if (!inputSlot.Empty && inputSlot.Itemstack.ItemAttributes?.KeyExists("squeezeInto") != true) inv.DropSlots(Pos.ToVec3d(), 0);
            releasePressure();
        }

        public bool TryAdd(ItemSlot slot, int quantity = 1)
        {
            if (isSqueezing || outputSlot.StackSize > 0) return false;

            if (slot.Itemstack?.ItemAttributes?["squeezeInto"].Exists != true) return false;
            int moveq = Math.Min(quantity > slot.StackSize ? slot.StackSize : quantity, (inputSlot.Itemstack?.Collectible.MaxStackSize ?? maxCap) - inputSlot.StackSize);
            
            int originalSize = slot.StackSize;

            if (inputSlot.Empty)
            {
                ItemStack moved = slot.TakeOut(moveq);
                inputSlot.Itemstack = moved;
            }
            else if (inputSlot.Itemstack.Equals(Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes) && moveq > 0)
            {
                inputSlot.Itemstack.StackSize += moveq;
                slot.Itemstack.StackSize -= moveq;
                if (slot.Itemstack.StackSize <= 0) slot.TakeOutWhole();
                slot.MarkDirty();
            }

            MarkDirty();
            return originalSize != slot.StackSize;
        }

        public bool usePress(IPlayer player = null)
        {
            if (isSqueezing || outputSlot.StackSize > 0 || inputSlot.StackSize <= 0) return false;

            int ratio = inputSlot.Itemstack.ItemAttributes["squeezeRatio"].AsInt();
            if (ratio > inputSlot.StackSize)
            {
                inv.DropSlots(Pos.ToVec3d(), new int[1] {0});
                return false;
            }

            isSqueezing = true;
            squeezeUntil = Api.World.Calendar.TotalHours + Block.Attributes["pressTime"].AsDouble(4);
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/creak/woodcreak_long3.ogg"), Pos.X, Pos.Y, Pos.Z);
            if (Api.Side != EnumAppSide.Client || animUtil is null) return true;
            if (animUtil.renderer is null) InitAnim();
            animUtil.StartAnimation(new AnimationMetaData()
            {
                Animation = "spin",
                Code = "spin",
                AnimationSpeed = 1.8f,
                EaseOutSpeed = 6,
                EaseInSpeed = 15
            });
            return true;
            
        }

        public void releasePressure()
        {
            if (squeezeUntil > Api.World.Calendar.TotalHours || !isSqueezing) return;

            outputSlot.Itemstack = new ItemStack(Api.World.GetItem(new AssetLocation(inputSlot.Itemstack.ItemAttributes["squeezeInto"].AsString("game:waterportion"))), inputSlot.StackSize / inputSlot.Itemstack.ItemAttributes["squeezeRatio"].AsInt(1));
            inputSlot.TakeOut(inputSlot.StackSize - (inputSlot.StackSize % inputSlot.Itemstack.ItemAttributes["squeezeRatio"].AsInt(1)));
            isSqueezing = false;
            squeezeUntil = 0;

            animUtil?.StopAnimation("spin");

        }

        public void dropLeftovers()
        {
            if (!isSqueezing) inv.DropSlots(Pos.ToVec3d(), new int[1] { 0 });
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("squeezeUntil", squeezeUntil);
            tree.SetFloat("meshAngle", MeshAngle);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            squeezeUntil = tree.GetDouble("squeezeUntil");
            isSqueezing = squeezeUntil > 0;
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);
            loaded = true;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            bool skipMesh = base.OnTesselation(mesher, tesselator);
            if (!skipMesh)
            {
                mesher.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(Block).Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            }

            return true;
        }

        public void GenAnim()
        {
            if (Api.Side == EnumAppSide.Client && animUtil != null && animUtil.renderer == null)
            {
                animUtil?.InitializeAnimator("screwpress", new Vec3f(0, MeshAngle * GameMath.RAD2DEG, 0), capi.Assets.TryGet(AssetLocation.Create("expandedfoods:shapes/block/screwpress.json")).ToObject<Shape>());
            }
        }

    }
}
