using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    // ======================================================================================================
    // Vertical-slice campaign state (Prompts 6 to 10): kept in one container on GalaxyState so the original
    // Aurelith campaign is untouched and older saves load with an empty container (save version 2 migration).
    // ======================================================================================================

    public enum ObjectiveStatus { Declared, Secured, Lost, Settled }

    /// <summary>A declared war objective. Only "passage" (secure access through a passage system) exists in the slice.</summary>
    public sealed class WarObjective
    {
        public string Id, Kind = "passage", PolityId, TargetPolityId, SystemId, Note;
        public ObjectiveStatus Status;
        public int DeclaredDay, Attempts;
    }

    /// <summary>Who may use a passage. Separate from system ownership, orbital control and planetary conquest.</summary>
    public sealed class PassageState
    {
        public string SystemId, Name;
        public string AccessHolderId;   // null = contested
        /// <summary>How the holder obtained access: "battle" or "treaty" (PassageWar), or null when contested.</summary>
        public string Basis;
        public int TollPerDay = 3;
        public int SinceDay;
    }

    public sealed class RelationModifier
    {
        public string Key, Cause;
        public int Amount, ExpiresDay;
    }

    public sealed class Relation
    {
        public string A, B;
        public string Status = "tension";   // tension, war, truce
        public List<RelationModifier> Modifiers = new List<RelationModifier>();
        public int Opinion(int day) => Math.Max(-100, Math.Min(100, Modifiers.Where(m => m.ExpiresDay > day).Sum(m => m.Amount)));
        public bool Is(string x, string y) => (A == x && B == y) || (A == y && B == x);
    }

    public sealed class ServiceEntry
    {
        public int Day;
        public string Kind, Text, BattleId;
    }

    /// <summary>The persistent identity of a ship (named capital ships carry history; escorts stay simple).</summary>
    public sealed class ShipProfile
    {
        public int Uid;
        public string DefId, OwnerId, Name;
        public List<string> Modules = new List<string>();
        public List<ServiceEntry> Service = new List<ServiceEntry>();
        /// <summary>Cosmetic wear only (battle scars shown on the hull); never affects rules.</summary>
        public int Scars;
        public bool Destroyed, Captured;
        public string Fate;
    }

    public sealed class HistoryEvent
    {
        public string Id, Kind, SystemId, BattleId, Text;
        public int Day;
        public List<int> Ships = new List<int>();
    }

    public sealed class WreckRecord
    {
        public string Id, SystemId, DefId, Name, OwnerId, BattleId;
        public int Uid, Day;
        public bool Salvaged;
        public string SalvagedBy;
        public string Module;          // a recoverable module, or null
        public Resources Materials;    // what salvage yields
    }

    public sealed class SliceCampaignState
    {
        public string PlayerPolityId;                       // null outside the passage campaign
        public List<WarObjective> Objectives = new List<WarObjective>();
        public List<PassageState> Passages = new List<PassageState>();
        public List<Relation> Relations = new List<Relation>();
        public Dictionary<int, ShipProfile> Ships = new Dictionary<int, ShipProfile>();
        public List<HistoryEvent> History = new List<HistoryEvent>();
        public List<WreckRecord> Wrecks = new List<WreckRecord>();
        public List<Expedition> Expeditions = new List<Expedition>();
        public List<Settlement> Settlements = new List<Settlement>();
        /// <summary>Keys of one-time consequences already applied (reactions, settlements, salvage): reopening or reloading never repeats them.</summary>
        public HashSet<string> Applied = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>A bounded scenario configuration (PassageWar), or null in the freeform passage campaign.</summary>
        public ScenarioState Scenario;

        public bool Active => PlayerPolityId != null;
        public PassageState Passage(string systemId) => Passages.FirstOrDefault(p => p.SystemId == systemId);
        public Relation RelationOf(string a, string b) => Relations.FirstOrDefault(r => r.Is(a, b));
        public ShipProfile Profile(int uid) { ShipProfile p; return Ships.TryGetValue(uid, out p) ? p : null; }
    }

    /// <summary>The passage campaign: creation, objectives, daily effects, encounter previews and battle consequences.</summary>
    public static partial class SliceCampaign
    {
        public const string ScenarioId = "trade_passage";
        public const string PassageSystem = "umbra";
        public const int VictoryPrize = 40;
        public const int MaxAttempts = 3;

        public static string PolityOf(FactionId f) => f == FactionId.Pygmaei ? "pygmaei" : f == FactionId.Inhumanus ? "inhumanus" : null;
        public static FactionId FactionOf(string polity) => polity == "pygmaei" ? FactionId.Pygmaei : FactionId.Inhumanus;
        public static string HomeOf(string polity) => polity == "pygmaei" ? "forgefall" : "dividend";
        public static string FleetIdOf(string polity) => polity == "pygmaei" ? "pygmaei_raiders" : "inhumanus_escort";

        /// <summary>Provisional flagship names (editable in the hangar).</summary>
        static string DefaultName(string defId) => defId == "pygmaei_revenant_command_ship" ? "Ember Tyrant" : defId == "inhumanus_executive_flagship" ? "Fiduciary Dawn" : null;

        public static GalaxyState Create(FactionId player)
        {
            var g = SliceScenario.Create();
            g.ScenarioId = ScenarioId;
            g.Fleets.Clear();
            g.Engagements.Clear();
            g.Log.Clear();
            string me = PolityOf(player), them = PolityOf(player == FactionId.Pygmaei ? FactionId.Inhumanus : FactionId.Pygmaei);
            foreach (var p in g.Polities) { p.PlayerControlled = p.Id == me; p.Playable = p.Id == me || p.Id == them; }
            g.GetPolity("pygmaei").CommandfallFaction = "Pygmaei";
            g.GetPolity("inhumanus").CommandfallFaction = "Inhumanus";
            g.GetPolity("pygmaei").Stock = new Resources(120, 160, 160, 30);
            g.GetPolity("inhumanus").Stock = new Resources(120, 140, 220, 30);
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                var home = g.GetSystem(HomeOf(pol));
                var yard = home.Planets.Where(p => p.OwnerId == pol).OrderBy(p => p.Id).FirstOrDefault() ?? home.Planets.First();
                yard.OwnerId = pol; yard.HasShipyard = true;
            }
            g.GetSystem(PassageSystem).BattlefieldMapId = "trade_passage";

            var s = g.Slice = new SliceCampaignState { PlayerPolityId = me };
            s.Passages.Add(new PassageState { SystemId = PassageSystem, Name = "The Umbra passage" });
            s.Relations.Add(new Relation { A = "pygmaei", B = "inhumanus", Status = "tension" });

            AddFleet(g, "pygmaei", "Forgefall Raiders", SliceRoster.Compositions(FactionId.Pygmaei).First(c => c.Name == "Wolfpack Raid").Army, ai: me != "pygmaei");
            AddFleet(g, "inhumanus", "Convoy Escort", SliceRoster.Compositions(FactionId.Inhumanus).First(c => c.Name == "Bulwark Charter").Army, ai: me != "inhumanus");
            s.Expeditions.Add(Expeditions.Create(g));
            g.Note("order", "The Umbra passage, on the edge of The Eclipse Sector, is the short route between the Pygmaei forges and the Inhumanus exchanges. Declare an objective to contest it.");
            return g;
        }

        static void AddFleet(GalaxyState g, string polity, string name, ArmyRoster army, bool ai)
        {
            var home = HomeOf(polity);
            var f = new Fleet { Id = FleetIdOf(polity), Name = name, OwnerId = polity, SystemId = home, HomeSystemId = home, LastSystemId = home, AIControlled = ai };
            foreach (var e in army.Units)
            {
                var ship = new ShipRecord { Uid = g.NextUid++, DefId = e.UnitId, Hp = ShipCatalog.MaxHp(e.UnitId) };
                f.Ships.Add(ship);
                if (ShipCatalog.IsLeader(e.UnitId))
                    g.Slice.Ships[ship.Uid] = new ShipProfile { Uid = ship.Uid, DefId = e.UnitId, OwnerId = polity, Name = DefaultName(e.UnitId),
                        Service = { new ServiceEntry { Day = g.Day, Kind = "commissioned", Text = $"Commissioned as flagship of the {name} at {g.GetSystem(home).Name}." } } };
            }
            g.Fleets.Add(f);
        }

        /// <summary>The name a ship shows: its given name, or its class.</summary>
        public static string ShipName(GalaxyState g, ShipRecord s)
        {
            var p = g.Slice?.Profile(s.Uid);
            return p?.Name ?? ShipCatalog.Name(s.DefId);
        }

        // ------------------------------------------------------------------ objectives

        public static StratResult DeclareObjective(GalaxyState g, string polity, string systemId)
        {
            var s = g.Slice;
            if (s == null || !s.Active) return StratResult.Reject("There is no passage campaign in progress.");
            var passage = s.Passage(systemId);
            if (passage == null) return StratResult.Reject("Only a passage system can be the objective.");
            if (passage.AccessHolderId == polity) return StratResult.Reject("You already hold access through this passage.");
            var open = s.Objectives.FirstOrDefault(o => o.PolityId == polity && o.SystemId == systemId && o.Status == ObjectiveStatus.Declared);
            if (open != null) return StratResult.Reject("That objective is already declared.");
            int failed = FailedAttempts(g, polity, systemId);
            if (failed >= MaxAttempts) return StratResult.Reject($"This objective has failed {MaxAttempts} times; the campaign treats the passage as lost for this slice.");
            string target = polity == "pygmaei" ? "inhumanus" : "pygmaei";
            var obj = new WarObjective { Id = "obj_" + g.NextId++, PolityId = polity, TargetPolityId = target, SystemId = systemId, Status = ObjectiveStatus.Declared, DeclaredDay = g.Day,
                Attempts = failed };
            s.Objectives.Add(obj);
            var rel = s.RelationOf(polity, target);
            rel.Status = "war";
            // The other power answers: its fleet sails for the passage in two days (disclosed).
            var rival = g.Fleets.FirstOrDefault(f => f.OwnerId == target && f.AIControlled);
            if (rival != null) { rival.AITarget = systemId; rival.AIDepartDay = g.Day + 2; }
            var sys = g.GetSystem(systemId);
            Record(g, new HistoryEvent { Kind = "declaration", SystemId = systemId, Text = $"{g.GetPolity(polity).Name} declared war over access through {passage.Name} ({sys.Name})." });
            PassageWar.ForPolity(g, polity, "declaration", $"Declared war over {passage.Name} (attempt {obj.Attempts + 1} of {MaxAttempts})", default);
            g.Note("danger", $"War declared over {passage.Name}. Scouts expect the {g.GetPolity(target).Name} fleet to sail for {sys.Name} in two days.");
            return StratResult.Accept();
        }

        /// <summary>How many times <paramref name="polity"/>'s objective at a system has failed. Each objective record carries the
        /// running count (the failures before it was declared, plus its own), so the count is the largest, never the sum
        /// (summing counted earlier failures twice: defect D-20).</summary>
        public static int FailedAttempts(GalaxyState g, string polity, string systemId) =>
            g.Slice?.Objectives.Where(o => o.PolityId == polity && o.SystemId == systemId).Select(o => o.Attempts).DefaultIfEmpty(0).Max() ?? 0;

        // ------------------------------------------------------------------ daily effects

        public static void OnDay(GalaxyState g)
        {
            var s = g.Slice;
            if (s == null || !s.Active) return;
            foreach (var p in s.Passages.Where(p => p.AccessHolderId != null))
            {
                var pol = g.GetPolity(p.AccessHolderId);
                if (pol != null) pol.Stock = pol.Stock + new Resources(0, 0, p.TollPerDay, 0);
            }
            foreach (var r in s.Relations) r.Modifiers.RemoveAll(m => m.ExpiresDay <= g.Day);
            CounterOffensives(g);
            Economy.Daily(g);
            PassageWar.OnDay(g);
        }

        // ------------------------------------------------------------------ previews

        /// <summary>What an engagement decides, stated before the battle (the same transaction applies it afterwards).</summary>
        public static List<string> PreviewEncounter(GalaxyState g, Engagement e)
        {
            var lines = new List<string>();
            var s = g.Slice;
            var sys = g.GetSystem(e.SystemId);
            var a = g.GetFleet(e.AttackerFleetId); var d = g.GetFleet(e.DefenderFleetId);
            lines.Add($"{a.Name} ({a.Ships.Count} ships) against {d.Name} ({d.Ships.Count} ships) at {sys.Name}. Destroying a flagship ends the battle at once.");
            var passage = s?.Passage(sys.Id);
            if (passage != null)
                lines.Add($"The winner takes access through {passage.Name} (+{passage.TollPerDay} credits a day) and a prize of {VictoryPrize} credits. Access is not ownership: no planet changes hands.");
            lines.Add("The loser's surviving ships withdraw to their previous system; they are not destroyed. Destroyed ships stay destroyed.");
            lines.Add("A battle saved part-way through reloads exactly; withdrawing now gives up the passage without losses.");
            return lines;
        }

        // ------------------------------------------------------------------ the battle transaction

        /// <summary>Called by <see cref="BattleBridge.Apply"/> inside its once-only transaction (keyed by battle id), after
        /// losses and survivors are applied and before the battle is marked applied.</summary>
        public static void OnBattleApplied(GalaxyState g, BattleSetup setup, BattleOutcome o, Fleet winner, Fleet loser, StarSystem sys)
        {
            var s = g.Slice;
            if (s == null) return;
            var passage = s.Passage(sys.Id);
            if (passage != null && o.Reason != "Settlement")
            {
                passage.AccessHolderId = winner.OwnerId; passage.SinceDay = g.Day; passage.Basis = PassageWar.Battle;
                foreach (var obj in s.Objectives.Where(x => x.SystemId == sys.Id && x.Status == ObjectiveStatus.Declared))
                {
                    if (obj.PolityId == winner.OwnerId) { obj.Status = ObjectiveStatus.Secured; obj.Note = $"Secured on day {g.Day} ({setup.BattleId})."; }
                    else { obj.Status = ObjectiveStatus.Lost; obj.Attempts++; obj.Note = $"Lost on day {g.Day}; {Math.Max(0, MaxAttempts - obj.Attempts)} attempt(s) remain after repairs."; }
                }
                var wp = g.GetPolity(winner.OwnerId);
                if (wp != null) wp.Stock = wp.Stock + new Resources(0, 0, VictoryPrize, 0);
            }
            ServiceRecords(g, setup, o, sys, winner);
            Consequences(g, setup, o, sys, winner, loser);
            if (o.Reason != "Settlement")
            {
                int w = o.WinnerSeat;
                int Lost(int seat) => o.Ships[seat].Count(x => x.Destroyed || x.Captured);
                string how = o.Reason switch { "CommanderEliminated" => "a flagship destroyed", "LeaderCaptured" => "a flagship captured", "RoundLimitObjectives" => "on the round limit", "Resignation" => "the other side yielded", _ => o.Reason };
                PassageWar.ForPolity(g, winner.OwnerId, "battle", $"Won the battle of {sys.Name} ({how}); lost {Lost(w)} ship(s), the enemy {Lost(1 - w)}", default);
                PassageWar.ForPolity(g, loser.OwnerId, "battle", $"Lost the battle of {sys.Name} ({how}); lost {Lost(1 - w)} ship(s), the enemy {Lost(w)}", default);
            }
        }
    }
}
