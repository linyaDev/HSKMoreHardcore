using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Делает бонус одежды носителю зависимым от качества (аддитивно).
    // База — apparelQualityBaselineIndex (по умолчанию Good): авторские equippedStatOffsets считаются
    // значением этого качества. На каждый тир от базы к офсету добавляется dir * step:
    // для «выше=лучше» статов вверх, для «ниже=лучше» — вниз.
    //
    // Работает за счёт того, что StatWorker.StatOffsetFromGear прогоняет StatPart'ы стата с запросом
    // по самой вещи (StatRequest.For(gear)) — и только при ненулевом офсете, т.е. лишь для вещей,
    // которые этот стат уже дают. Карточка предмета (ThingDef.SpecialDisplayStats) использует тот же
    // StatOffsetFromGear, поэтому скорректированное значение и пояснение видны при наведении.
    public class StatPart_ApparelQualityOffset : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetDelta(req, out float delta))
                val += delta;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (TryGetDelta(req, out float delta) && delta != 0f)
                return "HSKMoreHardcore_QualityAdj".Translate() + ": "
                     + delta.ToStringByStyle(parentStat.toStringStyle, ToStringNumberSense.Offset);
            return null;
        }

        private bool TryGetDelta(StatRequest req, out float delta)
        {
            delta = 0f;
            if (parentStat == null)
                return false;
            if (!(req.HasThing && req.Thing is Apparel ap))
                return false;
            if (!ap.TryGetQuality(out QualityCategory q))
                return false;
            if (!NerfSettings.apparelQualityStatStep.TryGetValue(parentStat.defName, out float step))
                return false;

            int dir = NerfSettings.apparelQualityLowerIsBetter.Contains(parentStat.defName) ? -1 : 1;
            delta = dir * ((int)q - NerfSettings.apparelQualityBaselineIndex) * step;
            return true;
        }
    }
}
