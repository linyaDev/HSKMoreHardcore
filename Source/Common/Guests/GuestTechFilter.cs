using System;
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

        public static bool PlanNewVisitPrefix(Faction faction)
        {
            if (faction == null || inviting || FactionTechAllowed(faction))
                return true;

            if (Prefs.DevMode)
                Log.Message($"[HSKMoreHardcore] GuestTechFilter: визит {faction.Name} (тех {faction.def.techLevel}, игрок {IgnoranceCompat.PlayerTechLevel}) не запланирован.");
            return false;
        }

        // Копия GenericUtility.FillIncidentQueue с фильтром техуровня
        public static bool FillIncidentQueuePrefix(Map map)
        {
            float days = Rand.Range(10f, 16f);
            int count = Rand.Range(1, 4);
            var candidates = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer) && FactionTechAllowed(f))
                .OrderBy(f => getTravelDays(f, map));
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
    }
}
