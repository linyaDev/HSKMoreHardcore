using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class EnemyLootNerf
    {
        private static Type ammoThingType;

        static EnemyLootNerf()
        {
            ammoThingType = AccessTools.TypeByName("CombatExtended.AmmoThing");

            var harmony = new Harmony("linya.hskmorehardcore.enemylootnerf");

            var original = AccessTools.Method(typeof(Pawn_InventoryTracker), "DropAllNearPawn");
            if (original != null)
            {
                harmony.Patch(original,
                    prefix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(Prefix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf applied.");
            }

            var tryDrop = AccessTools.Method(typeof(Pawn_EquipmentTracker), "TryDropEquipment");
            if (tryDrop != null)
            {
                harmony.Patch(tryDrop,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(TryDropEquipmentPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (equipment) applied.");
            }
        }

        public static void TryDropEquipmentPostfix(Pawn_EquipmentTracker __instance, bool __result, ThingWithComps resultingEq)
        {
            if (!__result || resultingEq == null)
                return;

            var pawn = __instance.pawn;
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer)
                return;

            var map = pawn.Map ?? pawn.MapHeld;
            bool isAwayMap = map != null && !map.IsPlayerHome;
            float hpMult = isAwayMap ? NerfSettings.weaponHpMultiplierAway : NerfSettings.weaponHpMultiplierHome;
            string tag = isAwayMap ? "away" : "home";

            if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1)
            {
                int before = resultingEq.HitPoints;
                resultingEq.HitPoints = Mathf.Max(1, Mathf.FloorToInt(resultingEq.MaxHitPoints * hpMult));
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} HP {before}/{resultingEq.MaxHitPoints} -> {resultingEq.HitPoints}/{resultingEq.MaxHitPoints}");
            }
        }

        public static void Prefix(Pawn_InventoryTracker __instance)
        {
            var pawn = __instance.pawn;

            // Only nerf enemy pawns
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer)
                return;

            var map = pawn.Map ?? pawn.MapHeld;
            bool isAwayMap = map != null && !map.IsPlayerHome;
            float medMult = isAwayMap ? NerfSettings.medicineDropMultiplierAway : NerfSettings.medicineDropMultiplier;
            float ammoMult = isAwayMap ? NerfSettings.ammoDropMultiplierAway : NerfSettings.ammoDropMultiplier;
            string tag = isAwayMap ? "away" : "home";
            var mapParent = map?.Parent;
            Log.Message($"[EnemyLootNerf] Pawn={pawn.LabelShort}, Faction={pawn.Faction?.Name}, Map={map}, MapNull={pawn.Map == null}, MapHeldNull={pawn.MapHeld == null}, IsPlayerHome={map?.IsPlayerHome}, ParentType={mapParent?.GetType()?.Name}, ParentDef={mapParent?.def?.defName}");

            var container = __instance.innerContainer;
            for (int i = container.Count - 1; i >= 0; i--)
            {
                var thing = container[i];
                if (thing.def.IsMedicine)
                {
                    int before = thing.stackCount;
                    thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * medMult));
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Medicine {thing.def.defName} {before} -> {thing.stackCount}");
                }
                else if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
                {
                    int before = thing.stackCount;
                    thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * ammoMult));
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Ammo {thing.def.defName} {before} -> {thing.stackCount}");
                }
            }

            // Damage weapons in inventory
            float weaponHpMult = isAwayMap ? NerfSettings.weaponHpMultiplierAway : NerfSettings.weaponHpMultiplierHome;
            for (int i = container.Count - 1; i >= 0; i--)
            {
                var thing = container[i];
                if (thing.def.IsWeapon && thing.HitPoints > 1)
                {
                    int before = thing.HitPoints;
                    thing.HitPoints = Mathf.Max(1, Mathf.FloorToInt(thing.MaxHitPoints * weaponHpMult));
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: InvWeapon {thing.def.defName} HP {before}/{thing.MaxHitPoints} -> {thing.HitPoints}/{thing.MaxHitPoints}");
                }
            }
        }
    }
}
