using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Raid Extension (sr.modrimworld.raidextension) переопределяет
    // FactionCanBeGroupSource в своих воркерах, не вызывая базовый метод, —
    // фильтр техуровня Ignorance Is Bliss (патчит только базовый
    // IncidentWorker_PawnsArrive) туда не дотягивается, и «враждебный караван» /
    // «заблудившийся рейдер» приходят фракциями любого техуровня.
    // Добиваем его переопределения постфиксом через IgnoranceCompat.
    // Surprise-варианты наследуют эти методы, отдельный патч им не нужен.
    [StaticConstructorOnStartup]
    public static class RaidExtensionTechFilter
    {
        private static readonly string[] workerTypeNames =
        {
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraderCaravanPassing",
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraveler",
            "SR.ModRimworld.RaidExtension.IncidentWorkerLogging",
            "SR.ModRimworld.RaidExtension.IncidentWorkerPoaching",
        };

        static RaidExtensionTechFilter()
        {
            if (AccessTools.TypeByName("SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraderCaravanPassing") == null)
                return; // Raid Extension не установлен

            var harmony = new Harmony("linya.hskmorehardcore.raidextensiontechfilter");
            int patched = 0;
            foreach (var typeName in workerTypeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                var method = type == null ? null : AccessTools.DeclaredMethod(type, "FactionCanBeGroupSource");
                if (method == null)
                    continue; // у Logging/Poaching фильтр идёт через базовую цепочку — не критично

                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(RaidExtensionTechFilter), nameof(FactionSourcePostfix)));
                patched++;
            }

            if (patched > 0)
                Log.Message($"[HSKMoreHardcore] RaidExtensionTechFilter: отфильтровано воркеров — {patched}.");
            else
                Log.Warning("[HSKMoreHardcore] RaidExtensionTechFilter: Raid Extension найден, но методы FactionCanBeGroupSource не пропатчены — API изменилось?");
        }

        public static void FactionSourcePostfix(Faction f, ref bool __result)
        {
            if (__result && !IgnoranceCompat.FactionIsEligible(f))
                __result = false;
        }
    }
}
