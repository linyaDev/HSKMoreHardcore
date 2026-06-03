using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Дописывает в подробное описание одежды (Apparel.DescriptionDetailed) секцию с уже
    // скорректированными по качеству бонусами для статов из apparelQualityStatStep.
    // Базовое описание кэшируется на дефе и показывает авторские (базовые) значения; здесь
    // показываем фактические для конкретного качества — через тот же StatWorker.StatOffsetFromGear.
    [StaticConstructorOnStartup]
    public static class ApparelQualityDescription
    {
        static ApparelQualityDescription()
        {
            var getter = AccessTools.PropertyGetter(typeof(Apparel), nameof(Apparel.DescriptionDetailed));
            if (getter == null)
                return;

            var harmony = new Harmony("linya.hskmorehardcore.apparelqualitydesc");
            harmony.Patch(getter, postfix: new HarmonyMethod(typeof(ApparelQualityDescription), nameof(Postfix)));
            Log.Message("[HSKMoreHardcore] ApparelQualityDescription applied.");
        }

        // Цвет секции «Поправка по качеству» (rich-text hex)
        private const string SectionColor = "#9BD7FF";

        public static void Postfix(Apparel __instance, ref string __result)
        {
            if (__instance?.def?.equippedStatOffsets == null)
                return;
            if (!__instance.TryGetQuality(out _))
                return;

            StringBuilder sb = null;
            foreach (var mod in __instance.def.equippedStatOffsets)
            {
                if (mod.stat == null || !NerfSettings.apparelQualityStatStep.ContainsKey(mod.stat.defName))
                    continue;

                float adjusted = StatWorker.StatOffsetFromGear(__instance, mod.stat);
                if (sb == null)
                {
                    sb = new StringBuilder();
                    sb.Append("HSKMoreHardcore_QualityAdj".Translate());
                    sb.Append(":");
                }
                sb.Append("\n");
                sb.Append(mod.stat.LabelCap);
                sb.Append(": ");
                sb.Append(mod.stat.ValueToString(adjusted, ToStringNumberSense.Offset));
            }

            if (sb != null)
                __result += $"\n\n<color={SectionColor}>{sb}</color>";
        }
    }
}
