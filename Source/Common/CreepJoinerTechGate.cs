using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Минимальный техуровень игрока для вариантов жуткого присоединившегося
    // (Anomaly): словарь creepJoinerFormMinTechLevel (defName формы -> TechLevel)
    // в Defs/Misc/HardcoreSettings.xml. Уровень берём из IgnoranceCompat, как в
    // IncidentTechGate. Варианты — не отдельные события: форма выбирается
    // случайно по весу внутри CreepJoinerUtility, поэтому IncidentTechGate их не
    // видит, а XML может только обнулить вес (выключить навсегда).
    // Ванилла выбирает форму в двух местах:
    //  - GetCreepjoinerSpecifics при form == null (QuestNode_SetupCreepjoiner);
    //  - GenerateAndSpawn(Map, float) (квест прихода без заранее выбранной формы).
    // В обоих подставляем случайную по весу форму только из разрешённых.
    // Если сейчас ничего не отсечено или разрешённых нет — работает ванилла.
    // Выбор формы вручную в dev-меню не ограничиваем.
    [StaticConstructorOnStartup]
    public static class CreepJoinerTechGate
    {
        static CreepJoinerTechGate()
        {
            if (!ModsConfig.AnomalyActive)
                return;

            var specifics = AccessTools.Method(typeof(CreepJoinerUtility), nameof(CreepJoinerUtility.GetCreepjoinerSpecifics));
            var spawn = AccessTools.Method(typeof(CreepJoinerUtility), nameof(CreepJoinerUtility.GenerateAndSpawn),
                new[] { typeof(Map), typeof(float) });
            if (specifics == null || spawn == null)
            {
                Log.Warning("[HSKMoreHardcore] CreepJoinerTechGate: API CreepJoinerUtility изменилось — варианты не ограничиваются.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.creepjoinertechgate");
            harmony.Patch(specifics,
                prefix: new HarmonyMethod(typeof(CreepJoinerTechGate), nameof(GetCreepjoinerSpecificsPrefix)));
            harmony.Patch(spawn,
                prefix: new HarmonyMethod(typeof(CreepJoinerTechGate), nameof(GenerateAndSpawnPrefix)));
        }

        public static void GetCreepjoinerSpecificsPrefix(ref CreepJoinerFormKindDef form)
        {
            if (form == null && TryGetAllowedForm(out var allowed))
                form = allowed;
        }

        // Копия ванильного GenerateAndSpawn(Map, float) с отфильтрованной формой
        public static bool GenerateAndSpawnPrefix(Map map, float combatPoints, ref Pawn __result)
        {
            if (!TryGetAllowedForm(out var form))
                return true;

            var requires = new List<CreepJoinerBaseDef>(form.Requires);
            var exclude = new List<CreepJoinerBaseDef>(form.Excludes);
            var benefit = CreepJoinerUtility.GetRandom(DefDatabase<CreepJoinerBenefitDef>.AllDefsListForReading, combatPoints, requires, exclude);
            var downside = CreepJoinerUtility.GetRandom(DefDatabase<CreepJoinerDownsideDef>.AllDefsListForReading, combatPoints, requires, exclude);
            var aggressive = CreepJoinerUtility.GetRandom(DefDatabase<CreepJoinerAggressiveDef>.AllDefsListForReading, combatPoints, requires, exclude);
            var rejection = CreepJoinerUtility.GetRandom(DefDatabase<CreepJoinerRejectionDef>.AllDefsListForReading, combatPoints, requires, exclude);
            __result = CreepJoinerUtility.GenerateAndSpawn(form, benefit, downside, aggressive, rejection, map);
            return false;
        }

        // false — ничего не отсечено (или все отсечены): пусть выбирает ванилла
        private static bool TryGetAllowedForm(out CreepJoinerFormKindDef form)
        {
            form = null;
            var gates = HardcoreSettingsDef.Instance?.creepJoinerFormMinTechLevel;
            if (gates == null || gates.Count == 0 || Current.Game == null)
                return false;

            var all = DefDatabase<CreepJoinerFormKindDef>.AllDefsListForReading;
            TechLevel playerTech = IgnoranceCompat.PlayerTechLevel;
            var allowed = all
                .Where(f => !gates.TryGetValue(f.defName, out TechLevel minTech) || playerTech >= minTech)
                .ToList();
            if (allowed.Count == all.Count)
                return false;

            if (Prefs.DevMode)
            {
                var blocked = all.Where(f => !allowed.Contains(f)).Select(f => f.defName);
                Log.Message($"[HSKMoreHardcore] CreepJoinerTechGate: игрок {playerTech}, отсечены: {string.Join(", ", blocked)}.");
            }
            return allowed.TryRandomElementByWeight(f => f.Weight, out form);
        }
    }
}
