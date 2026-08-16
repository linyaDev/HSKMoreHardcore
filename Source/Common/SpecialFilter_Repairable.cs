using Verse;

namespace HSKMoreHardcore
{
    public class SpecialThingFilterWorker_AllowRepairable : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            var comp = t.TryGetComp<CompWornByEnemy>();
            if (comp == null)
                return true; // нет компа — считаем восстанавливаемым
            return comp.CanMendNormally;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            return def.IsApparel || def.IsWeapon;
        }

        public override bool AlwaysMatches(ThingDef def)
        {
            return false;
        }
    }
}
