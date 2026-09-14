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
    // Если фракция задана заранее (parms.faction), ванилла выбор пропускает и
    // FactionCanBeGroupSource не спрашивает. Для каравана и путника префиксом на
    // их TryExecuteWorker сбрасываем такую фракцию, если она не проходит по
    // техуровню, — дальше ванилла выбирает заново уже с фильтром.
    [StaticConstructorOnStartup]
    public static class RaidExtensionTechFilter
    {
        // Диагностика: каждое решение фильтра и фактический запуск события в лог
        private const bool DebugLog = true;

        private static readonly string[] workerTypeNames =
        {
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraderCaravanPassing",
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraveler",
            "SR.ModRimworld.RaidExtension.IncidentWorkerLogging",
            "SR.ModRimworld.RaidExtension.IncidentWorkerPoaching",
        };

        // Воркеры, у которых заранее заданная фракция сбрасывается префиксом
        private static readonly string[] presetFactionResetTypeNames =
        {
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraderCaravanPassing",
            "SR.ModRimworld.RaidExtension.IncidentWorkerHostileTraveler",
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
                {
                    Log.Warning($"[HSKMoreHardcore] RaidExtensionTechFilter: не найден FactionCanBeGroupSource у {typeName}");
                    continue;
                }

                harmony.Patch(method,
                    postfix: new HarmonyMethod(typeof(RaidExtensionTechFilter), nameof(FactionSourcePostfix)));
                patched++;

                // Лог фактического запуска: какая фракция в итоге в parms
                var tryExec = AccessTools.DeclaredMethod(type, "TryExecuteWorker");
                if (tryExec != null)
                {
                    var prefix = System.Array.IndexOf(presetFactionResetTypeNames, typeName) >= 0
                        ? new HarmonyMethod(typeof(RaidExtensionTechFilter), nameof(TryExecutePrefix))
                        : null;
                    harmony.Patch(tryExec,
                        prefix: prefix,
                        postfix: new HarmonyMethod(typeof(RaidExtensionTechFilter), nameof(TryExecutePostfix)));
                }
                else
                {
                    Log.Warning($"[HSKMoreHardcore] RaidExtensionTechFilter: не найден TryExecuteWorker у {typeName}");
                }
            }

            if (patched > 0)
                Log.Message($"[HSKMoreHardcore] RaidExtensionTechFilter: отфильтровано воркеров — {patched}. IgnoranceCompat.Active={IgnoranceCompat.Active}");
            else
                Log.Warning("[HSKMoreHardcore] RaidExtensionTechFilter: Raid Extension найден, но методы FactionCanBeGroupSource не пропатчены — API изменилось?");
        }

        public static void FactionSourcePostfix(IncidentWorker __instance, Faction f, ref bool __result)
        {
            bool wasAllowed = __result;
            bool eligible = IgnoranceCompat.FactionIsEligible(f);
            if (__result && !eligible)
                __result = false;

            if (DebugLog && wasAllowed)
            {
                Log.Message($"[HSKMoreHardcore] RaidExtTechFilter: {__instance?.GetType().Name} кандидат {f?.Name} " +
                    $"(тех {f?.def?.techLevel}, игрок {IgnoranceCompat.PlayerTechLevel}) -> " +
                    (eligible ? "допущен" : "ОТСЕЧЁН"));
            }
        }

        public static void TryExecutePrefix(IncidentWorker __instance, IncidentParms parms)
        {
            var f = parms?.faction;
            if (f == null || IgnoranceCompat.FactionIsEligible(f))
                return;

            if (DebugLog)
            {
                Log.Message($"[HSKMoreHardcore] RaidExtTechFilter: {__instance?.GetType().Name} заданная фракция {f.Name} " +
                    $"(тех {f.def?.techLevel}, игрок {IgnoranceCompat.PlayerTechLevel}) СБРОШЕНА, выбор заново");
            }
            parms.faction = null;
        }

        public static void TryExecutePostfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!DebugLog || !__result)
                return;

            var f = parms?.faction;
            Log.Message($"[HSKMoreHardcore] RaidExtTechFilter: СОБЫТИЕ {__instance?.GetType().Name} ({__instance?.def?.defName}) " +
                $"выстрелило с фракцией {f?.Name ?? "null"} (тех {f?.def?.techLevel.ToString() ?? "-"}, игрок {IgnoranceCompat.PlayerTechLevel}), " +
                $"допустимость по IiB: {(f == null ? "-" : IgnoranceCompat.FactionIsEligible(f).ToString())}");
        }
    }
}
