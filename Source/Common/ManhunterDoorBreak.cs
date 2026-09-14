using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Когда любое безумное животное на карте получает урон от колониста,
    /// все manhunter на этой карте начинают ломать двери — но только если их
    /// цель пешка игрока. За чужими пешками (рейдеры, гости) гоняются как в ванилле.
    /// Как только manhunter кого-то убил, флаг для карты снимается (ярость остаётся).
    /// Глобальный флаг per-map: lastManhunterHarmTick.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ManhunterDoorBreak
    {
        private const bool Enabled = true;

        // Диагностика: урон/убийства/решения по каждому людоеду в лог.
        // Причина пропуска пишется только при смене её категории для животного.
        private const bool DebugLog = true;

        // Окно реагирования: 3 часа (7500 тиков) после урона по любому manhunter
        private const int AggroWindowTicks = 7500;

        // Per-map: тик последнего урона по manhunter
        private static Dictionary<int, int> lastHarmTickPerMap = new Dictionary<int, int>();

        // Последняя записанная в лог категория решения по животному (thingIDNumber -> категория)
        private static readonly Dictionary<int, string> lastLoggedDecision = new Dictionary<int, string>();

        static ManhunterDoorBreak()
        {
            if (!Enabled)
            {
                Log.Message("[HSKMoreHardcore] ManhunterDoorBreak disabled.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.manhunterdoorbreak");

            // Postfix на TryGiveJob — перенаправить на двери
            var tryGiveJob = AccessTools.Method(typeof(JobGiver_Manhunter), "TryGiveJob");
            if (tryGiveJob != null)
            {
                harmony.Patch(tryGiveJob,
                    postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(JobPostfix)));
            }

            // Postfix на Pawn_MindState.Notify_DamageTaken — отследить урон по manhunter
            var notifyDamage = AccessTools.Method(typeof(Pawn_MindState), "Notify_DamageTaken");
            if (notifyDamage != null)
            {
                harmony.Patch(notifyDamage,
                    postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(DamagePostfix)));
            }

            // Очистка при загрузке/новой игре
            var gameInit = AccessTools.Method(typeof(Game), "InitNewGame");
            if (gameInit != null)
                harmony.Patch(gameInit, postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(ClearFlags)));

            var gameLoad = AccessTools.Method(typeof(Game), "LoadGame");
            if (gameLoad != null)
                harmony.Patch(gameLoad, postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(ClearFlags)));

            // Postfix на Pawn.Kill — manhunter кого-то убил: снять флаг ломания дверей
            var kill = AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill));
            if (kill != null)
                harmony.Patch(kill, postfix: new HarmonyMethod(typeof(ManhunterDoorBreak), nameof(KillPostfix)));

            Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak applied. DebugLog={DebugLog}");
        }

        public static void ClearFlags()
        {
            lastHarmTickPerMap.Clear();
            lastLoggedDecision.Clear();
        }

        // Manhunter убил любую пешку — «месть» утолена: снимаем флаг ломания дверей
        // для его карты. Сама ярость (ментальное состояние) остаётся; новый урон от
        // колониста снова включит флаг.
        public static void KillPostfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (dinfo?.Instigator is not Pawn killer || !IsManhunter(killer))
                return;

            var map = killer.Map;
            if (map == null)
                return;

            bool hadFlag = lastHarmTickPerMap.Remove(map.uniqueID);
            if (DebugLog)
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(killer)} убил {Describe(__instance)} -> флаг карты {(hadFlag ? "СНЯТ" : "не был активен")}");
        }

        // Когда manhunter получает урон от колониста — ставим глобальный флаг для карты
        public static void DamagePostfix(Pawn_MindState __instance, DamageInfo dinfo)
        {
            var pawn = __instance.pawn;
            if (pawn?.Map == null)
                return;

            if (!IsManhunter(pawn))
                return;

            // Только урон от пешки фракции игрока
            if (dinfo.Instigator is not Pawn attacker || attacker.Faction != Faction.OfPlayer)
                return;

            bool wasActive = IsFlagActive(pawn.Map, out _);
            lastHarmTickPerMap[pawn.Map.uniqueID] = Find.TickManager.TicksGame;
            if (DebugLog)
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(attacker)} ранил {Describe(pawn)} -> флаг карты {(wasActive ? "продлён" : "ВКЛЮЧЁН")} на {AggroWindowTicks} тиков");
        }

        // Manhunter не нашёл цель — если глобальный флаг активен, ломать дверь
        public static void JobPostfix(ref Job __result, Pawn pawn)
        {
            // Только если оригинал не дал атаку (Goto/Wait/null = бродит или застрял)
            if (__result != null && __result.def != JobDefOf.Goto && __result.def != JobDefOf.Wait)
                return;

            if (!IsManhunter(pawn))
                return;

            if (pawn?.Map == null)
                return;

            string original = DescribeJob(__result);

            // Животных с дальней атакой (огнедышащие виверны и т.п.) сначала ведёт префикс
            // HSK SK.Patch_JobGiver_Manhunter_TryGiveJob: его Goto/Wait — выход на позицию
            // для стрельбы или к цели, а не «бродит у двери». Такие задачи не трогаем,
            // иначе животное на полпути к цели разворачивается бить дверь.
            // Если HSK сдался (return true), решает ванилла — её Goto/Wait у запертой
            // колонии перехватываем как у всех. Отличаем по меткам задач HSK:
            // Goto с checkOverrideOnExpire, Wait на 100 тиков (ванильный — 30).
            if (HasRangedVerb(pawn, out Verb rangedVerb) && IsHskRangedJob(__result))
            {
                LogSkip(pawn, "ranged-hsk", $"стрелок (verb {rangedVerb.verbProps.defaultProjectile?.defName ?? "?"}, range {rangedVerb.verbProps.range}), задача от HSK — не трогаем; {original}");
                return;
            }

            // Проверяем глобальный флаг для этой карты
            if (!IsFlagActive(pawn.Map, out int elapsed))
            {
                LogSkip(pawn, "flag-off", $"флаг карты не активен (elapsed={elapsed}); ванилла: {original}");
                return;
            }

            // Цель — как её выбирает сама ванилла. Ломаем двери, только если животное
            // идёт на пешку игрока; за чужими (рейдеры, гости) пусть гонится как обычно,
            // иначе оно разворачивается на полпути к ним и идёт бить дверь колонии.
            Pawn target = FindPawnTarget(pawn);
            if (target == null || target.Faction != Faction.OfPlayer)
            {
                LogSkip(pawn, "not-player:" + (target?.thingIDNumber ?? 0),
                    $"цель не игрока: {(target == null ? "нет цели" : Describe(target))}; ванилла: {original}");
                return;
            }

            // Если рядом есть колонист — не переключаемся на дверь, пусть AI атакует его
            if (HasNearbyEnemy(pawn, 10f, out Pawn nearby))
            {
                LogSkip(pawn, "nearby:" + nearby.thingIDNumber,
                    $"рядом пешка игрока {Describe(nearby)}; цель {Describe(target)}; ванилла: {original}");
                return;
            }

            Building_Door door = FindDoorNearestTo(pawn, target);
            if (door == null)
            {
                LogSkip(pawn, "no-door:" + target.thingIDNumber,
                    $"нет достижимой закрытой двери; цель {Describe(target)}; ванилла: {original}");
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, door);
            job.maxNumMeleeAttacks = 4;
            job.expiryInterval = 600;
            job.canBashDoors = true;
            __result = job;

            if (DebugLog)
            {
                lastLoggedDecision.Remove(pawn.thingIDNumber);
                Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(pawn)} -> ДВЕРЬ {door.Label} @{door.Position} " +
                    $"(цель {Describe(target)}, elapsed={elapsed}); ванилла была: {original}");
            }
        }

        private static bool IsFlagActive(Map map, out int elapsed)
        {
            elapsed = -1;
            if (!lastHarmTickPerMap.TryGetValue(map.uniqueID, out int lastTick))
                return false;
            elapsed = Find.TickManager.TicksGame - lastTick;
            return elapsed <= AggroWindowTicks;
        }

        private static bool IsManhunter(Pawn pawn)
        {
            if (pawn?.MentalStateDef == null)
                return false;
            return pawn.MentalStateDef == MentalStateDefOf.Manhunter
                || pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent;
        }

        // Тот же признак, что в префиксе HSK: есть verb с дальностью больше 1.1
        private static bool HasRangedVerb(Pawn pawn, out Verb ranged)
        {
            ranged = null;
            var verbs = pawn.verbTracker?.AllVerbs;
            if (verbs == null)
                return false;
            for (int i = 0; i < verbs.Count; i++)
            {
                if (verbs[i].verbProps.range > 1.1f)
                {
                    ranged = verbs[i];
                    return true;
                }
            }
            return false;
        }

        // Goto/Wait, выданные префиксом HSK для стрелка (а не ванильным JobGiver_Manhunter):
        //  - Goto на позицию стрельбы: checkOverrideOnExpire = true (у ванильного false);
        //  - Wait на месте: JobMaker.MakeJob(Wait, 100) (ванильный — 30 тиков).
        private static bool IsHskRangedJob(Job job)
        {
            if (job == null)
                return false;
            if (job.def == JobDefOf.Goto)
                return job.checkOverrideOnExpire;
            if (job.def == JobDefOf.Wait)
                return job.expiryInterval == 100;
            return false;
        }

        // Копия JobGiver_Manhunter.FindPawnTarget: любая пешка с интеллектом, двери ломать можно
        private static Pawn FindPawnTarget(Pawn pawn)
        {
            return (Pawn)AttackTargetFinder.BestAttackTarget(pawn,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                x => x is Pawn && (int)x.def.race.intelligence >= 1,
                0f, 9999f, default(IntVec3), float.MaxValue,
                canBashDoors: true, canTakeTargetsCloserThanEffectiveMinRange: true,
                canBashFences: pawn.FenceBlocked);
        }

        private static bool HasNearbyEnemy(Pawn pawn, float radius, out Pawn found)
        {
            found = null;
            float radiusSq = radius * radius;
            foreach (var p in pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (p.Dead)
                    continue;
                if (pawn.Position.DistanceToSquared(p.Position) <= radiusSq)
                {
                    found = p;
                    return true;
                }
            }
            return false;
        }

        // Закрытая дверь колонии, ближайшая к цели животного и достижимая для него
        private static Building_Door FindDoorNearestTo(Pawn pawn, Pawn target)
        {
            var map = pawn.Map;
            if (map == null)
                return null;

            Building_Door best = null;
            float bestDist = float.MaxValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_Door door && !door.Open
                    && pawn.CanReach(door, PathEndMode.Touch, Danger.Deadly))
                {
                    float dist = target.Position.DistanceToSquared(door.Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = door;
                    }
                }
            }

            return best;
        }

        // category — короткий ключ без координат: по нему решаем, писать ли повтор
        private static void LogSkip(Pawn pawn, string category, string detail)
        {
            if (!DebugLog)
                return;
            if (lastLoggedDecision.TryGetValue(pawn.thingIDNumber, out string last) && last == category)
                return;
            lastLoggedDecision[pawn.thingIDNumber] = category;
            Log.Message($"[HSKMoreHardcore] ManhunterDoorBreak: {Describe(pawn)} пропуск: {detail}");
        }

        private static string Describe(Pawn p)
        {
            if (p == null)
                return "null";
            return $"{p.LabelShort}#{p.thingIDNumber} [{p.Faction?.Name ?? "без фракции"}] @{p.Position}";
        }

        private static string DescribeJob(Job job)
        {
            if (job == null)
                return "null";
            var t = job.targetA;
            string target = t.Thing != null ? $"{t.Thing.LabelShort}#{t.Thing.thingIDNumber} @{t.Thing.Position}" : t.Cell.ToString();
            return $"{job.def.defName} -> {target} (expiry={job.expiryInterval}, checkOverride={job.checkOverrideOnExpire})";
        }
    }
}
