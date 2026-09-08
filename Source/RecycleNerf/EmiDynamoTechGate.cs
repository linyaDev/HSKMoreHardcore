using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// ЭМИ-динамо (Royalty condition causer) не должно приходить квестами, пока игрок
    /// не дошёл до индастриала: до электричества глушить нечего, угроза несоразмерна
    /// эпохе. Гейт — завершённое исследование Electricity. Квестовые сайты фильтруются
    /// через SitePartWorker.IsAvailable (SiteMakerHelper), сюда и ставим постфикс.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EmiDynamoTechGate
    {
        static EmiDynamoTechGate()
        {
            var harmony = new Harmony("linya.hskmorehardcore.emidynamogate");
            harmony.Patch(
                AccessTools.Method(typeof(SitePartWorker), nameof(SitePartWorker.IsAvailable)),
                postfix: new HarmonyMethod(typeof(EmiDynamoTechGate), nameof(IsAvailablePostfix)));
            Log.Message("[HSKMoreHardcore] EmiDynamoTechGate applied.");
        }

        public static void IsAvailablePostfix(SitePartWorker __instance, ref bool __result)
        {
            if (!__result || __instance.def?.defName != "EMIDynamo")
                return;

            var electricity = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Electricity");
            if (electricity != null && !electricity.IsFinished)
                __result = false;
        }
    }
}
