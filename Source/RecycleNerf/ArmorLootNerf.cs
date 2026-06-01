using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Нерф прочности брони в луте: зависит от защиты брони (по шкале Combat Extended) и нашего уровня развития.
    // Уровень развития берём из Faction.def.techLevel — его динамически поднимает мод Tech Advancing
    // по мере исследований. Значения enum TechLevel:
    // Undefined=0, Animal=1, Neolithic=2, Medieval=3, Industrial=4, Spacer=5, Ultra=6, Archotech=7.
    //
    // ВАЖНО про CE: Sharp (~мм RHA) и Blunt (~МПа) — разные единицы, их нельзя складывать.
    // Нормируем каждый по своей опорной точке (топ-броня = катафракт: Sharp 28, Blunt 60)
    // и берём максимум — насколько броня защищает «в лучшем случае».
    public static class ArmorLootNerf
    {
        // Полный разбор расчёта (для логов)
        public struct Calc
        {
            public float sharp, blunt;       // CE-значения брони
            public float sharpNorm, bluntNorm, protNorm;
            public int dev;
            public float devNorm;
            public float nerf;               // 0 = нет нерфа, 1 = максимум
            public float mult;               // итоговый множитель прочности
        }

        // Текущий уровень развития игрока
        public static int PlayerDevLevel()
        {
            var f = Faction.OfPlayerSilentFail;
            if (f?.def == null)
                return 0;
            return (int)f.def.techLevel;
        }

        public static Calc Compute(Apparel ap)
        {
            Calc c = default;
            c.sharp = ap.GetStatValue(StatDefOf.ArmorRating_Sharp);
            c.blunt = ap.GetStatValue(StatDefOf.ArmorRating_Blunt);

            c.sharpNorm = Mathf.Clamp01(c.sharp / NerfSettings.armorSharpRef);
            c.bluntNorm = Mathf.Clamp01(c.blunt / NerfSettings.armorBluntRef);
            c.protNorm = Mathf.Max(c.sharpNorm, c.bluntNorm);

            c.dev = PlayerDevLevel();
            // Шкала развития считается от Neolithic (Animal приравнивается к Neolithic) до Spacer
            float devSpan = Mathf.Max(1, NerfSettings.armorDevLevelRef - NerfSettings.armorDevLevelMin);
            c.devNorm = Mathf.Clamp01((c.dev - NerfSettings.armorDevLevelMin) / devSpan);

            c.nerf = c.protNorm * (1f - c.devNorm); // чем выше защита и ниже развитие — тем больше
            c.mult = Mathf.Lerp(1f, NerfSettings.armorHpMin, c.nerf);
            return c;
        }

        public static float MultiplierFor(Apparel ap) => Compute(ap).mult;

        // Применить нерф к броне (только понижаем прочность). Возвращает true, если что-то изменили.
        public static bool Apply(Apparel ap, string tag)
        {
            if (!NerfSettings.armorHpNerfEnabled || ap == null || ap.HitPoints <= 1)
                return false;

            Calc c = Compute(ap);
            string breakdown = $"защита S{c.sharp:F1}(норм {c.sharpNorm:F2})/B{c.blunt:F1}(норм {c.bluntNorm:F2}) " +
                               $"protNorm={c.protNorm:F2}, развитие={c.dev} (devNorm={c.devNorm:F2}) => " +
                               $"нерф={c.nerf:P0}, множитель={c.mult:F2}";

            if (c.mult >= 1f)
            {
                Log.Message($"[ArmorLootNerf] [{tag}] {ap.def.defName}: {breakdown} — без изменений (HP {ap.HitPoints}/{ap.MaxHitPoints})");
                return false;
            }

            int before = ap.HitPoints;
            int target = Mathf.Max(1, Mathf.FloorToInt(ap.MaxHitPoints * c.mult));
            if (target >= before)
            {
                Log.Message($"[ArmorLootNerf] [{tag}] {ap.def.defName}: {breakdown}, но прочность {before}/{ap.MaxHitPoints} и так ниже целевой ({target}) — без изменений");
                return false;
            }

            ap.HitPoints = target;
            Log.Message($"[ArmorLootNerf] [{tag}] {ap.def.defName}: {breakdown}, прочность {before}/{ap.MaxHitPoints} -> {target}/{ap.MaxHitPoints}");
            return true;
        }
    }
}
