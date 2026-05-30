using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    public class CompProperties_TradeSignal : CompProperties
    {
        public int cooldownTicks = 900000;
        public int silverCost = 200;
        public int arrivalDelayTicks = 120000;
        public TechLevel targetTechLevel = TechLevel.Neolithic;
        public bool destroyOnUse = true;
        public TechLevel disableAtTechLevel = TechLevel.Undefined;
        public string cooldownKey = "tribal";

        public string commandLabelKey = "TribalSignal_CommandLabel";
        public string commandDescKey = "TribalSignal_CommandDesc";
        public string scheduledKey = "TribalSignal_Scheduled";
        public string noFactionKey = "TribalSignal_NoFaction";
        public string activeKey = "TribalSignal_Burning";
        public string doneKey = "TribalSignal_BurnedOut";

        public CompProperties_TradeSignal()
        {
            compClass = typeof(CompTradeSignal);
        }
    }

    public class CompTradeSignal : ThingComp
    {
        private bool isActive;
        private int arrivalTick = -1;

        public bool IsActive => isActive;

        private CompProperties_TradeSignal Props => (CompProperties_TradeSignal)props;

        private static WorldComponent_TradeSignalCooldown Tracker =>
            Find.World?.GetComponent<WorldComponent_TradeSignalCooldown>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isActive, "isActive");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick", -1);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!isActive || parent.Map == null)
                return;

            if (Props.destroyOnUse)
            {
                Vector3 pos = parent.DrawPos;
                Map map = parent.Map;

                if (parent.IsHashIntervalTick(15))
                    FleckMaker.ThrowFireGlow(pos, map, 1.5f);

                if (parent.IsHashIntervalTick(30))
                    FleckMaker.ThrowSmoke(pos + new Vector3(0f, 0f, 0.5f), map, 2f);

                if (parent.IsHashIntervalTick(50))
                    FleckMaker.ThrowMicroSparks(pos, map);
            }

            if (arrivalTick > 0 && Find.TickManager.TicksGame >= arrivalTick)
            {
                isActive = false;
                Messages.Message(Props.doneKey.Translate(), MessageTypeDefOf.NeutralEvent);
                if (Props.destroyOnUse)
                {
                    parent.Destroy(DestroyMode.Vanish);
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is not Building || !parent.Spawned || parent.Map == null)
                yield break;

            if (isActive)
                yield break;

            var cmd = new Command_Action
            {
                defaultLabel = Props.commandLabelKey.Translate(),
                defaultDesc = Props.commandDescKey.Translate(Props.silverCost),
                icon = parent.def.uiIcon,
                action = TryCallTrader
            };

            if (!CooldownComplete(out int ticksLeft))
            {
                cmd.Disable("TradeSignal_OnCooldown".Translate(ticksLeft.ToStringTicksToPeriod()));
            }
            else if (!AnyValidTradeFaction(parent.Map))
            {
                cmd.Disable(Props.noFactionKey.Translate());
            }

            yield return cmd;
        }

        public override string CompInspectStringExtra()
        {
            if (isActive && arrivalTick > 0)
            {
                int ticksLeft = arrivalTick - Find.TickManager.TicksGame;
                if (ticksLeft > 0)
                    return Props.activeKey.Translate(ticksLeft.ToStringTicksToPeriod());
            }

            if (!CooldownComplete(out int cooldownLeft))
                return "TradeSignal_OnCooldown".Translate(cooldownLeft.ToStringTicksToPeriod());

            return "TradeSignal_CooldownShared".Translate();
        }

        private bool CooldownComplete(out int ticksLeft)
        {
            ticksLeft = 0;
            var tracker = Tracker;
            if (tracker == null)
                return true;

            return tracker.CooldownComplete(Props.cooldownKey, out ticksLeft);
        }

        private void TryCallTrader()
        {
            Map map = parent.Map;
            if (map == null) return;
            if (!CooldownComplete(out _)) return;

            if (IsDisabledByProgress())
            {
                Messages.Message("TradeSignal_TooAdvanced".Translate(), MessageTypeDefOf.RejectInput);
                Tracker?.NotifySignalUsed(Props.cooldownKey, Props.cooldownTicks);
                return;
            }

            List<Faction> candidates = FindValidTradeFactions(map).ToList();
            if (candidates.Count == 0)
            {
                Messages.Message(Props.noFactionKey.Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            int silverAvailable = CountSilverOnMap(map);
            if (silverAvailable < Props.silverCost)
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(Props.silverCost, silverAvailable), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "TradeSignal_ConfirmPayment".Translate(Props.silverCost),
                delegate { ExecuteSignal(map, candidates); }));
        }

        private void ExecuteSignal(Map map, List<Faction> candidates)
        {
            if (!TakeSilverFromMap(map, Props.silverCost))
            {
                Messages.Message("TradeSignal_NotEnoughSilver".Translate(Props.silverCost, CountSilverOnMap(map)), MessageTypeDefOf.RejectInput);
                return;
            }

            Faction faction = candidates.RandomElement();
            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                forced = true
            };

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("TraderCaravanArrival");
            if (incident == null)
            {
                Log.Error("[HSKMoreHardcore] TraderCaravanArrival incident not found.");
                return;
            }

            int fireTick = Find.TickManager.TicksGame + Props.arrivalDelayTicks;
            Find.Storyteller.incidentQueue.Add(incident, fireTick, parms);

            isActive = true;
            arrivalTick = fireTick;

            string delayDaysStr = ((float)Props.arrivalDelayTicks / GenDate.TicksPerDay).ToString("F1");
            Messages.Message(
                Props.scheduledKey.Translate(faction.Name, delayDaysStr),
                MessageTypeDefOf.PositiveEvent);

            Tracker?.NotifySignalUsed(Props.cooldownKey, Props.cooldownTicks);
        }

        private static int CountSilverOnMap(Map map)
        {
            int total = 0;
            foreach (Thing t in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
                total += t.stackCount;
            return total;
        }

        private static bool TakeSilverFromMap(Map map, int amount)
        {
            int remaining = amount;
            List<Thing> silvers = map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList();
            foreach (Thing silver in silvers)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(silver.stackCount, remaining);
                silver.SplitOff(take).Destroy();
                remaining -= take;
            }
            return remaining <= 0;
        }

        private bool IsDisabledByProgress()
        {
            if (Props.disableAtTechLevel != TechLevel.Undefined &&
                Faction.OfPlayer.def.techLevel >= Props.disableAtTechLevel)
                return true;

            return false;
        }

        private bool AnyValidTradeFaction(Map map)
        {
            return FindValidTradeFactions(map).Any();
        }

        private List<Faction> FindValidTradeFactions(Map map)
        {
            return FindFactionsOfTechLevel(map, Props.targetTechLevel);
        }

        private static List<Faction> FindFactionsOfTechLevel(Map map, TechLevel techLevel)
        {
            List<Faction> result = new List<Faction>();
            foreach (Faction f in Find.FactionManager.AllFactions)
            {
                if (IsValidTradeSource(f, map, techLevel))
                    result.Add(f);
            }
            return result;
        }

        private static bool IsValidTradeSource(Faction f, Map map, TechLevel techLevel)
        {
            if (f.IsPlayer || f.defeated || f.def.hidden)
                return false;
            if (f.def.techLevel != techLevel)
                return false;
            if (f.def.caravanTraderKinds.NullOrEmpty())
                return false;
            if (f.HostileTo(Faction.OfPlayer))
                return false;
            if (!f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.OutdoorTemp))
                return false;
            if (!f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.SeasonalTemp))
                return false;
            if (NeutralGroupIncidentUtility.AnyBlockingHostileLord(map, f))
                return false;

            return true;
        }
    }
}
