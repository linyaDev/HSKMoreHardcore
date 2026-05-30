using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    public class Building_SignalCampfire : Building
    {
        public override bool DeconstructibleBy(Faction faction)
        {
            var comp = GetComp<CompTradeSignal>();
            if (comp != null && comp.IsActive)
                return false;

            return base.DeconstructibleBy(faction);
        }
    }
}
