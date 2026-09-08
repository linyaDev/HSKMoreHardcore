using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Минимальный техуровень игрока для квестовых сайтов: словарь sitePartMinTechLevel
    // (defName -> TechLevel) в Defs/Misc/HardcoreSettings.xml. Уровень берём из
    // IgnoranceCompat — как в IncidentTechGate. Кондишн-козеры (ЭМИ-динамо и т.п.)
    // приходят не инцидентами, а квестовыми сайтами: их выбор фильтруется через
    // SitePartWorker.IsAvailable (SiteMakerHelper), сюда и ставим постфикс.
    [StaticConstructorOnStartup]
    public static class SitePartTechGate
    {
        static SitePartTechGate()
        {
            var harmony = new Harmony("linya.hskmorehardcore.siteparttechgate");
            harmony.Patch(
                AccessTools.Method(typeof(SitePartWorker), nameof(SitePartWorker.IsAvailable)),
                postfix: new HarmonyMethod(typeof(SitePartTechGate), nameof(IsAvailablePostfix)));
            Log.Message("[HSKMoreHardcore] SitePartTechGate applied.");
        }

        public static void IsAvailablePostfix(SitePartWorker __instance, ref bool __result)
        {
            if (!__result)
                return;

            var gates = HardcoreSettingsDef.Instance?.sitePartMinTechLevel;
            string defName = __instance?.def?.defName;
            if (gates == null || defName == null)
                return;

            if (gates.TryGetValue(defName, out TechLevel minTech)
                && IgnoranceCompat.PlayerTechLevel < minTech)
            {
                __result = false;
            }
        }
    }
}
