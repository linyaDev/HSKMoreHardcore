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
        // Окно реагирования: 3 часа (7500 тиков) после урона по любому manhunter
        private const int AggroWindowTicks = 7500;

        // Per-map: тик последнего урона по manhunter
        private static Dictionary<int, int> lastHarmTickPerMap = new Dictionary<int, int>();

        static ManhunterDoorBreak()
        {
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

            Log.Message("[HSKMoreHardcore] ManhunterDoorBreak applied.");
        }

        public static void ClearFlags()
        {
            lastHarmTickPerMap.Clear();
        }

        // Manhunter убил любую пешку — «месть» утолена: снимаем флаг ломания дверей
        // для его карты. Сама ярость (ментальное состояние) остаётся; новый урон от
        // колониста снова включит флаг.
        public static void KillPostfix(DamageInfo? dinfo)
        {
            if (dinfo?.Instigator is not Pawn killer || !IsManhunter(killer))
                return;

            var map = killer.Map;
            if (map != null)
                lastHarmTickPerMap.Remove(map.uniqueID);
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

            lastHarmTickPerMap[pawn.Map.uniqueID] = Find.TickManager.TicksGame;
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

            // Проверяем глобальный флаг для этой карты
            if (!lastHarmTickPerMap.TryGetValue(pawn.Map.uniqueID, out int lastTick))
                return;

            int elapsed = Find.TickManager.TicksGame - lastTick;
            if (elapsed > AggroWindowTicks)
                return;

            // Цель — как её выбирает сама ванилла. Ломаем двери, только если животное
            // идёт на пешку игрока; за чужими (рейдеры, гости) пусть гонится как обычно,
            // иначе оно разворачивается на полпути к ним и идёт бить дверь колонии.
            Pawn target = FindPawnTarget(pawn);
            if (target == null || target.Faction != Faction.OfPlayer)
                return;

            // Если рядом есть колонист — не переключаемся на дверь, пусть AI атакует его
            if (HasNearbyEnemy(pawn, 10f))
                return;

            Building_Door door = FindDoorNearestTo(pawn, target);
            if (door == null)
                return;
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, door);
            job.maxNumMeleeAttacks = 4;
            job.expiryInterval = 600;
            job.canBashDoors = true;
            __result = job;
        }

        private static bool IsManhunter(Pawn pawn)
        {
            if (pawn?.MentalStateDef == null)
                return false;
            return pawn.MentalStateDef == MentalStateDefOf.Manhunter
                || pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent;
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

        private static bool HasNearbyEnemy(Pawn pawn, float radius)
        {
            float radiusSq = radius * radius;
            foreach (var p in pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (p.Dead)
                    continue;
                if (pawn.Position.DistanceToSquared(p.Position) <= radiusSq)
                    return true;
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
    }
}
