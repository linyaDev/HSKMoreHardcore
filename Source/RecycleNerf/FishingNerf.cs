using System;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class FishingNerf
    {
        static FishingNerf()
        {
            var zoneType = AccessTools.TypeByName("SK.Zone_Fishing");
            if (zoneType == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.fishingnerf");

            // Slow down fish spawn rate x3
            var updateFishSpawn = AccessTools.Method("SK.Util_Zone_Fishing:UpdateFishSpawnRateFactor");
            if (updateFishSpawn != null)
            {
                harmony.Patch(updateFishSpawn,
                    postfix: new HarmonyMethod(typeof(FishingNerf), nameof(SpawnRatePostfix)));
            }

            // Increase min distance between fishing piers: 10 -> 15
            var isNearPier = AccessTools.Method("SK.Util_PlaceWorker:IsNearFishingPier");
            if (isNearPier != null)
            {
                harmony.Patch(isNearPier,
                    prefix: new HarmonyMethod(typeof(FishingNerf), nameof(PierDistancePrefix)));
            }

            // Slow down fish traps x4
            var trapSpawnSetup = AccessTools.Method("SK.Building_FishTrap:SpawnSetup");
            if (trapSpawnSetup != null)
            {
                harmony.Patch(trapSpawnSetup,
                    postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapSpawnPostfix)));
            }

            var trapPlaceProduct = AccessTools.Method("SK.Building_FishTrap:PlaceProduct");
            if (trapPlaceProduct != null)
            {
                harmony.Patch(trapPlaceProduct,
                    postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapPlaceProductPostfix)));
            }

            Log.Message("[HSKMoreHardcore] FishingNerf applied.");
        }

        // spawnRateFactor is multiplied into MTB: higher = slower spawn
        // We multiply it by 3 to make fish spawn 3x slower
        public static void SpawnRatePostfix(ref float spawnRateFactor)
        {
            spawnRateFactor *= 3f;
        }

        // Increase min distance between piers: 10 -> 15
        public static void PierDistancePrefix(ref float distance)
        {
            if (distance == 10f)
                distance = 15f;
        }

        // Fish trap: multiply ticksToCatch x4 after SpawnSetup calculates it
        public static void TrapSpawnPostfix(object __instance)
        {
            var field = AccessTools.Field(__instance.GetType(), "ticksToCatch");
            var totalField = AccessTools.Field(__instance.GetType(), "totalTicks");
            if (field != null)
            {
                int ticks = (int)field.GetValue(__instance);
                field.SetValue(__instance, ticks * 4);
                totalField?.SetValue(__instance, ticks * 4);
            }
        }

        // Fish trap: multiply ticksToCatch x4 after PlaceProduct resets it
        public static void TrapPlaceProductPostfix(object __instance)
        {
            var field = AccessTools.Field(__instance.GetType(), "ticksToCatch");
            var totalField = AccessTools.Field(__instance.GetType(), "totalTicks");
            if (field != null)
            {
                int ticks = (int)field.GetValue(__instance);
                field.SetValue(__instance, ticks * 4);
                totalField?.SetValue(__instance, ticks * 4);
            }
        }
    }
}
