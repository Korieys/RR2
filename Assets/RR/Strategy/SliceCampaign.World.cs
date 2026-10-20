using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>Prompt 9: a galaxy that remembers. Every consequence here comes from an event the battle or diplomacy
    /// transaction already produced, is keyed so it applies once, and is written from authored templates.</summary>
    public static partial class SliceCampaign
    {
        public const int FlagshipLossOpinion = -20, SettlementOpinion = 15, ReactionDays = 60, OpinionFloor = -40;
        public const string WreckedPassageMap = "trade_passage_wrecked";

        static void Record(GalaxyState g, HistoryEvent e)
        {
            if (e.Id == null) e.Id = "ev_" + g.NextId++;
            e.Day = g.Day;
            g.Slice.History.Add(e);
        }

        public static void RecordEvent(GalaxyState g, string kind, string systemId, string battleId, string text) =>
            Record(g, new HistoryEvent { Kind = kind, SystemId = systemId, BattleId = battleId, Text = text });

        /// <summary>Applies a political modifier once (by key), bounded in duration and in how far one cause can push.</summary>
        public static bool React(GalaxyState g, string key, string a, string b, int amount, string cause)
        {
            var s = g.Slice;
            if (s == null || !s.Applied.Add("react:" + key)) return false;
            var rel = s.RelationOf(a, b);
            if (rel == null) { rel = new Relation { A = a, B = b }; s.Relations.Add(rel); }
            int sameCause = rel.Modifiers.Where(m => m.Cause == cause && m.ExpiresDay > g.Day).Sum(m => m.Amount);
            int allowed = amount < 0 ? Math.Max(amount, OpinionFloor - sameCause) : amount;
            if (allowed == 0) return false;
            rel.Modifiers.Add(new RelationModifier { Key = key, Cause = cause, Amount = allowed, ExpiresDay = g.Day + ReactionDays });
            return true;
        }

        static void Consequences(GalaxyState g, BattleSetup setup, BattleOutcome o, StarSystem sys, Fleet winner, Fleet loser)
        {
            var s = g.Slice;
            if (s == null || !s.Active) return;
            string wp = winner.OwnerId, lp = loser.OwnerId;
            if (o.Reason == "Settlement") return;   // Negotiation.ApplyTerms records the settlement itself
            // The battle itself.
            Record(g, new HistoryEvent { Kind = "battle", SystemId = sys.Id, BattleId = setup.BattleId,
                Text = $"Battle of {sys.Name}: {winner.Name} ({g.GetPolity(wp).Name}) defeated {loser.Name} ({g.GetPolity(lp).Name})." + (o.DecisiveText != null ? " " + o.DecisiveText : ""),
                Ships = o.Ships.SelectMany(x => x).Select(x => x.Uid).Where(u => s.Profile(u) != null).ToList() });
            var passage = s.Passage(sys.Id);
            if (passage != null && o.Reason != "Settlement")
                Record(g, new HistoryEvent { Kind = "passage", SystemId = sys.Id, BattleId = setup.BattleId, Text = $"{g.GetPolity(wp).Name} took access through {passage.Name}." });
            // Destroyed capital ships: a wreck, a history entry and a reaction. Captured ships leave no wreck.
            for (int seat = 0; seat < 2; seat++)
                foreach (var so in o.Ships[seat].Where(x => x.Destroyed))
                {
                    var p = s.Profile(so.Uid);
                    if (p == null) continue;
                    string name = p.Name ?? ShipCatalog.Name(p.DefId);
                    var w = new WreckRecord
                    {
                        Id = "wreck_" + setup.BattleId + "_" + so.Uid, SystemId = sys.Id, Uid = so.Uid, DefId = p.DefId, Name = name, OwnerId = p.OwnerId,
                        BattleId = setup.BattleId, Day = g.Day, Module = p.Modules.FirstOrDefault(), Materials = new Resources(0, 60, 0, 0),
                    };
                    if (s.Wrecks.All(x => x.Id != w.Id)) s.Wrecks.Add(w);
                    Record(g, new HistoryEvent { Kind = "loss", SystemId = sys.Id, BattleId = setup.BattleId, Ships = { so.Uid },
                        Text = $"The {g.GetPolity(p.OwnerId).Name} flagship {name} was destroyed at {sys.Name}; its wreck remains." });
                    string killer = p.OwnerId == wp ? lp : wp;
                    React(g, "flagship_" + setup.BattleId + "_" + so.Uid, p.OwnerId, killer, FlagshipLossOpinion, "destroyed a named flagship");
                    if (sys.Id == PassageSystem) sys.BattlefieldMapId = WreckedPassageMap;   // the next battle here is fought among the wreckage
                }
        }

        // ------------------------------------------------------------------ salvage

        public static string SalvageProblem(GalaxyState g, string wreckId, string fleetId)
        {
            var s = g.Slice;
            var w = s?.Wrecks.FirstOrDefault(x => x.Id == wreckId);
            if (w == null) return "There is no such wreck.";
            if (w.Salvaged) return $"The wreck of {w.Name} has already been salvaged by {g.GetPolity(w.SalvagedBy)?.Name ?? w.SalvagedBy}.";
            var f = g.GetFleet(fleetId);
            if (f == null || f.Ships.Count == 0) return "Choose a fleet to salvage with.";
            if (f.SystemId != w.SystemId || f.Moving) return $"A fleet must hold at {g.GetSystem(w.SystemId).Name} to salvage there.";
            if (f.EngagementId != null || g.FleetsAt(w.SystemId).Any(o => StrategyRules.Hostile(o.OwnerId, f.OwnerId))) return "Hostile ships hold the area; salvage needs a quiet orbit.";
            if (w.Module != null)
            {
                var flagship = f.Ships.FirstOrDefault(x => IsCapital(x.DefId));
                var prof = flagship != null ? s.Profile(flagship.Uid) : null;
                if (prof != null && prof.Modules.Contains(w.Module)) return null;   // it becomes spare materials instead
            }
            return null;
        }

        /// <summary>What salvage would award, stated before it is taken.</summary>
        public static string SalvagePreview(GalaxyState g, string wreckId)
        {
            var w = g.Slice?.Wrecks.FirstOrDefault(x => x.Id == wreckId);
            if (w == null) return "";
            return $"Salvaging the wreck of {w.Name} recovers {w.Materials.Materials} materials" + (w.Module != null ? $" and its damaged {SliceRules.Module(w.Module).Name} (fitted to your flagship at no cost the next time it refits at a shipyard)" : "") +
                   ". A wreck can be salvaged once.";
        }

        public static StratResult Salvage(GalaxyState g, string wreckId, string fleetId)
        {
            var why = SalvageProblem(g, wreckId, fleetId);
            if (why != null) return StratResult.Reject(why);
            var s = g.Slice;
            var w = s.Wrecks.First(x => x.Id == wreckId);
            if (!s.Applied.Add("salvage:" + w.Id)) return StratResult.Reject("Already salvaged.");
            var f = g.GetFleet(fleetId);
            var pol = g.GetPolity(f.OwnerId);
            pol.Stock = pol.Stock + w.Materials;
            w.Salvaged = true; w.SalvagedBy = f.OwnerId;
            string got = $"{w.Materials.Materials} materials";
            if (w.Module != null)
            {
                s.Applied.Add("spare:" + f.OwnerId + ":" + w.Module + ":" + w.Id);
                got += " and a damaged " + SliceRules.Module(w.Module).Name;
            }
            Record(g, new HistoryEvent { Kind = "salvage", SystemId = w.SystemId, Text = $"{f.Name} salvaged the wreck of {w.Name}: {got}.", Ships = { w.Uid } });
            PassageWar.ForPolity(g, f.OwnerId, "salvage", $"Salvaged the wreck of {w.Name}" + (w.Module != null ? " (and a spare module)" : ""), default(Resources) - w.Materials);
            g.Note("result", $"{f.Name} salvaged the wreck of {w.Name}: {got}.");
            return StratResult.Accept();
        }

        /// <summary>Spare modules recovered by salvage or an expedition, each fitted once at no cost.</summary>
        public static List<string> SpareModules(GalaxyState g, string polity) =>
            g.Slice?.Applied.Where(k => k.StartsWith("spare:" + polity + ":", StringComparison.Ordinal) && !g.Slice.Applied.Contains("used" + k))
                .Select(k => k.Split(':')[2]).ToList() ?? new List<string>();

        static string SpareKey(GalaxyState g, string polity, string module) =>
            g.Slice?.Applied.Where(k => k.StartsWith("spare:" + polity + ":" + module + ":", StringComparison.Ordinal) && !g.Slice.Applied.Contains("used" + k))
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();

        // ------------------------------------------------------------------ history for a system or ship

        public static IEnumerable<HistoryEvent> HistoryOf(GalaxyState g, string systemId) => g.Slice?.History.Where(e => e.SystemId == systemId) ?? Enumerable.Empty<HistoryEvent>();
        public static IEnumerable<HistoryEvent> HistoryOfShip(GalaxyState g, int uid) => g.Slice?.History.Where(e => e.Ships.Contains(uid)) ?? Enumerable.Empty<HistoryEvent>();
    }
}
