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
        // Множители цены продажи игроком. Сначала точечные по defName, потом
        // список правил по категориям / статам (первое подходящее).
        public Dictionary<string, float> sellPriceOverrides;
        public List<SellPriceRule> sellPriceRules;
        // Животные с меткой CompFreeAnimal продаются с этими множителями:
        // пришедшие событием и прирученные вручную. 1 = без изменений.
        // Рождённые в колонии метки не получают и продаются по полной цене.
        public float freeAnimalSellMultiplier = 1f;
        public float tamedAnimalSellMultiplier = 1f;
        // Гейты событий (техуровень для событий, форм жуткого присоединившегося и
        // гостей) живут в отдельном моде HSK More Balance: Quests and Events.
        // Фактическое значение берётся из Defs/Misc/HardcoreSettings.xml; здесь только фолбэк.
        public float treeRegrowthChance = 0.25f;
        // Срок жизни деревьев = growDays * это значение (ваниль 8; 0 = не менять)
        public float treeLifespanDaysPerGrowDays = 0f;
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
