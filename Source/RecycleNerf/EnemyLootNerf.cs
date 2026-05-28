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

            // Пропускаем Core_SK Det() для оружия — мы нерфим его сами
            var detMethod = AccessTools.Method("SK.Patch_GenDrop:Det");
            if (detMethod != null)
            {
                harmony.Patch(detMethod,
                    prefix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(DetPrefix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (skip Det for weapons) applied.");
            }

            var genInv = AccessTools.Method(typeof(RimWorld.PawnInventoryGenerator), "GenerateInventoryFor");
            if (genInv != null)
            {
                harmony.Patch(genInv,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(InventoryGenPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (inventory gen) applied.");
            }

            var tryDrop = AccessTools.Method(typeof(Pawn_EquipmentTracker), "TryDropEquipment");
            if (tryDrop != null)
            {
                harmony.Patch(tryDrop,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(TryDropEquipmentPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (equipment) applied.");
            }
        }

        // Пропускаем Core_SK урон для оружия — наша формула вместо этого
        public static bool DetPrefix(Thing thing)
        {
            if (thing != null && thing.def.IsWeapon)
                return false; // пропускаем Det() для оружия
            return true;
        }

        public static void InventoryGenPostfix(Pawn p)
        {
            try
            {
                if (p == null || (p.Faction != null && p.Faction.IsPlayer))
                    return;

                // Не трогать торговцев
                if (p.kindDef?.trader == true)
                    return;

                var herbal = DefDatabase<ThingDef>.GetNamedSilentFail("HerbMedicine")
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
                if (herbal == null)
                    return;

                var container = p.inventory?.innerContainer;
                if (container == null)
                    return;

                Log.Message($"[EnemyLootNerf] InventoryGen for {p.LabelShort}, faction={p.Faction?.Name}, items={container.Count}");

                for (int i = container.Count - 1; i >= 0; i--)
                {
                    var thing = container[i];
                    if (thing.def.defName == "MedicineIndustrial" || thing.def.defName == "MedicineUltratech")
                    {
                        int count = thing.stackCount;
                        Log.Message($"[EnemyLootNerf] Replace {thing.def.defName} x{count} -> MedicineHerbal on {p.LabelShort}");
                        container.Remove(thing);
                        thing.Destroy();
                        var replacement = ThingMaker.MakeThing(herbal);
                        replacement.stackCount = count;
                        container.TryAdd(replacement);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[EnemyLootNerf] InventoryGenPostfix error on {p?.LabelShort}: {e}");
            }
        }

        public static void TryDropEquipmentPostfix(Pawn_EquipmentTracker __instance, bool __result, ThingWithComps resultingEq)
        {
            if (!__result || resultingEq == null)
                return;

            var pawn = __instance.pawn;
            if (pawn == null || (pawn.Faction != null && pawn.Faction.IsPlayer))
                return;

            var map = pawn.Map ?? pawn.MapHeld;
            bool isAwayMap = map != null && !map.IsPlayerHome;
            float hpMult = isAwayMap ? NerfSettings.weaponHpMultiplierAway
                : NerfSettings.weaponHpMultiplierHomeMin + (NerfSettings.weaponHpMultiplierHomeMax - NerfSettings.weaponHpMultiplierHomeMin) * Mathf.Pow(Rand.Value, NerfSettings.weaponHpCurvePower);
            string tag = isAwayMap ? "away" : "home";

            if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1)
            {
                int before = resultingEq.HitPoints;
                int target = Mathf.Max(1, Mathf.FloorToInt(resultingEq.MaxHitPoints * hpMult));
                resultingEq.HitPoints = Mathf.Min(before, target);
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} HP {before}/{resultingEq.MaxHitPoints} -> {resultingEq.HitPoints}/{resultingEq.MaxHitPoints}");
            }
        }

        public static void Prefix(Pawn_InventoryTracker __instance)
        {
            var pawn = __instance.pawn;

            // Only nerf enemy pawns
            if (pawn == null || (pawn.Faction != null && pawn.Faction.IsPlayer))
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
                else if (thing.def.IsDrug && thing.stackCount > 1)
                {
                    int before = thing.stackCount;
                    thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * NerfSettings.drugDropMultiplier));
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Drug {thing.def.defName} {before} -> {thing.stackCount}");
                }
            }

            // Damage weapons in inventory
            float weaponHpMult = isAwayMap ? NerfSettings.weaponHpMultiplierAway
                : NerfSettings.weaponHpMultiplierHomeMin + (NerfSettings.weaponHpMultiplierHomeMax - NerfSettings.weaponHpMultiplierHomeMin) * Mathf.Pow(Rand.Value, NerfSettings.weaponHpCurvePower);
            for (int i = container.Count - 1; i >= 0; i--)
            {
                var thing = container[i];
                if (thing.def.IsWeapon && thing.HitPoints > 1)
                {
                    int before = thing.HitPoints;
                    int target = Mathf.Max(1, Mathf.FloorToInt(thing.MaxHitPoints * weaponHpMult));
                    thing.HitPoints = Mathf.Min(before, target);
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: InvWeapon {thing.def.defName} HP {before}/{thing.MaxHitPoints} -> {thing.HitPoints}/{thing.MaxHitPoints}");
                }
            }
        }
    }
}
