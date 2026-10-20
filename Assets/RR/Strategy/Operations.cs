using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    // ======================================================================================================
    // Campaign alpha Prompt 16: one war-operation state machine over the two existing combat modes. Orbital engagements
    // and planetary Commandfall matches are ordinary battles (BattleSession, the same rules); this file only decides
    // which battle comes next, what it is for, and what the whole operation settles. Battle victory, operation victory and
    // campaign victory stay separate: a battle changes only its own casualties; the operation's final legal outcome is the
    // only thing that can change who owns a world; the campaign's mandate is judged by PassageWar.
    // ======================================================================================================

    public sealed class GroundUnit
    {
        public int Uid;
        public string DefId;
        public int Hp;
    }

    /// <summary>Ground units at one place: a garrison on a planet, or embarked in a fleet (never both, never invisible).</summary>
    public sealed class GroundForce
    {
        public string Id, OwnerId, PlanetId, FleetId;
        public List<GroundUnit> Units = new List<GroundUnit>();
    }

    public enum OperationPhase { Orbital, DefenderChoice, AttackerChoice, Series, Settled }

    public sealed class OperationBattle
    {
        public string BattleId, Kind, WinnerPolity, Reason, Note, MapId;
        public int Index;
        public bool Conceded;
        /// <summary>Uids that deployed in this battle (both sides).</summary>
        public List<int> Deployed = new List<int>();
    }

    public sealed class Operation
    {
        public string Id, AttackerPolity, DefenderPolity, SystemId, PlanetId, AttackerFleetId, DefenderFleetId;
        public OperationPhase Phase;
        public int StartedDay, EndedDay;
        /// <summary>Orbital supply defence: engagements fought and won by each side (at most two).</summary>
        public int OrbitalFought, OrbitalAttackerWins, OrbitalDefenderWins;
        public bool SuppliesProtected, SuppliesSeized, FollowUp, FollowUpResolved;
        /// <summary>Ground units the attacker landed (uids, from its embarked force); the defender fights with the garrison.</summary>
        public List<int> Landed = new List<int>();
        public bool HasLanded;
        public int AttackerWins, DefenderWins;
        public List<OperationBattle> Battles = new List<OperationBattle>();
        /// <summary>Acting commanders chosen by succession: "orbital:polity" or "ground:polity" to a uid.</summary>
        public Dictionary<string, int> Acting = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<int> Prisoners = new List<int>();
        public string Outcome, WinnerPolity, PendingBattleId;
        public bool Ended => Phase == OperationPhase.Settled;
    }

    public static partial class Operations
    {
        public const int SeriesWins = 2, MaxOrbital = 2, DeployCap = 6, SupplyMaterials = 40, FollowUpMaterials = 30;
        public const int SurrenderPressureSeized = 40, SurrenderPressureBehind = 30, WorldValue = 60;
        public const string Scenario = "operation";
        static readonly string[] SeriesMaps = { "shattered_citadel", "citadel_crossroads", "proving_ground" };

        static SliceCampaignState S(GalaxyState g) => g.Slice;
        public static Operation Active(GalaxyState g) => g.Slice?.Operations.FirstOrDefault(o => !o.Ended);
        public static Operation Get(GalaxyState g, string id) => g.Slice?.Operations.FirstOrDefault(o => o.Id == id);
        static string Other(string p) => PassageWar.Other(p);

        // ------------------------------------------------------------------ ground forces and transport

        /// <summary>Provisional transport rule: a flagship carries two ground units, every other ship one.</summary>
        public static int Capacity(Fleet f) => f == null ? 0 : f.Ships.Sum(s => ShipCatalog.IsLeader(s.DefId) ? 2 : 1);
        public static GroundForce Embarked(GalaxyState g, string fleetId) => S(g)?.Ground.FirstOrDefault(x => x.FleetId == fleetId);
        public static GroundForce Garrison(GalaxyState g, string planetId) => S(g)?.Ground.FirstOrDefault(x => x.PlanetId == planetId);
        public static IEnumerable<GroundUnit> AllUnits(GalaxyState g) => S(g)?.Ground.SelectMany(x => x.Units) ?? Enumerable.Empty<GroundUnit>();

        public static GroundForce NewForce(GalaxyState g, string owner, string planetId, string fleetId, params string[] defIds)
        {
            var f = new GroundForce { Id = "ground_" + g.NextId++, OwnerId = owner, PlanetId = planetId, FleetId = fleetId };
            foreach (var d in defIds) f.Units.Add(new GroundUnit { Uid = g.NextUid++, DefId = d, Hp = ShipCatalog.MaxHp(d) });
            S(g).Ground.Add(f);
            return f;
        }

        static GroundForce ForceFor(GalaxyState g, string owner, string planetId, string fleetId) =>
            S(g).Ground.FirstOrDefault(x => x.OwnerId == owner && x.PlanetId == planetId && x.FleetId == fleetId) ?? NewForce(g, owner, planetId, fleetId);

        /// <summary>Embarks ground units from a garrison on a world in the fleet's system, up to the fleet's transport capacity.</summary>
        public static StratResult Embark(GalaxyState g, string fleetId, string planetId, IEnumerable<int> uids)
        {
            var f = g.GetFleet(fleetId);
            var p = g.GetPlanet(planetId);
            if (f == null || p == null) return StratResult.Reject("No such fleet or world.");
            if (f.Moving || f.EngagementId != null || g.SystemOf(p).Id != f.SystemId) return StratResult.Reject($"{f.Name} must hold at {g.SystemOf(p).Name} to embark troops there.");
            if (p.OwnerId != f.OwnerId) return StratResult.Reject($"{p.Name} is not yours.");
            if (Active(g) != null) return StratResult.Reject("An operation is in progress.");
            var gar = Garrison(g, planetId);
            var take = gar?.Units.Where(u => uids.Contains(u.Uid)).ToList() ?? new List<GroundUnit>();
            if (take.Count == 0) return StratResult.Reject("Choose units from the garrison.");
            var carried = Embarked(g, fleetId);
            int room = Capacity(f) - (carried?.Units.Count ?? 0);
            if (take.Count > room) return StratResult.Reject($"{f.Name} can carry {Capacity(f)} ground units (a flagship two, every other ship one); it has room for {Math.Max(0, room)}.");
            var dest = ForceFor(g, f.OwnerId, null, fleetId);
            foreach (var u in take) { gar.Units.Remove(u); dest.Units.Add(u); }
            S(g).Ground.RemoveAll(x => x.Units.Count == 0 && x.FleetId == null && x != gar);
            PassageWar.ForPolity(g, f.OwnerId, "embark", $"Embarked {take.Count} ground unit(s) in {f.Name} at {p.Name}", default);
            g.Note("order", $"{f.Name} embarked {take.Count} ground unit(s) at {p.Name}.");
            return StratResult.Accept();
        }

        public static StratResult Disembark(GalaxyState g, string fleetId, string planetId)
        {
            var f = g.GetFleet(fleetId); var p = g.GetPlanet(planetId);
            var carried = Embarked(g, fleetId);
            if (f == null || p == null || carried == null || carried.Units.Count == 0) return StratResult.Reject("The fleet carries no ground units.");
            if (f.Moving || g.SystemOf(p).Id != f.SystemId || p.OwnerId != f.OwnerId) return StratResult.Reject($"Troops land only on your own worlds where the fleet holds.");
            if (Active(g) != null) return StratResult.Reject("An operation is in progress.");
            var gar = ForceFor(g, f.OwnerId, planetId, null);
            gar.Units.AddRange(carried.Units); carried.Units.Clear();
            S(g).Ground.Remove(carried);
            return StratResult.Accept();
        }

        /// <summary>After any battle or loss of ships: troops beyond a fleet's remaining capacity are lost with their transports
        /// (highest uid first: the most recently embarked), and every loss is recorded. A fleet that disappears loses all.</summary>
        public static void EnforceCapacity(GalaxyState g)
        {
            if (S(g) == null) return;
            foreach (var force in S(g).Ground.Where(x => x.FleetId != null).ToList())
            {
                var f = g.GetFleet(force.FleetId);
                int cap = Capacity(f);
                if (force.Units.Count <= cap) continue;
                var lost = force.Units.OrderByDescending(u => u.Uid).Take(force.Units.Count - cap).ToList();
                foreach (var u in lost) force.Units.Remove(u);
                string where = f != null ? f.Name : "a fleet that no longer exists";
                SliceCampaign.RecordEvent(g, "troops", f?.SystemId, null, $"{lost.Count} {g.GetPolity(force.OwnerId).Name} ground unit(s) were lost with the transports of {where}.");
                if (force.Units.Count == 0) S(g).Ground.Remove(force);
            }
        }

        // ------------------------------------------------------------------ launching

        public static string LaunchProblem(GalaxyState g, string fleetId, string planetId)
        {
            if (S(g) == null || PassageWar.Of(g) == null) return "Operations belong to the Passage War scenario.";
            if (Active(g) != null) return "Another operation is in progress.";
            var f = g.GetFleet(fleetId); var p = g.GetPlanet(planetId);
            if (f == null || p == null) return "No such fleet or world.";
            var sys = g.SystemOf(p);
            if (f.SystemId != sys.Id || f.Moving) return $"{f.Name} must hold at {sys.Name}.";
            if (p.OwnerId == null || p.OwnerId == f.OwnerId || !(p.OwnerId == "pygmaei" || p.OwnerId == "inhumanus")) return $"{p.Name} is not a rival world.";
            if (PassageWar.AtPeace(g, f.OwnerId, p.OwnerId)) return "A truce is in force; an invasion would break it. Declare war first.";
            if (!StrategyRules.CanFight(f)) return $"{f.Name} has no flagship to lead the operation.";
            var carried = Embarked(g, fleetId);
            if (carried == null || carried.Units.Count == 0) return $"{f.Name} carries no ground units. Embark troops at one of your worlds first (capacity {Capacity(f)}).";
            return null;
        }

        /// <summary>Launches an invasion of <paramref name="planetId"/> by a fleet holding in its system. If an engagement with
        /// the defending fleet is pending there, it becomes the operation's first orbital engagement instead of an ordinary
        /// battle. With no defending fleet, the orbital supply defence is uncontested: the defender's supplies are not
        /// protected, and nothing is seized (no engagement was fought).</summary>
        public static StratResult Launch(GalaxyState g, string fleetId, string planetId, out Operation op)
        {
            op = null;
            var f = g.GetFleet(fleetId);
            var e = f != null && f.EngagementId != null ? g.GetEngagement(f.EngagementId) : null;
            if (e != null && e.State == EngagementState.Pending) f.EngagementId = null;   // checked again below with the engagement set aside
            var why = LaunchProblem(g, fleetId, planetId);
            if (e != null && e.State == EngagementState.Pending) f.EngagementId = e.Id;
            if (why != null) return StratResult.Reject(why);
            var p = g.GetPlanet(planetId); var sys = g.SystemOf(p);
            var defenderFleet = e != null ? new[] { g.GetFleet(e.AttackerFleetId), g.GetFleet(e.DefenderFleetId) }.FirstOrDefault(x => x != null && x.OwnerId == p.OwnerId)
                                          : g.FleetsAt(sys.Id).Where(x => x.OwnerId == p.OwnerId && x.EngagementId == null).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            op = new Operation { Id = "op_" + g.NextId++, AttackerPolity = f.OwnerId, DefenderPolity = p.OwnerId, SystemId = sys.Id, PlanetId = p.Id, AttackerFleetId = f.Id,
                DefenderFleetId = defenderFleet?.Id, StartedDay = g.Day };
            if (e != null)
            {
                e.State = EngagementState.Resolved;
                e.ResultSummary = $"The encounter at {sys.Name} became {g.GetPolity(f.OwnerId).Name} operation {op.Id} against {p.Name}.";
                foreach (var x in new[] { g.GetFleet(e.AttackerFleetId), g.GetFleet(e.DefenderFleetId) }) if (x != null) x.EngagementId = null;
            }
            if (defenderFleet == null || !StrategyRules.CanFight(defenderFleet))
            {
                op.DefenderFleetId = null;
                op.Phase = OperationPhase.AttackerChoice;
                op.SuppliesProtected = false;
                sys.OrbitalControlId = f.OwnerId; sys.ConquestBy = f.OwnerId;
                if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
                op.Battles.Add(new OperationBattle { Index = 0, Kind = "orbital", WinnerPolity = f.OwnerId, Conceded = true,
                    Note = $"Uncontested: {g.GetPolity(p.OwnerId).Name} had no fleet able to fight at {sys.Name}; its supplies are not protected. Nothing was seized." });
            }
            else op.Phase = OperationPhase.Orbital;
            S(g).Operations.Add(op);
            if (g.Slice.RelationOf(f.OwnerId, p.OwnerId) is Relation rel && rel.Status != "war") rel.Status = "war";
            SliceCampaign.RecordEvent(g, "operation", sys.Id, null, $"{g.GetPolity(f.OwnerId).Name} launched an invasion of {p.Name} ({sys.Name}) with {Embarked(g, f.Id).Units.Count} ground unit(s).");
            PassageWar.ForPolity(g, f.OwnerId, "operation", $"Launched an invasion of {p.Name}", default);
            g.Note("danger", $"Operation against {p.Name} begun. Strategic time is stopped for both powers until it is settled.");
            return StratResult.Accept();
        }

        // ------------------------------------------------------------------ the stakes, stated before commitment

        public static List<string> Stakes(GalaxyState g, Operation op)
        {
            var a = g.GetPolity(op.AttackerPolity).Name; var d = g.GetPolity(op.DefenderPolity).Name;
            var p = g.GetPlanet(op.PlanetId);
            return new List<string>
            {
                $"Objective: {a} invades {p.Name}. The world changes hands only through the operation's final legal outcome: the attacker winning the planetary series, or the defender yielding it under terms. No single battle takes it.",
                $"Orbital supply defence: {d} needs one victory in up to {MaxOrbital} orbital engagements to protect the world's supplies. If its first engagement is won, it may stop there (supplies protected) or elect a second for a chance to seize {FollowUpMaterials} materials of the attacker's supplies and take prisoners by boarding; losing that optional second never revokes the protection already earned. If the attacker wins both, it seizes {SupplyMaterials} materials of the world's supplies and the garrison's reserves are cut off. There is no random capture roll.",
                $"Planetary series: best of three Commandfall matches; the first side to {SeriesWins} wins takes the series, and a 2-0 ends it without a third match. Destroying the opposing commander ends that match at once, not the operation.",
                $"Each match deploys at most {DeployCap} units (and the battle point budget). Destroyed units stay destroyed and damage carries over; positions, statuses, heat, cooldowns and objectives reset for every match. Units not deployed are reserves: the attacker's landed reserves can always replace losses; the defender's only while its supplies are protected.",
                "Ground units travel aboard ships (a flagship carries two, every other ship one). If ships are lost, the units beyond the remaining capacity are lost with them.",
                "Command succession (provisional): if a side's flagship or commander is lost, it names an acting commander from its surviving committed units before the next engagement. Nobody is resurrected. A side that cannot deploy a commander concedes that engagement or match.",
                "Matches never end drawn: at the round limit the rules decide on objectives held, then surviving strength, then the commander's hull, and a full tie goes to the defender.",
                "Either side may end the operation instead: the attacker by withdrawing (its fleet and landed troops leave; nothing changes hands), the defender by yielding the world under terms. Strategic time stays stopped until the operation is settled.",
            };
        }

        /// <summary>What happens next, in one sentence.</summary>
        public static string Next(GalaxyState g, Operation op)
        {
            var a = g.GetPolity(op.AttackerPolity).Name; var d = g.GetPolity(op.DefenderPolity).Name;
            switch (op.Phase)
            {
                case OperationPhase.Orbital: return $"Orbital engagement {op.OrbitalFought + 1}" + (op.FollowUp ? $" (the defender's elected follow-up)" : op.OrbitalFought == 1 ? " (the defender's second chance to protect its supplies)" : "") + ".";
                case OperationPhase.DefenderChoice: return $"{d} won the first orbital engagement: its supplies are protected. It may stop, or elect a second engagement to try to seize the attacker's supplies.";
                case OperationPhase.AttackerChoice: return $"{a} decides: land the embarked troops for the planetary series, offer terms, or withdraw.";
                case OperationPhase.Series: return $"Planetary match {op.AttackerWins + op.DefenderWins + 1} on {MapFor(op, op.AttackerWins + op.DefenderWins)}; the series stands {op.AttackerWins}-{op.DefenderWins} ({a} first).";
                default: return op.Outcome;
            }
        }

        public static string MapFor(Operation op, int matchIndex)
        {
            int start = SliceScenario.Hash(op.Id) % SeriesMaps.Length;
            return SeriesMaps[(start + matchIndex) % SeriesMaps.Length];
        }

        /// <summary>Every remaining obligation and risk, for both sides (the operation window shows it to the player).</summary>
        public static List<string> Obligations(GalaxyState g, Operation op)
        {
            var lines = new List<string> { Next(g, op) };
            foreach (var pol in new[] { op.AttackerPolity, op.DefenderPolity })
            {
                var name = g.GetPolity(pol).Name;
                if (op.Phase == OperationPhase.Orbital || op.Phase == OperationPhase.DefenderChoice)
                {
                    var f = g.GetFleet(pol == op.AttackerPolity ? op.AttackerFleetId : op.DefenderFleetId);
                    lines.Add($"{name}: fleet {(f == null ? "none" : $"{f.Name}, {f.Ships.Count} ships")}; commander {CommanderLine(g, op, pol, BattleDomain.Orbital)}.");
                }
                else
                {
                    var units = GroundFor(g, op, pol);
                    lines.Add($"{name}: {units.Count} ground unit(s) available ({units.Sum(u => u.Hp)} hull), {Reserves(g, op, pol).Count} beyond the {DeployCap}-unit deployment; commander {CommanderLine(g, op, pol, BattleDomain.Planetary)}.");
                }
            }
            if (op.SuppliesProtected) lines.Add("The defender's supplies are protected: its reserves can replace losses between matches.");
            if (op.SuppliesSeized) lines.Add("The defender's supplies were seized: its reserves are cut off.");
            return lines;
        }

        static string CommanderLine(GalaxyState g, Operation op, string pol, BattleDomain d)
        {
            var c = Commander(g, op, pol, d);
            if (c.uid == 0) return Successors(g, op, pol, d).Count == 0 ? "none and no eligible successor: it would concede" : "lost; an acting commander must be named";
            return ShipCatalog.Name(c.defId) + (c.acting ? " (acting)" : "");
        }

        // ------------------------------------------------------------------ commanders and succession

        /// <summary>Units a side could field in the operation's current domain. Without protected supplies the defender's
        /// reserves are cut off: after the first planetary match only the units that deployed in it may fight again (unless its
        /// doctrine bonds the garrison). Succession, the lineup, the obligations and terms all read this one pool (Prompt 20:
        /// a successor named from a cut-off reserve could not deploy, and the operation stalled).</summary>
        public static List<GroundUnit> GroundFor(GalaxyState g, Operation op, string pol)
        {
            if (pol == op.AttackerPolity) return (Embarked(g, op.AttackerFleetId)?.Units ?? new List<GroundUnit>()).Where(u => op.Landed.Contains(u.Uid) || !op.HasLanded).ToList();
            var pool = Garrison(g, op.PlanetId)?.Units.ToList() ?? new List<GroundUnit>();
            if (!op.SuppliesProtected && !Doctrines.Has(g, pol, "inh_bonded_garrisons"))
            {
                var first = op.Battles.FirstOrDefault(b => b.Kind == "planetary" && !b.Conceded);
                if (first != null) pool = pool.Where(u => first.Deployed.Contains(u.Uid)).ToList();
            }
            return pool;
        }

        static List<(int uid, string defId, int hp)> Members(GalaxyState g, Operation op, string pol, BattleDomain d)
        {
            if (d == BattleDomain.Orbital)
            {
                var f = g.GetFleet(pol == op.AttackerPolity ? op.AttackerFleetId : op.DefenderFleetId);
                return f?.Ships.Select(s => (s.Uid, s.DefId, s.Hp)).ToList() ?? new List<(int, string, int)>();
            }
            return GroundFor(g, op, pol).Select(u => (u.Uid, u.DefId, u.Hp)).ToList();
        }

        /// <summary>The side's commander for the next engagement: its leader-role unit, or the acting commander named by succession.</summary>
        public static (int uid, string defId, bool acting) Commander(GalaxyState g, Operation op, string pol, BattleDomain d)
        {
            var m = Members(g, op, pol, d);
            var leader = m.Where(x => ShipCatalog.IsLeader(x.defId)).OrderBy(x => x.uid).FirstOrDefault();
            if (leader.defId != null) return (leader.uid, leader.defId, false);
            if (op.Acting.TryGetValue(Key(d, pol), out var uid) && m.Any(x => x.uid == uid)) return (uid, m.First(x => x.uid == uid).defId, true);
            return (0, null, false);
        }

        static string Key(BattleDomain d, string pol) => (d == BattleDomain.Orbital ? "orbital:" : "ground:") + pol;

        /// <summary>Eligible successors: surviving committed units (most valuable first, then lowest uid).</summary>
        public static List<int> Successors(GalaxyState g, Operation op, string pol, BattleDomain d) =>
            Members(g, op, pol, d).Where(x => !ShipCatalog.IsLeader(x.defId) && ShipCatalog.Def(x.defId) != null)
                .OrderByDescending(x => ShipCatalog.Cost(x.defId)).ThenBy(x => x.uid).Select(x => x.uid).ToList();

        public static StratResult NameSuccessor(GalaxyState g, Operation op, string pol, BattleDomain d, int uid)
        {
            if (Commander(g, op, pol, d).uid != 0 && !op.Acting.ContainsKey(Key(d, pol))) return StratResult.Reject("Its commander still serves.");
            if (!Successors(g, op, pol, d).Contains(uid)) return StratResult.Reject("That unit is not an eligible successor.");
            op.Acting[Key(d, pol)] = uid;
            var name = ShipCatalog.Name(Members(g, op, pol, d).First(x => x.uid == uid).defId);
            SliceCampaign.RecordEvent(g, "succession", op.SystemId, null, $"{g.GetPolity(pol).Name} named its {name} acting {(d == BattleDomain.Orbital ? "flagship" : "commander")} for operation {op.Id}.");
            return StratResult.Accept();
        }

        /// <summary>Names a successor automatically when one is needed (the AI's rule, also the default the player can change).</summary>
        public static void EnsureCommanders(GalaxyState g, Operation op, BattleDomain d)
        {
            foreach (var pol in new[] { op.AttackerPolity, op.DefenderPolity })
            {
                if (Commander(g, op, pol, d).uid != 0) continue;
                var next = Successors(g, op, pol, d).FirstOrDefault();
                if (next != 0) NameSuccessor(g, op, pol, d, next);
            }
        }

        public static List<GroundUnit> Reserves(GalaxyState g, Operation op, string pol) => GroundFor(g, op, pol).Skip(DeployCap).ToList();
    }
}
