using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    public class HardcoreGameComponent : GameComponent
    {
        private Dictionary<int, int> fishSpawnTimers = new Dictionary<int, int>();

        // Стабильный бросок для лута: сид перегенерируется раз в 3 дня, бросок = f(сид, defName).
        // В течение 3 дней все предметы одного defName получают одинаковый бросок (анти-сейв-скам).
        private const int StableRollPeriodTicks = 180000; // 3 дня (60000 тиков/день)
        private int stableRollSeed;
        private int stableRollPeriod = int.MinValue;

        public HardcoreGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref fishSpawnTimers, "fishSpawnTimers", LookMode.Value, LookMode.Value);
            if (fishSpawnTimers == null)
                fishSpawnTimers = new Dictionary<int, int>();

            Scribe_Values.Look(ref stableRollSeed, "stableRollSeed", 0);
            Scribe_Values.Look(ref stableRollPeriod, "stableRollPeriod", int.MinValue);
        }

        public Dictionary<int, int> FishSpawnTimers => fishSpawnTimers;

        // Детерминированный бросок [0,1) для данного defName в текущем 3-дневном окне
        public float StableRoll(string defName)
        {
            int period = Find.TickManager.TicksGame / StableRollPeriodTicks;
            if (period != stableRollPeriod)
            {
                stableRollPeriod = period;
                stableRollSeed = Rand.Int; // новый сид раз в 3 дня
            }

            int combined = Gen.HashCombineInt(stableRollSeed, GenText.StableStringHash(defName));
            Rand.PushState(combined);
            float value = Rand.Value;
            Rand.PopState();
            return value;
        }

        public static HardcoreGameComponent Get()
        {
            return Current.Game?.GetComponent<HardcoreGameComponent>();
        }

        // Стабильный бросок с фолбэком на обычный Rand, если компонент недоступен
        public static float Roll(string defName)
        {
            var comp = Get();
            return comp != null ? comp.StableRoll(defName) : Rand.Value;
        }
    }
}
