using HarmonyLib;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

namespace ExpandedFoods
{
    public class ExpandedFoods : ModSystem
    {
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            CookingRecipe.NamingRegistry["augratin"] = new ExpandedFoodsRecipeNames();
            CookingRecipe.NamingRegistry["riceandbeans"] = new ExpandedFoodsRecipeNames();
            CookingRecipe.NamingRegistry["meatysalad"] = new ExpandedFoodsRecipeNames();
            CookingRecipe.NamingRegistry["yogurtmeal"] = new ExpandedFoodsRecipeNames();
            CookingRecipe.NamingRegistry["pastahot"] = new ExpandedFoodsRecipeNames();
            CookingRecipe.NamingRegistry["pastacold"] = new ExpandedFoodsRecipeNames();

            api.RegisterBlockClass("BlockMeatHooks", typeof(BlockMeatHooks));
            api.RegisterBlockEntityClass("MeatHooks", typeof(BlockEntityMeatHooks));

            api.RegisterBlockClass("BlockMixingBowl", typeof(BlockMixingBowl));
            api.RegisterBlockEntityClass("MixingBowl", typeof(BlockEntityMixingBowl));

            api.RegisterBlockClass("BlockBottle", typeof(BlockBottle));
            api.RegisterBlockEntityClass("Bottle", typeof(BlockEntityBottle));

            api.RegisterBlockClass("BlockSpile", typeof(BlockSpile));
            api.RegisterBlockEntityClass("Spile", typeof(BlockEntitySpile));

            api.RegisterBlockClass("BlockSaucepan", typeof(BlockSaucepan));
            api.RegisterBlockEntityClass("Saucepan", typeof(BlockEntitySaucepan));

            api.RegisterBlockClass("BlockScrewPress", typeof(BlockScrewPress));
            api.RegisterBlockEntityClass("BEScrewPress", typeof(BEScrewPress));

            api.RegisterItemClass("SuperFood", typeof(ItemSuperFood));
            api.RegisterItemClass("ExpandedRawFood", typeof(ItemExpandedRawFood));
            api.RegisterItemClass("ExpandedFood", typeof(ItemExpandedFood));
            api.RegisterItemClass("TransFix", typeof(ItemTransFix));
            api.RegisterItemClass("TransLiquid", typeof(ItemTransLiquid));
            api.RegisterItemClass("ExpandedLiquid", typeof(ItemExpandedLiquid));
            api.RegisterItemClass("ExpandedDough", typeof(ItemExpandedDough));

            harmony = new Harmony("com.jakecool19.expandedfoods.cookingoverhaul");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            
            api.RegisterCommand("efremap", "Remaps items in Expanded Foods", "",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    api.World.BlockAccessor.WalkBlocks(player.Entity.ServerPos.AsBlockPos.AddCopy(-10), player.Entity.ServerPos.AsBlockPos.AddCopy(10), (block, pos) => {

                        BottleFix(pos, block, api.World);
                });
                }, Privilege.chat);
        }

        public void BottleFix(BlockPos pos, Block block, IWorldAccessor world)
        {
            BlockEntityContainer bc;
            if (block.Code.Path.Contains("bottle") && !block.Code.Path.Contains("burned") && !block.Code.Path.Contains("raw"))
            {
                Block replacement = world.GetBlock(new AssetLocation(block.Code.Domain + ":bottle-" + block.FirstCodePart(1) + "-burned"));
                
                if (replacement != null) world.BlockAccessor.SetBlock(replacement.BlockId, pos);
            }
            else if ((bc = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer) != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    if (slot.Itemstack?.Block != null && slot.Itemstack.Block.Code.Path.Contains("bottle") && !slot.Itemstack.Block.Code.Path.Contains("burned") && !slot.Itemstack.Block.Code.Path.Contains("raw"))
                    {
                        Block replacement = world.GetBlock(new AssetLocation(slot.Itemstack.Block.Code.Domain + ":bottle-" + slot.Itemstack.Block.FirstCodePart(1) + "-burned"));
                        if (replacement != null)
                        {
                            slot.Itemstack = new ItemStack(replacement, slot.Itemstack.StackSize);
                        }
                    }
                }
            }
        }

    }

    public class ItemExpandedDough : ItemExpandedRawFood
    {
        ItemStack[] tableStacks;

        public override void OnLoaded(ICoreAPI api)
        {
            List<ItemStack> tableStacks = new List<ItemStack>();
            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj is Block && (obj as Block).Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    tableStacks.Add(new ItemStack(obj));
                }
            }

            this.tableStacks = tableStacks.ToArray();
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block.Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    BlockPie blockform = api.World.GetBlock(new AssetLocation("game:pie-raw")) as BlockPie;

                    handling = EnumHandHandling.PreventDefault;
                    blockform.TryPlacePie(byEntity, blockSel);
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-makepie",
                    Itemstacks = tableStacks,
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }

    public class ExpandedFoodsRecipeNames : ICookingRecipeNamingHelper
    {
        public string GetNameForIngredients(IWorldAccessor worldForResolve, string recipeCode, ItemStack[] stacks)
        {
            string mealName = Lang.Get("expandedfoods:meal-normal-" + recipeCode);
            string full = " ";

            List<string> ings = new List<string>();

            if (stacks == null || stacks.Length <= 0) return mealName;
            //System.Diagnostics.Debug.WriteLine("Hello World");
            foreach (ItemStack stack in stacks)
            {
                if (!ings.Contains(Lang.Get("recipeingredient-" + (stack.Block != null ? "block-" : "item-") + stack.Collectible.Code.Path))) 
                    ings.Add(Lang.Get("recipeingredient-" + (stack.Block != null ? "block-" : "item-") + stack.Collectible.Code.Path));
            }

            if (ings.Count == 1) return mealName + full + Lang.Get("expandedfoods:made with ") + ings[0];

            full = mealName + full + Lang.Get("expandedfoods:made with ");
            
            for (int i = 0; i < ings.Count; i++)
            {
                if (i + 1 == ings.Count)
                {
                    full += Lang.Get("expandedfoods:and ") + ings[i];
                }
                else
                {
                    full += ings[i] + ", ";
                }
            }


            return full;
        }
    }

    public class MixingRecipeRegistry
    {
        private static MixingRecipeRegistry loaded;
        private List<CookingRecipe> mixingRecipes = new List<CookingRecipe>();
        private List<DoughRecipe> kneadingRecipes = new List<DoughRecipe>();
        public List<CookingRecipe> MixingRecipes
        {
            get
            {
                return mixingRecipes;
            }
            set
            {
                mixingRecipes = value;
            }
        }
        public List<DoughRecipe> KneadingRecipes
        {
            get
            {
                return kneadingRecipes;
            }
            set
            {
                kneadingRecipes = value;
            }
        }
        public static MixingRecipeRegistry Create()
        {
            if (loaded == null)
            {
                loaded = new MixingRecipeRegistry();
            }
            return Loaded;
        }

        public static MixingRecipeRegistry Loaded
        {
            get
            {
                if (loaded == null)
                {
                    loaded = new MixingRecipeRegistry();
                }
                return loaded;
            }
        }

        public static void Dispose()
        {
            if (loaded == null) return;
            loaded = null;
        }
    }

    public class DoughRecipe : IByteSerializable
    {
        public string Code = "something";
        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;


        public DoughIngredient[] Ingredients;

        public JsonItemStack Output;

        public ItemStack TryCraftNow(ICoreAPI api, ItemSlot[] inputslots)
        {

            var matched = pairInput(inputslots);

            ItemStack mixedStack = Output.ResolvedItemstack.Clone();
            mixedStack.StackSize = getOutputSize(matched);

            if (mixedStack.StackSize <= 0) return null;

            /*
            TransitionableProperties[] props = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null);
            TransitionableProperties perishProps = props != null && props.Length > 0 ? props[0] : null;

            if (perishProps != null)
            {
                CollectibleObject.CarryOverFreshness(api, inputslots, new ItemStack[] { mixedStack }, perishProps);
            }*/

            IExpandedFood food;
            if ((food = mixedStack.Collectible as IExpandedFood) != null) food.OnCreatedByKneading(matched, mixedStack);

            foreach (var val in matched)
            {
                val.Key.TakeOut(val.Value.Quantity * (mixedStack.StackSize / Output.StackSize));
                val.Key.MarkDirty();
            }

            return mixedStack;
        }

        public bool Matches(IWorldAccessor worldForResolve, ItemSlot[] inputSlots)
        {
            int outputStackSize = 0;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = pairInput(inputSlots);
            if (matched == null) return false;

            outputStackSize = getOutputSize(matched);

            return outputStackSize >= 0;
        }

        List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairInput(ItemSlot[] inputStacks)
        {
            List<int> alreadyFound = new List<int>();

            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (var val in inputStacks) if (!val.Empty) inputSlotsList.Enqueue(val);

            if (inputSlotsList.Count != Ingredients.Length) return null;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();

            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;

                for (int i = 0; i < Ingredients.Length; i++)
                {
                    CraftingRecipeIngredient ingred = Ingredients[i].GetMatch(inputSlot.Itemstack);

                    if (ingred != null && !alreadyFound.Contains(i))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                        alreadyFound.Add(i);
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            // We're missing ingredients
            if (matched.Count != Ingredients.Length)
            {
                return null;
            }

            return matched;
        }


        int getOutputSize(List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched)
        {
            int outQuantityMul = -1;

            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;
                int posChange = inputSlot.StackSize / ingred.Quantity;
                
                if (posChange < outQuantityMul || outQuantityMul == -1) outQuantityMul = posChange;
            }

            if (outQuantityMul == -1)
            {
                return -1;
            }


            foreach (var val in matched)
            {
                ItemSlot inputSlot = val.Key;
                CraftingRecipeIngredient ingred = val.Value;


                // Must have same or more than the total crafted amount
                if (inputSlot.StackSize < ingred.Quantity * outQuantityMul) return -1;

            }

            outQuantityMul = 1;
            return Output.StackSize * outQuantityMul;
        }

        public string GetOutputName()
        {
            return Lang.Get("expandedfoods:Will make {0}", Output.ResolvedItemstack.GetName());
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;

            for (int i = 0; i < Ingredients.Length; i++)
            {
                ok &= Ingredients[i].Resolve(world, sourceForErrorLogging);
            }
            
            ok &= Output.Resolve(world, sourceForErrorLogging);
            

            return ok;
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Code);
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }

            Output.ToBytes(writer);
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Code = reader.ReadString();
            Ingredients = new DoughIngredient[reader.ReadInt32()];

            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new DoughIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Dough Recipe (FromBytes)");
            }

            Output = new JsonItemStack();
            Output.FromBytes(reader, resolver.ClassRegistry);
            Output.Resolve(resolver, "Dough Recipe (FromBytes)");
        }

        public DoughRecipe Clone()
        {
            DoughIngredient[] ingredients = new DoughIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ingredients[i] = Ingredients[i].Clone();
            }

            return new DoughRecipe()
            {
                Output = Output.Clone(),
                Code = Code,
                Enabled = Enabled,
                Name = Name,
                Ingredients = ingredients
            };
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();

            if (Ingredients == null || Ingredients.Length == 0) return mappings;

            foreach (var ingreds in Ingredients)
            {
                if (ingreds.Inputs.Length <= 0) continue;
                CraftingRecipeIngredient ingred = ingreds.Inputs[0];
                if (ingred == null || !ingred.Code.Path.Contains("*") || ingred.Name == null) continue;

                int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;

                List<string> codes = new List<string>();

                if (ingred.Type == EnumItemClass.Block)
                {
                    for (int i = 0; i < world.Blocks.Count; i++)
                    {
                        if (world.Blocks[i].Code == null || world.Blocks[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                        {
                            string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);

                        }
                    }
                }
                else
                {
                    for (int i = 0; i < world.Items.Count; i++)
                    {
                        if (world.Items[i].Code == null || world.Items[i].IsMissing) continue;

                        if (WildcardUtil.Match(ingred.Code, world.Items[i].Code))
                        {
                            string code = world.Items[i].Code.Path.Substring(wildcardStartLen);
                            string codepart = code.Substring(0, code.Length - wildcardEndLen);
                            if (ingred.AllowedVariants != null && !ingred.AllowedVariants.Contains(codepart)) continue;

                            codes.Add(codepart);
                        }
                    }
                }

                mappings[ingred.Name] = codes.ToArray();
            }

            return mappings;
        }
    }

    public class ExpandedFoodsRecipeLoader : RecipeLoader
    {
        public ICoreServerAPI api;

        public override void StartServerSide(ICoreServerAPI api)
        {
            MixingRecipeRegistry.Create();
            this.api = api;
            api.Event.SaveGameLoaded += LoadFoodRecipes;
        }

        public override void Dispose()
        {
            base.Dispose();
            MixingRecipeRegistry.Dispose();
        }

        public void LoadFoodRecipes()
        {
            LoadMixingRecipes();
            LoadKneadingRecipes();
        }

        public void LoadMixingRecipes()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/mixing");
            int recipeQuantity = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    CookingRecipe rec = val.Value.ToObject<CookingRecipe>();
                    if (!rec.Enabled) continue;

                    rec.Resolve(api.World, "mixing recipe " + val.Key);
                    MixingRecipeRegistry.Loaded.MixingRecipes.Add(rec);

                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        CookingRecipe rec = token.ToObject<CookingRecipe>();
                        if (!rec.Enabled) continue;

                        rec.Resolve(api.World, "mixing recipe " + val.Key);
                        MixingRecipeRegistry.Loaded.MixingRecipes.Add(rec);

                        recipeQuantity++;
                    }
                }
            }

            api.World.Logger.Event("{0} mixing recipes loaded", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("expandedfoods:The chef and the apprentice..."));
        }

        public void LoadKneadingRecipes()
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, "recipes/kneading");
            int recipeQuantity = 0;
            int ignored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    DoughRecipe rec = val.Value.ToObject<DoughRecipe>();
                    if (!rec.Enabled) continue;

                    LoadKneadingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        DoughRecipe rec = token.ToObject<DoughRecipe>();
                        if (!rec.Enabled) continue;

                        LoadKneadingRecipe(val.Key, rec, ref recipeQuantity, ref ignored);
                    }
                }
            }

            api.World.Logger.Event("{0} kneading recipes loaded", recipeQuantity);
            api.World.Logger.StoryEvent(Lang.Get("expandedfoods:The butter and the bread..."));
        }

        void LoadKneadingRecipe(AssetLocation path, DoughRecipe recipe, ref int quantityRegistered, ref int quantityIgnored)
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;
            string className = "kneading recipe";

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<DoughRecipe> subRecipes = new List<DoughRecipe>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        DoughRecipe rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingreds in rec.Ingredients)
                            {
                                if (ingreds.Inputs.Length <= 0) continue;
                                CraftingRecipeIngredient ingred = ingreds.Inputs[0];

                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                }

                foreach (DoughRecipe subRecipe in subRecipes)
                {
                    if (!subRecipe.Resolve(api.World, className + " " + path))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    MixingRecipeRegistry.Loaded.KneadingRecipes.Add(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                if (!recipe.Resolve(api.World, className + " " + path))
                {
                    quantityIgnored++;
                    return;
                }

                MixingRecipeRegistry.Loaded.KneadingRecipes.Add(recipe);
                quantityRegistered++;
            }
        }


    }

    public class DoughIngredient : IByteSerializable
    {
        public CraftingRecipeIngredient[] Inputs;

        public CraftingRecipeIngredient GetMatch(ItemStack stack)
        {
            if (stack == null) return null;

            for (int i = 0; i < Inputs.Length; i++)
            {
                if (Inputs[i].SatisfiesAsIngredient(stack)) return Inputs[i];
            }

            return null;
        }

        public bool Resolve(IWorldAccessor world, string debug)
        {
            bool ok = true;

            for (int i = 0; i < Inputs.Length; i++)
            {
                ok &= Inputs[i].Resolve(world, debug);
            }

            return ok;
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            Inputs = new CraftingRecipeIngredient[reader.ReadInt32()];

            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i] = new CraftingRecipeIngredient();
                Inputs[i].FromBytes(reader, resolver);
                Inputs[i].Resolve(resolver, "Dough Ingredient (FromBytes)");
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(Inputs.Length);
            for (int i = 0; i < Inputs.Length; i++)
            {
                Inputs[i].ToBytes(writer);
            }
        }

        public DoughIngredient Clone()
        {
            CraftingRecipeIngredient[] newings = new CraftingRecipeIngredient[Inputs.Length];

            for (int i = 0; i < Inputs.Length; i++)
            {
                newings[i] = Inputs[i].Clone();
            }

            return new DoughIngredient()
            {
                Inputs = newings
            };
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeUpload
    {
        public List<string> dvalues;
        public List<string> cvalues;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecipeResponse
    {
        public string response;
    }

    /// <summary>
    /// A basic example of client<->server networking using a custom network communication
    /// </summary>
    public class RecipeUploadSystem : ModSystem
    {
        #region Client
        IClientNetworkChannel clientChannel;
        ICoreClientAPI clientApi;

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientApi = api;

            clientChannel =
                api.Network.RegisterChannel("networkapitest")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeUpload>(OnServerMessage)
            ;
        }

        private void OnServerMessage(RecipeUpload networkMessage)
        {
            List<DoughRecipe> drecipes = new List<DoughRecipe>();
            List<CookingRecipe> crecipes = new List<CookingRecipe>();

            foreach(string drec in networkMessage.dvalues)
            {
                using (MemoryStream ms = new MemoryStream(Ascii85.Decode(drec)))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    DoughRecipe retr = new DoughRecipe();
                    retr.FromBytes(reader, clientApi.World);

                    drecipes.Add(retr);
                }
            }

            MixingRecipeRegistry.Loaded.KneadingRecipes = drecipes;

            foreach (string crec in networkMessage.cvalues)
            {
                using (MemoryStream ms = new MemoryStream(Ascii85.Decode(crec)))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    CookingRecipe retr = new CookingRecipe();
                    retr.FromBytes(reader, clientApi.World);

                    crecipes.Add(retr);
                }
            }

            MixingRecipeRegistry.Loaded.MixingRecipes = crecipes;

            System.Diagnostics.Debug.WriteLine(MixingRecipeRegistry.Loaded.KneadingRecipes.Count + " kneading recipes and " + MixingRecipeRegistry.Loaded.MixingRecipes.Count + " mixing recipes loaded to client.");
        }

        #endregion

        #region Server
        IServerNetworkChannel serverChannel;
        ICoreServerAPI serverApi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverApi = api;

            serverChannel =
                api.Network.RegisterChannel("networkapitest")
                .RegisterMessageType(typeof(RecipeUpload))
                .RegisterMessageType(typeof(RecipeResponse))
                .SetMessageHandler<RecipeResponse>(OnClientMessage)
            ;

            api.RegisterCommand("recipeupload", "Resync recipes", "", OnRecipeUploadCmd, Privilege.chat);
            api.Event.PlayerNowPlaying += (hmm) => { OnRecipeUploadCmd(); };
        }

        private void OnRecipeUploadCmd(IServerPlayer player = null, int groupId = 0, CmdArgs args = null)
        {
            List<string> drecipes = new List<string>();
            List<string> crecipes = new List<string>();

            foreach (DoughRecipe drec in MixingRecipeRegistry.Loaded.KneadingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    drec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    drecipes.Add(value);
                }
            }

            foreach (CookingRecipe crec in MixingRecipeRegistry.Loaded.MixingRecipes)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);

                    crec.ToBytes(writer);

                    string value = Ascii85.Encode(ms.ToArray());
                    crecipes.Add(value);
                }
            }

            serverChannel.BroadcastPacket(new RecipeUpload()
            {
                dvalues = drecipes,
                cvalues = crecipes
            });
        }

        private void OnClientMessage(IPlayer fromPlayer, RecipeResponse networkMessage)
        {
            OnRecipeUploadCmd();
        }


        #endregion
    }

    #region HarmonyPatches

    [HarmonyPatch(typeof(InventorySmelting))]
    class SmeltingInvPatches
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetOutputText")]
        static void displayFix(ref string __result, InventorySmelting __instance)
        {
            if (__instance[1].Itemstack?.Collectible is BlockSaucepan)
            {
                __result = (__instance[1].Itemstack.Collectible as BlockSaucepan).GetOutputText(__instance.Api.World, __instance);
            }
        }
    }

    [HarmonyPatch(typeof(CookingRecipeIngredient))]
    class CookingIngredientPatches
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetMatchingStack")]
        static bool displayFix(ItemStack inputStack, ref CookingRecipeStack __result, CookingRecipeIngredient __instance)
        {
            if (inputStack == null) { __result = null; return false; }

            for (int i = 0; i < __instance.ValidStacks.Length; i++)
            {
                bool isWildCard = __instance.ValidStacks[i].Code.Path.Contains("*");
                bool found =
                    (isWildCard && inputStack.Collectible.WildCardMatch(__instance.ValidStacks[i].Code))
                    || (!isWildCard && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats" }).ToArray()))
                    || (__instance.ValidStacks[i].CookedStack?.ResolvedItemstack != null && inputStack.Equals(__instance.world, __instance.ValidStacks[i].ResolvedItemstack, GlobalConstants.IgnoredStackAttributes.Concat(new string[] { "madeWith", "expandedSats"}).ToArray()))
                ;

                if (found) { __result = __instance.ValidStacks[i]; return false; }
            }


            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityShelf))]
    class ShelfPatches
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("genMesh")]
        static bool displayFix(ItemStack stack, int index, ref MeshData __result, BlockEntityShelf __instance, ref Item ___nowTesselatingItem, ref Matrixf ___mat)
        {
            if (stack.Collectible is ItemExpandedRawFood)
            {
                string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
                if (ings == null || ings.Length <= 0) return true;

                ___nowTesselatingItem = stack.Item;


                __result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, __instance, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
                __result.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);

                float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
                float y = index >= 4 ? 10 / 16f : 2 / 16f;
                float z = (index % 2 == 0) ? 4 / 16f : 10 / 16f;

                Vec4f offset = ___mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                __result.Translate(offset.XYZ);

                return false;
            }
            if (stack.Collectible is BlockBottle && !stack.Collectible.Code.Path.Contains("clay"))
            {
                ItemStack content = (stack.Collectible as BlockBottle).GetContent(__instance.Api.World, stack);
                if (content == null) return true;
                __result = (stack.Collectible as BlockBottle).GenMesh(__instance.Api as ICoreClientAPI, content);
                //__result.RenderPasses.Fill((short)EnumChunkRenderPass.BlendNoCull);

                float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
                float y = index >= 4 ? 10 / 16f : 2 / 16f;
                float z = (index % 2 == 0) ? 4 / 16f : 10 / 16f;

                Vec4f offset = ___mat.TransformVector(new Vec4f(x - 0.5f, y, z - 0.5f, 0));
                __result.Translate(offset.XYZ);

                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetBlockInfo")]
        static bool descFix(IPlayer forPlayer, StringBuilder sb, ref BlockEntityShelf __instance)
        {
            float rate = __instance.GetPerishRate();

            sb.AppendLine(Lang.Get("Stored food perish speed: {0}x", Math.Round(rate, 2)));

            float ripenRate = GameMath.Clamp(((1 - rate) - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            {
                sb.Append("Suitable spot for food ripening.");
            }


            sb.AppendLine();

            bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (int j = 3; j >= 0; j--)
            {
                int i = j + (up ? 4 : 0);
                i ^= 2;   //Display shelf contents text for items from left-to-right, not right-to-left

                if (__instance.Inventory[i].Empty) continue;

                ItemStack stack = __instance.Inventory[i].Itemstack;

                if (stack.Collectible is BlockCrock)
                {
                    sb.Append(__instance.CrockInfoCompact(__instance.Inventory[i]));
                }
                else if (stack.Collectible is BlockBottle)
                {
                    (stack.Collectible as BlockBottle).GetShelfInfo(__instance.Inventory[i], sb, __instance.Api.World);
                }
                else
                {
                    if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                    {
                        sb.Append(BlockEntityShelf.PerishableInfoCompact(__instance.Api, __instance.Inventory[i], ripenRate));
                    }
                    else
                    {
                        sb.AppendLine(stack.GetName());
                    }
                }
            }

            return false;
        }

    }

    [HarmonyPatch(typeof(BlockEntityDisplay))]
    class DisplayPatches
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("genMesh")]
        static bool displayFix(ItemStack stack, int index, ref MeshData __result, BlockEntityDisplay __instance, ref Item ___nowTesselatingItem)
        {
            if (!(stack.Collectible is ItemExpandedRawFood)) return true;
            string[] ings = (stack.Attributes?["madeWith"] as StringArrayAttribute)?.value;
            if (ings == null || ings.Length <= 0) return true;

            ___nowTesselatingItem = stack.Item;
            //nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);

            __result = (stack.Collectible as ItemExpandedRawFood).GenMesh(__instance.Api as ICoreClientAPI, ings, __instance, new Vec3f(0, __instance.Block.Shape.rotateY, 0));
            __result.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);



            if (stack.Collectible.Attributes?[__instance.AttributeTransformCode].Exists == true)
            {
                ModelTransform transform = stack.Collectible.Attributes?[__instance.AttributeTransformCode].AsObject<ModelTransform>();
                transform.EnsureDefaultValues();
                transform.Rotation.Y += __instance.Block.Shape.rotateY;
                __result.ModelTransform(transform);
            }

            //if (__instance.Block.Shape.rotateY == 90 || __instance.Block.Shape.rotateY == 270) __result.Rotate(new Vec3f(0f, 0f, 0f), 0f, 90 * GameMath.DEG2RAD, 0f);

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityCookedContainer))]
    class BECookedContainerPatches
    {
        //This is for the cooking pot entity
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityCookedContainer", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityCookedContainer __instance)
        {            
            if (__result == null) __result = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetBlockInfo")]
        static bool infoFix(IPlayer forPlayer, ref StringBuilder dsc, BlockEntityCookedContainer __instance)
        {
            ItemStack[] contentStacks = __instance.GetNonEmptyContentStacks();
            CookingRecipe recipe = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
            if (recipe == null) return true;

            float servings = __instance.QuantityServings;
            int temp = (int)contentStacks[0].Collectible.GetTemperature(__instance.Api.World, contentStacks[0]); ;
            string temppretty = Lang.Get("{0}°C", temp);
            if (temp < 20) temppretty = "Cold";

            BlockMeal mealblock = __instance.Api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;
            string nutriFacts = mealblock.GetContentNutritionFacts(__instance.Api.World, __instance.Inventory[0], contentStacks, forPlayer.Entity);


            if (servings == 1)
            {
                dsc.Append(Lang.Get("{0} serving of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }
            else
            {
                dsc.Append(Lang.Get("{0} servings of {1}\nTemperature: {2}{3}{4}", Math.Round(servings, 1), recipe.GetOutputName(forPlayer.Entity.World, contentStacks), temppretty, nutriFacts != null ? "\n" : "", nutriFacts));
            }


            foreach (var slot in __instance.Inventory)
            {
                if (slot.Empty) continue;

                TransitionableProperties[] propsm = slot.Itemstack.Collectible.GetTransitionableProperties(__instance.Api.World, slot.Itemstack, null);
                if (propsm != null && propsm.Length > 0)
                {
                    slot.Itemstack.Collectible.AppendPerishableInfoText(slot, dsc, __instance.Api.World);
                    break;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityMeal))]
    class BEMealContainerPatches
    {
        //This is for the meal bowl entity
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("BlockEntityMeal", MethodType.Constructor)]
        static void invFix(ref InventoryGeneric ___inventory)
        {
            ___inventory = new InventoryGeneric(6, null, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("FromRecipe", MethodType.Getter)]
        static void recipeFix(ref CookingRecipe __result, BlockEntityMeal __instance)
        {
            if (__result == null) __result = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.RecipeCode);
        }
    }

    [HarmonyPatch(typeof(BlockCookedContainerBase))]
    class BlockMealContainerBasePatches
    {
        //This is for the base food container
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null) __result = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetMealRecipe")]
        static void mealFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null) __result = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }
    }

    [HarmonyPatch(typeof(BlockMeal))]
    class BlockMealBowlBasePatches
    {
        //This is for the food bowl block
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("GetCookingRecipe")]
        static void recipeFix(ref CookingRecipe __result, ItemStack containerStack, IWorldAccessor world, BlockCookedContainerBase __instance)
        {
            if (__result == null) __result = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault(rec => rec.Code == __instance.GetRecipeCode(world, containerStack));
        }

        /*[HarmonyPostfix]
        [HarmonyPatch("GetContentNutritionProperties", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent))]
        static void nutriFix(IWorldAccessor world, ItemSlot inSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref FoodNutritionProperties[] __result)
        {
            List<FoodNutritionProperties> foodProps = new List<FoodNutritionProperties>();
            if (contentStacks == null)
            {
                __result = foodProps.ToArray();
                return;
            }


            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                CollectibleObject obj = contentStacks[i].Collectible;
                FoodNutritionProperties stackProps;



                if (obj.CombustibleProps != null && obj.CombustibleProps.SmeltedStack != null)
                {
                    stackProps = obj.CombustibleProps.SmeltedStack.ResolvedItemstack.Collectible.GetNutritionProperties(world, obj.CombustibleProps.SmeltedStack.ResolvedItemstack, forEntity);
                }
                else
                {
                    stackProps = obj.GetNutritionProperties(world, contentStacks[i], forEntity);
                }

                if (obj.Attributes?["nutritionPropsWhenInMeal"].Exists == true)
                {
                    stackProps = obj.Attributes?["nutritionPropsWhenInMeal"].AsObject<FoodNutritionProperties>();
                }

                if (stackProps == null) continue;

                FoodNutritionProperties props = stackProps.Clone();

                DummySlot slot = new DummySlot(contentStacks[i], inSlot.Inventory);
                TransitionState state = contentStacks[i].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                float healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                if (obj is ItemExpandedRawFood && (contentStacks[i].Attributes["expandedSats"] as FloatArrayAttribute)?.value?.Length == 6)
                {
                    FoodNutritionProperties[] exProps = (obj as ItemExpandedRawFood).GetPropsFromArray((contentStacks[i].Attributes["expandedSats"] as FloatArrayAttribute).value);

                    if (exProps == null || exProps.Length <= 0) continue;

                    foreach (FoodNutritionProperties exProp in exProps)
                    {
                        exProp.Satiety *= satLossMul;
                        exProp.Health *= healthLoss;

                        foodProps.Add(exProp);
                    }
                }

                props.Satiety *= satLossMul;
                props.Health *= healthLoss;

                foodProps.Add(props);

            }

            __result = foodProps.ToArray();
        }


        [HarmonyPrefix]
        [HarmonyPatch("GetContentNutritionFacts", typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack[]), typeof(EntityAgent), typeof(bool))]
        static bool nutriFactsFix(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, ref string __result, bool mulWithStacksize = false)
        {
            FoodNutritionProperties[] props = BlockMeal.GetContentNutritionProperties(world, inSlotorFirstSlot, contentStacks, forEntity);

            Dictionary<EnumFoodCategory, float> totalSaturation = new Dictionary<EnumFoodCategory, float>();
            float totalHealth = 0;

            for (int i = 0; i < props.Length; i++)
            {
                FoodNutritionProperties prop = props[i];
                if (prop == null) continue;

                float sat = 0;
                totalSaturation.TryGetValue(prop.FoodCategory, out sat);

                DummySlot slot = new DummySlot(contentStacks[0], inSlotorFirstSlot.Inventory);
                TransitionState state = contentStacks[0].Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;

                float mul = mulWithStacksize ? contentStacks[0].StackSize : 1;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);

                totalHealth += prop.Health * healthLossMul * mul;
                totalSaturation[prop.FoodCategory] = (sat + prop.Satiety * satLossMul) * mul;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Nutrition Facts"));

            foreach (var val in totalSaturation)
            {
                sb.AppendLine("- " + Lang.Get("" + val.Key) + ": " + Math.Round(val.Value) + " sat.");
            }

            if (totalHealth != 0)
            {
                sb.AppendLine("- " + Lang.Get("Health: {0}{1} hp", totalHealth > 0 ? "+" : "", totalHealth));
            }

            __result = sb.ToString();

            return false;
        }*/

        [HarmonyPrefix]
        [HarmonyPatch("Consume")]
        static bool consumeFix(IWorldAccessor world, IPlayer eatingPlayer, ItemSlot inSlot, ItemStack[] contentStacks, float remainingServings, bool mulwithStackSize, ref float __result)
        {
            FoodNutritionProperties[] multiProps = BlockMeal.GetContentNutritionProperties(world, inSlot, contentStacks, eatingPlayer.Entity);
            if (multiProps == null) { __result = remainingServings; return false; }

            float totalHealth = 0;
            EntityBehaviorHunger ebh = eatingPlayer.Entity.GetBehavior<EntityBehaviorHunger>();
            float satiablePoints = ebh.MaxSaturation - ebh.Saturation;


            float mealSatpoints = 0;
            for (int i = 0; i < multiProps.Length; i++)
            {
                FoodNutritionProperties nutriProps = multiProps[i];
                if (nutriProps == null) continue;

                mealSatpoints += nutriProps.Satiety * (mulwithStackSize ? contentStacks[i].StackSize : 1);
            }

            float servingsNeeded = GameMath.Clamp(satiablePoints / Math.Max(1, mealSatpoints), 0, 1);

            float servingsToEat = Math.Min(remainingServings, servingsNeeded);



            for (int i = 0; i < multiProps.Length; i++)
            {
                FoodNutritionProperties nutriProps = multiProps[i];
                if (nutriProps == null) continue;

                float mul = servingsToEat * (mulwithStackSize ? contentStacks[i].StackSize : 1);
                float sat = mul * nutriProps.Satiety;

                eatingPlayer.Entity.ReceiveSaturation(sat, nutriProps.FoodCategory, 10 + sat / 70f * 60f, 1f);

                if (nutriProps.EatenStack?.ResolvedItemstack != null)
                {
                    if (eatingPlayer == null || !eatingPlayer.InventoryManager.TryGiveItemstack(nutriProps.EatenStack.ResolvedItemstack.Clone(), true))
                    {
                        world.SpawnItemEntity(nutriProps.EatenStack.ResolvedItemstack.Clone(), eatingPlayer.Entity.SidedPos.XYZ);
                    }
                }

                totalHealth += mul * nutriProps.Health;
            }


            if (totalHealth != 0)
            {
                eatingPlayer.Entity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
                    Type = totalHealth > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                }, Math.Abs(totalHealth));
            }

            __result = Math.Max(0, remainingServings - servingsToEat);
            return false;
        }

    }

    [HarmonyPatch(typeof(BlockCrock))]
    class BlockCrockContainerPatches
    {
        //This is for the cooking pot entity
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch("GetPlacedBlockInfo")]
        static bool infoFix(IWorldAccessor world, BlockPos pos, IPlayer forPlayer, BlockCrock __instance, ref string __result)
        {
            BlockEntityCrock becrock = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrock;
            if (becrock == null) return true;

            BlockMeal mealblock = world.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            CookingRecipe recipe = MixingRecipeRegistry.Loaded.MixingRecipes.FirstOrDefault((rec) => becrock.RecipeCode == rec.Code);
            ItemStack[] stacks = becrock.inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).ToArray();

            if (stacks == null || stacks.Length == 0)
            {
                return true;
            }

            StringBuilder dsc = new StringBuilder();

            if (recipe != null)
            {
                ItemSlot slot = BlockCrock.GetDummySlotForFirstPerishableStack(world, stacks, forPlayer.Entity, becrock.inventory);

                if (recipe != null)
                {
                    if (becrock.QuantityServings == 1)
                    {
                        dsc.AppendLine(Lang.Get("{0} serving of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                    else
                    {
                        dsc.AppendLine(Lang.Get("{0} servings of {1}", Math.Round(becrock.QuantityServings, 1), recipe.GetOutputName(world, stacks)));
                    }
                }

                string facts = mealblock.GetContentNutritionFacts(world, new DummySlot(__instance.OnPickBlock(world, pos)), null);

                if (facts != null)
                {
                    dsc.Append(facts);
                }

                slot.Itemstack?.Collectible.AppendPerishableInfoText(slot, dsc, world);
            }
            else
            {
                return true;
            }

            if (becrock.Sealed)
            {
                dsc.AppendLine("<font color=\"lightgreen\">" + Lang.Get("Sealed.") + "</font>");
            }
            

            __result = dsc.ToString();
            return false;
        }
    }

    #endregion
}
