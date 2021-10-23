using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedFoods
{
    public class BlockEntityBottleRack : BlockEntityDisplay
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "bottlerack";
        public override string AttributeTransformCode => "bottleRackTransform";

        Block block;


        public BlockEntityBottleRack()
        {
            inv = new InventoryGeneric(8, "bottlerack-0", null, null);
            meshes = new MeshData[8];
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);
        }

        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            if (transType == EnumTransitionType.Dry) return 0;
            if (Api == null) return 0;


            if (transType == EnumTransitionType.Cure)
            {
                return 5f;
            }
            else if (transType == EnumTransitionType.Perish || transType == EnumTransitionType.Ripen)
            {
                float perishRate = GetPerishRate();
                if (transType == EnumTransitionType.Ripen)
                {
                    return GameMath.Clamp(((1 - perishRate) - 0.5f) * 3, 0, 1);
                }

                return baseMul * perishRate;
            }

            return 1;

        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                CollectibleObject colObj = slot.Itemstack.Collectible;
                if (colObj.Attributes != null && colObj.Attributes["bottlerackable"].AsBool(false) == true)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                        updateMeshes();
                        return true;
                    }

                    return false;
                }
            }


            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int selectionBoxIndex = blockSel.SelectionBoxIndex;
            //Api.Logger.Debug("bottle {0}", blockSel.SelectionBoxIndex);

            if (inv[selectionBoxIndex].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[selectionBoxIndex]);
                updateMesh(selectionBoxIndex);
                MarkDirty(true);
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int selectionBoxIndex = blockSel.SelectionBoxIndex;
            if (!inv[selectionBoxIndex].Empty)
            {
                ItemStack stack = inv[selectionBoxIndex].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                MarkDirty(true);
                updateMesh(selectionBoxIndex);
                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);


            float ripenRate = GameMath.Clamp(((1 - GetPerishRate()) - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            {
                sb.Append("Suitable spot for food ripening.");
            }


            sb.AppendLine();

            bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (int j = 7; j >= 0; j--) // Number of slots within blockinfo so it know where to put the given bottle it.
            {

                if (inv[j].Empty) continue;

                ItemStack stack = inv[j].Itemstack;


                if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                {
                    sb.Append(PerishableInfoCompact(Api, inv[j], ripenRate));
                }
                else
                {
                    sb.AppendLine(stack.GetName());
                }
            }
        }


        public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }


                if (appendLine) dsc.AppendLine();
            }

            return dsc.ToString();
        }


        protected override MeshData genMesh(ItemStack stack, int index)
        {
            BlockBottle bottle = stack.Collectible as BlockBottle;
            MeshData mesh;
            ICoreClientAPI capi = Api as ICoreClientAPI;

            if (bottle != null)
            {
                ItemStack content = (stack.Collectible as BlockBottle).GetContent(Api.World, stack);
                mesh = (stack.Collectible as BlockBottle).GenMesh(Api as ICoreClientAPI, content);

                return mesh;
            }

            capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

            return mesh;

        }

        protected override void translateMesh(MeshData mesh, int index)
        {
            float x;
            float z;
            float y;
            //Api.Logger.Debug("bottle {0}", index);
            switch (index)
            {
                case 0: // Slot ID
                    x = 2.5f / 16f; // W & E // Red
                    y = 14.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 1: // Slot ID
                    x = 6.5f / 16f; // W & E // Red
                    y = 14.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 2: // Slot ID
                    x = 10.5f / 16f; // W & E // Red
                    y = 14.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 3: // Slot ID
                    x = 14.5f / 16f; // W & E // Red
                    y = 14.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                 case 4: // Slot ID
                    x = 2.5f / 16f; // W & E // Red
                    y = 10.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 5: // Slot ID
                    x = 6.5f / 16f; // W & E // Red
                    y = 10.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 6: // Slot ID
                    x = 10.5f / 16f; // W & E // Red
                    y = 10.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                case 7: // Slot ID
                    x = 14.5f / 16f; // W & E // Red
                    y = 10.5f / 16f; // U & D // Green
                    z = 0.5f / 16f; // N & S // Blue
                    break;
                default: // Default Fallback ID?
                    x = 0f;
                    z = 0f;
                    y = 0f / 16f;
                    break;
            }

            mesh.Scale(new Vec3f(0.5f, 0, 0.5f), 0.99f, 0.99f, 0.99f);
            mesh.Rotate(new Vec3f(0.5f, 0, 0.5f), 90 * GameMath.DEG2RAD, 0, 0);
            mesh.Translate(x - 0.5f, y, z - 0.5f);
        }
    }
}
