using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Когда любое безумное животное на карте получает урон от колониста,
    /// все manhunter на этой карте начинают ломать двери.
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

            Log.Message("[HSKMoreHardcore] ManhunterDoorBreak applied.");
        }

        public static void ClearFlags()
        {
            lastHarmTickPerMap.Clear();
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

            Log.Message($"[ManhunterDoorBreak] HARM FLAG SET: {pawn.LabelShort} hit by {attacker.LabelShort} on map {pawn.Map.uniqueID}, tick={Find.TickManager.TicksGame}");
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
            {
                Log.Message($"[ManhunterDoorBreak] {pawn.LabelShort}: no harm flag for map {pawn.Map.uniqueID}");
                return;
            }

            int elapsed = Find.TickManager.TicksGame - lastTick;
            if (elapsed > AggroWindowTicks)
            {
                Log.Message($"[ManhunterDoorBreak] {pawn.LabelShort}: harm flag expired ({elapsed} > {AggroWindowTicks})");
                return;
            }

            Log.Message($"[ManhunterDoorBreak] {pawn.LabelShort}: harm flag active (elapsed={elapsed}), looking for door...");

            Building_Door door = FindNearestDoor(pawn);
            if (door == null)
            {
                Log.Message($"[ManhunterDoorBreak] {pawn.LabelShort}: no reachable door found");
                return;
            }

            Log.Message($"[ManhunterDoorBreak] {pawn.LabelShort}: attacking door at {door.Position}");
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

        private static Building_Door FindNearestDoor(Pawn pawn)
        {
            Building_Door best = null;
            float bestDist = float.MaxValue;

            var map = pawn.Map;
            if (map == null)
                return null;

            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (b is Building_Door door && !door.Open
                    && pawn.CanReach(door, PathEndMode.Touch, Danger.Deadly))
                {
                    float dist = pawn.Position.DistanceToSquared(door.Position);
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
