using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    // The negotiated route (Prompt 14): a passage treaty proposed between encounters. It extends the existing negotiation
    // (the same pressure-against-cost verdict, every factor listed, decided from public information) to the strategic layer,
    // because the pre-battle settlement has no battle pressure and so could never succeed on its own. One proposal per side
    // per settlement interval; accepted terms grant passage rights by treaty and a truce. Experimental values.
    public static partial class PassageWar
    {
        public const int MaxPayment = 100;
        /// <summary>What giving up the passage costs the deciding side: it holds access, it is contested, or the proposer
        /// already holds it by battle and asks only for recognition.</summary>
        public const int HeldValue = 60, ContestedValue = 30, RecognitionValue = 20, LeaderlessPressure = 40, UntestedValue = 20, BlockadePressure = 30;

        public static string CanPropose(GalaxyState g, string polity, int payment)
        {
            var sc = Of(g);
            if (sc == null || !Running(g)) return "Treaties belong to the Passage War scenario while it is in progress.";
            if (g.PendingEngagement != null) return "An encounter is pending: offer terms in battle preparation or during the battle instead.";
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            if (p.AccessHolderId == polity && p.Basis == Treaty) return "You already hold passage rights by treaty.";
            if (sc.LastProposal.TryGetValue(polity, out var last) && g.Day < last + sc.Interval)
                return $"A proposal was made on day {last}; the next can be made on day {last + sc.Interval}.";
            if (payment < 0 || payment > MaxPayment) return $"The payment must be between 0 and {MaxPayment} credits.";
            if (g.GetPolity(polity).Stock.Credits < payment) return $"You cannot pay {payment} credits (you have {g.GetPolity(polity).Stock.Credits}).";
            return null;
        }

        static int Strength(GalaxyState g, string polity) => g.Fleets.Where(f => f.OwnerId == polity).SelectMany(f => f.Ships).Sum(s => s.Hp);

        /// <summary>The other side's deterministic decision from public information: payment, its fleet's state, the strength
        /// balance and standing, against what it gives up. Fleets and their hulls are visible on the galaxy map; nothing hidden is read.</summary>
        public static OfferVerdict EvaluateTreaty(GalaxyState g, string polity, int payment)
        {
            var v = new OfferVerdict();
            var why = CanPropose(g, polity, payment);
            if (why != null) { v.Reason = why; return v; }
            v.Valid = true;
            string decider = Other(polity);
            int pressure = 0;
            if (payment >= 4) { pressure += payment / 4; v.Factors.Add($"+{payment / 4}: {payment} credits for the rights"); }
            if (!g.Fleets.Any(f => f.OwnerId == decider && StrategyRules.CanFight(f))) { pressure += LeaderlessPressure; v.Factors.Add($"+{LeaderlessPressure}: we have no fleet led by a flagship"); }
            var hurt = DesignatedProblem(g, decider);
            if (hurt != null) { pressure += BlockadePressure; v.Factors.Add($"+{BlockadePressure}: {hurt}"); }
            int ours = Strength(g, decider), theirs = Strength(g, polity);
            int sp = ours > 0 ? Math.Max(0, Math.Min(30, (theirs - ours) * 30 / ours)) : 30;
            if (sp > 0) { pressure += sp; v.Factors.Add($"+{sp}: their fleets' hull {theirs} against our {ours}"); }
            var rel = g.Slice.RelationOf(polity, decider);
            int op = rel?.Opinion(g.Day) ?? 0;
            if (op / 2 != 0) { pressure += op / 2; v.Factors.Add($"{op / 2:+0;-0}: standing {op:+0;-0}"); }
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            int cost = p.AccessHolderId == decider ? HeldValue : p.AccessHolderId == polity ? RecognitionValue : ContestedValue;
            v.Factors.Add(p.AccessHolderId == decider ? $"−{HeldValue}: we hold the passage" : p.AccessHolderId == polity ? $"−{RecognitionValue}: recognising the access they won" : $"−{ContestedValue}: our claim on the contested passage");
            // A power that has not yet lost a battle over the passage is not ready to sign it away (pressure comes first).
            bool tested = Of(g).Ledger.Any(l => l.Polity == decider && l.Kind == "battle" && l.Text.StartsWith("Lost", StringComparison.Ordinal));
            if (!tested) { cost += UntestedValue; v.Factors.Add($"−{UntestedValue}: we have not lost a battle over the passage"); }
            v.Pressure = pressure; v.Cost = cost;
            v.Accepted = pressure >= cost;
            v.Reason = (v.Accepted ? "Accepted" : "Rejected") + $": pressure {pressure} against cost {cost}. " + string.Join("; ", v.Factors) + ".";
            return v;
        }

        /// <summary>Proposes the treaty. Whatever the answer, the proposal uses the side's one proposal for this interval.</summary>
        public static StratResult ProposeTreaty(GalaxyState g, string polity, int payment, out OfferVerdict verdict)
        {
            verdict = EvaluateTreaty(g, polity, payment);
            if (!verdict.Valid) return StratResult.Reject(verdict.Reason);
            var sc = Of(g);
            sc.LastProposal[polity] = g.Day;
            string decider = Other(polity);
            var pp = g.GetPolity(polity); var dp = g.GetPolity(decider);
            if (!verdict.Accepted)
            {
                ForPolity(g, polity, "treaty", $"Proposed passage rights for {payment} credits; {dp.Name} declined (pressure {verdict.Pressure} against {verdict.Cost}).", default);
                g.Note("info", $"{dp.Name} declined the passage treaty. " + verdict.Reason);
                return StratResult.Accept(verdict.Reason);
            }
            if (!g.Slice.Applied.Add($"treaty:{polity}:{g.Day}")) return StratResult.Reject("This treaty was already concluded.");
            pp.Stock = pp.Stock - new Resources(0, 0, payment, 0);
            dp.Stock = dp.Stock + new Resources(0, 0, payment, 0);
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            p.AccessHolderId = polity; p.Basis = Treaty; p.SinceDay = g.Day;
            foreach (var obj in g.Slice.Objectives.Where(x => x.SystemId == p.SystemId && x.Status == ObjectiveStatus.Declared))
            { obj.Status = obj.PolityId == polity ? ObjectiveStatus.Secured : ObjectiveStatus.Settled; obj.Note = $"Settled by treaty on day {g.Day}."; }
            var rel = g.Slice.RelationOf(polity, decider);
            if (rel != null) rel.Status = "truce";
            // The other side stands down: its fleets stop heading for the passage (a fleet already in a lane finishes the jump).
            foreach (var f in g.Fleets.Where(f => f.OwnerId == decider && f.AIControlled))
            {
                f.AITarget = null;
                if (f.Order != null && f.Order.DaysIntoLane == 0) f.Order = null;
                else if (f.Order != null && f.Order.Path.Count > 1) f.Order.Path.RemoveRange(1, f.Order.Path.Count - 1);
            }
            SliceCampaign.React(g, $"treaty_{polity}_{g.Day}", polity, decider, SliceCampaign.SettlementOpinion, "an honoured settlement");
            SliceCampaign.RecordEvent(g, "treaty", p.SystemId, null, $"Treaty: {dp.Name} granted {pp.Name} passage rights through {p.Name}" + (payment > 0 ? $" for {payment} credits." : "."));
            ForPolity(g, polity, "treaty", $"Obtained passage rights through {p.Name} by treaty", new Resources(0, 0, payment, 0));
            ForPolity(g, decider, "treaty", $"Granted {pp.Name} passage rights by treaty", new Resources(0, 0, -payment, 0));
            g.Note("result", $"{dp.Name} accepted: {pp.Name} holds passage rights through {p.Name} by treaty. They are ratified at the settlement on day {NextSettlementDay(g)} if the treaty still stands.");
            return StratResult.Accept(verdict.Reason);
        }

        /// <summary>Whether the two powers are under a truce (a settlement or a treaty): their fleets do not engage on contact.</summary>
        public static bool AtPeace(GalaxyState g, string a, string b)
        {
            var rel = g?.Slice?.RelationOf(a, b);
            return rel != null && rel.Status == "truce";
        }
    }
}
