using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Минимальный техуровень игрока для событий: словарь incidentMinTechLevel
    // (defName -> TechLevel) в Defs/Misc/HardcoreSettings.xml. Уровень берём из
    // IgnoranceCompat: с Ignorance Is Bliss — его расчётный, иначе уровень
    // фракции игрока. Патчим общий вход IncidentWorker.CanFireNow — через него
    // идут и сторителлер, и dev-меню.
    [StaticConstructorOnStartup]
    public static class IncidentTechGate
    {
        static IncidentTechGate()
        {
            var canFireNow = AccessTools.Method(typeof(IncidentWorker), "CanFireNow");
            if (canFireNow == null)
            {
                Log.Warning("[HSKMoreHardcore] IncidentTechGate: IncidentWorker.CanFireNow not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.incidenttechgate").Patch(canFireNow,
                postfix: new HarmonyMethod(typeof(IncidentTechGate), nameof(CanFireNowPostfix)));
        }

        public static void CanFireNowPostfix(IncidentWorker __instance, ref bool __result)
        {
            if (!__result)
                return;

            var gates = HardcoreSettingsDef.Instance?.incidentMinTechLevel;
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
