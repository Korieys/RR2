using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    // ======================================================================================================
    // "The Passage War" (campaign alpha Prompt 14; working title, not canon): a bounded scenario configuration of the
    // existing passage campaign. Same galaxy, same rules, same saves; it adds an active scope, a mandate with transparent
    // victory, defeat and inconclusive conditions, settlement checks every few days, a decision ledger and an ending.
    // The freeform passage campaign (SliceCampaign.Create without this) is unchanged.
    // ======================================================================================================

    public enum CampaignOutcome { None, MilitaryVictory, TreatyVictory, Defeat, Inconclusive }

    /// <summary>One decision or event with what it cost (negative costs are gains). Written once by the transaction that made it.</summary>
    public sealed class LedgerEntry
    {
        public int Day;
        public string Polity, Kind, Text;
        public Resources Cost;
    }

    /// <summary>The strategic event summary produced at each settlement.</summary>
    public sealed class SettlementReport
    {
        public int Day;
        public List<string> Lines = new List<string>();
    }

    public sealed class ScenarioState
    {
        public string Id, Title;
        public int Version = 1;
        public List<string> ActiveSystems = new List<string>();
        /// <summary>Per polity, the systems whose worlds the mandate requires to stay operational (owned and not blockaded).</summary>
        public Dictionary<string, List<string>> Designated = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public int Interval = 10, HoldRequired = 3, TreatyHoldRequired = 1, MandateDay = 240;
        /// <summary>Consecutive settlements at which the player's hold counted.</summary>
        public int HoldCount;
        public string HoldNote;
        public CampaignOutcome Outcome;
        public int EndedDay;
        public string EndText;
        /// <summary>The player chose to keep playing after the ending; settlements stop deciding anything.</summary>
        public bool Freeform;
        /// <summary>Day of each side's last strategic treaty proposal (one per settlement interval).</summary>
        public Dictionary<string, int> LastProposal = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<LedgerEntry> Ledger = new List<LedgerEntry>();
        public List<SettlementReport> Reports = new List<SettlementReport>();
        /// <summary>Completed local priorities, as "polity:priority" (Economy, Prompt 15).</summary>
        public HashSet<string> Built = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Materials held in a forward dock's stores, by system.</summary>
        public Dictionary<string, int> Stores = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>Unpaid upkeep, by polity (never a negative balance).</summary>
        public Dictionary<string, int> Arrears = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>The player's supplied systems and known threats as last announced (so each change is announced once).</summary>
        public List<string> SupplyKnown, ThreatsKnown;
        /// <summary>The rival's current movement plan per AI power (commitment against oscillation) and its developer log
        /// (reasons; never shown in the player's interface).</summary>
        public Dictionary<string, string> AiPlan = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string> AiLog = new List<string>();
        /// <summary>Automation only: the rival planner also commands the player's side (AI-against-AI campaign runs).</summary>
        public bool AutoPlayer;
        /// <summary>The starting situation (Prompt 19) and the setup seed, stored so a reload never changes the world.</summary>
        public string Variant = Variants.Standard;
        public int SetupSeed;
        /// <summary>Doctrine projects (Prompt 18): one under way per power, the adopted ones ("polity:id"), and each power's path.</summary>
        public Dictionary<string, DoctrineResearch> Researching = new Dictionary<string, DoctrineResearch>(StringComparer.Ordinal);
        public HashSet<string> Researched = new HashSet<string>(StringComparer.Ordinal);
        public Dictionary<string, string> DoctrinePath = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>A treaty an AI power proposed to the player, waiting for the player's answer (expires at the next settlement).</summary>
        public Proposal Pending;
        public bool Ended => Outcome != CampaignOutcome.None;
    }

    /// <summary>A proposal from an AI power that the player answers.</summary>
    public sealed class Proposal
    {
        public string From, Kind;   // "rights": the proposer buys passage rights; "statusquo": a truce leaving access as it is
        public int Payment, Day, Expires;
        public string Reason;
    }

    public static partial class PassageWar
    {
        public const string Id = "passage_war";
        public const string Title = "The Passage War";
        public const string Battle = "battle", Treaty = "treaty";

        /// <summary>Ten existing systems: both home regions, the contested passage, the Eclipse border at Gloam (the optional
        /// expedition) and the northern route through the Pirate Reach that bypasses Umbra.</summary>
        public static readonly string[] ActiveSystems =
            { "forgefall", "ironmouth", "cinderhold", "gloam", "umbra", "salvage_row", "blackwake", "shoal_haven", "dividend", "ledgerhold" };

        static readonly Dictionary<string, string[]> DesignatedSystems = new Dictionary<string, string[]>
        {
            ["pygmaei"] = new[] { "forgefall", "ironmouth" },
            ["inhumanus"] = new[] { "dividend", "ledgerhold" },
        };

        public static GalaxyState Create(FactionId player) => Create(player, Variants.Standard);

        public static GalaxyState Create(FactionId player, string variant)
        {
            var g = SliceCampaign.Create(player);
            var sc = new ScenarioState { Id = Id, Title = Title };
            sc.ActiveSystems.AddRange(ActiveSystems);
            foreach (var kv in DesignatedSystems) sc.Designated[kv.Key] = kv.Value.ToList();
            g.Slice.Scenario = sc;
            // Ground forces (Prompt 16): an expeditionary legion at each home world and a garrison on each second world. Real units
            // from the existing planetary rosters; they move only by embarking in fleets (a flagship carries two, other ships one).
            Operations.NewForce(g, "pygmaei", HomeWorld(g, "forgefall", "pygmaei"), null, "pygmaei_iron_marshal", "pygmaei_rush_trooper", "pygmaei_rush_trooper", "pygmaei_volt_striker",
                "pygmaei_ember_vanguard", "pygmaei_combat_engineer", "pygmaei_blast_furnace_tank", "pygmaei_razorbug_speeder");
            Operations.NewForce(g, "pygmaei", HomeWorld(g, "ironmouth", "pygmaei"), null, "pygmaei_iron_marshal", "pygmaei_forge_warden", "pygmaei_rush_trooper", "pygmaei_rush_trooper",
                "pygmaei_ember_vanguard", "pygmaei_shock_medic", "pygmaei_combat_engineer");
            Operations.NewForce(g, "inhumanus", HomeWorld(g, "dividend", "inhumanus"), null, "inhumanus_executive_adjudicator", "inhumanus_contract_trooper", "inhumanus_contract_trooper",
                "inhumanus_vector_sniper", "inhumanus_asset_recovery_walker", "inhumanus_gravity_weaver", "inhumanus_security_drone", "inhumanus_spectrum_analyst");
            Operations.NewForce(g, "inhumanus", HomeWorld(g, "ledgerhold", "inhumanus"), null, "inhumanus_executive_adjudicator", "inhumanus_contract_trooper", "inhumanus_contract_trooper",
                "inhumanus_contract_trooper", "inhumanus_asset_recovery_agent", "inhumanus_phase_auditor", "inhumanus_vector_skimmer", "inhumanus_security_drone");
            g.Log.Clear();
            Variants.Apply(g, variant);
            g.Note("order", $"{Title}: secure durable access through the Umbra passage and keep {DesignatedNames(g, g.Player.Id)} operational. " +
                            "Win access in battle and hold it, or obtain passage rights by treaty. The mandate panel states every condition.");
            return g;
        }

        public static ScenarioState Of(GalaxyState g) => g?.Slice?.Scenario;
        static string HomeWorld(GalaxyState g, string systemId, string polity) =>
            g.GetSystem(systemId).Planets.Where(p => p.OwnerId == polity).OrderBy(p => p.Id, StringComparer.Ordinal).First().Id;
        /// <summary>The scenario is deciding outcomes (it exists, has not ended, and the player has not switched to freeform).</summary>
        public static bool Running(GalaxyState g) { var sc = Of(g); return sc != null && !sc.Ended && !sc.Freeform; }
        public static bool InScope(GalaxyState g, string systemId) { var sc = Of(g); return sc == null || sc.ActiveSystems.Contains(systemId); }
        public static string Other(string polity) => polity == "pygmaei" ? "inhumanus" : "pygmaei";

        public static string DesignatedNames(GalaxyState g, string polity)
        {
            var sc = Of(g);
            if (sc == null || !sc.Designated.TryGetValue(polity, out var ids)) return "your home worlds";
            return string.Join(" and ", ids.Select(id => g.GetSystem(id).Name));
        }

        /// <summary>Why a designated system of <paramref name="polity"/> is not operational, or null if all are.</summary>
        public static string DesignatedProblem(GalaxyState g, string polity)
        {
            var sc = Of(g);
            if (sc == null || !sc.Designated.TryGetValue(polity, out var ids)) return null;
            foreach (var id in ids)
            {
                var sys = g.GetSystem(id);
                if (!sys.Planets.Any(p => p.OwnerId == polity)) return $"{sys.Name} is no longer under {g.GetPolity(polity).Name} rule";
                if (sys.Blockaded) return $"{sys.Name} is blockaded by {g.GetPolity(sys.OrbitalControlId).Name}";
            }
            return null;
        }

        // ------------------------------------------------------------------ the mandate, stated

        /// <summary>The exact conditions, as the mandate panel shows them.</summary>
        public static List<string> Requirements(GalaxyState g)
        {
            var sc = Of(g);
            var lines = new List<string>();
            if (sc == null) return lines;
            string mine = DesignatedNames(g, g.Player.Id);
            lines.Add($"Victory by arms: hold access through the Umbra passage, won in battle, at {sc.HoldRequired} consecutive settlements (every {sc.Interval} days) " +
                      $"while {mine} stay operational (yours and not blockaded).");
            lines.Add("A settlement does not count, and the count restarts, while: your access is lost; a designated world is lost or blockaded; your supply does not " +
                      "reach Umbra (see the supply overlay); an enemy fleet led by a flagship holds position at Umbra; or the rival is actively contesting the passage " +
                      "(a declared objective and a fleet that can fight).");
            lines.Add($"At each settlement both powers pay fleet upkeep ({Economy.UpkeepPerPoint} credit per fleet point). Upkeep that cannot be paid becomes arrears: " +
                      "projects pause and repairs, refits and new projects wait until income has paid it.");
            lines.Add("Access by arms comes from winning a battle at Umbra, or from occupation: at war over the passage, your fleet led by a flagship holds Umbra at a " +
                      "settlement with no enemy fleet able to fight there. The same rule applies to the rival.");
            lines.Add($"Victory by treaty: hold passage rights granted by a settlement or a treaty, still in force at the next settlement, with {mine} operational.");
            lines.Add("If the rival has no way left to contest the passage (no fleet that can fight, none being built, none it can pay for, or no attempts left), the next " +
                      "settlement that counts ends the campaign at once.");
            lines.Add($"Defeat: your objective fails {SliceCampaign.MaxAttempts} times and the rival holds access at a settlement; or, at a settlement, you have no fleet led " +
                      "by a flagship, none being built and cannot pay for one, and no access. A lost battle alone never ends the campaign.");
            lines.Add($"Inconclusive: day {sc.MandateDay} ({GalaxyState.DateLabel(sc.MandateDay)}) arrives first. The campaign ends with the passage as it stands.");
            lines.Add("During an encounter strategic time stops for both powers: no income, production, movement or settlement until it is resolved. Pausing never pays income.");
            lines.Add("Planetary operations (landings and ground series) are not connected to this scenario yet; orbital victory blockades a world but never takes it.");
            return lines;
        }

        public static int NextSettlementDay(GalaxyState g) { var sc = Of(g); return sc == null ? 0 : (g.Day / sc.Interval + 1) * sc.Interval; }

        /// <summary>One line of progress for the compact panel.</summary>
        public static string Progress(GalaxyState g)
        {
            var sc = Of(g);
            if (sc == null) return null;
            if (sc.Ended) return $"{OutcomeLabel(sc.Outcome)} on day {sc.EndedDay}." + (sc.Freeform ? " Continuing freeform." : "");
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            string me = g.Player.Id;
            int next = NextSettlementDay(g);
            if (p.AccessHolderId != me)
                return (p.AccessHolderId == null ? "Access is contested." : $"{g.GetPolity(p.AccessHolderId).Name} holds access.") + $" Next settlement day {next}.";
            int need = p.Basis == Treaty ? sc.TreatyHoldRequired : sc.HoldRequired;
            var why = HoldBlocker(g, p);
            return $"You hold access ({(p.Basis == Treaty ? "by treaty" : "by battle")}): {sc.HoldCount} of {need} settlements. Next settlement day {next}" +
                   (why != null ? $"; it will not count while {why}." : ".");
        }

        public static string OutcomeLabel(CampaignOutcome o) => o switch
        {
            CampaignOutcome.MilitaryVictory => "Victory by arms",
            CampaignOutcome.TreatyVictory => "Victory by treaty",
            CampaignOutcome.Defeat => "Defeat",
            CampaignOutcome.Inconclusive => "Inconclusive end",
            _ => "In progress",
        };

        /// <summary>Warnings the player must see before a settlement can end the campaign against them.</summary>
        public static List<string> Warnings(GalaxyState g)
        {
            var w = new List<string>();
            if (!Running(g)) return w;
            string me = g.Player.Id;
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            if (Attempts(g, me) >= SliceCampaign.MaxAttempts && p.AccessHolderId != me)
                w.Add($"Your objective has failed {SliceCampaign.MaxAttempts} times. Unless a treaty grants you passage rights by the settlement on day {NextSettlementDay(g)}, the mandate fails.");
            if (!g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f)) && !FlagshipInProduction(g, me))
                w.Add(CanAffordFlagship(g, me)
                    ? $"You have no fleet led by a flagship. Commission a replacement at your shipyard ({CostText(FlagshipCost(me))}) before you can fight again."
                    : $"You have no fleet led by a flagship and cannot pay for one. If nothing changes, the settlement on day {NextSettlementDay(g)} ends the campaign in defeat.");
            var dp = DesignatedProblem(g, me);
            if (dp != null) w.Add(dp + "; your hold cannot count until it is restored.");
            int left = Of(g).MandateDay - g.Day;
            if (left <= 30) w.Add($"The mandate ends on day {Of(g).MandateDay} ({left} days).");
            return w;
        }

        // ------------------------------------------------------------------ the settlement check

        public static int Attempts(GalaxyState g, string polity) => SliceCampaign.FailedAttempts(g, polity, SliceCampaign.PassageSystem);

        public static bool FlagshipInProduction(GalaxyState g, string polity) =>
            g.Systems.SelectMany(s => s.Planets).Any(p => p.OwnerId == polity && p.Project != null && p.Project.Kind == ProjectKind.CommissionShip && ShipCatalog.IsLeader(p.Project.ShipDefId));

        public static bool CanAffordFlagship(GalaxyState g, string polity)
        {
            var leader = FlagshipDef(polity);
            return leader != null && g.GetPolity(polity).Stock.Covers(FlagshipCost(polity));
        }

        static string FlagshipDef(string polity) => SliceRoster.Classes(SliceCampaign.FactionOf(polity)).FirstOrDefault(ShipCatalog.IsLeader);

        /// <summary>The shipyard price of a replacement flagship (the same formula as SliceCampaign.Commissionable).</summary>
        public static Resources FlagshipCost(string polity)
        {
            var id = FlagshipDef(polity);
            int c = id == null ? 0 : RosterContent.Cost(RosterContent.Unit(id));
            return new Resources(15 * c, 30 * c, 12 * c, 0);
        }

        /// <summary>Whether <paramref name="polity"/> still has any lawful way to fight for the passage.</summary>
        public static bool CanContest(GalaxyState g, string polity)
        {
            var rel = g.Slice.RelationOf(polity, Other(polity));
            if (rel != null && rel.Status == "truce") return false;
            if (Attempts(g, polity) >= SliceCampaign.MaxAttempts) return false;
            return g.Fleets.Any(f => f.OwnerId == polity && StrategyRules.CanFight(f)) || FlagshipInProduction(g, polity) || CanAffordFlagship(g, polity);
        }

        /// <summary>Why the access holder's settlement would not count now, or null.</summary>
        public static string HoldBlocker(GalaxyState g, PassageState p)
        {
            if (p.AccessHolderId == null) return "access is contested";
            string h = p.AccessHolderId, o = Other(h);
            var dp = DesignatedProblem(g, h);
            if (dp != null) return dp;
            if (p.Basis == Treaty)
            {
                var rel = g.Slice.RelationOf(h, o);
                if (rel == null || rel.Status != "truce") return "the treaty is no longer in force";
            }
            var net = Supply.Of(g, h);
            if (!net.Has(p.SystemId))
            {
                var cut = net.Nodes.Values.Where(n => !n.Supplied && n.Reason != null && g.LanesOf(n.SystemId).Any(l => l.Other(n.SystemId) == p.SystemId || net.Has(l.Other(n.SystemId))))
                                   .Select(n => n.Reason).FirstOrDefault() ?? net.Node(p.SystemId)?.Reason;
                return $"supply does not reach {g.GetSystem(p.SystemId).Name} ({cut})";
            }
            if (!AtPeace(g, h, o) && g.Fleets.Any(f => f.OwnerId == o && f.SystemId == p.SystemId && !f.Moving && StrategyRules.CanFight(f)))
                return $"a {g.GetPolity(o).Name} fleet led by a flagship holds position at {g.GetSystem(p.SystemId).Name}";
            if (g.Slice.Objectives.Any(x => x.PolityId == o && x.SystemId == p.SystemId && x.Status == ObjectiveStatus.Declared) &&
                g.Fleets.Any(f => f.OwnerId == o && StrategyRules.CanFight(f)))
                return $"{g.GetPolity(o).Name} is actively contesting the passage";
            return null;
        }

        /// <summary>Called once per strategic day (inside AdvanceDay, after the economy). Settlements happen on multiples of
        /// the interval and are keyed by day, so a reload can neither repeat nor skip one.</summary>
        public static void OnDay(GalaxyState g)
        {
            var sc = Of(g);
            if (!Running(g) || g.Day <= 0 || g.Day % sc.Interval != 0) return;
            if (!g.Slice.Applied.Add("settlement:" + g.Day)) return;
            if (sc.Pending != null && sc.Pending.Expires <= g.Day) { g.Note("info", $"{g.GetPolity(sc.Pending.From).Name}'s proposal expired unanswered."); sc.Pending = null; }
            var rep = new SettlementReport { Day = g.Day };
            int prev = g.Day - sc.Interval;
            string me = g.Player.Id, them = Other(me);
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);

            foreach (var e in g.Slice.History.Where(x => x.Day > prev && x.Day <= g.Day)) rep.Lines.Add($"Day {e.Day}: {e.Text}");
            if (rep.Lines.Count == 0) rep.Lines.Add("No battles, declarations or settlements since the last settlement.");
            rep.Lines.AddRange(Economy.Settle(g));

            int need = p.Basis == Treaty ? sc.TreatyHoldRequired : sc.HoldRequired;
            if (p.AccessHolderId == me)
            {
                var why = HoldBlocker(g, p);
                if (why == null) { sc.HoldCount++; sc.HoldNote = $"Your hold counted: {sc.HoldCount} of {need}."; }
                else { sc.HoldCount = 0; sc.HoldNote = $"Your hold did not count ({why}); the count restarts."; }
            }
            else
            {
                sc.HoldCount = 0;
                sc.HoldNote = p.AccessHolderId == null ? "Access is contested; no hold is counting." : $"{g.GetPolity(p.AccessHolderId).Name} holds access; your hold is at 0.";
            }
            rep.Lines.Add(sc.HoldNote);
            ForPolity(g, me, "settlement", sc.HoldNote, default);
            sc.Reports.Add(rep);

            // Outcomes, in a fixed order: victory, defeat, the mandate's end.
            if (p.AccessHolderId == me && sc.HoldCount >= need)
                End(g, p.Basis == Treaty ? CampaignOutcome.TreatyVictory : CampaignOutcome.MilitaryVictory,
                    p.Basis == Treaty ? $"Passage rights through {p.Name} were ratified at the settlement on day {g.Day}." :
                                        $"You held {p.Name} through {need} consecutive settlements.");
            else if (p.AccessHolderId == me && sc.HoldCount >= 1 && !CanContest(g, them))
                End(g, p.Basis == Treaty ? CampaignOutcome.TreatyVictory : CampaignOutcome.MilitaryVictory,
                    $"You hold {p.Name}, and {g.GetPolity(them).Name} has no way left to contest it (no fleet that can fight, none being built or affordable, or no attempts left).");
            else if (Attempts(g, me) >= SliceCampaign.MaxAttempts && p.AccessHolderId == them)
                End(g, CampaignOutcome.Defeat, $"Your objective failed {SliceCampaign.MaxAttempts} times and {g.GetPolity(them).Name} holds the passage.");
            else if (p.AccessHolderId != me && !g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f)) && !FlagshipInProduction(g, me) && !CanAffordFlagship(g, me))
                End(g, CampaignOutcome.Defeat, "You had no fleet led by a flagship, none being built, and no means to pay for one.");
            else if (g.Day >= sc.MandateDay)
                End(g, CampaignOutcome.Inconclusive, $"The mandate ran out on day {sc.MandateDay}. " +
                    (p.AccessHolderId == null ? "The passage remains contested." : $"{g.GetPolity(p.AccessHolderId).Name} holds access."));
            if (!sc.Ended) Occupation(g, p, rep);
            if (sc.Ended) rep.Lines.Add($"{OutcomeLabel(sc.Outcome)}: {sc.EndText}");
            g.Note(sc.Ended ? (sc.Outcome == CampaignOutcome.Defeat ? "danger" : "result") : "info", $"Settlement, day {g.Day}: {sc.HoldNote}");
        }

        /// <summary>Occupation (campaign alpha Prompt 20, CA-27): a power at war over the passage whose fleet led by a flagship holds
        /// position there at a settlement, with no hostile fleet able to fight present, takes access by arms. Its hold starts
        /// counting at the next settlement. Applied after the hold check, once per settlement, to either power.</summary>
        static void Occupation(GalaxyState g, PassageState p, SettlementReport rep)
        {
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                if (p.AccessHolderId == pol) continue;
                string other = Other(pol);
                if (AtPeace(g, pol, other)) continue;
                if (!g.Slice.Objectives.Any(o => o.PolityId == pol && o.SystemId == p.SystemId && o.Status == ObjectiveStatus.Declared)) continue;
                var mine = g.Fleets.Where(f => f.OwnerId == pol && f.SystemId == p.SystemId && !f.Moving && f.EngagementId == null && StrategyRules.CanFight(f)).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (mine == null) continue;
                if (g.Fleets.Any(f => f.OwnerId == other && f.SystemId == p.SystemId && !f.Moving && StrategyRules.CanFight(f))) continue;
                if (!g.Slice.Applied.Add($"occupation:{pol}:{g.Day}")) continue;
                p.AccessHolderId = pol; p.Basis = Battle; p.SinceDay = g.Day;
                foreach (var obj in g.Slice.Objectives.Where(x => x.SystemId == p.SystemId && x.Status == ObjectiveStatus.Declared && x.PolityId == pol))
                { obj.Status = ObjectiveStatus.Secured; obj.Note = $"Secured by occupation on day {g.Day}."; }
                if (pol == g.Player.Id) Of(g).HoldCount = 0;
                string text = $"{g.GetPolity(pol).Name} took access through {p.Name} by holding {g.GetSystem(p.SystemId).Name} unopposed with {mine.Name}.";
                SliceCampaign.RecordEvent(g, "passage", p.SystemId, null, text);
                ForPolity(g, pol, "occupation", $"Took access through {p.Name} by occupation", default);
                rep.Lines.Add(text);
                g.Note(pol == g.Player.Id ? "result" : "danger", text + (pol == g.Player.Id ? " Your hold counts from the next settlement." : ""));
                return;
            }
        }

        static void End(GalaxyState g, CampaignOutcome o, string text)
        {
            var sc = Of(g);
            if (sc.Ended) return;
            sc.Outcome = o; sc.EndedDay = g.Day; sc.EndText = text;
            SliceCampaign.RecordEvent(g, "ending", SliceCampaign.PassageSystem, null, $"{Title}: {OutcomeLabel(o)}. {text}");
        }

        /// <summary>The player keeps playing after the ending. The outcome stays recorded; settlements no longer decide anything.</summary>
        public static StratResult ContinueFreeform(GalaxyState g)
        {
            var sc = Of(g);
            if (sc == null || !sc.Ended) return StratResult.Reject("The scenario has not ended.");
            if (sc.Freeform) return StratResult.Reject("Already continuing freeform.");
            sc.Freeform = true;
            g.Note("info", $"{Title} ended ({OutcomeLabel(sc.Outcome)}); the galaxy continues without a mandate.");
            return StratResult.Accept();
        }

        // ------------------------------------------------------------------ the ledger and the ending

        /// <summary>Writes a ledger entry for any polity (no-op outside the scenario).</summary>
        public static void ForPolity(GalaxyState g, string polity, string kind, string text, Resources cost)
        {
            var sc = Of(g);
            if (sc == null || polity == null) return;
            sc.Ledger.Add(new LedgerEntry { Day = g.Day, Polity = polity, Kind = kind, Text = text, Cost = cost });
        }

        public static string CostText(Resources r)
        {
            var parts = new List<string>();
            if (r.Energy != 0) parts.Add($"{r.Energy} energy");
            if (r.Materials != 0) parts.Add($"{r.Materials} materials");
            if (r.Credits != 0) parts.Add($"{r.Credits} credits");
            if (r.Influence != 0) parts.Add($"{r.Influence} influence");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        static int Sum(Resources r) => r.Energy + r.Materials + r.Credits + r.Influence;

        /// <summary>The ending: what happened, recounted from the ledger and the history (never invented).</summary>
        public static List<string> Ending(GalaxyState g)
        {
            var sc = Of(g);
            var lines = new List<string>();
            if (sc == null || !sc.Ended) return lines;
            string me = g.Player.Id, them = Other(me);
            lines.Add($"{OutcomeLabel(sc.Outcome)} on day {sc.EndedDay}. {sc.EndText}");
            var mine = sc.Ledger.Where(l => l.Polity == me && l.Kind != "settlement").ToList();
            var spent = mine.Where(l => Sum(l.Cost) > 0).Aggregate(default(Resources), (a, l) => a + l.Cost);
            var gained = mine.Where(l => Sum(l.Cost) < 0).Aggregate(default(Resources), (a, l) => a - l.Cost);
            int battles = mine.Count(l => l.Kind == "battle"), won = mine.Count(l => l.Kind == "battle" && l.Text.StartsWith("Won", StringComparison.Ordinal));
            lines.Add($"Battles: {battles} ({won} won, {battles - won} lost). Spent on decisions: {(CostText(spent) == "" ? "nothing" : CostText(spent))}. " +
                      $"Recovered or received: {(CostText(gained) == "" ? "nothing" : CostText(gained))}.");
            var lostFlags = g.Slice.Ships.Values.Where(p => p.Destroyed && Faction(p) == me).Select(p => p.Name ?? ShipCatalog.Name(p.DefId)).ToList();
            if (lostFlags.Count > 0) lines.Add("Flagships lost: " + string.Join(", ", lostFlags) + ".");
            var rel = g.Slice.RelationOf(me, them);
            if (rel != null) lines.Add($"Relations with {g.GetPolity(them).Name}: {rel.Status}, standing {rel.Opinion(g.Day):+0;-0;0}.");
            var p0 = g.Slice.Passage(SliceCampaign.PassageSystem);
            lines.Add(p0.AccessHolderId == null ? "The passage is contested." : $"{g.GetPolity(p0.AccessHolderId).Name} holds access through {p0.Name} ({(p0.Basis == Treaty ? "by treaty" : "by battle")}).");
            var upkeep = mine.Where(l => l.Kind == "upkeep").Aggregate(default(Resources), (a, l) => a + l.Cost);
            if (Sum(upkeep) > 0) lines.Add($"Fleet upkeep over the campaign: {CostText(upkeep)} (included in the spending above).");
            lines.Add("Your decisions, in order:");
            foreach (var l in mine.Where(l => l.Kind != "upkeep" && l.Kind != "stores"))
                lines.Add($"  Day {l.Day}: {l.Text}" + (Sum(l.Cost) > 0 ? $" ({CostText(l.Cost)})" : Sum(l.Cost) < 0 ? $" (+{CostText(default(Resources) - l.Cost)})" : ""));
            return lines;
        }

        /// <summary>The faction a capital ship was built for (its class), whatever custody it is in now.</summary>
        static string Faction(ShipProfile p) => p.DefId.StartsWith("pygmaei", StringComparison.Ordinal) ? "pygmaei" : p.DefId.StartsWith("inhumanus", StringComparison.Ordinal) ? "inhumanus" : p.OwnerId;
    }
}
