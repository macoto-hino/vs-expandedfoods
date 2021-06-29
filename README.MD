-----Meat Hooks Usage-----
The meat hook works similarly to the shelf, tool rack, and display case. To use put meathookable in its attributes along with meatHookTransform to change the look of it on the hook to your liking

meathookable: true,
meatHookTransform: {
  translation: { x: 0, y: -0.4, z: -0.02 },
  rotation: { x: 0, y: 90, z: 90 },
  origin: { x: 0.5, y: 0, z: 0.5 },
  scale: 2
},

Additionally in the meathook's attributes its dry and curing multiplier can be changed as well

cureRate: 3,
dryRate: 6,

-----Spile Usage-----
The spile can only be placed on a block without another spile on it or else it will pop off. A bucket or cauldron must be placed directly beneath it in order to collect sap. The sap properties must be defined in the attributes of the block it is attached to. It has a chance to drip a certain amount of sap every interval within its harvest season(month). Must be a liquid.

sapProperties: {
  sap: "expandedfoods:birchsapportion",
  dripCount: 1,
  dripChance: 1.0,
  dripTime: 12,
  seasons: [6, 5],
},

-----Saucepan(Cauldron) Usage-----
The cauldron has all the same functions of a bucket except it cannot be used to milk animals or place water. Liquids inside the cauldron can be boiled into other liquids, if the liquid inside has combustible properties given to it, like a food item like bread.

combustibleProps: {
  meltingPoint: 200,
  meltingDuration: 15,
  smeltedRatio: 4,
  smeltingType: "cook",
  smeltedStack: { type: "item", code: "expandedfoods:birchsyrupportion" },
  requiresContainer: false
},

-----Super Food Class Usage-----
Giving food the SuperFood class, will allow them to alternate saturations and healing upon eating. To use it simply set this class in any food item(It MUST be a food item/have nutritional properties already) and put additionalNutritionProperties in its attributes. Inside of that put additional food properties you want in there.

class: "SuperFood",

additionalNutritionProperties: [
  { saturation: 80, foodcategory: "Fruit" }
],

-----Screwpress Usage-----
The screw press turns solid items like seeds and fruit, and extracts liquids from them which can be scooped out with a bucket. For an item to be squeezed it needs to have squeezeInto and squeezeRatio in its attributes.

squeezeInto: "expandedfoods:fruitjuiceportion",
squeezeRatio: 1,

The amount of time it takes for the press to squeeze things can be determined by its pressTime in its attributes

pressTime: 4,

-----Mixing Bowl Usage-----
The mixing bowl acts much like the quern except it can create cooking recipes with up to 6 ingredients(requires pot in the bottom slot) or can be used for "kneading" recipes that must not have a pot in the bottom slot. To create mixing recipes, simply put them in recipes/mixing. Mixing recipes are identical to cooking recipes. For kneading recipes put them in recipes/kneading. They are a very simple format, list of ingredients with an output. Note: Because you can only have stack sizes up to six, ingredients with higher stack sizes than that will not work.

{
	ingredients: [
		{ type: "item", code: "game:waterportion",  quantity: 1 },
		{ type: "item", code: "game:salt", quantity: 1 },
		{ type: "item", code: "game:flour-*", allowedVariants: ["flax", "rice", "rye", "spelt"], name: "type", quantity: 1 }
	],
	output: { type: "item", code: "game:dough-{type}", stackSize: 1}
}
