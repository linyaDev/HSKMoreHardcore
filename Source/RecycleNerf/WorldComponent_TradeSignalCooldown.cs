using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace HSKMoreHardcore
{
    public class WorldComponent_TradeSignalCooldown : WorldComponent
    {
        private Dictionary<string, int> lastSignalTicks = new Dictionary<string, int>();
        private Dictionary<string, int> cooldownDurations = new Dictionary<string, int>();

        public WorldComponent_TradeSignalCooldown(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastSignalTicks, "lastSignalTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref cooldownDurations, "cooldownDurations", LookMode.Value, LookMode.Value);
            lastSignalTicks ??= new Dictionary<string, int>();
            cooldownDurations ??= new Dictionary<string, int>();
        }

        public bool CooldownComplete(string key, out int ticksLeft)
        {
            ticksLeft = 0;
            if (!lastSignalTicks.TryGetValue(key, out int lastTick) || lastTick < 0)
                return true;

            if (!cooldownDurations.TryGetValue(key, out int duration))
                return true;

            int elapsed = Find.TickManager.TicksGame - lastTick;
            if (elapsed >= duration)
                return true;

            ticksLeft = duration - elapsed;
            return false;
        }

        public void NotifySignalUsed(string key, int cooldownTicks)
        {
            lastSignalTicks[key] = Find.TickManager.TicksGame;
            cooldownDurations[key] = cooldownTicks;
        }
    }
}
