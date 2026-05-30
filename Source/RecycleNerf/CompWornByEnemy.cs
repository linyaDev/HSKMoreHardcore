using Verse;

namespace HSKMoreHardcore
{
    public class CompWornByEnemy : ThingComp
    {
        public bool worn;
        public bool wornByEnemy;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref worn, "worn", false);
            Scribe_Values.Look(ref wornByEnemy, "wornByEnemy", false);
        }

        public override string CompInspectStringExtra()
        {
            if (wornByEnemy)
                return "HSKMoreHardcore_WornByEnemy".Translate();
            if (worn)
                return "HSKMoreHardcore_Worn".Translate();
            return null;
        }

        public override string GetDescriptionPart()
        {
            if (wornByEnemy)
                return "HSKMoreHardcore_WornByEnemyDesc".Translate();
            if (worn)
                return "HSKMoreHardcore_WornDesc".Translate();
            return null;
        }
    }

    public class CompProperties_WornByEnemy : CompProperties
    {
        public CompProperties_WornByEnemy()
        {
            compClass = typeof(CompWornByEnemy);
        }
    }
}
