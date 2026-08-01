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
            var generatePublic = AccessTools.Method(typeof(ThingSetMaker), "Generate",
                new Type[] { typeof(ThingSetMakerParams) });
            if (generatePublic != null)
            {
                harmony.Patch(generatePublic,
                    postfix: new HarmonyMethod(typeof(TraderAmmoNerf), nameof(StockPostfix)));
                Log.Message("[HSKMoreHardcore] TraderAmmoNerf (stock) applied.");
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

        private static Type traderStockType;
        private static Type rewardMarketValueType;

        public static void StockPostfix(ThingSetMaker __instance, ref List<Thing> __result)
        {
            if (traderStockType == null)
                traderStockType = typeof(ThingSetMaker).Assembly.GetType("RimWorld.ThingSetMaker_TraderStock");
            if (rewardMarketValueType == null)
                rewardMarketValueType = typeof(ThingSetMaker).Assembly.GetType("RimWorld.ThingSetMaker_MarketValue");

            bool isTrader = traderStockType != null && traderStockType.IsInstanceOfType(__instance);
            bool isReward = rewardMarketValueType != null && rewardMarketValueType.IsInstanceOfType(__instance);

            if (!isTrader && !isReward)
                return;

            var settings = HardcoreSettingsDef.Instance;
            int silverMin = settings?.traderSilverMinimum ?? 3500;

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                var thing = __result[i];
                if (isTrader && thing.def == ThingDefOf.Silver && thing.stackCount < silverMin)
                {
                    thing.stackCount = silverMin;
                }
                else if (ammoThingType.IsInstanceOfType(thing) && thing.stackCount > 1)
                {
                    float mult = isTrader ? NerfSettings.traderAmmoMultiplier : NerfSettings.rewardAmmoMultiplier;
                    thing.stackCount = Mathf.Max(5, Mathf.FloorToInt(thing.stackCount * mult));
                }
            }
        }

        public static void PricePostfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            var thing = __instance.AnyThing;
            if (thing == null)
                return;

            if (action == TradeAction.PlayerBuys)
            {
                if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
                {
                    __result *= NerfSettings.ammoPriceMultiplier;
                }
                else if (thing.def.IsWeapon)
                {
                    __result *= NerfSettings.weaponPriceMultiplier;
                }
            }
            else if (action == TradeAction.PlayerSells)
            {
                var settings = HardcoreSettingsDef.Instance;
                if (settings?.sellPriceOverrides != null && settings.sellPriceOverrides.TryGetValue(thing.def.defName, out float mult))
                {
                    __result *= mult;
                }
            }
        }
    }
}
