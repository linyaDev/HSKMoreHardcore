using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class TraderAmmoNerf
    {
        private static Type ammoThingType;

        static TraderAmmoNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");
            if (ammoThingType == null)
            {
                Log.Warning("[HSKMoreHardcore] TraderAmmoNerf: CombatExtended.AmmoThing not found.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.traderammo");

            // Патч на количество патронов у торговцев
            var traderStockType = typeof(ThingSetMaker).Assembly.GetType("RimWorld.ThingSetMaker_TraderStock");
            if (traderStockType != null)
            {
                var generate = AccessTools.Method(traderStockType, "Generate",
                    new Type[] { typeof(ThingSetMakerParams), typeof(List<Thing>) });
                if (generate != null)
                {
                    harmony.Patch(generate,
                        postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(StockPostfix)));
                    Log.Message("[HSKMoreHardcore] TraderAmmoNerf (stock) applied.");
                }
                else
                {
                    Log.Warning("[HSKMoreHardcore] TraderAmmoNerf: Generate method not found on ThingSetMaker_TraderStock.");
                }
            }

            // Патч на цену покупки у торговца
            var getPriceFor = AccessTools.Method(typeof(Tradeable), "GetPriceFor");
            if (getPriceFor != null)
            {
                harmony.Patch(getPriceFor,
                    postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(PricePostfix)));
                Log.Message("[HSKMoreHardcore] TraderAmmoNerf (price) applied.");
            }
        }

        public static void StockPostfix(ThingSetMakerParams parms, List<Thing> outThings)
        {
            for (int i = outThings.Count - 1; i >= 0; i--)
            {
                var thing = outThings[i];
                if (ammoThingType.IsInstanceOfType(thing) && thing.stackCount > 1)
                {
                    int before = thing.stackCount;
                    thing.stackCount = Mathf.Max(5, Mathf.FloorToInt(thing.stackCount * NerfSettings.traderAmmoMultiplier));
                    Log.Message($"[TraderAmmoNerf] {thing.def.defName}: {before} -> {thing.stackCount}");
                }
            }
        }

        public static void PricePostfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            if (action != TradeAction.PlayerBuys)
                return;

            var thing = __instance.AnyThing;
            if (thing == null)
                return;

            if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
            {
                float before = __result;
                __result *= NerfSettings.ammoPriceMultiplier;
                Log.Message($"[TraderAmmoNerf] Price ammo {thing.def.defName}: {before} -> {__result}");
            }
            else if (thing.def.IsWeapon)
            {
                float before = __result;
                __result *= NerfSettings.weaponPriceMultiplier;
                Log.Message($"[TraderAmmoNerf] Price weapon {thing.def.defName}: {before} -> {__result}");
            }
        }
    }
}
