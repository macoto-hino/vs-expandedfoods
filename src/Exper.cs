using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using ExpandedFoods;
using HarmonyLib;
using System.Reflection;
using System.IO;

namespace Exper
{
    public class Exper : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterItemClass("test", typeof(Test));
            api.RegisterBlockBehaviorClass("testdecor", typeof(TestDecor));
            
            
            

            harmony = new Harmony("com.jakecool19.strange.experiments");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    public class TestDecor : BlockBehavior
    {
        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ref EnumHandling handling)
        {
            base.OnBlockPlaced(world, blockPos, ref handling);
            if (world.Side == EnumAppSide.Server) FloodFillDecorAt(blockPos.X, blockPos.Y, blockPos.Z, world);
        }

        public void FloodFillDecorAt(int posX, int posY, int posZ, IWorldAccessor world)
        {
            Queue<Vec4i> bfsQueue = new Queue<Vec4i>();
            HashSet<BlockPos> fillablePositions = new HashSet<BlockPos>();

            bfsQueue.Enqueue(new Vec4i(posX, posY, posZ, 0));
            fillablePositions.Add(new BlockPos(posX, posY, posZ));

            float radius = 10;

            BlockFacing[] faces = BlockFacing.ALLFACES;
            BlockPos curPos = new BlockPos();
            List<BlockPos> modifyPos = new List<BlockPos>();
            List<IWorldChunk> chunksMarked = new List<IWorldChunk>();
            string[] plants = { "lichen", "moss", "barnacle"};




            while (bfsQueue.Count > 0)
            {
                Vec4i bpos = bfsQueue.Dequeue();

                foreach (BlockFacing facing in faces)
                {
                    curPos.Set(bpos.X + facing.Normali.X, bpos.Y + facing.Normali.Y, bpos.Z + facing.Normali.Z);

                    Block block = world.BlockAccessor.GetBlock(curPos);
                    bool inBounds = bpos.W < radius;

                    if (inBounds)
                    {
                        if (block.BlockId == 0 && !fillablePositions.Contains(curPos))
                        {
                            bfsQueue.Enqueue(new Vec4i(curPos.X, curPos.Y, curPos.Z, bpos.W + 1));
                            fillablePositions.Add(curPos.Copy());
                        }
                        else if (block.SideSolid[facing.Opposite.Index])
                        {
                            Block attach = world.BlockAccessor.GetBlock(new AssetLocation("game:linen-normal-" + facing.Code));
                            world.BlockAccessor.AddDecor(attach, curPos, facing.Opposite);
                            IWorldChunk dirty = world.BlockAccessor.GetChunkAtBlockPos(curPos);
                            if (!chunksMarked.Contains(dirty))
                            {
                                chunksMarked.Add(dirty);
                                modifyPos.Add(curPos.Copy());
                            }

                        }

                    }
                }
            }
            
            if (world.Side == EnumAppSide.Server)
            foreach (IWorldChunk chunk in chunksMarked)
            {
                chunk.MarkModified();
            }
        }

        public TestDecor(Block block) : base(block)
        {
        }
    }

    public class Test : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefault;

            BlockPos checkAt = byEntity.ServerPos.AsBlockPos;
            IServerChunk chunk = byEntity.World.BlockAccessor.GetChunkAtBlockPos(checkAt) as IServerChunk;
            bool[] caves = null;

            if (chunk != null)
            {
                try
                {
                    caves = SerializerUtil.Deserialize<bool[]>(chunk.GetServerModdata("noiseCaves"));
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Did not work");
                }

                int chunksize = byEntity.World.BlockAccessor.ChunkSize;

                int localX = checkAt.X % chunksize;
                int localY = checkAt.Y % chunksize;
                int localZ = checkAt.Z % chunksize;

                System.Diagnostics.Debug.WriteLine(caves[MapUtil.Index3d(localX, localY, localZ, chunksize,chunksize)]);
            }
        }
    }

    public class SuperPick : Item
    {
        Dictionary<int, string> ores = new Dictionary<int, string>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            foreach (var val in api.World.Blocks)
            {
                if (val == null || val.BlockMaterial != EnumBlockMaterial.Ore || !val.Variant.ContainsKey("type")) continue;

                ores[val.BlockId] = "ore-" + val.Variant["type"];
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefault;
            if (blockSel != null && byEntity.World.Side == EnumAppSide.Server)
            {
                System.Diagnostics.Debug.WriteLine(byEntity.World.BlockAccessor.GetBlock(blockSel.Position).Attributes);
                /*Dictionary<string, int> oreCount = new Dictionary<string, int>();
                int chunkNum = byEntity.World.BlockAccessor.MapSizeY / byEntity.World.BlockAccessor.ChunkSize;

                for (int c = 0; c < chunkNum; c++)
                {
                    IChunkBlocks scan = byEntity.World.BlockAccessor.GetChunk(blockSel.Position.X / byEntity.World.BlockAccessor.ChunkSize, c, blockSel.Position.Z / byEntity.World.BlockAccessor.ChunkSize)?.Blocks;
                    if (scan == null) continue;

                    for (int i = 0; i < scan.Length; i++)
                    {
                        string ore = null;

                        ores.TryGetValue(scan[i], out ore);

                        if (ore != null)
                        {
                            if (!oreCount.ContainsKey(ore)) oreCount[ore] = 1; else oreCount[ore]++;
                        }
                    }
                }

                IServerPlayer splr = (byEntity as EntityPlayer)?.Player as IServerPlayer;
                if (splr == null) return;

                if (oreCount.Count == 0)
                {
                    splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("No ore node nearby"), EnumChatType.Notification);
                }
                else
                {
                    splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("Found the following ore nodes"), EnumChatType.Notification);
                    foreach (var val in oreCount)
                    {
                        splr.SendMessage(GlobalConstants.InfoLogChatGroup, val.Value + " blocks of " + Lang.Get(val.Key), EnumChatType.Notification);
                    }
                }*/
            }


        }

        
    }

    



    
}
