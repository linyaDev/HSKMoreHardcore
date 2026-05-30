using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class RecycleNerf
    {
        static RecycleNerf()
        {
            var reclaimMethod = AccessTools.Method("Mending.JobDriverUtils:Reclaim");
            if (reclaimMethod == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.recyclenerf");
            harmony.Patch(reclaimMethod,
                postfix: new HarmonyMethod(typeof(RecycleNerf), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] RecycleNerf applied.");
        }

        public static void Postfix(ref List<Thing> __result, Thing thing, float efficiency)
        {
            // Только одежда — оружие не трогаем
            if (!thing.def.IsApparel)
                return;

            var costList = CostListCalculator.CostListAdjusted(thing);
            int totalCost = 0;
            foreach (var cost in costList)
            {
                totalCost += cost.count;
            }

            if (totalCost <= 0)
                return;

            // 10-15% of total material cost
            int amount = Mathf.Max(1, Mathf.FloorToInt(totalCost * Rand.Range(0.10f, 0.15f)));

            // Determine output: leather stuff -> Leather_Patch, fabric stuff -> Cloth
            ThingDef outputDef = DetermineOutput(thing);

            Thing output = ThingMaker.MakeThing(outputDef);
            output.stackCount = amount;

            __result.Clear();
            __result.Add(output);
        }

        private static ThingDef DetermineOutput(Thing thing)
        {
            if (thing.Stuff?.stuffProps?.categories != null)
            {
                foreach (var cat in thing.Stuff.stuffProps.categories)
                {
                    if (cat.defName == "Leathery")
                        return RecycleNerfDefs.Leather_Patch;
                }
            }

            return RimWorld.ThingDefOf.Cloth;
        }
    }

    [DefOf]
    public static class RecycleNerfDefs
    {
        public static ThingDef Leather_Patch;
    }
}
