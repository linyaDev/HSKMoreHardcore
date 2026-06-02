using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Снижает рыночную цену помеченной одежды (CompWornByEnemy).
    // Применяется НАИМЕНЬШИЙ (самый жёсткий) множитель из применимых:
    //  - снято с врага в бою (wornByEnemy) -> wornByEnemyValueMult (по умолчанию 0.3)
    //  - носил наш колонист (worn)          -> wornValueMult        (по умолчанию 0.4)
    // Если стоят оба флага — берём меньший. С ванильным «снято с трупа» (x0.1) не складываемся:
    // там уже более жёсткий штраф, его и оставляем.
    public class StatPart_WornByEnemy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetPenalty(req, out float mult, out _))
                val *= mult;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (TryGetPenalty(req, out float mult, out string labelKey))
                return labelKey.Translate() + ": x" + mult.ToStringPercent();
            return null;
        }

        private static bool TryGetPenalty(StatRequest req, out float mult, out string labelKey)
        {
            mult = 1f;
            labelKey = null;

            var comp = GetComp(req);
            if (comp == null)
                return false;

            bool any = false;
            if (comp.wornByEnemy)
            {
                mult = NerfSettings.wornByEnemyValueMult;
                labelKey = "HSKMoreHardcore_WornByEnemy";
                any = true;
            }
            if (comp.worn && (!any || NerfSettings.wornValueMult < mult))
            {
                mult = NerfSettings.wornValueMult;
                labelKey = "HSKMoreHardcore_Worn";
                any = true;
            }
            return any;
        }

        private static CompWornByEnemy GetComp(StatRequest req)
        {
            if (req.HasThing && req.Thing is Apparel ap)
            {
                // Не складываемся с ванильным штрафом трупной одежды (x0.1) — он жёстче
                if (ap.WornByCorpse)
                    return null;
                return ap.TryGetComp<CompWornByEnemy>();
            }
            return null;
        }
    }
}
