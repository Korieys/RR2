using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>Terms one side proposes for ending the passage conflict (Prompt 8). The conceding side yields access
    /// through the passage and withdraws without further losses; optionally it also pays bounded reparations and
    /// surrenders its flagship into the other side's custody.</summary>
    public sealed class Offer
    {
        public string Id, BattleId, EngagementId;
        public int FromSeat;              // the side making the offer
        public int ConcederSeat;          // the side that yields
        public int Reparations;           // credits, 0..MaxReparations
        public bool SurrenderFlagship;
        public int Revision;              // the battle state revision the terms were made against (-1 before battle)
        public int Round;
        public string Text(BattleSetup setup) =>
            $"{setup.Config.Players[ConcederSeat].DisplayName} yields access through the passage and withdraws without further losses" +
            (Reparations > 0 ? $", paying {Reparations} credits" : "") + (SurrenderFlagship ? ", and surrenders its flagship into custody" : "") + ".";
    }

    public sealed class OfferVerdict
    {
        public bool Valid, Accepted;
        public string Reason;              // why invalid, or the explanation of the decision
        public int Pressure, Cost;
        public List<string> Factors = new List<string>();
    }

    /// <summary>An accepted settlement, recorded once (keyed by its offer id) and applied atomically with the battle result.</summary>
    public sealed class Settlement
    {
        public string Id, OfferId, BattleId, SystemId, ConcederPolity, RecipientPolity;
        public int Reparations, Day;
        public bool Surrendered;
        public int SurrenderedUid;
        public string Text;
    }

    public static class Negotiation
    {
        public const int MaxReparations = 100;
        /// <summary>The value an AI puts on the passage it would give up, and on its flagship (experimental).</summary>
        public const int PassageValue = 30, FlagshipValue = 60;

        /// <summary>Phase boundary: offers are made at the start of the offering side's activation (nothing mid-activation),
        /// at most once per round per side, before the battle ends.</summary>
        public static string CanOffer(MatchState s, IReadOnlyList<Offer> made, int seat)
        {
            if (s.IsOver) return "The battle is over.";
            if (s.ActiveSeat != seat) return "Offers are made at the start of your own activation.";
            if (s.ActiveUnitId != 0) return "Finish the current activation first.";
            if (made.Any(o => o.FromSeat == seat && o.Round == s.Round)) return "You have already made an offer this round.";
            return null;
        }

        /// <summary>Checks the terms are possible at all (resources, custody, staleness). Returns null when they are.</summary>
        public static string Validate(GalaxyState g, BattleSetup setup, MatchState s, Offer o)
        {
            if (o.Reparations < 0 || o.Reparations > MaxReparations) return $"Reparations must be between 0 and {MaxReparations} credits.";
            if (s != null)
            {
                if (o.Revision != s.Revision) return "These terms were drafted against an earlier position; the battle has moved on. Draft them again.";
                if (s.IsOver) return "The battle is already over.";
                var flag = s.Commander(o.ConcederSeat);
                if (o.SurrenderFlagship && (flag == null || !flag.Alive)) return "That flagship no longer exists; it cannot be surrendered.";
                if (o.SurrenderFlagship && flag.HasR(RStatusType.Captured)) return "That flagship is already in custody.";
            }
            if (g != null && setup.FleetIds[o.ConcederSeat] != null)
            {
                var fleet = g.GetFleet(setup.FleetIds[o.ConcederSeat]);
                var pol = fleet != null ? g.GetPolity(fleet.OwnerId) : null;
                if (pol != null && pol.Stock.Credits < o.Reparations) return $"{pol.Name} cannot pay {o.Reparations} credits (it has {pol.Stock.Credits}).";
                if (g.Slice != null && g.Slice.Applied.Contains("offer:" + o.Id)) return "These terms were already settled.";
            }
            return null;
        }

        /// <summary>Deterministic, inspectable AI decision from what the deciding side can see: its leader's danger, the
        /// strength balance and the passage's value, against what the terms cost it.</summary>
        public static OfferVerdict Decide(GalaxyState g, BattleSetup setup, MatchState s, Offer o, int decidingSeat)
        {
            var v = new OfferVerdict();
            var invalid = Validate(g, setup, s, o);
            if (invalid != null) { v.Reason = invalid; return v; }
            v.Valid = true;
            bool concede = o.ConcederSeat == decidingSeat;
            if (!concede)
            {
                // The other side is yielding: accept unless this side is about to destroy their flagship anyway.
                var mine = s != null ? CommandThreat.Assess(s.Config.FogOfWar ? Vision.PlanningView(s, decidingSeat) : s, 1 - decidingSeat) : null;
                bool winning = mine != null && s.ActiveSeat == decidingSeat && mine.Lines.Count > 0;
                v.Accepted = !winning;
                v.Reason = winning ? "Declined: we already have a lethal line on their flagship." : "Accepted: they yield the passage on terms that cost us nothing.";
                return v;
            }
            int pressure = 0;
            if (s != null)
            {
                var view = s.Config.FogOfWar ? Vision.PlanningView(s, decidingSeat) : s;
                var threat = CommandThreat.Assess(view, decidingSeat);
                var focus = CommandThreat.RoundExposure(view, decidingSeat);
                if (threat.Lines.Count > 0) { pressure += 40; v.Factors.Add($"+40: our flagship has {threat.Lines.Count} lethal line(s) against it"); }
                int ratio = focus.LeaderHull > 0 ? Math.Min(150, focus.Total * 100 / focus.LeaderHull) : 0;
                int fp = ratio / 4;
                if (fp > 0) { pressure += fp; v.Factors.Add($"+{fp}: focus fire could deal {focus.Total} to our flagship's {focus.LeaderHull}"); }
                int ours = RulesEngine.ArmyValue(s, decidingSeat), theirs = RulesEngine.ArmyValue(s, 1 - decidingSeat);
                int sp = ours > 0 ? Math.Max(0, Math.Min(30, (theirs - ours) * 30 / ours)) : 30;
                if (sp > 0) { pressure += sp; v.Factors.Add($"+{sp}: their surviving strength {theirs} against our {ours}"); }
            }
            int cost = PassageValue + o.Reparations / 2 + (o.SurrenderFlagship ? FlagshipValue : 0);
            v.Factors.Add($"−{PassageValue}: the passage");
            if (o.Reparations > 0) v.Factors.Add($"−{o.Reparations / 2}: {o.Reparations} credits of reparations");
            if (o.SurrenderFlagship) v.Factors.Add($"−{FlagshipValue}: our flagship in their custody");
            v.Pressure = pressure; v.Cost = cost;
            v.Accepted = pressure >= cost;
            v.Reason = (v.Accepted ? "Accepted" : "Rejected") + $": pressure {pressure} against cost {cost}. " + string.Join("; ", v.Factors) + ".";
            return v;
        }

        /// <summary>Ends the battle on accepted terms: the conceding side yields (one rules command), so the battle's
        /// record and replay still hold, and the campaign transaction applies the terms once.</summary>
        public static CommandResult Conclude(BattleSession b, Offer o)
        {
            if (o.Revision != b.State.Revision) return CommandResult.Reject(null, "The terms are stale.");
            return b.Submit(Command.Resign(o.ConcederSeat), BattleSession.Host);
        }

        /// <summary>Applies an accepted settlement inside <see cref="BattleBridge.Apply"/>'s once-only transaction.</summary>
        public static void ApplyTerms(GalaxyState g, BattleSetup setup, BattleOutcome outcome, Offer o, StarSystem sys)
        {
            var s = g.Slice;
            if (s == null || !s.Applied.Add("offer:" + o.Id)) return;
            var conceder = g.GetFleet(setup.FleetIds[o.ConcederSeat]);
            var recipient = g.GetFleet(setup.FleetIds[1 - o.ConcederSeat]);
            var cp = g.GetPolity(conceder.OwnerId); var rp = g.GetPolity(recipient.OwnerId);
            int paid = Math.Min(o.Reparations, cp.Stock.Credits);
            cp.Stock = cp.Stock - new Resources(0, 0, paid, 0);
            rp.Stock = rp.Stock + new Resources(0, 0, paid, 0);
            var passage = s.Passage(sys.Id);
            if (passage != null) { passage.AccessHolderId = recipient.OwnerId; passage.SinceDay = g.Day; passage.Basis = PassageWar.Treaty; }
            foreach (var obj in s.Objectives.Where(x => x.SystemId == sys.Id && x.Status == ObjectiveStatus.Declared))
            { obj.Status = obj.PolityId == recipient.OwnerId ? ObjectiveStatus.Secured : ObjectiveStatus.Settled; obj.Note = $"Settled on day {g.Day}."; }
            var rel = s.RelationOf(cp.Id, rp.Id);
            if (rel != null) rel.Status = "truce";
            int surrendered = 0;
            if (o.SurrenderFlagship)
            {
                var flag = conceder.Ships.FirstOrDefault(x => ShipCatalog.IsLeader(x.DefId));
                if (flag != null)
                {
                    // Custody moves the one record: the ship leaves the conceder's fleet and joins the recipient's.
                    conceder.Ships.Remove(flag);
                    recipient.Ships.Add(flag);
                    surrendered = flag.Uid;
                    var prof = s.Profile(flag.Uid);
                    if (prof != null)
                    {
                        prof.Captured = true; prof.OwnerId = recipient.OwnerId;
                        prof.Service.Add(new ServiceEntry { Day = g.Day, Kind = "captured", BattleId = setup.BattleId, Text = $"Surrendered into {rp.Name} custody at {sys.Name} under a settlement." });
                    }
                }
            }
            var st = new Settlement { Id = "settle_" + o.Id, OfferId = o.Id, BattleId = setup.BattleId, SystemId = sys.Id, ConcederPolity = cp.Id, RecipientPolity = rp.Id,
                Reparations = paid, Day = g.Day, Surrendered = surrendered != 0, SurrenderedUid = surrendered, Text = o.Text(setup) };
            s.Settlements.Add(st);
            SliceCampaign.React(g, "settlement_" + o.Id, cp.Id, rp.Id, SliceCampaign.SettlementOpinion, "an honoured settlement");
            SliceCampaign.RecordEvent(g, "settlement", sys.Id, setup.BattleId, $"Settlement at {sys.Name}: {st.Text}");
            PassageWar.ForPolity(g, cp.Id, "settlement", $"Yielded the passage under terms at {sys.Name}" + (surrendered != 0 ? ", surrendering the flagship" : ""), new Resources(0, 0, paid, 0));
            PassageWar.ForPolity(g, rp.Id, "settlement", $"Obtained passage rights under terms at {sys.Name}", new Resources(0, 0, -paid, 0));
        }
    }
}
