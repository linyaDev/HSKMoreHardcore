using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    // Подселяет Княжество (MedievalPrincipality) в существующие сейвы — тем же
    // механизмом, каким ванила добавляет DLC-фракции в старые миры
    // (BackCompatibility.FactionManagerPostLoadInit -> CreateFactionAndAddToManager).
    // Ванильный путь не создаёт поселений, поэтому доспавниваем их сами.
    // В новых мирах фракция появляется штатно (requiredCountAtGameStart) и
    // компонент ничего не делает.
    public class PrincipalityBackCompat : GameComponent
    {
        private const int SettlementCount = 2;

        public PrincipalityBackCompat(Game game)
        {
        }

        public override void FinalizeInit()
        {
            var def = DefDatabase<FactionDef>.GetNamedSilentFail("MedievalPrincipality");
            if (def == null)
                return;
            if (Find.FactionManager.FirstFactionOfDef(def) != null)
                return;

            FactionGenerator.CreateFactionAndAddToManager(def);
            var faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
                Log.Warning("[HSKMoreHardcore] PrincipalityBackCompat: faction generation failed.");
                return;
            }

            int spawned = 0;
            for (int i = 0; i < SettlementCount; i++)
            {
                var tile = TileFinder.RandomSettlementTileFor(faction);
                if (!tile.Valid)
                    continue;
                var settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                settlement.SetFaction(faction);
                settlement.Tile = tile;
                settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
                Find.WorldObjects.Add(settlement);
                spawned++;
            }

            Log.Message($"[HSKMoreHardcore] PrincipalityBackCompat: добавлена фракция {faction.Name} и {spawned} поселения(й) в существующий мир.");
        }
    }
}
