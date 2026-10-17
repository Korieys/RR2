using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Targeting, validation and execution of Full Roster active abilities: one handler per <see cref="EffectCode"/>, driven
    /// by the unit's hand-authored <see cref="ActiveSpec"/>. A command carries the target picks in order
    /// (<see cref="Command.Picks"/>); <see cref="Steps"/> says what each pick is, <see cref="CheckPick"/> validates one pick
    /// given the earlier ones, and the UI highlights <see cref="Candidates"/>.
    /// </summary>
    public static class RosterAbilities
    {
        // ================================================================== targeting steps

        public static List<PickStep> Steps(UnitState u, int option = 0)
        {
            var a = u.R.Active;
            var steps = new List<PickStep>();
            string sp = a.Special ?? "";
            if (a.Target == TargetKind.Self || sp == "AllEnemiesInRange" || sp == "RevealArea" || sp == "RevealAreaMarkNearest") return steps;
            switch (sp)
            {
                case "PullToHex": steps.Add(new PickStep(PickKind.Unit, "Pick a visible enemy within 3")); steps.Add(new PickStep(PickKind.Hex, "Pick the empty hex next to it to pull it into")); return steps;
                case "MassExtraction": steps.Add(new PickStep(PickKind.Unit, "Pick an allied infantry or a captured unit within 2")); steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex next to the Hauler")); return steps;
                case "Evacuate": steps.Add(new PickStep(PickKind.Unit, "Pick an adjacent allied infantry unit")); steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex within 2 of the vehicle")); return steps;
                case "GravityFold": steps.Add(new PickStep(PickKind.Unit, "Pick this cruiser or an allied corvette within 2")); steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex within 2 of that ship")); return steps;
                case "JumpAdjacent": steps.Add(new PickStep(PickKind.Unit, "Pick an allied ship within 2")); steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex next to it to jump to")); return steps;
                case "AlsoShieldAlly": steps.Add(new PickStep(PickKind.Unit, "Pick an enemy within 5 to DISABLE")); steps.Add(new PickStep(PickKind.Unit, "Optionally pick an ally within 3 to shield", true)); return steps;
                case "AdjacentPair": steps.Add(new PickStep(PickKind.Unit, "Pick an enemy within 4")); steps.Add(new PickStep(PickKind.Unit, "Pick a second enemy standing next to it")); return steps;
                case "AllHands":
                    for (int i = 0; i < a.MaxTargets; i++)
                    {
                        steps.Add(new PickStep(PickKind.Unit, $"Pick ally {i + 1} (up to {a.MaxTargets}) within 4", i > 0));
                        steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex next to it"));
                    }
                    return steps;
                case "TwoAdjoining": steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex within 2")); steps.Add(new PickStep(PickKind.Hex, "Pick an empty hex next to the first")); return steps;
                case "Line2": case "Line2Ignite": case "RevealCorridor": steps.Add(new PickStep(PickKind.Hex, "Pick an adjacent hex to set the direction")); return steps;
                case "DriveBy":
                    steps.Add(new PickStep(PickKind.Hex, "Pick where to move (up to 2), or this ship's own hex to stay"));
                    steps.Add(new PickStep(PickKind.Unit, "Pick an enemy within 3"));
                    steps.Add(new PickStep(PickKind.Unit, "Optionally pick a second, different enemy within 3", true));
                    return steps;
                case "PillageRush":
                    steps.Add(new PickStep(PickKind.Hex, "Pick where to rush (straight line, up to 3)"));
                    steps.Add(new PickStep(PickKind.Unit, "Optionally pick a target for its basic attack (+1 if EXPOSED)", true));
                    return steps;
            }
            if (a.Dash != DashKind.None)
            {
                steps.Add(new PickStep(PickKind.Hex, DashPrompt(a)));
                if (a.StrikeDamage > 0) steps.Add(new PickStep(PickKind.Unit, $"Optionally pick an adjacent enemy{(sp == "StrikeBarrier" ? " or barrier" : "")} to strike for {a.StrikeDamage}", true));
                return steps;
            }
            if (a.Target == TargetKind.Hex)
            {
                int n = Math.Max(1, a.MaxTargets);
                for (int i = 0; i < n; i++) steps.Add(new PickStep(PickKind.Hex, HexPrompt(a)));
                return steps;
            }
            string who = a.Target == TargetKind.Enemy ? "enemy" : "ally";
            for (int i = 0; i < Math.Max(1, a.MaxTargets); i++)
            {
                string filter = a.FilterText != null ? $" ({a.FilterText})" : "";
                string prompt = a.MaxTargets <= 1 ? $"Pick an {who}{filter} within {a.Range}"
                                                  : i == 0 ? $"Pick {who} 1 of up to {a.MaxTargets}{filter} within {a.Range}" : $"Pick {who} {i + 1} of up to {a.MaxTargets}";
                steps.Add(new PickStep(PickKind.Unit, prompt, i >= Math.Max(1, a.MinTargets)));
            }
            return steps;
        }

        static string DashPrompt(ActiveSpec a) => a.Dash switch
        {
            DashKind.Straight => $"Pick where to charge (straight line, up to {a.DashRange})",
            DashKind.Path => $"Pick where to move (open path, up to {a.DashRange})",
            DashKind.Teleport => $"Pick where to teleport (within {a.DashRange})",
            _ => $"Pick where to jump (within {a.DashRange})",
        };

        static string HexPrompt(ActiveSpec a)
        {
            switch (a.Code)
            {
                case EffectCode.BARRIER: return a.Special == "RebuildFacet" ? "Pick an adjacent empty hex, or a friendly barrier to restore" : "Pick an adjacent empty hex";
                case EffectCode.MINE: return $"Pick an empty hex within {a.Range} for the mine";
                case EffectCode.SPAWN: return "Pick an adjacent empty hex for the token";
                default: return $"Pick the centre hex (within {a.Range})";
            }
        }

        // ================================================================== candidate highlighting

        /// <summary>Hexes the UI should evaluate for the next pick (valid ones are highlighted, invalid ones explain why).</summary>
        public static List<AbilityTarget> Candidates(MatchState s, UnitState u, IList<Hex> picks, int option = 0)
        {
            var result = new List<AbilityTarget>();
            var steps = Steps(u, option);
            if (picks.Count >= steps.Count) return result;
            var a = u.R.Active;
            int reach = Math.Max(a.Range, a.DashRange) + 4;
            Hex anchor = u.Position;
            foreach (var h in s.Map.AllHexes)
            {
                if (Hex.Distance(h, anchor) > reach) continue;
                var why = CheckPick(s, u, picks, h, option);
                result.Add(new AbilityTarget { Hex = h, Valid = why == null, Reason = why, Option = option });
            }
            return result;
        }

        /// <summary>Is the pick list complete enough to submit (all required steps filled)?</summary>
        public static bool Ready(UnitState u, IList<Hex> picks, int option = 0)
        {
            var steps = Steps(u, option);
            if (picks.Count > steps.Count) return false;
            // AllHands: one or more (ally, destination) pairs.
            if (u.R.Active.Special == "AllHands") return picks.Count >= 2 && picks.Count % 2 == 0;
            for (int i = picks.Count; i < steps.Count; i++) if (!steps[i].Optional) return false;
            return true;
        }

        // ================================================================== validation

        public static Validation Validate(MatchState s, UnitState u, IList<Hex> picks, int option)
        {
            var why = RosterRules.CanAct(s, u, attack: false);
            if (why != null) return Validation.Fail(why);
            var a = u.R.Active;
            if (a.RequiresNoMove && u.MovedThisActivation) return Validation.Fail($"{u.R.ActiveName} cannot be used after moving this activation.");
            if (a.Options.Length > 1 && (option < 0 || option >= a.Options.Length)) return Validation.Fail("Choose an option.");
            if (Moves(a) && u.HasR(RStatusType.Rooted)) return Validation.Fail($"{u.Def.Name} is ROOTED: it cannot move, so {u.R.ActiveName} is unavailable.");
            var steps = Steps(u, option);
            if (picks == null) picks = new List<Hex>();
            if (picks.Count > steps.Count) return Validation.Fail("Too many targets.");
            var sofar = new List<Hex>();
            foreach (var p in picks)
            {
                var r = CheckPick(s, u, sofar, p, option);
                if (r != null) return Validation.Fail(r);
                sofar.Add(p);
            }
            if (!Ready(u, picks, option)) return Validation.Fail(steps[picks.Count].Prompt + ".");
            var global = GlobalCheck(s, u, picks);
            return global == null ? Validation.Success : Validation.Fail(global);
        }

        static bool Moves(ActiveSpec a) => a.Dash != DashKind.None || a.Special == "JumpAdjacent";

        /// <summary>Conditions that do not depend on picks (token caps, nothing to act on).</summary>
        static string GlobalCheck(MatchState s, UnitState u, IList<Hex> picks)
        {
            var a = u.R.Active;
            if (a.Code == EffectCode.SPAWN)
            {
                int living = s.Units.Count(x => x.Alive && x.SpawnedBy == u.Id);
                if (living >= Math.Max(1, a.TokenCap)) return $"This carrier already has {living} living token{(living == 1 ? "" : "s")} (maximum {a.TokenCap}).";
            }
            if (a.Special == "AllEnemiesInRange" && !s.Units.Any(e => e.OnBoard && e.Owner != u.Owner && Hex.Distance(e.Position, u.Position) <= a.Range))
                return $"No enemy within {a.Range}.";
            return null;
        }

        static int EffectiveRange(MatchState s, UnitState u) => u.R.Active.Range + RosterPassives.ActiveRangeBonus(s, u);

        /// <summary>Distance for ally-targeted abilities: Cräigon Collective Link lets adjacent Cräigon infantry relay the range.</summary>
        static int AllyDistance(MatchState s, UnitState u, Hex h)
        {
            int d = Hex.Distance(u.Position, h);
            if (u.Def.Faction == FactionId.Craigon && u.R.IsInfantry)
                foreach (var n in u.Position.Neighbors())
                {
                    var x = s.UnitAt(n);
                    if (x != null && x.Owner == u.Owner && x.Def.Faction == FactionId.Craigon && x.R.IsInfantry)
                        d = Math.Min(d, Hex.Distance(n, h));
                }
            return d;
        }

        static bool Los(MatchState s, UnitState u, Hex from, Hex to, bool unitsBlock = true) =>
            Hex.Distance(from, to) <= 1 ||
            (unitsBlock ? RosterCombat.LineOfSight(s, u, from, to) : RosterTerrain.LineOfSight(s, from, to, true)) == LosState.Clear;

        /// <summary>Why <paramref name="h"/> is not a valid next pick (null = valid).</summary>
        public static string CheckPick(MatchState s, UnitState u, IList<Hex> picks, Hex h, int option)
        {
            var a = u.R.Active;
            var steps = Steps(u, option);
            int i = picks.Count;
            if (i >= steps.Count) return "No more targets needed.";
            if (!s.Map.Contains(h)) return "Outside the battlefield.";
            if (picks.Contains(h) && a.Special != "DriveBy") return "Already picked.";
            string sp = a.Special ?? "";
            var unit = s.UnitAt(h);
            int range = EffectiveRange(s, u);

            switch (sp)
            {
                case "PullToHex":
                    if (i == 0) return EnemyPick(s, u, h, range, a);
                    {
                        var e = s.UnitAt(picks[0]);
                        if (Hex.Distance(h, e.Position) != 1) return "The hex must be next to the target.";
                        if (!RosterMovement.Open(s, h)) return "That hex is not empty.";
                        if (Hex.Distance(h, u.Position) >= Hex.Distance(e.Position, u.Position)) return "The pull must bring it closer to the Weaver.";
                        return null;
                    }
                case "MassExtraction":
                    if (i == 0)
                    {
                        if (unit == null) return "Pick a unit.";
                        if (Hex.Distance(u.Position, h) > range) return $"Out of range ({range}).";
                        bool ok = unit.Owner == u.Owner ? unit.R.IsInfantry && unit.Id != u.Id : unit.HasR(RStatusType.Captured);
                        return ok ? null : "Pick an allied infantry unit or a captured unit.";
                    }
                    if (Hex.Distance(h, u.Position) != 1) return "Must be next to the Hauler.";
                    return RosterMovement.Open(s, h) ? null : "That hex is not empty.";
                case "Evacuate":
                    if (i == 0) return AllyPick(s, u, h, 1, a);
                    if (Hex.Distance(h, u.Position) > 2) return "Must be within 2 of the vehicle.";
                    return RosterMovement.Open(s, h) ? null : "That hex is not empty.";
                case "GravityFold":
                    if (i == 0) return AllyPick(s, u, h, range, a);
                    {
                        var ship = s.UnitAt(picks[0]);
                        if (Hex.Distance(h, ship.Position) > 2) return "Must be within 2 of that ship.";
                        if (!RosterMovement.Open(s, h)) return "That hex is not empty.";
                        if (!Los(s, u, u.Position, h, unitsBlock: false)) return "The cruiser needs line of sight to the destination.";
                        return null;
                    }
                case "JumpAdjacent":
                    if (i == 0) return AllyPick(s, u, h, range, a);
                    if (Hex.Distance(h, picks[0]) != 1) return "Must be next to that ship.";
                    return RosterMovement.Open(s, h) ? null : "That hex is not empty.";
                case "AlsoShieldAlly":
                    if (i == 0) return EnemyPick(s, u, h, range, a);
                    if (unit == null || unit.Owner != u.Owner) return "Pick an ally.";
                    if (Hex.Distance(u.Position, h) > 3) return "Out of range (3).";
                    return Los(s, u, u.Position, h) ? null : "No line of sight to that ally.";
                case "AdjacentPair":
                    if (i == 0) return EnemyPick(s, u, h, range, a);
                    if (Hex.Distance(h, picks[0]) != 1) return "The second enemy must stand next to the first.";
                    return EnemyPick(s, u, h, range, a);
                case "AllHands":
                    if (i % 2 == 0)
                    {
                        var r = AllyPick(s, u, h, range, a);
                        if (r != null) return r;
                        if (RosterRules.Inert(unit) || unit.HasR(RStatusType.Rooted)) return "That ally cannot move.";
                        for (int k = 0; k < picks.Count; k += 2) if (picks[k] == h) return "Already picked.";
                        return null;
                    }
                    if (Hex.Distance(h, picks[i - 1]) != 1) return "Must be next to that ally.";
                    if (!RosterMovement.Open(s, h)) return "That hex is not empty.";
                    for (int k = 1; k < picks.Count; k += 2) if (picks[k] == h) return "Another ally is already moving there.";
                    return null;
                case "TwoAdjoining":
                    if (Hex.Distance(u.Position, h) > range) return $"Out of range ({range}).";
                    if (!RosterMovement.Open(s, h) || s.BarrierAt(h) != null) return "Needs an empty hex.";
                    if (i == 1 && Hex.Distance(h, picks[0]) != 1) return "The second wall must adjoin the first.";
                    return null;
                case "Line2": case "Line2Ignite": case "RevealCorridor":
                    return Hex.Distance(h, u.Position) == 1 ? null : "Pick an adjacent hex to set the direction.";
                case "DriveBy":
                    if (i == 0)
                    {
                        if (h == u.Position) return null;
                        return DashDestinations(s, u).ContainsKey(h) ? null : "Not reachable along an open path within 2.";
                    }
                    {
                        if (picks.Skip(1).Contains(h)) return "Already picked.";
                        var from = picks[0];
                        if (unit == null || unit.Owner == u.Owner || !unit.OnBoard) return "Pick an enemy.";
                        int d = Hex.Distance(from, h);
                        if (d > a.Range) return $"Out of range ({a.Range}) from the move's end.";
                        var blocked = RosterCombat.TargetBlocked(s, u, unit, from, false);
                        if (blocked != null) return blocked;
                        return d <= 1 || RosterTerrain.LineOfSight(s, from, h, false, u.Id, u.Owner) == LosState.Clear ? null : "No line of sight from the move's end.";
                    }
                case "PillageRush":
                    if (i == 0) return DashDestinations(s, u).ContainsKey(h) ? null : "Not a straight, open line within 3.";
                    {
                        var o = RosterCombat.ResolveBasic(s, u, h, picks[0], checkAction: false);
                        return o.Legal ? null : o.Reason;
                    }
            }

            if (a.Dash != DashKind.None)
            {
                if (i == 0) return DashDestinations(s, u).ContainsKey(h) ? null : DashFail(a);
                // strike pick, adjacent to the destination
                if (Hex.Distance(h, picks[0]) != 1) return "The strike must hit a hex next to where it lands.";
                if (unit != null && unit.Id != u.Id)
                {
                    if (unit.Owner == u.Owner) return "Pick an enemy.";
                    return RosterCombat.TargetBlocked(s, u, unit, picks[0], false);
                }
                var bar = s.BarrierAt(h);
                if (sp == "StrikeBarrier" && bar != null && bar.Owner != u.Owner) return null;
                return "Pick an adjacent enemy.";
            }

            switch (a.Target)
            {
                case TargetKind.Enemy:
                {
                    var r = EnemyPick(s, u, h, range, a);
                    if (r != null) return r;
                    return CodeCheck(s, u, unit, a);
                }
                case TargetKind.Ally:
                    return AllyPick(s, u, h, range, a);
                case TargetKind.Hex:
                    return HexPick(s, u, h, range, a);
            }
            return "No target needed.";
        }

        static string EnemyPick(MatchState s, UnitState u, Hex h, int range, ActiveSpec a)
        {
            var t = s.UnitAt(h);
            if (t == null || !t.OnBoard) return "Pick an enemy unit.";
            if (t.Owner == u.Owner) return "Pick an enemy, not an ally.";
            int d = Hex.Distance(u.Position, h);
            int min = Math.Max(1, a.MinRange);
            if (d < min) return $"Too close (minimum {min}).";
            if (d > range) return range == 1 ? "Must be adjacent." : $"Out of range ({range}).";
            if (a.Filter != null && !a.Filter(s, u, t)) return $"Needs a {a.FilterText}.";
            var blocked = RosterCombat.TargetBlocked(s, u, t, u.Position, false);
            if (blocked != null) return blocked;
            if (a.NeedsLos && !Los(s, u, u.Position, h)) return "No line of sight.";
            return null;
        }

        static string AllyPick(MatchState s, UnitState u, Hex h, int range, ActiveSpec a)
        {
            var t = s.UnitAt(h);
            if (t == null || !t.OnBoard) return "Pick an allied unit.";
            if (t.Owner != u.Owner) return "Pick an ally, not an enemy.";
            if (t.Id == u.Id && !a.AllowSelf) return "Pick another unit.";
            int d = AllyDistance(s, u, h);
            if (d > Math.Max(1, range)) return range <= 1 ? "Must be adjacent." : $"Out of range ({range}).";
            if (a.Filter != null && !a.Filter(s, u, t)) return $"Needs a {a.FilterText}.";
            if (a.Code == EffectCode.HEAL || a.Code == EffectCode.REPAIR)
            {
                bool clears = a.Special == "ClearDisabled" && t.HasR(RStatusType.Disabled);
                if (a.Special == "OptionHealOrUnroot") return null;
                if (t.Hp >= t.MaxHp && !clears) return $"{t.Def.Name} is at full HP.";
            }
            return null;
        }

        static string HexPick(MatchState s, UnitState u, Hex h, int range, ActiveSpec a)
        {
            int d = Hex.Distance(u.Position, h);
            switch (a.Code)
            {
                case EffectCode.BARRIER:
                    if (a.Special == "RebuildFacet")
                    {
                        var own = s.BarrierAt(h);
                        if (own != null && own.Owner == u.Owner && d <= 1) return own.Hp < own.MaxHp + 3 ? null : "That barrier is at full strength.";
                    }
                    if (d != 1) return "Must be adjacent.";
                    if (!RosterMovement.Open(s, h) || s.BarrierAt(h) != null) return "Needs an empty hex.";
                    return null;
                case EffectCode.MINE:
                    if (d < 1 || d > range) return range == 1 ? "Must be adjacent." : $"Out of range ({range}).";
                    if (!RosterMovement.Open(s, h) || s.BarrierAt(h) != null) return "Needs an empty hex.";
                    if (s.Mines.Any(m => m.Hex == h && m.Owner == u.Owner)) return "You already have a mine there.";
                    return null;
                case EffectCode.SPAWN:
                    if (d != 1) return "Must be adjacent.";
                    return RosterMovement.Open(s, h) ? null : "Needs an empty hex.";
                default: // AOE / BOMBARD centre
                    if (d < Math.Max(1, a.MinRange)) return "Too close.";
                    if (d > range) return $"Out of range ({range}).";
                    if (a.NeedsLos && !Los(s, u, u.Position, h)) return "No line of sight to that hex.";
                    var occ = s.UnitAt(h);
                    if (occ != null && occ.Owner != u.Owner)
                    {
                        var blocked = RosterCombat.TargetBlocked(s, u, occ, u.Position, false);
                        if (blocked != null) return blocked;
                    }
                    return null;
            }
        }

        /// <summary>Extra eligibility by effect code (capture thresholds, boarding conditions…).</summary>
        static string CodeCheck(MatchState s, UnitState u, UnitState t, ActiveSpec a)
        {
            switch (a.Code)
            {
                case EffectCode.CAPTURE:
                    if (t.HasR(RStatusType.Captured)) return "Already captured.";
                    if (t.R.IsToken) return "Tokens cannot be captured.";
                    if (t.Hp > a.HpThreshold) return $"{t.Def.Name} has {t.Hp} HP: The Gift needs {a.HpThreshold} or less.";
                    return null;
                case EffectCode.CONVERT:
                    if (!t.HasR(RStatusType.Captured)) return "Conversion needs a CAPTURED enemy.";
                    if (t.HasR(RStatusType.Converting)) return "Already being converted.";
                    return null;
                case EffectCode.BOARD:
                    if (t.HasR(RStatusType.Boarded)) return "Already boarded.";
                    if (t.Hp > a.HpThreshold) return $"{t.Def.Name} has {t.Hp} HP: needs {a.HpThreshold} or less.";
                    if (a.Special == "AssetTow" && s.Units.Any(x => x.OnBoard && x.Owner == t.Owner && x.Id != t.Id && Hex.Distance(x.Position, t.Position) == 1))
                        return "An allied ship is escorting it (adjacent).";
                    return null;
            }
            return null;
        }

        /// <summary>Legal destinations of the dash/charge/teleport part of an active.</summary>
        public static Dictionary<Hex, MoveOption> DashDestinations(MatchState s, UnitState u)
        {
            var a = u.R.Active;
            switch (a.Dash)
            {
                case DashKind.Leap:
                {
                    var d = RosterMovement.Leap(s, u.Position, a.DashRange, ignoreUnits: true);
                    if (a.NeedsLos) foreach (var k in d.Keys.ToList()) if (!Los(s, u, u.Position, k, unitsBlock: false)) d.Remove(k);
                    return d;
                }
                case DashKind.Straight: return RosterMovement.Straight(s, u.Position, a.DashRange);
                case DashKind.Path: return RosterMovement.Paths(s, u, a.DashRange, MovePattern.Step);
                case DashKind.Teleport:
                {
                    var d = new Dictionary<Hex, MoveOption>();
                    foreach (var h in Hex.Spiral(u.Position, a.DashRange))
                    {
                        if (h == u.Position || !s.Map.Contains(h) || !RosterMovement.Open(s, h)) continue;
                        if (a.NeedsLos && !Los(s, u, u.Position, h, unitsBlock: false)) continue;
                        d[h] = new MoveOption { Destination = h, Cost = Hex.Distance(u.Position, h), Path = new List<Hex> { u.Position, h } };
                    }
                    return d;
                }
            }
            return new Dictionary<Hex, MoveOption>();
        }

        static string DashFail(ActiveSpec a) => a.Dash switch
        {
            DashKind.Straight => $"Not on a straight, open line within {a.DashRange}.",
            DashKind.Path => $"Not reachable along an open path within {a.DashRange}.",
            DashKind.Teleport => $"Not an empty, visible hex within {a.DashRange}.",
            _ => $"Not an empty hex within {a.DashRange}.",
        };

        /// <summary>Does the unit have at least one legal use of its active right now?</summary>
        public static bool AnyUse(MatchState s, UnitState u)
        {
            if (RosterRules.CanAct(s, u, attack: false) != null) return false;
            var a = u.R.Active;
            if (a.RequiresNoMove && u.MovedThisActivation) return false;
            if (Moves(a) && u.HasR(RStatusType.Rooted)) return false;
            if (GlobalCheck(s, u, new List<Hex>()) != null) return false;
            if (Steps(u).Count == 0) return true;
            return Candidates(s, u, new List<Hex>()).Any(c => c.Valid);
        }

        // ================================================================== AI enumeration

        /// <summary>Legal pick lists for the AI (depth-first over candidates, capped). Options are expanded too.</summary>
        public static List<(List<Hex> picks, int option)> Enumerate(MatchState s, UnitState u, int cap = 48)
        {
            var result = new List<(List<Hex>, int)>();
            if (RosterRules.CanAct(s, u, attack: false) != null) return result;
            var a = u.R.Active;
            int options = Math.Max(1, a.Options.Length);
            for (int opt = 0; opt < options && result.Count < cap; opt++)
            {
                var steps = Steps(u, opt);
                Dfs(s, u, steps, new List<Hex>(), opt, result, cap);
            }
            return result;
        }

        static void Dfs(MatchState s, UnitState u, List<PickStep> steps, List<Hex> picks, int opt, List<(List<Hex>, int)> result, int cap)
        {
            if (result.Count >= cap) return;
            if (Ready(u, picks, opt) && Validate(s, u, picks, opt).Ok)
            {
                // Prefer full target lists: record a partial list only when no further pick is possible.
                bool more = picks.Count < steps.Count && Candidates(s, u, picks, opt).Any(c => c.Valid);
                if (!more) { result.Add((new List<Hex>(picks), opt)); return; }
            }
            if (picks.Count >= steps.Count) return;
            var cands = Candidates(s, u, picks, opt).Where(c => c.Valid).Select(c => c.Hex).ToList();
            // Keep the search small: for unit steps after the first, only extend in increasing hex order (combinations, not permutations).
            if (steps[picks.Count].Kind == PickKind.Unit && picks.Count > 0 && steps[picks.Count - 1].Kind == PickKind.Unit)
                cands = cands.Where(h => h.CompareTo(picks[picks.Count - 1]) > 0).ToList();
            int width = picks.Count == 0 ? 12 : 6;
            foreach (var h in cands.OrderBy(h => Hex.Distance(h, u.Position)).ThenBy(h => h).Take(width))
            {
                picks.Add(h);
                Dfs(s, u, steps, picks, opt, result, cap);
                picks.RemoveAt(picks.Count - 1);
                if (result.Count >= cap) return;
            }
        }

        // ================================================================== execution

        /// <summary>Resolves an active on the real state. <paramref name="planned"/> is the fogged view it was validated on:
        /// a hidden enemy standing on a hex the ability needed empty blocks it (an ambush; the activation is spent), and
        /// dashes follow the planned route and stop short of hidden units like a normal move.</summary>
        public static void Apply(MatchState s, UnitState u, IList<Hex> picks, int option, List<GameEvent> ev, MatchState planned = null)
        {
            if (ReferenceEquals(planned, s)) planned = null;
            var a = u.R.Active;
            string sp = a.Special ?? "";
            var start = u.Position;
            ev.Add(new GameEvent(GameEventType.AbilityActivated)
            {
                Seat = u.Owner, UnitId = u.Id, Ability = AbilityId.UnitActive, From = start, To = picks.Count > 0 ? picks[0] : start,
                Text = u.R.ActiveName, FxKey = "codex_" + a.Code.ToString().ToLowerInvariant(),
            });
            u.UsedActiveThisActivation = true;
            u.ActedThisActivation = true;
            u.Cooldown = a.Cooldown;
            bool offensive = a.Target == TargetKind.Enemy || a.Damage > 0 || a.StrikeDamage > 0 || sp == "AllEnemiesInRange" || sp == "PillageRush";
            if (offensive) RosterRules.BreakCloak(s, u, ev);
            bool dealtDamage = false;
            Hex at = default;
            var lurker = planned != null ? PlacementLurker(s, planned, u, picks, out at) : null;
            if (lurker != null)
            {
                ev.Add(new GameEvent(GameEventType.Ambushed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = lurker.Id, From = u.Position, To = at, Text = $"{u.R.ActiveName}: a hidden unit is in the way" });
                return;
            }

            switch (a.Code)
            {
                case EffectCode.DASH: case EffectCode.CHARGE: case EffectCode.LEAP: case EffectCode.TELEPORT:
                    if (sp == "PillageRush") { dealtDamage = PillageRush(s, u, picks, ev, planned); break; }
                    if (sp == "Tunnel" || a.Dash != DashKind.None) dealtDamage = DashAndStrike(s, u, picks, ev, planned);
                    if (sp == "GravityFold") Teleport(s, s.UnitAt(picks[0]), picks[1], u, ev);
                    break;
                case EffectCode.REPOSITION:
                    if (sp == "MassExtraction" || sp == "Evacuate") Reposition(s, s.UnitAt(picks[0]), picks[1], u, ev);
                    else dealtDamage = DashAndStrike(s, u, picks, ev, planned);
                    break;
                case EffectCode.BARRAGE:
                    if (sp == "DriveBy")
                    {
                        if (picks[0] != u.Position) MoveDash(s, u, picks[0], ev);
                        foreach (var h in picks.Skip(1)) dealtDamage |= DirectHit(s, u, s.UnitAt(h), a.Damage, a.Pierce, ev, picks[0]);
                    }
                    else foreach (var h in picks) dealtDamage |= DirectHit(s, u, s.UnitAt(h), a.Damage, a.Pierce, ev);
                    break;
                case EffectCode.AOE: case EffectCode.BOMBARD:
                    dealtDamage = Area(s, u, picks, ev);
                    break;
                case EffectCode.PULL:
                    foreach (var h in picks.Take(1))
                    {
                        var t = s.UnitAt(h);
                        var dest = sp == "PullToHex" ? picks[1] : RosterMovement.StepToward(t.Position, u.Position);
                        var fail = RosterRules.ForceMove(s, t, dest, u, ev);
                        if (fail != null) ev.Add(new GameEvent(GameEventType.AbilityActivated) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, Ability = AbilityId.UnitActive, Text = $"pull failed: {fail}" });
                        if (a.Damage > 0 && t.Alive) dealtDamage |= DirectHit(s, u, t, a.Damage, a.Pierce, ev, unavoidable: a.Unavoidable);
                    }
                    break;
                case EffectCode.CAPTURE:
                {
                    var t = s.UnitAt(picks[0]);
                    RosterRules.AddStatus(s, t, new Grant(RStatusType.Captured, 0, RExpire.Battle), u.Id, ev);
                    t.RescueProgress = 0;
                    ev.Add(new GameEvent(GameEventType.UnitCaptured) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position });
                    break;
                }
                case EffectCode.CONVERT:
                {
                    var t = s.UnitAt(picks[0]);
                    t.RStatuses.Add(new RStatus { Type = RStatusType.Converting, SourceId = u.Id, Amount = s.Round + 1, Expire = RExpire.Battle });
                    ev.Add(new GameEvent(GameEventType.ConversionStarted) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, Amount = s.Round + 1 });
                    break;
                }
                case EffectCode.BOARD:
                {
                    var t = s.UnitAt(picks[0]);
                    t.RStatuses.RemoveAll(x => x.Type == RStatusType.Boarded);
                    t.RStatuses.Add(new RStatus { Type = RStatusType.Boarded, SourceId = u.Id, Expire = RExpire.Battle, Mode = (byte)(sp == "AssetTow" ? 1 : 0) });
                    s.BoardsThisRound[u.Owner]++;
                    ev.Add(new GameEvent(GameEventType.UnitBoarded) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, Text = sp == "AssetTow" ? "claimed" : "boarded" });
                    break;
                }
                case EffectCode.BARRIER:
                    if (sp == "RebuildFacet" && s.BarrierAt(picks[0]) is RosterBarrier own && own.Owner == u.Owner)
                    {
                        own.Hp += 3;
                        own.MaxHp = Math.Max(own.MaxHp, own.Hp);
                        ev.Add(new GameEvent(GameEventType.BarrierDamaged) { Seat = u.Owner, UnitId = u.Id, To = own.Hex, Amount = -3, HpAfter = own.Hp, Text = "restored" });
                        break;
                    }
                    foreach (var h in picks)
                    {
                        var b = new RosterBarrier { Hex = h, Owner = u.Owner, SourceUnitId = u.Id, Hp = a.BarrierHp, MaxHp = a.BarrierHp, Blocking = a.BarrierBlocking, ExpireRound = s.Round + Math.Max(1, a.BarrierRounds) - 1 };
                        s.Barriers.Add(b);
                        ev.Add(new GameEvent(GameEventType.BarrierRaised) { Seat = u.Owner, UnitId = u.Id, To = h, Amount = b.Hp, Lethal = b.Blocking, HpAfter = b.ExpireRound });
                    }
                    break;
                case EffectCode.MINE:
                    s.Mines.Add(new RosterMine { Hex = picks[0], Owner = u.Owner, SourceUnitId = u.Id, Damage = a.Damage });
                    ev.Add(new GameEvent(GameEventType.MineLaid) { Seat = u.Owner, UnitId = u.Id, To = picks[0], Amount = a.Damage });
                    break;
                case EffectCode.SPAWN:
                {
                    var caps = s.Config.Players[u.Owner].Caps;
                    var token = RosterArmy.CreateUnit(s.NextUnitId++, u.Owner, a.TokenName, caps);
                    token.Deployed = true;
                    token.Position = picks[0];
                    token.SpawnedBy = u.Id;
                    token.ActivatedRound = s.Round;    // launched tokens act from the next round
                    s.Units.Add(token);
                    ev.Add(new GameEvent(GameEventType.TokenSpawned) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = token.Id, From = u.Position, To = token.Position, Text = token.Def.Name });
                    break;
                }
                case EffectCode.REVEAL:
                    Reveal(s, u, picks, ev);
                    break;
                case EffectCode.CLOAK:
                    foreach (var g in a.SelfGrants) RosterRules.AddStatus(s, u, g, u.Id, ev);
                    break;
                case EffectCode.HEAL: case EffectCode.REPAIR:
                    foreach (var h in picks)
                    {
                        var t = s.UnitAt(h);
                        if (sp == "OptionHealOrUnroot" && option == 1)
                        {
                            int ri = t.RIndex(RStatusType.Rooted);
                            if (ri >= 0) { t.RStatuses.RemoveAt(ri); ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = t.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, RStatus = RStatusType.Rooted, Text = "cleared" }); }
                            continue;
                        }
                        int amt = t.R.IsVehicle && a.HealVehicle > 0 ? a.HealVehicle : a.Heal;
                        if (a.Code == EffectCode.REPAIR) amt += RosterPassives.RepairBonus(s, t);
                        RosterRules.Heal(s, t, amt, u, ev, u.R.ActiveName);
                        if (sp == "ClearDisabled") t.RStatuses.RemoveAll(x => x.Type == RStatusType.Disabled);
                        foreach (var g in a.Grants) RosterRules.AddStatus(s, t, g, u.Id, ev);
                    }
                    break;
                default:
                    dealtDamage = Targeted(s, u, picks, option, ev);
                    break;
            }
            foreach (var g in a.SelfGrants)
                if (a.Code != EffectCode.CLOAK && u.Alive) RosterRules.AddStatus(s, u, g, u.Id, ev);
            if (a.SelfDamage > 0 && u.Alive)
                RosterCombat.Hit(s, u, new HitSpec { Raw = a.SelfDamage, Pierce = 99, Cause = u.R.ActiveName, From = u.Position }, ev);
            if (dealtDamage) u.AttackedRound = s.Round;
        }

        /// <summary>PIN, DISABLE, DEBUFF, SUPPRESS, MARK, ARMOR_BREAK, DAMAGE, PIERCE, GUARD, INTERCEPT, SHIELD, RALLY, COUNTER:
        /// optional damage to each picked unit, then the grants.</summary>
        static bool Targeted(MatchState s, UnitState u, IList<Hex> picks, int option, List<GameEvent> ev)
        {
            var a = u.R.Active;
            string sp = a.Special ?? "";
            bool dealt = false;
            if (sp == "AllEnemiesInRange")
            {
                foreach (var e in s.Units.Where(e => e.OnBoard && e.Owner != u.Owner && Hex.Distance(e.Position, u.Position) <= a.Range).OrderBy(e => e.Id).ToList())
                    foreach (var g in a.Grants) RosterRules.AddStatus(s, e, g, u.Id, ev);
                return false;
            }
            if (sp == "JumpAdjacent")
            {
                MoveDash(s, u, picks[1], ev);
                var ally = s.UnitAt(picks[0]);
                foreach (var g in a.Grants) RosterRules.AddStatus(s, ally, g, u.Id, ev);
                return false;
            }
            if (sp == "AllHands")
            {
                for (int k = 0; k + 1 < picks.Count; k += 2)
                {
                    var ally = s.UnitAt(picks[k]);
                    if (ally == null || !RosterMovement.Open(s, picks[k + 1])) continue;
                    var from = ally.Position;
                    RosterRules.MoveAlong(s, ally, new List<Hex> { from, picks[k + 1] }, false, ev, own: true);
                    ev.Add(new GameEvent(GameEventType.FreeMove) { Seat = u.Owner, UnitId = ally.Id, From = from, To = ally.Position, Text = u.R.ActiveName });
                }
                return false;
            }
            var grants = a.Grants;
            if (sp == "OptionShieldOrAttack" && option == 1) grants = new[] { new Grant(RStatusType.AttackBuff, 1, RExpire.AfterOwnActivation, 1, uses: 1) };
            if (sp == "OptionAttackOrShield" && option == 1) grants = new[] { new Grant(RStatusType.Shield, 2, RExpire.SourceNextActivation) };
            var targets = picks.Select(h => s.UnitAt(h)).Where(t => t != null).ToList();
            if (sp == "AlsoShieldAlly") targets = targets.Take(1).ToList();
            foreach (var t in targets)
            {
                if (a.Damage > 0)
                {
                    dealt |= DirectHit(s, u, t, a.Damage, a.Pierce, ev, unavoidable: a.Unavoidable);
                    if (!t.Alive) continue;
                }
                foreach (var g in grants)
                {
                    var gg = g;
                    if (g.Type == RStatusType.Protected || g.Type == RStatusType.Intercepted) { ApplyProtect(s, u, t, g, sp, ev); continue; }
                    RosterRules.AddStatus(s, t, gg, u.Id, ev);
                }
                if (sp == "StepAway" && u.Alive)
                {
                    var away = RosterMovement.StepAway(s, u.Position, t.Position);
                    if (away.HasValue && !u.HasR(RStatusType.Rooted)) MoveDash(s, u, away.Value, ev, path: true);
                }
            }
            if (sp == "AlsoShieldAlly" && picks.Count > 1)
            {
                var ally = s.UnitAt(picks[1]);
                if (ally != null) RosterRules.AddStatus(s, ally, new Grant(RStatusType.Shield, 2, RExpire.SourceNextActivation), u.Id, ev);
            }
            return dealt;
        }

        static void ApplyProtect(MatchState s, UnitState u, UnitState t, Grant g, string sp, List<GameEvent> ev)
        {
            var a = u.R.Active;
            if (!RosterRules.AddStatus(s, t, g, u.Id, ev)) return;
            int i = -1;
            for (int k = 0; k < t.RStatuses.Count; k++) if (t.RStatuses[k].Type == g.Type && t.RStatuses[k].SourceId == u.Id) i = k;
            if (i < 0) return;
            var st = t.RStatuses[i];
            st.Range = Math.Max(1, g.Range > 0 ? g.Range : a.Range);
            st.Mode = (byte)(sp == "RangedOnly" ? 1 : sp == "BasicOnly" ? 2 : g.Type == RStatusType.Intercepted ? 3 : 0);
            t.RStatuses[i] = st;
        }

        /// <summary>A single-target damaging hit from an active (direct: guards may intercept it, buffs and marks apply).</summary>
        static bool DirectHit(MatchState s, UnitState u, UnitState t, int damage, int pierce, List<GameEvent> ev, Hex? from = null, bool unavoidable = false)
        {
            if (t == null || !t.Alive || damage <= 0) return false;
            var hit = new HitSpec
            {
                Attacker = u, Raw = damage, Pierce = pierce, Direct = !unavoidable, Unavoidable = unavoidable, From = from ?? u.Position,
                TargetHex = t.Position, Cause = u.R.ActiveName, FxKey = u.Weapon.FxKey,
            };
            var r = RosterCombat.Compute(s, t, hit, from);
            ev.Add(new GameEvent(GameEventType.AttackResolved)
            {
                Seat = u.Owner, UnitId = u.Id, TargetUnitId = r.TargetId, From = from ?? u.Position, To = s.Unit(r.TargetId).Position, Amount = r.Total,
                FxKey = u.Weapon.FxKey, Text = u.R.ActiveName, Lethal = r.Lethal, Ability = AbilityId.UnitActive,
            });
            RosterCombat.Apply(s, r, ev);
            u.AttackedThisActivation = true;
            return true;
        }

        static bool Area(MatchState s, UnitState u, IList<Hex> picks, List<GameEvent> ev)
        {
            var a = u.R.Active;
            string sp = a.Special ?? "";
            var hexes = new List<(Hex hex, int dmg)>();
            if (sp == "Line2" || sp == "Line2Ignite")
            {
                var dir = picks[0] - u.Position;
                hexes.Add((picks[0], a.Damage));
                hexes.Add((picks[0] + dir, a.Damage));
                if (sp == "Line2Ignite")
                    foreach (var (h, _) in hexes)
                        if (s.Map.Contains(h) && !RosterTerrain.Blocks(s, h))
                        {
                            s.Ignited.RemoveAll(x => x.Hex == h);
                            s.Ignited.Add(new IgnitedHex { Hex = h, ExpireRound = s.Round + 1 });
                            ev.Add(new GameEvent(GameEventType.HexIgnited) { Seat = u.Owner, UnitId = u.Id, To = h });
                        }
            }
            else
            {
                var center = picks[0];
                hexes.Add((center, a.Damage));
                if (a.Splash > 0) foreach (var n in center.Neighbors()) hexes.Add((n, a.Splash));
            }
            ev.Add(new GameEvent(GameEventType.AttackResolved)
            {
                Seat = u.Owner, UnitId = u.Id, From = u.Position, To = sp.StartsWith("Line2") ? picks[0] : picks[0], Amount = a.Damage,
                FxKey = a.Code == EffectCode.BOMBARD ? "explosion_large" : u.Weapon.FxKey, Text = u.R.ActiveName, Ability = AbilityId.UnitActive,
            });
            bool dealt = false;
            foreach (var (h, dmg) in hexes)
            {
                if (!s.Map.Contains(h)) continue;
                var t = s.UnitAt(h);
                if (t != null && t.Id != u.Id && t.Alive)
                {
                    if (a.EnemiesOnly && t.Owner == u.Owner) continue;
                    var hit = new HitSpec { Attacker = u, Raw = dmg, Area = true, IgnoreCover = a.IgnoresCover, From = u.Position, TargetHex = h, Cause = u.R.ActiveName };
                    RosterCombat.Hit(s, t, hit, ev);
                    dealt = true;
                }
                else if (t == null)
                {
                    var b = s.BarrierAt(h);
                    if (b != null && (b.Owner != u.Owner || !a.EnemiesOnly)) RosterCombat.DamageBarrier(s, b, dmg + RosterPassives.BarrierDamageBonus(s, u, false), u, ev);
                }
            }
            u.AttackedThisActivation = true;
            return dealt;
        }

        /// <summary>DASH / CHARGE / LEAP / TELEPORT / self-REPOSITION: move to picks[0], then the optional strike on picks[1].</summary>
        static bool DashAndStrike(MatchState s, UnitState u, IList<Hex> picks, List<GameEvent> ev, MatchState planned = null)
        {
            var a = u.R.Active;
            var opt = PlannedDash(s, u, picks[0], planned);
            bool jump = a.Dash != DashKind.Path && a.Dash != DashKind.Straight;
            RosterRules.MoveAlong(s, u, opt.Path, jump, ev, own: true, planned: planned);
            u.MovedRound = s.Round;
            if (!u.Alive || u.Position != picks[0] || picks.Count < 2 || a.StrikeDamage <= 0) return false;
            var t = s.UnitAt(picks[1]);
            if (t != null && t.Owner != u.Owner && Hex.Distance(t.Position, u.Position) == 1)
                return DirectHit(s, u, t, a.StrikeDamage, a.StrikePierce, ev);
            var b = s.BarrierAt(picks[1]);
            if (b != null && b.Owner != u.Owner && t == null)
            {
                RosterCombat.DamageBarrier(s, b, a.StrikeDamage + RosterPassives.BarrierDamageBonus(s, u, false), u, ev);
                return true;
            }
            return false;
        }

        static bool PillageRush(MatchState s, UnitState u, IList<Hex> picks, List<GameEvent> ev, MatchState planned = null)
        {
            var opt = PlannedDash(s, u, picks[0], planned);
            RosterRules.MoveAlong(s, u, opt.Path, false, ev, own: true, planned: planned);
            u.MovedRound = s.Round;
            if (!u.Alive || u.Position != picks[0] || picks.Count < 2) return false;
            var o = RosterCombat.ResolveBasic(s, u, picks[1], checkAction: false);
            if (!o.Legal || o.Roster == null) return false;
            var t = s.Unit(o.TargetUnitId);
            if (t != null && t.HasR(RStatusType.Exposed))
            {
                var hit = o.Roster.Spec;
                hit.Raw += 1;
                hit.Cause = u.Weapon.Name + " (Pillage Rush +1)";
                var r = RosterCombat.Compute(s, t, hit);
                o.Roster = r;
            }
            ev.Add(new GameEvent(GameEventType.AttackResolved)
            {
                Seat = u.Owner, UnitId = u.Id, TargetUnitId = o.Roster.TargetId, From = u.Position, To = s.Unit(o.Roster.TargetId).Position, Amount = o.Roster.Total,
                FxKey = u.Weapon.FxKey, Text = u.Weapon.Name, Lethal = o.Roster.Lethal,
            });
            RosterCombat.Apply(s, o.Roster, ev);
            u.AttackedThisActivation = true;
            return true;
        }

        /// <summary>The dash route as planned: on the fogged view when there is one (MoveAlong then stops short of hidden units).</summary>
        static MoveOption PlannedDash(MatchState s, UnitState u, Hex to, MatchState planned)
        {
            var view = planned ?? s;
            return DashDestinations(view, view.Unit(u.Id))[to];
        }

        /// <summary>Fog of war: the first hidden enemy standing on a hex this use needed empty (a placement, or where an
        /// ally is moved to), or null. Dash destinations are handled by the move itself; area centres may hold anyone.</summary>
        static UnitState PlacementLurker(MatchState s, MatchState planned, UnitState u, IList<Hex> picks, out Hex at)
        {
            at = default;
            var a = u.R.Active;
            string sp = a.Special ?? "";
            for (int i = 0; i < picks.Count; i++)
            {
                bool placement;
                switch (sp)
                {
                    case "PullToHex": case "MassExtraction": case "Evacuate": case "GravityFold": case "JumpAdjacent": placement = i == 1; break;
                    case "AllHands": placement = i % 2 == 1; break;
                    case "TwoAdjoining": placement = true; break;
                    case "Line2": case "Line2Ignite": case "RevealCorridor": case "DriveBy": case "PillageRush": placement = false; break;
                    default:
                        placement = a.Dash == DashKind.None && a.Target == TargetKind.Hex &&
                                    (a.Code == EffectCode.BARRIER || a.Code == EffectCode.MINE || a.Code == EffectCode.SPAWN) &&
                                    !(sp == "RebuildFacet" && s.BarrierAt(picks[i])?.Owner == u.Owner);
                        break;
                }
                if (!placement) continue;
                var occ = s.UnitAt(picks[i]);
                if (occ != null && occ.Owner != u.Owner && planned.UnitAt(picks[i]) == null) { at = picks[i]; return occ; }
            }
            return null;
        }

        static void MoveDash(MatchState s, UnitState u, Hex to, List<GameEvent> ev, bool path = false)
        {
            RosterRules.MoveAlong(s, u, new List<Hex> { u.Position, to }, jump: !path || Hex.Distance(u.Position, to) > 1, ev, own: true);
            u.MovedRound = s.Round;
        }

        static void Teleport(MatchState s, UnitState t, Hex to, UnitState source, List<GameEvent> ev)
        {
            if (t == null || !RosterMovement.Open(s, to)) return;
            if (t.Id == source.Id) { MoveDash(s, t, to, ev); return; }
            var from = t.Position;
            t.Position = to;
            ev.Add(new GameEvent(GameEventType.UnitJumped) { Seat = t.Owner, UnitId = t.Id, TargetUnitId = source.Id, From = from, To = to });
            RosterRules.EndedOn(s, t, ev, own: false);
        }

        static void Reposition(MatchState s, UnitState t, Hex to, UnitState source, List<GameEvent> ev)
        {
            if (t == null) return;
            if (t.Owner != source.Owner)
            {
                var fail = RosterRules.ForceMove(s, t, to, source, ev);
                if (fail != null) ev.Add(new GameEvent(GameEventType.AbilityActivated) { Seat = source.Owner, UnitId = source.Id, TargetUnitId = t.Id, To = t.Position, Ability = AbilityId.UnitActive, Text = $"failed: {fail}" });
                return;
            }
            if (!RosterMovement.Open(s, to)) return;
            var from = t.Position;
            t.Position = to;
            ev.Add(new GameEvent(GameEventType.UnitDisplaced) { Seat = t.Owner, UnitId = t.Id, TargetUnitId = source.Id, From = from, To = to });
            RosterRules.EndedOn(s, t, ev, own: false);
        }

        static void Reveal(MatchState s, UnitState u, IList<Hex> picks, List<GameEvent> ev)
        {
            var a = u.R.Active;
            string sp = a.Special ?? "";
            var area = new HashSet<Hex>();
            if (sp == "RevealCorridor")
            {
                var dir = picks[0] - u.Position;
                var h = u.Position;
                for (int k = 0; k < 4; k++) { h += dir; area.Add(h); }
            }
            else foreach (var h in Hex.Spiral(u.Position, a.Range)) area.Add(h);
            var revealed = new List<UnitState>();
            foreach (var e in s.Units.Where(e => e.OnBoard && e.Owner != u.Owner && area.Contains(e.Position)).OrderBy(e => e.Id))
            {
                bool hidden = e.HasR(RStatusType.Cloaked) || RosterPassives.ConcealedBeyond(s, e) > 0 || RosterPassives.Has(e, PassiveKind.UntargetableByCapitalBeyond);
                if (!hidden) continue;
                RosterRules.AddStatus(s, e, new Grant(RStatusType.Revealed, 0, RExpire.RoundEnd, 1), u.Id, ev);
                revealed.Add(e);
            }
            foreach (var m in s.Mines.Where(m => m.Owner != u.Owner && area.Contains(m.Hex)))
            {
                m.RevealedUntilRound = s.Round;
                ev.Add(new GameEvent(GameEventType.UnitsRevealed) { Seat = u.Owner, UnitId = u.Id, To = m.Hex, Text = "mine" });
            }
            ev.Add(new GameEvent(GameEventType.UnitsRevealed) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Amount = revealed.Count, Text = u.R.ActiveName });
            if (sp == "RevealAreaMarkNearest")
            {
                var pool = revealed.Count > 0 ? revealed : s.Units.Where(e => e.OnBoard && e.Owner != u.Owner && area.Contains(e.Position)).ToList();
                var nearest = pool.OrderBy(e => Hex.Distance(e.Position, u.Position)).ThenBy(e => e.Id).FirstOrDefault();
                if (nearest != null) RosterRules.AddStatus(s, nearest, new Grant(RStatusType.Marked, 1, RExpire.RoundEnd, 1, uses: 1), u.Id, ev);
            }
        }
    }
}
