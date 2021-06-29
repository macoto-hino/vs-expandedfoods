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
            
            
            

            harmony = new Harmony("com.jakecool19.strange.experiments");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    public class Test : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefault;
            if (blockSel != null && byEntity.World.Side == EnumAppSide.Server)
            {
                IMapRegion region = byEntity.World.BlockAccessor.GetMapChunkAtBlockPos(byEntity.SidedPos.AsBlockPos).MapRegion;
                foreach (GeneratedStructure struc in region.GeneratedStructures)
                System.Diagnostics.Debug.WriteLine(struc.Code);
            }


        }

        
    }

    



    
}
