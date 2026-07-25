using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    public class HardcoreSettingsDef : Def
    {
        public float ammoCraftCostMultiplier = 5f;
        public float arrowCraftCostMultiplier = 1f;
        public float boltCraftCostMultiplier = 2f;
        public float deepResourceMultiplier = 0.143f; // ~1/7
        public List<string> ammoCraftExcludedMaterials;
        public List<string> ammoCraftExcludedRecipes;
        public int traderSilverMinimum = 3500;
        public Dictionary<string, float> sellPriceOverrides;
        public float treeRegrowthChance = 0.25f;

        private static HardcoreSettingsDef cachedInstance;

        public static HardcoreSettingsDef Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = DefDatabase<HardcoreSettingsDef>.GetNamedSilentFail("HSK_HardcoreSettings");
                return cachedInstance;
            }
        }
    }
}
