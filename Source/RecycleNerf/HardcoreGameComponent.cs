using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    public class HardcoreGameComponent : GameComponent
    {
        private Dictionary<int, int> fishSpawnTimers = new Dictionary<int, int>();

        public HardcoreGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref fishSpawnTimers, "fishSpawnTimers", LookMode.Value, LookMode.Value);
            if (fishSpawnTimers == null)
                fishSpawnTimers = new Dictionary<int, int>();
        }

        public Dictionary<int, int> FishSpawnTimers => fishSpawnTimers;

        public static HardcoreGameComponent Get()
        {
            return Current.Game?.GetComponent<HardcoreGameComponent>();
        }
    }
}
