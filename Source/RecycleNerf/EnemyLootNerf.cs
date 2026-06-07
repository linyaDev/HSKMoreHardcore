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

            // Нерф расходников/оружия из инвентаря — на универсальном GenDrop.TryDropSpawn,
            // чтобы ловить все пути дропа (раздеть всё, выборочно через NonUnoPinata, смерть).
            var tryDropSpawn = AccessTools.Method(typeof(GenDrop), "TryDropSpawn");
            if (tryDropSpawn != null)
            {
                harmony.Patch(tryDropSpawn,
                    prefix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(TryDropSpawnPrefix)));
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

            // Замена индустриальной/ультратех медицины на травы при спавне (ловит мед, добавленный
            // после генерации — например CE-медиком). Только враждебные, торговцев не трогаем.
            var spawnSetup = AccessTools.Method(typeof(Pawn), "SpawnSetup");
            if (spawnSetup != null)
            {
                harmony.Patch(spawnSetup,
                    postfix: new HarmonyMethod(typeof(EnemyLootNerf), nameof(SpawnSetupMedicinePostfix)));
                Log.Message("[HSKMoreHardcore] EnemyLootNerf (spawn medicine replace) applied.");
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

        // При спавне врага меняем индустриальную/ультратех медицину в инвентаре на травы.
        // Ловит мед, добавленный после генерации (CE-медик и т.п.). Торговцы/нейтралы не задеты.
        public static void SpawnSetupMedicinePostfix(Pawn __instance, bool respawningAfterLoad)
        {
            try
            {
                if (respawningAfterLoad)
                    return;
                var p = __instance;
                if (p?.inventory == null || p.Faction == null)
                    return;
                if (!p.HostileTo(Faction.OfPlayer)) // только враждебные: торговцев/нейтралов не трогаем
                    return;

                var herbal = DefDatabase<ThingDef>.GetNamedSilentFail("HerbMedicine")
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
                if (herbal == null)
                    return;

                var container = p.inventory.innerContainer;
                for (int i = container.Count - 1; i >= 0; i--)
                {
                    var thing = container[i];
                    if (thing.def.defName == "MedicineIndustrial" || thing.def.defName == "MedicineUltratech")
                    {
                        int count = thing.stackCount;
                        Log.Message($"[EnemyLootNerf] [spawn] Replace {thing.def.defName} x{count} -> {herbal.defName} on {p.LabelShort}");
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
                Log.Error($"[EnemyLootNerf] SpawnSetupMedicinePostfix error on {__instance?.LabelShort}: {e}");
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
            string tag = isAwayMap ? "away" : "home";

            if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1 && UsesArrows(resultingEq))
            {
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} uses arrows, skipping HP nerf");
            }
            else if (resultingEq.def.IsWeapon && resultingEq.HitPoints > 1)
            {
                WeaponHpCalc calc = ComputeWeaponHpMult(resultingEq, isAwayMap);
                int before = resultingEq.HitPoints;
                int target = Mathf.Max(1, Mathf.FloorToInt(resultingEq.MaxHitPoints * calc.mult));
                resultingEq.HitPoints = Mathf.Min(before, target);
                string note = target >= before ? "; цель >= текущей, оставлено боевое значение" : "";
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Weapon {resultingEq.def.defName} " +
                            $"{WeaponHpBreakdown(calc)}, цель={target}/{resultingEq.MaxHitPoints}, " +
                            $"HP {before}/{resultingEq.MaxHitPoints} -> {resultingEq.HitPoints}/{resultingEq.MaxHitPoints}{note}");
            }
        }

        // Разбор расчёта множителя прочности оружия (для логов).
        // Прогрессия по разрыву техуровня, как у брони: power растёт с gap = техур.оружия − развитие.
        private struct WeaponHpCalc
        {
            public bool away;
            public int tier;     // техуровень оружия (def.techLevel)
            public int dev;      // наш уровень развития
            public int gap;      // tier - dev
            public float power;  // степень кривой (0 если нерфа нет)
            public float roll;   // Rand.Value (NaN если нерфа нет / away)
            public float mult;
        }

        private static WeaponHpCalc ComputeWeaponHpMult(Thing weapon, bool isAwayMap)
        {
            WeaponHpCalc c = default;
            c.away = isAwayMap;
            if (isAwayMap)
            {
                c.roll = float.NaN;
                c.mult = NerfSettings.weaponHpMultiplierAway;
                return c;
            }

            c.tier = (int)weapon.def.techLevel;
            c.dev = ArmorLootNerf.PlayerDevLevel();
            c.gap = c.tier - c.dev;

            if (c.gap <= 0)
            {
                // Оружие нашего уровня или ниже — не трогаем
                c.power = 0f;
                c.roll = float.NaN;
                c.mult = 1f;
                return c;
            }

            c.power = NerfSettings.weaponGapPower * c.gap;
            c.roll = HardcoreGameComponent.Roll(weapon.def.defName);
            c.mult = NerfSettings.weaponHpMultiplierHomeMin
                   + (1f - NerfSettings.weaponHpMultiplierHomeMin) * Mathf.Pow(c.roll, c.power);
            return c;
        }

        private static string WeaponHpBreakdown(WeaponHpCalc c)
        {
            if (c.away)
                return $"away (фикс. множитель={c.mult:F2})";

            string head = $"техур.оружия={ArmorLootNerf.TierName(c.tier)}, развитие={ArmorLootNerf.TierName(c.dev)}, разрыв={c.gap}";
            if (c.gap <= 0)
                return head + " (<=0, без изменений)";
            return head + $", power={c.power:F1}, roll={c.roll:F3}, roll^power={Mathf.Pow(c.roll, c.power):F3} => множитель={c.mult:F2}";
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

        // Срабатывает на ЛЮБОМ дропе вещи; нерфим только то, что падает из инвентаря вражеской пешки.
        // Это ловит все пути: «раздеть всё» (DropAllNearPawn), выборочно (NonUnoPinata innerContainer.TryDrop), смерть.
        public static void TryDropSpawnPrefix(Thing thing, Map map)
        {
            if (thing == null)
                return;

            // Только инвентарь вражеской пешки (не игрок/пленные/рабы). Экипировка/одежда сюда не попадают.
            var pawn = (thing.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (pawn == null || pawn.Faction == null || pawn.Faction.IsPlayer
                || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
                return;

            bool isAwayMap = map != null && !map.IsPlayerHome;
            string tag = isAwayMap ? "away" : "home";

            if (thing.def.IsMedicine)
            {
                float medMult = isAwayMap ? NerfSettings.medicineDropMultiplierAway : NerfSettings.medicineDropMultiplier;
                int before = thing.stackCount;
                thing.stackCount = Mathf.Max(1, Mathf.FloorToInt(thing.stackCount * medMult));
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: Medicine {thing.def.defName} {before} -> {thing.stackCount}");
            }
            else if (ammoThingType != null && ammoThingType.IsInstanceOfType(thing))
            {
                float ammoMult = isAwayMap ? NerfSettings.ammoDropMultiplierAway : NerfSettings.ammoDropMultiplier;
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
            else if (thing.def.IsWeapon && thing.HitPoints > 1 && !UsesArrows(thing))
            {
                WeaponHpCalc calc = ComputeWeaponHpMult(thing, isAwayMap);
                int before = thing.HitPoints;
                int target = Mathf.Max(1, Mathf.FloorToInt(thing.MaxHitPoints * calc.mult));
                thing.HitPoints = Mathf.Min(before, target);
                string note = target >= before ? "; цель >= текущей, оставлено боевое значение" : "";
                Log.Message($"[EnemyLootNerf] [{tag}] {pawn.LabelShort}: InvWeapon {thing.def.defName} " +
                            $"{WeaponHpBreakdown(calc)}, цель={target}/{thing.MaxHitPoints}, " +
                            $"HP {before}/{thing.MaxHitPoints} -> {thing.HitPoints}/{thing.MaxHitPoints}{note}");
            }
            else if (thing is RimWorld.Apparel apparel)
            {
                // Одежда в инвентаре врага (в т.ч. груз вьючных животных): метка «со следами боя» + нерф прочности
                var comp = apparel.TryGetComp<CompWornByEnemy>();
                if (comp != null)
                    comp.wornByEnemy = true;
                ArmorLootNerf.Apply(apparel, tag);
            }
        }
    }
}
