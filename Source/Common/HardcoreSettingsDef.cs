using System.Collections.Generic;
using RimWorld;
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
        // Автоправило: всё со статом BurnDurationHours продаётся с этим множителем.
        // sellPriceOverrides имеет приоритет; категории из fuelSellExcludedCategories не трогаются.
        public float fuelSellPriceMultiplier = 1f;
        public List<string> fuelSellExcludedCategories;
        // Минимальный техуровень игрока для событий (defName -> TechLevel)
        public Dictionary<string, TechLevel> incidentMinTechLevel;
        // Фактическое значение берётся из Defs/Misc/HardcoreSettings.xml; здесь только фолбэк.
        public float treeRegrowthChance = 0.25f;
        // Срок жизни деревьев = growDays * это значение (ваниль 8; 0 = не менять)
        public float treeLifespanDaysPerGrowDays = 0f;
        // Гости Hospitality: на сколько техуровней фракция может быть выше / ниже игрока (-1 = без ограничения)
        public int guestMaxTechAhead = -1;
        public int guestMaxTechBehind = -1;
        // Гости Hospitality: множитель выданного им серебра по техуровню фракции (нет строки — без изменений)
        public Dictionary<TechLevel, float> guestSilverMultiplierByTech;

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
