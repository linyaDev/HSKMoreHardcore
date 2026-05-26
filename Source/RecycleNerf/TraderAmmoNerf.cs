using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class TraderAmmoNerf
    {
        private static Type ammoThingType;
        private static HashSet<ThingDef> pricedDefs = new HashSet<ThingDef>();

        static TraderAmmoNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");
            if (ammoThingType == null)
            {
                Log.Warning("[HSKMoreHardcore] TraderAmmoNerf: CombatExtended.AmmoThing not found.");
                return;
            }

            // Увеличиваем цену всех патронов при загрузке
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (ammoThingType.IsAssignableFrom(def.thingClass))
                {
                    float before = def.BaseMarketValue;
                    def.BaseMarketValue *= NerfSettings.ammoPriceMultiplier;
                    pricedDefs.Add(def);
                    Log.Message($"[TraderAmmoNerf] Price {def.defName}: {before} -> {def.BaseMarketValue}");
                }
            }
            Log.Message($"[TraderAmmoNerf] Modified price for {pricedDefs.Count} ammo defs.");

            var generate = AccessTools.Method("RimWorld.ThingSetMaker_TraderStock:Generate");
            if (generate == null)
            {
                Log.Warning("[HSKMoreHardcore] TraderAmmoNerf: ThingSetMaker_TraderStock:Generate not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.traderammo");
            harmony.Patch(generate,
                postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] TraderAmmoNerf applied.");
        }

        public static void Postfix(List<Thing> outThings)
        {
            for (int i = outThings.Count - 1; i >= 0; i--)
            {
                var thing = outThings[i];
                if (ammoThingType.IsInstanceOfType(thing) && thing.stackCount > 1)
                {
                    int before = thing.stackCount;
                    thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.traderAmmoMultiplier));
                    Log.Message($"[TraderAmmoNerf] {thing.def.defName}: {before} -> {thing.stackCount}");
                }
            }
        }
    }
}
