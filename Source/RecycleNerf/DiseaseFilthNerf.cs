using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Микробы болезней нельзя убрать первые сутки (60000 тиков).
    /// Postfix на WorkGiver_CleanFilth.HasJobOnThing.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DiseaseFilthNerf
    {
        private const int UnclearableTicks = 120000; // 2 дня

        static DiseaseFilthNerf()
        {
            var harmony = new Harmony("linya.hskmorehardcore.diseasefilthnerf");
            var targetType = AccessTools.TypeByName("RimWorld.WorkGiver_CleanFilth");
            if (targetType == null)
                return;

            var target = AccessTools.Method(targetType, "HasJobOnThing");
            if (target != null)
            {
                harmony.Patch(target,
                    postfix: new HarmonyMethod(typeof(DiseaseFilthNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] DiseaseFilthNerf applied.");
            }
        }

        public static void Postfix(ref bool __result, Thing t)
        {
            if (!__result)
                return;

            if (t is Filth filth
                && filth.def.defName.StartsWith("Filth_")
                && filth.def.defName.EndsWith("Germs")
                && filth.TicksSinceThickened < UnclearableTicks)
            {
                __result = false;
            }
        }
    }
}
