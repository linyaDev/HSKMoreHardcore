using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
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

            // Помечаем одежду при раздевании вражеской пешки
            var apparelTryDrop = AccessTools.Method(typeof(Pawn_ApparelTracker), "TryDrop",
                new Type[] { typeof(RimWorld.Apparel), typeof(RimWorld.Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) });
            if (apparelTryDrop != null)
            {
                harmony.Patch(apparelTryDrop,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(ApparelDropPostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (apparel worn mark) applied.");
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

        // Помечаем одежду при раздевании вражеской пешки
        public static void ApparelDropPostfix(Pawn_ApparelTracker __instance, RimWorld.Apparel ap, RimWorld.Apparel resultingAp)
        {
            var droppedApparel = resultingAp ?? ap;
            if (droppedApparel == null)
                return;

            var pawn = __instance.pawn;

            // Добавляем комп если нет
            var comp = droppedApparel.TryGetComp<CompWornByEnemy>();
            if (comp == null)
            {
                comp = new CompWornByEnemy();
                comp.parent = droppedApparel;
                droppedApparel.AllComps.Add(comp);
            }

            if (pawn != null && pawn.Faction != null && pawn.Faction.IsPlayer)
            {
                comp.worn = true;
            }
            else
            {
                comp.wornByEnemy = true;
                // Нерф прочности брони, снятой с врага (по защите и нашему уровню развития)
                ArmorLootNerf.Apply(droppedApparel, "enemy");
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

                if (container.Count > 0)
                {
                    string items = "";
                    foreach (var t in container)
                        items += $"{t.def.defName}x{t.stackCount}, ";
                    Log.Message($"[EnemyLootNerf] InventoryGen {p.LabelShort}: [{items}]");
                }

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
            WeaponHpCalc calc = ComputeWeaponHpMult(isAwayMap);
            string tag = isAwayMap ? "away" : "home";

            if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1 && UsesArrows(resultingEq))
            {
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} uses arrows, skipping HP nerf");
            }
            else if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1)
            {
                int before = resultingEq.HitPoints;
                int target = Mathf.Max(1, Mathf.FloorToInt(resultingEq.MaxHitPoints * calc.mult));
                resultingEq.HitPoints = Mathf.Min(before, target);
                string note = target >= before ? "; цель >= текущей, оставлено боевое значение" : "";
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} " +
                            $"{WeaponHpBreakdown(calc)}, цель={target}/{resultingEq.MaxHitPoints}, " +
                            $"HP {before}/{resultingEq.MaxHitPoints} -> {resultingEq.HitPoints}/{resultingEq.MaxHitPoints}{note}");
            }
        }

        // Разбор расчёта множителя прочности оружия (для логов)
        private struct WeaponHpCalc
        {
            public bool away;
            public float roll;    // Rand.Value (NaN на away — там фиксированный множитель)
            public float curved;  // roll^power
            public float mult;
        }

        private static WeaponHpCalc ComputeWeaponHpMult(bool isAwayMap)
        {
            WeaponHpCalc c = default;
            c.away = isAwayMap;
            if (isAwayMap)
            {
                c.roll = float.NaN;
                c.curved = float.NaN;
                c.mult = NerfSettings.weaponHpMultiplierAway;
            }
            else
            {
                c.roll = Rand.Value;
                c.curved = Mathf.Pow(c.roll, NerfSettings.weaponHpCurvePower);
                c.mult = NerfSettings.weaponHpMultiplierHomeMin
                       + (NerfSettings.weaponHpMultiplierHomeMax - NerfSettings.weaponHpMultiplierHomeMin) * c.curved;
            }
            return c;
        }

        private static string WeaponHpBreakdown(WeaponHpCalc c)
        {
            if (c.away)
                return $"away (фикс. множитель={c.mult:F2})";
            return $"roll={c.roll:F3}, roll^{NerfSettings.weaponHpCurvePower:F1}={c.curved:F3}, " +
                   $"диапазон {NerfSettings.weaponHpMultiplierHomeMin:F2}..{NerfSettings.weaponHpMultiplierHomeMax:F2} => множитель={c.mult:F2}";
        }

        private static bool UsesArrows(Thing thing)
        {
            var comp = thing.TryGetComp<ThingComp>();
            // Ищем CompAmmoUser через рефлексию
            foreach (var c in (thing as ThingWithComps)?.AllComps ?? new List<ThingComp>())
            {
                var propsField = AccessTools.Field(c.GetType(), "props");
                var props = propsField?.GetValue(c);
                if (props == null) continue;

                var ammoSetField = AccessTools.Field(props.GetType(), "ammoSet");
                if (ammoSetField == null) continue;

                var ammoSet = ammoSetField.GetValue(props) as Def;
                if (ammoSet != null && ammoSet.defName.Contains("Arrow"))
                    return true;
            }
            return false;
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

            // Damage weapons in inventory (один бросок на пешку, применяется ко всему её оружию в инвентаре)
            WeaponHpCalc weaponCalc = ComputeWeaponHpMult(isAwayMap);
            for (int i = container.Count - 1; i >= 0; i--)
            {
                var thing = container[i];
                if (thing.def.IsWeapon && thing.HitPoints > 1 && !UsesArrows(thing))
                {
                    int before = thing.HitPoints;
                    int target = Mathf.Max(1, Mathf.FloorToInt(thing.MaxHitPoints * weaponCalc.mult));
                    thing.HitPoints = Mathf.Min(before, target);
                    string note = target >= before ? "; цель >= текущей, оставлено боевое значение" : "";
                    Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: InvWeapon {thing.def.defName} " +
                                $"{WeaponHpBreakdown(weaponCalc)}, цель={target}/{thing.MaxHitPoints}, " +
                                $"HP {before}/{thing.MaxHitPoints} -> {thing.HitPoints}/{thing.MaxHitPoints}{note}");
                }
            }
        }
    }
}
