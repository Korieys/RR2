using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>One refit choice for a capital ship, with everything the player needs to decide.</summary>
    public sealed class RefitOption
    {
        public ModuleDef Module;
        public bool Fitted;
        public string Replaces;          // module currently in that slot, or null
        public Resources Cost;
        public string Blocked;           // why it cannot be fitted now, or null
        public string Compare;           // stat changes against the current fit
    }

    /// <summary>Prompt 7: persistent capital ships. Identity and history live in <see cref="ShipProfile"/>, keyed by the
    /// ship's stable uid; the battle reads the same ship (its modules become army-entry tokens the rules apply).</summary>
    public static partial class SliceCampaign
    {
        public const int MaxNameLength = 32;
        public static readonly Resources RepairPerHull = new Resources(0, 3, 2, 0);

        public static bool IsCapital(string defId) => ShipCatalog.IsLeader(defId);

        /// <summary>Refit prices (experimental values).</summary>
        public static Resources ModuleCost(string id) => id switch
        {
            "afterburners" => new Resources(10, 20, 30, 0),
            "ablative_plating" => new Resources(0, 40, 30, 0),
            "heavy_battery" => new Resources(10, 30, 40, 0),
            _ => new Resources(0, 0, 0, 0),
        };

        /// <summary>A fleet is at an eligible refit location when it holds at a system with its own shipyard.</summary>
        public static string RefitLocationProblem(GalaxyState g, Fleet f)
        {
            if (f == null) return "The ship is not in a fleet.";
            if (f.EngagementId != null) return "The fleet is engaged.";
            if (f.Moving) return "The fleet is under way.";
            var sys = g.GetSystem(f.SystemId);
            if (!sys.Planets.Any(p => p.HasShipyard && p.OwnerId == f.OwnerId)) return $"{sys.Name} has no {g.GetPolity(f.OwnerId)?.Name} shipyard. Refits and repairs need one.";
            return null;
        }

        public static Fleet FleetOf(GalaxyState g, int uid) => g.Fleets.FirstOrDefault(f => f.Ships.Any(s => s.Uid == uid));
        public static ShipRecord Ship(GalaxyState g, int uid) => FleetOf(g, uid)?.Ships.First(s => s.Uid == uid);

        public static int MaxHull(GalaxyState g, ShipRecord s) => ShipCatalog.MaxHp(s.DefId) + (g.Slice?.Profile(s.Uid)?.Modules.Sum(m => SliceRules.Module(m)?.Hull ?? 0) ?? 0);

        public static List<RefitOption> RefitOptions(GalaxyState g, int uid)
        {
            var list = new List<RefitOption>();
            var ship = Ship(g, uid);
            var p = g.Slice?.Profile(uid);
            if (ship == null || p == null || !IsCapital(ship.DefId)) return list;
            var where = RefitLocationProblem(g, FleetOf(g, uid));
            var pol = g.GetPolity(p.OwnerId);
            foreach (var m in SliceRules.Modules)
            {
                var current = p.Modules.FirstOrDefault(x => SliceRules.Module(x)?.Slot == m.Slot);
                bool spare = SpareKey(g, p.OwnerId, m.Id) != null;
                var o = new RefitOption { Module = m, Fitted = p.Modules.Contains(m.Id), Replaces = current, Cost = spare ? default : ModuleCost(m.Id),
                    Compare = m.Summary + (spare ? " A salvaged spare covers the cost." : "") };
                o.Blocked = o.Fitted ? "Already fitted." : where ?? (pol != null && !pol.Stock.Covers(o.Cost) ? $"Not enough resources: needs {o.Cost}, you have {pol.Stock}." : null);
                list.Add(o);
            }
            return list;
        }

        public static StratResult ApplyRefit(GalaxyState g, int uid, string moduleId)
        {
            var o = RefitOptions(g, uid).FirstOrDefault(x => x.Module.Id == moduleId);
            if (o == null) return StratResult.Reject("That module does not fit this ship.");
            if (o.Blocked != null) return StratResult.Reject(o.Blocked);
            var p = g.Slice.Profile(uid);
            var pol = g.GetPolity(p.OwnerId);
            var spare = SpareKey(g, p.OwnerId, moduleId);
            if (spare != null) g.Slice.Applied.Add("used" + spare);   // a salvaged spare is fitted once, at no cost
            else pol.Stock = pol.Stock - o.Cost;
            if (o.Replaces != null) p.Modules.Remove(o.Replaces);
            p.Modules.Add(o.Module.Id);
            p.Modules.Sort(StringComparer.Ordinal);
            var ship = Ship(g, uid);
            ship.Hp = Math.Min(ship.Hp, MaxHull(g, ship));
            var sys = g.GetSystem(FleetOf(g, uid).SystemId);
            p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "refit", Text = $"Refitted at {sys.Name}: {o.Module.Name}{(o.Replaces != null ? " replaced " + SliceRules.Module(o.Replaces).Name : "")}." });
            g.Note("build", $"{p.Name ?? ShipCatalog.Name(p.DefId)} refitted with {o.Module.Name} at {sys.Name}.");
            return StratResult.Accept();
        }

        public static Resources RepairCost(GalaxyState g, int uid)
        {
            var s = Ship(g, uid);
            if (s == null) return default;
            int missing = Math.Max(0, MaxHull(g, s) - s.Hp);
            return new Resources(RepairPerHull.Energy * missing, RepairPerHull.Materials * missing, RepairPerHull.Credits * missing, 0);
        }

        public static StratResult Repair(GalaxyState g, int uid)
        {
            var s = Ship(g, uid);
            if (s == null) return StratResult.Reject("That ship is not in service.");
            var f = FleetOf(g, uid);
            var where = RefitLocationProblem(g, f);
            if (where != null) return StratResult.Reject(where);
            int max = MaxHull(g, s);
            if (s.Hp >= max) return StratResult.Reject("The hull is already at full strength.");
            var cost = RepairCost(g, uid);
            var pol = g.GetPolity(f.OwnerId);
            if (!pol.Stock.Covers(cost)) return StratResult.Reject($"Not enough resources: repairs need {cost}, you have {pol.Stock}.");
            pol.Stock = pol.Stock - cost;
            int before = s.Hp;
            s.Hp = max;
            var p = g.Slice?.Profile(uid);
            p?.Service.Add(new ServiceEntry { Day = g.Day, Kind = "repair", Text = $"Repaired at {g.GetSystem(f.SystemId).Name}: hull {before} → {max}. Its scars remain (cosmetic)." });
            g.Note("build", $"{ShipName(g, s)} repaired ({cost}).");
            // Rematch rule: a repaired fleet may try the objective again (bounded by MaxAttempts).
            foreach (var obj in g.Slice?.Objectives.Where(o => o.PolityId == f.OwnerId && o.Status == ObjectiveStatus.Lost && o.Attempts < MaxAttempts) ?? Enumerable.Empty<WarObjective>())
                obj.Note = $"Lost; {MaxAttempts - obj.Attempts} attempt(s) remain. Declare it again when ready.";
            return StratResult.Accept();
        }

        public static StratResult Rename(GalaxyState g, int uid, string name)
        {
            var p = g.Slice?.Profile(uid);
            if (p == null || p.Destroyed) return StratResult.Reject("Only a capital ship in service can be renamed.");
            name = (name ?? "").Trim();
            if (name.Length == 0) return StratResult.Reject("A name cannot be empty.");
            if (name.Length > MaxNameLength) return StratResult.Reject($"Names are at most {MaxNameLength} characters.");
            if (name.Any(c => char.IsControl(c))) return StratResult.Reject("Names cannot contain control characters.");
            if (name == p.Name) return StratResult.Reject("That is already its name.");
            p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "renamed", Text = p.Name != null ? $"Renamed from {p.Name} to {name}." : $"Named {name}." });
            p.Name = name;
            return StratResult.Accept();
        }

        /// <summary>Service records from the resolved battle (participation, survival, damage, the decisive action, loss).</summary>
        static void ServiceRecords(GalaxyState g, BattleSetup setup, BattleOutcome o, StarSystem sys, Fleet winner)
        {
            var s = g.Slice;
            for (int seat = 0; seat < 2; seat++)
                foreach (var so in o.Ships[seat])
                {
                    var p = s.Profile(so.Uid);
                    if (p == null) continue;
                    bool won = o.WinnerSeat == seat;
                    var start = setup.StartHp[seat][setup.ShipUids[seat].IndexOf(so.Uid)];
                    if (o.Reason == "Settlement")
                    {
                        if (!p.Captured) p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "settlement", BattleId = setup.BattleId,
                            Text = $"Present at {sys.Name} on day {g.Day} when terms were agreed ({(won ? "they favoured its side" : "its side yielded")})." });
                        continue;
                    }
                    if (so.Destroyed)
                    {
                        p.Destroyed = true;
                        p.Fate = $"Destroyed at {sys.Name} on day {g.Day} ({setup.BattleId}).";
                        p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "destroyed", BattleId = setup.BattleId, Text = p.Fate + (o.DecisiveText != null && o.DecisiveUid != so.Uid ? " " + o.DecisiveText : "") });
                        continue;
                    }
                    if (so.Captured)
                    {
                        p.Captured = true;
                        p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "captured", BattleId = setup.BattleId, Text = $"Captured at {sys.Name} on day {g.Day}." });
                        continue;
                    }
                    p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "battle", BattleId = setup.BattleId,
                        Text = $"Fought at {sys.Name} on day {g.Day}: {(won ? "victory" : "defeat")}; hull {start} → {so.Hp} of {ShipCatalog.MaxHp(p.DefId) + p.Modules.Sum(m => SliceRules.Module(m)?.Hull ?? 0)}." });
                    if (o.DecisiveUid == so.Uid && o.DecisiveText != null)
                        p.Service.Add(new ServiceEntry { Day = g.Day, Kind = "decisive", BattleId = setup.BattleId, Text = "Gave the decisive order: " + o.DecisiveText });
                    if (start - so.Hp >= Math.Max(1, ShipCatalog.MaxHp(p.DefId) / 4)) p.Scars++;
                }
        }
    }
}
