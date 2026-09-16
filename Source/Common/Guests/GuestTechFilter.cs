using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Гости Hospitality по техуровню: фракция выше игрока не приходит, сильно ниже —
    // тоже (границы guestMaxTechAhead / guestMaxTechBehind в HardcoreSettings.xml).
    // Hospitality не спрашивает FactionCanBeGroupSource (где стоит фильтр Ignorance
    // Is Bliss): у события VisitorGroup шанс 0, визиты идут из собственной очереди,
    // фракция назначается при планировании. Поэтому фильтруем само планирование:
    //  - PlanNewVisit: неподходящая фракция в очередь не встаёт (цепочки повторных
    //    визитов, переносы, «заодно пригласим соседей»);
    //  - FillIncidentQueue: ближайшие 1-3 фракции выбираются только из подходящих,
    //    иначе при неподходящих соседях очередь навсегда осталась бы пустой.
    // Приглашение с консоли связи не ограничиваем — звать можно кого угодно.
    // Уже стоящие в очереди визиты (старые сейвы) приходят один раз, их повтор
    // отсекается.
    [StaticConstructorOnStartup]
    public static class GuestTechFilter
    {
        // Диагностика: каждое решение PlanNewVisit и каждый фактический приход гостей
        // в лог (без режима разработчика)
        private const bool DebugLog = true;

        private static readonly System.Reflection.MethodInfo planNewVisitMethod;
        private static readonly Func<Faction, Map, float> getTravelDays;
        private static readonly AccessTools.FieldRef<DiaOption, string> diaOptionText;

        // Выставляется на время обработки кнопки «Пригласить гостей»
        private static bool inviting;

        static GuestTechFilter()
        {
            var utility = AccessTools.TypeByName("Hospitality.Utilities.GenericUtility");
            if (utility == null)
                return; // Hospitality не установлен

            var planNewVisit = AccessTools.Method(utility, "PlanNewVisit");
            var fillQueue = AccessTools.Method(utility, "FillIncidentQueue");
            var travelDays = AccessTools.Method(utility, "GetTravelDays");
            var dialogFor = AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor));
            if (planNewVisit == null || fillQueue == null || travelDays == null || dialogFor == null)
            {
                Log.Warning("[HSKMoreHardcore] GuestTechFilter: API Hospitality изменилось — фильтр гостей отключён.");
                return;
            }

            planNewVisitMethod = planNewVisit;
            getTravelDays = AccessTools.MethodDelegate<Func<Faction, Map, float>>(travelDays);
            diaOptionText = AccessTools.FieldRefAccess<DiaOption, string>("text");

            var harmony = new Harmony("linya.hskmorehardcore.guesttechfilter");
            harmony.Patch(planNewVisit,
                prefix: new HarmonyMethod(typeof(GuestTechFilter), nameof(PlanNewVisitPrefix)));
            harmony.Patch(fillQueue,
                prefix: new HarmonyMethod(typeof(GuestTechFilter), nameof(FillIncidentQueuePrefix)));
            // После постфикса Hospitality, который добавляет кнопку приглашения
            harmony.Patch(dialogFor,
                postfix: new HarmonyMethod(typeof(GuestTechFilter), nameof(FactionDialogForPostfix)) { priority = Priority.Last });

            // Второй путь визитов — мимо очереди: рассказчик (StorytellerComp_FactionInteraction,
            // incident VisitorGroup у Kara) запускает событие без фракции, и ванильный
            // TryResolveParmsGeneral выбирает её через FactionCanBeGroupSource. Hospitality
            // переопределяет этот метод без вызова базового — ни нашего фильтра, ни IiB там нет.
            // Визиты из очереди и приглашения не затрагиваются: фракция в них уже задана.
            var visitorWorkerType = AccessTools.TypeByName("Hospitality.IncidentWorker_VisitorGroup");
            var factionSource = visitorWorkerType == null ? null : AccessTools.DeclaredMethod(visitorWorkerType, "FactionCanBeGroupSource");
            if (factionSource != null)
                harmony.Patch(factionSource,
                    postfix: new HarmonyMethod(typeof(GuestTechFilter), nameof(FactionCanBeGroupSourcePostfix)));
            else
                Log.Warning("[HSKMoreHardcore] GuestTechFilter: IncidentWorker_VisitorGroup.FactionCanBeGroupSource не найден — визиты от рассказчика не фильтруются.");

            if (DebugLog)
            {
                // Фактический приход гостей — чтобы увидеть визиты в обход фильтра
                var visitorWorker = AccessTools.TypeByName("Hospitality.IncidentWorker_VisitorGroup");
                var tryExecute = visitorWorker == null ? null : AccessTools.Method(visitorWorker, "TryExecuteWorker");
                if (tryExecute != null)
                    harmony.Patch(tryExecute,
                        postfix: new HarmonyMethod(typeof(GuestTechFilter), nameof(VisitorArrivalPostfix)));
                else
                    Log.Warning("[HSKMoreHardcore] GuestTechFilter: IncidentWorker_VisitorGroup.TryExecuteWorker не найден — приходы гостей не логируются.");

                Log.Message($"[HSKMoreHardcore] GuestTechFilter applied. DebugLog={DebugLog}, IiB active={IgnoranceCompat.Active}");
            }
        }

        /// <summary>Может ли фракция прислать гостей при текущем техуровне игрока.</summary>
        public static bool FactionTechAllowed(Faction faction)
        {
            var settings = HardcoreSettingsDef.Instance;
            if (faction?.def == null || settings == null || Current.Game == null)
                return true;

            TechLevel factionTech = faction.def.techLevel;
            TechLevel playerTech = IgnoranceCompat.PlayerTechLevel;
            if (factionTech == TechLevel.Undefined || playerTech == TechLevel.Undefined)
                return true;
            // IiB считает игрока Animal, пока не набран порог неолита; гостей-зверей
            // не бывает, так что ниже неолита не опускаем — иначе племя без гостей
            if (playerTech < TechLevel.Neolithic)
                playerTech = TechLevel.Neolithic;

            int diff = (int)factionTech - (int)playerTech;
            if (settings.guestMaxTechAhead >= 0 && diff > settings.guestMaxTechAhead)
                return false;
            if (settings.guestMaxTechBehind >= 0 && -diff > settings.guestMaxTechBehind)
                return false;
            return true;
        }

        public static bool PlanNewVisitPrefix(float afterDays, Faction faction)
        {
            if (faction == null)
                return true;

            bool allowed = FactionTechAllowed(faction);
            bool run = inviting || allowed;

            if (DebugLog)
            {
                string verdict = inviting ? "ПРИГЛАШЕНИЕ (фильтр не применяется)" : allowed ? "поставлен в очередь" : "ОТКЛОНЁН";
                Log.Message($"[HSKMoreHardcore] GuestTechFilter: план визита {DescribeFaction(faction)} через {afterDays:0.#} дн. -> {verdict}; " +
                    $"{DescribePlayerTech()}; вызвал {Caller()}");
            }
            else if (!run && Prefs.DevMode)
            {
                Log.Message($"[HSKMoreHardcore] GuestTechFilter: визит {faction.Name} (тех {faction.def.techLevel}, игрок {IgnoranceCompat.PlayerTechLevel}) не запланирован.");
            }
            return run;
        }

        // Фракции, про отсечение которых уже написали в лог в этой сессии
        private static readonly System.Collections.Generic.HashSet<int> loggedCutFactions = new System.Collections.Generic.HashSet<int>();

        // Выбор фракции для визита, который запустил рассказчик (без заранее заданной фракции)
        public static void FactionCanBeGroupSourcePostfix(Faction f, ref bool __result)
        {
            if (!__result || f == null || FactionTechAllowed(f))
                return;

            __result = false;
            if (DebugLog && loggedCutFactions.Add(f.loadID))
                Log.Message($"[HSKMoreHardcore] GuestTechFilter: визит от рассказчика — фракция {DescribeFaction(f)} ОТСЕЧЕНА; {DescribePlayerTech()}");
        }

        // Лог фактического прихода: какая фракция пришла и прошла бы она фильтр сейчас
        public static void VisitorArrivalPostfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            var faction = parms?.faction;
            string filter = faction == null ? "-" : FactionTechAllowed(faction) ? "да" : "НЕТ — пришли в обход фильтра";
            Log.Message($"[HSKMoreHardcore] GuestTechFilter: ПРИХОД гостей ({__instance?.def?.defName}, результат {__result}) " +
                $"{(faction == null ? "фракция null" : DescribeFaction(faction))}; {DescribePlayerTech()}; по фильтру: {filter}");
        }

        // Копия GenericUtility.FillIncidentQueue с фильтром техуровня
        public static bool FillIncidentQueuePrefix(Map map)
        {
            float days = Rand.Range(10f, 16f);
            int count = Rand.Range(1, 4);
            var candidates = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer) && FactionTechAllowed(f))
                .OrderBy(f => getTravelDays(f, map));
            if (DebugLog)
                Log.Message($"[HSKMoreHardcore] GuestTechFilter: заполнение очереди, кандидаты: {string.Join(", ", candidates.Select(DescribeFaction))}; {DescribePlayerTech()}");
            foreach (var faction in candidates)
            {
                count--;
                // Через Hospitality: он проверит мирные группы и поставит в очередь
                planNewVisitMethod.Invoke(null, new object[] { map, days, faction });
                days += Rand.Range(15f, 25f);
                if (count <= 0)
                    break;
            }
            return false;
        }

        // Кнопка «Пригласить гостей» ставит визит в обход фильтра
        public static void FactionDialogForPostfix(DiaNode __result)
        {
            if (__result?.options == null)
                return;

            string inviteLabel = "InviteGuests".Translate();
            foreach (var option in __result.options)
            {
                if (option?.action == null || diaOptionText(option) != inviteLabel)
                    continue;

                var original = option.action;
                option.action = () =>
                {
                    inviting = true;
                    try
                    {
                        original();
                    }
                    finally
                    {
                        inviting = false;
                    }
                };
            }
        }

        private static string DescribeFaction(Faction f)
        {
            return $"{f.Name} [{f.def.defName}, тех {f.def.techLevel}]";
        }

        private static string DescribePlayerTech()
        {
            var settings = HardcoreSettingsDef.Instance;
            return $"игрок по IiB {IgnoranceCompat.PlayerTechLevel} (IiB active={IgnoranceCompat.Active}), " +
                $"фракция игрока {Faction.OfPlayer?.def?.techLevel}, ahead={settings?.guestMaxTechAhead}, behind={settings?.guestMaxTechBehind}";
        }

        // Первый метод Hospitality в стеке, кроме самого PlanNewVisit
        private static string Caller()
        {
            var frames = new StackTrace().GetFrames();
            if (frames == null)
                return "?";
            foreach (var frame in frames)
            {
                var m = frame.GetMethod();
                var type = m?.DeclaringType;
                if (type == null || type == typeof(GuestTechFilter))
                    continue;
                string ns = type.Namespace ?? "";
                if (!ns.StartsWith("Hospitality") && !ns.StartsWith("RimWorld") && !ns.StartsWith("Verse"))
                    continue;
                if (m.Name.Contains("PlanNewVisit"))
                    continue;
                return $"{type.Name}.{m.Name}";
            }
            return "?";
        }
    }
}
