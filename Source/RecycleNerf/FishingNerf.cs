using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class FishingNerf
    {
        private static Type pierTypeCache;

        private static Dictionary<int, int> GetTimers()
        {
            return HardcoreGameComponent.Get()?.FishSpawnTimers;
        }

        static FishingNerf()
        {
            var zoneType = AccessTools.TypeByName("SK.Zone_Fishing");
            if (zoneType == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.fishingnerf");

            // Убиваем оригинальное MTB пополнение (ставим огромный фактор)
            var updateFishSpawn = AccessTools.Method("SK.Util_Zone_Fishing:UpdateFishSpawnRateFactor");
            if (updateFishSpawn != null)
            {
                harmony.Patch(updateFishSpawn,
                    postfix: new HarmonyMethod(typeof(FishingNerf), nameof(SpawnRatePostfix)));
            }

            var pierType = AccessTools.TypeByName("SK.Building_FishingPier");
            if (pierType != null)
            {
                // Постфикс на TickRare — наш фиксированный таймер
                var tickRare = AccessTools.Method(pierType, "TickRare");
                if (tickRare != null)
                {
                    harmony.Patch(tickRare,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TickRarePostfix)));
                }

                // Показать время до пополнения
                var getInspectString = AccessTools.Method(pierType, "GetInspectString");
                if (getInspectString != null)
                {
                    harmony.Patch(getInspectString,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(InspectStringPostfix)));
                }

                // Причал начинает с 0 рыбы
                var spawnSetup = AccessTools.Method(pierType, "SpawnSetup");
                if (spawnSetup != null)
                {
                    harmony.Patch(spawnSetup,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(PierSpawnPostfix)));
                }
            }

            // Increase min distance between fishing piers: 10 -> 15
            var isNearPier = AccessTools.Method("SK.Util_PlaceWorker:IsNearFishingPier");
            if (isNearPier != null)
            {
                harmony.Patch(isNearPier,
                    prefix: new HarmonyMethod(typeof(FishingNerf), nameof(PierDistancePrefix)));
            }

            // Перевод ловушек на нашу формулу таймера (биом × fishSpawnSlowdown)
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
                    prefix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapPlaceProductPrefix)),
                    postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapPlaceProductPostfix)));
            }

            // Показать время до поимки у ловушки
            var trapType = AccessTools.TypeByName("SK.Building_FishTrap");
            if (trapType != null)
            {
                var trapInspect = AccessTools.Method(trapType, "GetInspectString");
                if (trapInspect != null)
                {
                    harmony.Patch(trapInspect,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapInspectPostfix)));
                }
            }

            // Ограничение количества ловушек
            var trapPlaceWorkerType = AccessTools.TypeByName("SK.PlaceWorker_FishTrap");
            if (trapPlaceWorkerType != null)
            {
                var trapAllows = AccessTools.Method(trapPlaceWorkerType, "AllowsPlacing");
                if (trapAllows != null)
                {
                    harmony.Patch(trapAllows,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(TrapLimitPostfix)));
                }
            }

            // Ограничение количества причалов
            var placeWorkerType = AccessTools.TypeByName("SK.PlaceWorker_FishingPierSpawner");
            if (placeWorkerType != null)
            {
                var allowsPlacing = AccessTools.Method(placeWorkerType, "AllowsPlacing");
                if (allowsPlacing != null)
                {
                    harmony.Patch(allowsPlacing,
                        postfix: new HarmonyMethod(typeof(FishingNerf), nameof(PierLimitPostfix)));
                }
            }

            Log.Message("[HSKMoreHardcore] FishingNerf applied.");
        }

        // Убиваем оригинальное MTB — ставим огромный фактор чтобы MTB никогда не сработал
        public static void SpawnRatePostfix(ref float spawnRateFactor)
        {
            spawnRateFactor = 999999f;
        }

        // Рассчитать интервал пополнения в тиках (повторяем формулу из Util_Zone_Fishing)
        private static int GetSpawnIntervalTicks(object pier)
        {
            var oceanField = AccessTools.Field(pier.GetType(), "oceanCellsCount");
            var riverField = AccessTools.Field(pier.GetType(), "riverCellsCount");
            var marshField = AccessTools.Field(pier.GetType(), "marshCellsCount");
            var tempField = AccessTools.Field(pier.GetType(), "isAffectedByBadTemperature");

            int ocean = (int)oceanField.GetValue(pier);
            int river = (int)riverField.GetValue(pier);
            int marsh = (int)marshField.GetValue(pier);
            int total = ocean + river + marsh;

            if (total <= 0)
                return int.MaxValue;

            var thing = pier as Thing;
            if (thing?.Map == null)
                return int.MaxValue;

            // Биомный множитель
            float biomeFactor = 1f;
            var getBiomeFactor = AccessTools.Method("SK.Util_Zone_Fishing:GetBiomeFishSpawnRateFactor");
            if (getBiomeFactor != null)
            {
                biomeFactor = (float)getBiomeFactor.Invoke(null, new object[] { thing.Map });
            }

            // Тип воды: ocean=1, river=0.75, marsh=0.5
            float waterQuality = (float)total / (ocean * 1f + river * 0.75f + marsh * 0.5f);

            // Вулканическая зима
            float weatherFactor = 1f;
            bool badTemp = (bool)tempField.GetValue(pier);
            if (badTemp && thing.Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.VolcanicWinter))
            {
                weatherFactor = 1.5f;
            }

            float spawnRate = biomeFactor * waterQuality * weatherFactor;

            // Наш множитель
            spawnRate *= NerfSettings.fishSpawnSlowdown;

            return (int)(360000f * spawnRate);
        }

        // Наш фиксированный таймер пополнения — постфикс после оригинального TickRare
        public static void TickRarePostfix(object __instance)
        {
            var thing = __instance as Thing;
            if (thing == null)
                return;

            var stockField = AccessTools.Field(__instance.GetType(), "fishStock");
            var maxStockField = AccessTools.Field(__instance.GetType(), "maxFishStock");
            if (stockField == null || maxStockField == null)
                return;

            int fishStock = (int)stockField.GetValue(__instance);
            int maxFishStock = (int)maxStockField.GetValue(__instance);

            var timers = GetTimers();
            if (timers == null)
                return;

            if (fishStock >= maxFishStock)
            {
                timers.Remove(thing.thingIDNumber);
                return;
            }

            var oceanField = AccessTools.Field(__instance.GetType(), "oceanCellsCount");
            var riverField = AccessTools.Field(__instance.GetType(), "riverCellsCount");
            var marshField = AccessTools.Field(__instance.GetType(), "marshCellsCount");
            int viable = (int)oceanField.GetValue(__instance) +
                         (int)riverField.GetValue(__instance) +
                         (int)marshField.GetValue(__instance);

            if (viable <= 0)
                return;

            int interval = GetSpawnIntervalTicks(__instance);

            if (!timers.ContainsKey(thing.thingIDNumber))
                timers[thing.thingIDNumber] = interval;

            timers[thing.thingIDNumber] -= 250; // TickRare = 250 тиков

            if (timers[thing.thingIDNumber] <= 0)
            {
                fishStock++;
                stockField.SetValue(__instance, fishStock);

                if (fishStock < maxFishStock)
                    timers[thing.thingIDNumber] = interval;
                else
                    timers.Remove(thing.thingIDNumber);
            }
        }

        // Показать точное время до пополнения
        public static void InspectStringPostfix(object __instance, ref string __result)
        {
            var thing = __instance as Thing;
            if (thing == null)
                return;

            var stockField = AccessTools.Field(__instance.GetType(), "fishStock");
            var maxStockField = AccessTools.Field(__instance.GetType(), "maxFishStock");
            if (stockField == null || maxStockField == null)
                return;

            int fishStock = (int)stockField.GetValue(__instance);
            int maxFishStock = (int)maxStockField.GetValue(__instance);

            if (fishStock >= maxFishStock || maxFishStock <= 0)
            {
                __result += "\nFish: full";
                return;
            }

            var timers = GetTimers();
            if (timers != null && timers.TryGetValue(thing.thingIDNumber, out int ticksLeft))
            {
                float hoursLeft = ticksLeft / 2500f;
                float daysLeft = hoursLeft / 24f;

                string timeStr;
                if (daysLeft >= 1f)
                    timeStr = $"{daysLeft:F1} days";
                else
                    timeStr = $"{hoursLeft:F1}h";

                __result += $"\nNext fish: {timeStr}";
            }
        }

        // Increase min distance between piers: 10 -> 15
        public static void PierDistancePrefix(ref float distance)
        {
            if (distance == 10f)
                distance = 15f;
        }

        // Показать точное время до поимки у ловушки
        public static void TrapInspectPostfix(object __instance, ref string __result)
        {
            var ticksField = AccessTools.Field(__instance.GetType(), "ticksToCatch");
            var fullField = AccessTools.Field(__instance.GetType(), "isFull");
            if (ticksField == null)
                return;

            bool isFull = fullField != null && (bool)fullField.GetValue(__instance);
            if (isFull)
                return;

            int ticksLeft = (int)ticksField.GetValue(__instance);
            if (ticksLeft <= 0)
                return;

            // ticksLeft — единицы счётчика (1 = 250 игровых тиков), без наживки. 2500 тиков = 1 час.
            float hoursLeft = ticksLeft * 250f / 2500f;
            float daysLeft = hoursLeft / 24f;

            string timeStr;
            if (daysLeft >= 1f)
                timeStr = $"{daysLeft:F1} days";
            else
                timeStr = $"{hoursLeft:F1}h";

            __result += $"\nNext catch (no bait): {timeStr}";
        }

        private static Type trapTypeCache;

        // Ограничение количества ловушек на карте
        public static void TrapLimitPostfix(Map map, ref AcceptanceReport __result)
        {
            if (!__result.Accepted)
                return;

            if (trapTypeCache == null)
                trapTypeCache = AccessTools.TypeByName("SK.Building_FishTrap");

            if (trapTypeCache == null)
                return;

            int built = 0;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (trapTypeCache.IsInstanceOfType(b))
                    built++;
            }

            int planned = 0;
            foreach (var bp in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (bp.def.entityDefToBuild is ThingDef td &&
                    td.thingClass != null &&
                    trapTypeCache.IsAssignableFrom(td.thingClass))
                    planned++;
            }
            foreach (var frame in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (frame.def.entityDefToBuild is ThingDef td &&
                    td.thingClass != null &&
                    trapTypeCache.IsAssignableFrom(td.thingClass))
                    planned++;
            }

            int count = built + planned;
            if (count >= NerfSettings.maxFishTraps)
            {
                __result = new AcceptanceReport($"Maximum {NerfSettings.maxFishTraps} fish traps allowed ({built} built, {planned} planned).");
            }
        }

        // Ловушка: фиксированный таймер поимки (с учётом биома)
        public static void TrapSpawnPostfix(object __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
                return;

            var field = AccessTools.Field(__instance.GetType(), "ticksToCatch");
            var totalField = AccessTools.Field(__instance.GetType(), "totalTicks");
            if (field == null)
                return;

            var thing = __instance as Thing;
            if (thing?.Map == null)
                return;

            // Биомный множитель
            float biomeFactor = 1f;
            var getBiomeFactor = AccessTools.Method("SK.Util_Zone_Fishing:GetBiomeFishSpawnRateFactor");
            if (getBiomeFactor != null)
            {
                biomeFactor = (float)getBiomeFactor.Invoke(null, new object[] { thing.Map });
            }

            float factor = biomeFactor * NerfSettings.fishSpawnSlowdown;
            // /250: у ловушки ticksToCatch — это счётчик, уменьшается на 1 за каждый 250-тиковый интервал,
            // а не на 1 за тик. Поэтому переводим целевые игровые тики в единицы счётчика.
            int interval = (int)(360000f * factor / 250f);

            field.SetValue(__instance, interval);
            totalField?.SetValue(__instance, interval);
        }

        private static Type pierSpawnerTypeCache;

        // Ограничение количества причалов на карте
        public static void PierLimitPostfix(Map map, ref AcceptanceReport __result)
        {
            if (!__result.Accepted)
                return;

            if (pierTypeCache == null)
                pierTypeCache = AccessTools.TypeByName("SK.Building_FishingPier");
            if (pierSpawnerTypeCache == null)
                pierSpawnerTypeCache = AccessTools.TypeByName("SK.Building_FishingPierSpawner");

            // Построенные пирсы
            int built = 0;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if ((pierTypeCache != null && pierTypeCache.IsInstanceOfType(b)) ||
                    (pierSpawnerTypeCache != null && pierSpawnerTypeCache.IsInstanceOfType(b)))
                    built++;
            }

            // Чертежи и рамки
            int planned = 0;
            foreach (var bp in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                if (bp.def.entityDefToBuild is ThingDef td &&
                    td.thingClass != null &&
                    ((pierTypeCache != null && pierTypeCache.IsAssignableFrom(td.thingClass)) ||
                     (pierSpawnerTypeCache != null && pierSpawnerTypeCache.IsAssignableFrom(td.thingClass))))
                    planned++;
            }
            foreach (var frame in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                if (frame.def.entityDefToBuild is ThingDef td &&
                    td.thingClass != null &&
                    ((pierTypeCache != null && pierTypeCache.IsAssignableFrom(td.thingClass)) ||
                     (pierSpawnerTypeCache != null && pierSpawnerTypeCache.IsAssignableFrom(td.thingClass))))
                    planned++;
            }

            int count = built + planned;
            if (count >= NerfSettings.maxFishingPiers)
            {
                __result = new AcceptanceReport($"Maximum {NerfSettings.maxFishingPiers} fishing piers allowed ({built} built, {planned} planned).");
            }
        }

        // Причал начинает с 0 рыбы (только при новой постройке, не при загрузке)
        public static void PierSpawnPostfix(object __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad)
                return;

            var field = AccessTools.Field(__instance.GetType(), "fishStock");
            if (field != null)
            {
                field.SetValue(__instance, 0);
            }
        }

        // Кэш рефлексии для выбора вида рыбы
        private static System.Reflection.MethodInfo getFishListMethod;
        private static Type fishSpeciesType;
        private static System.Reflection.FieldInfo fLivesOcean, fLivesRiver, fLivesMarsh, fCommonality;

        // Ловушка: полностью заменяем оригинальную логику поимки на гарантированную.
        // Таймер дошёл до 0 -> 100% ловится одна рыба (без шанса провала и без самоповреждения ловушки).
        // Возврат false пропускает оригинальный PlaceProduct; таймер сбросит постфикс ниже.
        public static bool TrapPlaceProductPrefix(object __instance)
        {
            var thing = __instance as Thing;
            if (thing?.Map == null)
                return false;

            // Износ ловушки при поимке (как в оригинале). Если разрушилась — рыбы нет.
            if (ApplyTrapWear(thing))
                return false;

            SpawnGuaranteedFish(thing);
            return false;
        }

        // Износ ловушки: шанс получить урон зависит от качества. Возвращает true, если ловушка разрушилась.
        private static bool ApplyTrapWear(Thing trap)
        {
            float damageChance = 0.5f;
            if (QualityUtility.TryGetQuality(trap, out QualityCategory q))
            {
                switch ((int)q)
                {
                    case 0: damageChance = 0.7f; break;  // Awful
                    case 1: damageChance = 0.6f; break;  // Poor
                    case 3: damageChance = 0.4f; break;  // Good
                    case 4: damageChance = 0.3f; break;  // Excellent
                    case 5: damageChance = 0.2f; break;  // Masterwork
                    case 6: damageChance = 0.15f; break; // Legendary
                }
            }

            int damage = 0;
            if (Rand.Value <= damageChance)
            {
                damage = Rand.RangeInclusive(0, 10);
                if (Rand.Value < 0.5f)
                    damage += Rand.RangeInclusive(10, 20);
            }
            if (damage <= 0)
                return false;

            if (trap.HitPoints <= damage)
            {
                var map = trap.Map;
                var pos = trap.Position;
                trap.Destroy((DestroyMode)2); // Deconstruct — как в оригинале (роняет материалы)
                Messages.Message("HSK.FishTrapDestroyed".Translate(),
                    new TargetInfo(pos, map, false), MessageTypeDefOf.NeutralEvent, true);
                return true;
            }

            trap.HitPoints -= damage;
            return false;
        }

        private static void SpawnGuaranteedFish(Thing trap)
        {
            var map = trap.Map;
            var pos = trap.Position;

            if (getFishListMethod == null)
                getFishListMethod = AccessTools.Method("SK.Util_FishIndustry:GetFishSpeciesList");
            if (fishSpeciesType == null)
                fishSpeciesType = AccessTools.TypeByName("SK.PawnKindDef_FishSpecies");
            if (getFishListMethod == null || fishSpeciesType == null)
                return;
            if (fLivesOcean == null) fLivesOcean = AccessTools.Field(fishSpeciesType, "livesInOcean");
            if (fLivesRiver == null) fLivesRiver = AccessTools.Field(fishSpeciesType, "livesInRiver");
            if (fLivesMarsh == null) fLivesMarsh = AccessTools.Field(fishSpeciesType, "livesInMarsh");
            if (fCommonality == null) fCommonality = AccessTools.Field(fishSpeciesType, "commonality");
            if (fLivesOcean == null || fLivesRiver == null || fLivesMarsh == null || fCommonality == null)
                return;

            var species = getFishListMethod.Invoke(null, new object[] { map.Biome }) as System.Collections.IEnumerable;
            if (species == null)
                return;

            var terr = map.terrainGrid.TerrainAt(pos);
            bool ocean = terr == TerrainDefOf.WaterOceanShallow || terr == TerrainDefOf.WaterOceanDeep;
            bool marsh = terr == TerrainDef.Named("Marsh");

            // Взвешенный выбор вида по типу воды (по commonality)
            var candidates = new List<KeyValuePair<PawnKindDef, float>>();
            float totalWeight = 0f;
            foreach (var s in species)
            {
                bool ok = ocean ? (bool)fLivesOcean.GetValue(s)
                        : (marsh ? (bool)fLivesMarsh.GetValue(s) : (bool)fLivesRiver.GetValue(s));
                if (!ok)
                    continue;
                float w = (float)fCommonality.GetValue(s);
                if (w <= 0f)
                    continue;
                candidates.Add(new KeyValuePair<PawnKindDef, float>((PawnKindDef)s, w));
                totalWeight += w;
            }
            if (candidates.Count == 0 || totalWeight <= 0f)
                return;

            PawnKindDef chosen = candidates[candidates.Count - 1].Key;
            float roll = Rand.Value * totalWeight;
            foreach (var c in candidates)
            {
                roll -= c.Value;
                if (roll <= 0f)
                {
                    chosen = c.Key;
                    break;
                }
            }

            var pawn = PawnGenerator.GeneratePawn(chosen, null);
            var corpse = (Corpse)ThingMaker.MakeThing(pawn.RaceProps.corpseDef);
            corpse.InnerPawn = pawn;
            GenSpawn.Spawn(corpse, pos, map);
        }

        // Ловушка: после поимки сбросить таймер по нашей формуле
        public static void TrapPlaceProductPostfix(object __instance)
        {
            var field = AccessTools.Field(__instance.GetType(), "ticksToCatch");
            var totalField = AccessTools.Field(__instance.GetType(), "totalTicks");
            if (field == null)
                return;

            var thing = __instance as Thing;
            if (thing?.Map == null)
                return;

            float biomeFactor = 1f;
            var getBiomeFactor = AccessTools.Method("SK.Util_Zone_Fishing:GetBiomeFishSpawnRateFactor");
            if (getBiomeFactor != null)
            {
                biomeFactor = (float)getBiomeFactor.Invoke(null, new object[] { thing.Map });
            }

            float factor = biomeFactor * NerfSettings.fishSpawnSlowdown;
            // /250: у ловушки ticksToCatch — это счётчик, уменьшается на 1 за каждый 250-тиковый интервал,
            // а не на 1 за тик. Поэтому переводим целевые игровые тики в единицы счётчика.
            int interval = (int)(360000f * factor / 250f);

            field.SetValue(__instance, interval);
            totalField?.SetValue(__instance, interval);
        }
    }
}
