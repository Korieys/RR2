using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>One way the enemy can destroy a leader in a single activation: an optional move, then an action.</summary>
    public sealed class LethalLine
    {
        public int AttackerId;
        public Command Move;
        public Command Action;
        public string Text;
    }

    /// <summary>
    /// The Command Threat assessment: every way the enemy could destroy <see cref="Seat"/>'s leader with its next activation.
    /// Computed by applying the enemy's legal commands (from <see cref="LegalActions"/>) to copies of the state with the
    /// same <see cref="RulesEngine.Apply"/> that resolves them, so movement, costs, cooldowns, guards, shields, cover and
    /// passives are all real. An empty result is a bounded search result, never a promise of safety.
    /// </summary>
    public sealed class ThreatReport
    {
        public int Seat;
        /// <summary>Enemy activations searched (this assessment covers exactly one).</summary>
        public int Horizon = 1;
        public List<LethalLine> Lines = new List<LethalLine>();
        public int ShipsChecked;
        public int CommandsChecked;
        /// <summary>True when the board was evaluated as if the enemy acted next (it is currently the leader's side's turn).</summary>
        public bool AssumesTurnPasses;
        public bool LethalNow => Lines.Any(l => l.Move == null);
        public bool LethalAfterMove => Lines.Any(l => l.Move != null);

        public string Summary(string leaderName)
        {
            if (Lines.Count == 0)
                return $"No lethal strike on {leaderName} was found for the enemy's next activation ({ShipsChecked} ship{(ShipsChecked == 1 ? "" : "s")}, {CommandsChecked} possible orders checked). " +
                       "Later activations are not checked, so this is not a guarantee.";
            var first = Lines[0];
            string more = Lines.Count > 1 ? $" {Lines.Count - 1} other lethal option{(Lines.Count == 2 ? "" : "s")} also exist." : "";
            return $"{leaderName} can be destroyed by the enemy's next activation. {first.Text}{more}";
        }
    }

    /// <summary>Round-horizon focus-fire measure on a leader (see <see cref="CommandThreat.RoundExposure"/>).</summary>
    public sealed class Exposure
    {
        public int Seat;
        public int LeaderHull;
        public int Total;
        public int CommandsChecked;
        public List<(int unitId, int damage)> ByShip = new List<(int, int)>();
        /// <summary>The enemy's ships could, between them, destroy the leader over their next round if all chose it.</summary>
        public bool Critical => LeaderHull > 0 && Total >= LeaderHull;
        public string Summary(string leaderName) => ByShip.Count == 0
            ? $"No enemy ship can damage {leaderName} in one activation from where they stand."
            : $"Focus fire: {ByShip.Count} enemy ship{(ByShip.Count == 1 ? "" : "s")} could each hit {leaderName} in one activation, {Total} damage in all against {LeaderHull} hull and shields. " +
              "This assumes each enemy ship acts once and your side does nothing in between.";
    }

    public static class CommandThreat
    {
        /// <summary>Most lethal lines kept (the search still counts every command it checks).</summary>
        public const int MaxLines = 24;

        public static ThreatReport Assess(MatchState s, int seat)
        {
            var rep = new ThreatReport { Seat = seat };
            int enemy = 1 - seat;
            if (s.IsOver || s.Phase != MatchPhase.Battle || s.Commander(seat) == null || !s.Commander(seat).Alive) return rep;
            var view = s.Clone();
            if (view.ActiveSeat != enemy)
            {
                // Evaluate the board as it stands, as if the leader's side had finished its activation without changing it.
                rep.AssumesTurnPasses = true;
                view.ActiveSeat = enemy;
                view.ActiveUnitId = 0;
                if (!view.Units.Any(u => u.Owner == enemy && RosterRules.Eligible(view, u)))
                    foreach (var u in view.Units.Where(u => u.Owner == enemy && u.OnBoard)) u.ActivatedRound = view.Round - 1;   // a new round begins
            }
            var leaderId = view.Players[seat].CommanderId;
            var attackers = view.Units.Where(u => u.Owner == enemy && (view.ActiveUnitId == 0 ? RosterRules.Eligible(view, u) : u.Id == view.ActiveUnitId))
                                      .OrderBy(u => u.Id).Select(u => u.Id).ToList();
            rep.ShipsChecked = attackers.Count;
            var all = LegalActions.EnumerateRoster(view, abilityCap: 512, abilityWidth: 128);
            var leaderHex = view.Unit(leaderId).Position;
            foreach (int id in attackers)
            {
                var au = view.Unit(id);
                if (Hex.Distance(au.Position, leaderHex) > Reach(view, au)) continue;   // cannot touch the leader this activation
                int strike = Reach(view, au) - Math.Max(au.R.MoveHexes, RosterMovement.Budget(view, au)) * (au.R.Pattern == MovePattern.Diag ? 2 : 1);
                foreach (var c in all.Where(c => c.UnitId == id && c.Type != CommandType.Pass))
                {
                    if (c.Type == CommandType.Move)
                    {
                        if (c.Option == SliceRules.TurnOption ? false : Hex.Distance(c.Target, leaderHex) > strike + SliceRules.OverclockMove + 1) continue;
                        var moved = view.Clone();
                        rep.CommandsChecked++;
                        if (!RulesEngine.Apply(moved, c.Clone()).Accepted || moved.IsOver || moved.ActiveUnitId != id) continue;
                        foreach (var a in LegalActions.EnumerateRoster(moved, abilityCap: 512, abilityWidth: 128).Where(x => x.UnitId == id && x.Type != CommandType.Move && x.Type != CommandType.Pass))
                            Try(moved, c, a, seat, leaderId, rep);
                    }
                    else Try(view, null, c, seat, leaderId, rep);
                }
            }
            rep.Lines = rep.Lines.OrderBy(l => l.Move == null ? 0 : 1).ThenBy(l => l.AttackerId).Take(MaxLines).ToList();
            return rep;
        }

        /// <summary>
        /// Round-horizon exposure: for each enemy ship, the most damage it alone could deal to <paramref name="seat"/>'s
        /// leader in one activation (any legal move, then an attack or ability), from the current board. The total is what
        /// focus fire over the enemy's next round could do if every ship chose the leader; it ignores what the leader's side
        /// does in between, so it is a planning measure, shown as such.
        /// </summary>
        public static Exposure RoundExposure(MatchState s, int seat)
        {
            var ex = new Exposure { Seat = seat };
            int enemy = 1 - seat;
            var leader = s.Commander(seat);
            if (s.IsOver || leader == null || !leader.Alive) return ex;
            ex.LeaderHull = leader.Hp + leader.ShieldHp;
            foreach (var e in s.Units.Where(u => u.Owner == enemy && u.OnBoard && !RosterRules.Inert(u)).OrderBy(u => u.Id).ToList())
            {
                var view = s.Clone();
                view.ActiveSeat = enemy; view.ActiveUnitId = 0;
                var ve = view.Unit(e.Id);
                ve.ActivatedRound = view.Round - 1; ve.MovedThisActivation = ve.ActedThisActivation = false;
                int best = BestDamage(view, ve, leader.Id, ref ex.CommandsChecked);
                if (best > 0) ex.ByShip.Add((e.Id, best));
            }
            ex.Total = ex.ByShip.Sum(x => x.damage);
            return ex;
        }

        static int BestDamage(MatchState view, UnitState u, int leaderId, ref int checkedCount)
        {
            int best = 0;
            int hp0 = Hull(view, leaderId);
            var leaderHex = view.Unit(leaderId).Position;
            int reach = Reach(view, u);
            if (Hex.Distance(u.Position, leaderHex) > reach) return 0;
            foreach (var c in Strikes(view, u, leaderHex))
                best = Math.Max(best, DamageOf(view, c, leaderId, hp0, ref checkedCount));
            var moves = RosterMovement.Reachable(view, u).Keys.Select(h => SliceRules.Order(CommandType.Move, u.Owner, u.Id, h, 0)).ToList();
            if (SliceRules.CanOverclock(view, u) == null)
                moves.AddRange(RosterMovement.Reachable(view, u, SliceRules.OverclockMove).Keys.Select(h => SliceRules.Order(CommandType.Move, u.Owner, u.Id, h, SliceRules.OverclockOption)));
            int strike = Math.Max(u.Weapon.MaxRange + 1, u.R.Active == null ? 0 : Math.Max(u.R.Active.Range, u.R.ActiveRange) + u.R.Active.DashRange + 2);
            foreach (var mv in moves.Where(m => Hex.Distance(m.Target, leaderHex) <= strike).OrderBy(m => m.Target).ThenBy(m => m.Option))
            {
                var moved = view.Clone();
                checkedCount++;
                if (!RulesEngine.Apply(moved, mv.Clone()).Accepted || moved.IsOver || moved.ActiveUnitId != u.Id) continue;
                foreach (var a in Strikes(moved, moved.Unit(u.Id), leaderHex))
                    best = Math.Max(best, DamageOf(moved, a, leaderId, hp0, ref checkedCount));
            }
            return best;
        }

        /// <summary>Orders aimed at or next to the leader's hex: its basic attack (and overclocked), and its active ability
        /// with a pick within two hexes of the leader.</summary>
        static IEnumerable<Command> Strikes(MatchState s, UnitState u, Hex leaderHex)
        {
            if (RosterRules.CanAct(s, u, attack: true) == null)
            {
                if (RosterCombat.ResolveBasic(s, u, leaderHex).Legal) yield return Command.Attack(u.Owner, u.Id, leaderHex);
                if (SliceRules.CanOverclock(s, u) == null && RosterCombat.ResolveBasic(s, u, leaderHex, overclock: true).Legal)
                    yield return SliceRules.Order(CommandType.Attack, u.Owner, u.Id, leaderHex, SliceRules.OverclockOption);
            }
            foreach (var (picks, opt) in RosterAbilities.Enumerate(s, u, 48, 48))
                if (picks.Any(p => Hex.Distance(p, leaderHex) <= 2)) yield return Command.Active(u.Owner, u.Id, picks, opt);
        }

        /// <summary>An upper bound on how far from its hex a ship can affect anything in one activation: its move (overclocked
        /// if it can be), plus the longer of its weapon range and its active ability's range and dash, plus slack for area
        /// effects. A ship farther than this from the leader cannot touch it this activation.</summary>
        public static int Reach(MatchState s, UnitState u)
        {
            int move = Math.Max(u.R.MoveHexes, RosterMovement.Budget(s, u)) * (u.R.Pattern == MovePattern.Diag ? 2 : 1) + (u.R.Pattern == MovePattern.Knight ? 3 : 0);
            if (SliceRules.OverclockCapable(u)) move += SliceRules.OverclockMove * (u.R.Pattern == MovePattern.Diag ? 2 : 1);
            var a = u.R.Active;
            int act = a == null ? 0 : Math.Max(a.Range, u.R.ActiveRange) + a.DashRange + 2;
            return move + Math.Max(u.Weapon.MaxRange + 1, act) + 1;
        }

        static int Hull(MatchState s, int id) { var u = s.Unit(id); return u == null || !u.Alive ? 0 : u.Hp + u.ShieldHp; }

        static int DamageOf(MatchState from, Command c, int leaderId, int hp0, ref int checkedCount)
        {
            checkedCount++;
            var copy = from.Clone();
            if (!RulesEngine.Apply(copy, c.Clone()).Accepted) return 0;
            return Math.Max(0, hp0 - Hull(copy, leaderId));
        }

        static void Try(MatchState from, Command move, Command action, int seat, int leaderId, ThreatReport rep)
        {
            rep.CommandsChecked++;
            var copy = from.Clone();
            var r = RulesEngine.Apply(copy, action.Clone());
            if (!r.Accepted) return;
            var leader = copy.Unit(leaderId);
            if (leader != null && leader.Alive) return;
            if (rep.Lines.Count >= MaxLines * 4) return;
            var u = from.Unit(action.UnitId);
            string what = action.Type == CommandType.Attack ? $"fire {u.Weapon.Name}" : $"use {u.R.ActiveName}";
            string where = move != null ? $"move to {move.Target} and " : "";
            string detail = "";
            if (action.Type == CommandType.Attack)
            {
                var o = RosterCombat.ResolveBasic(from, u, action.Target);
                if (o.Legal) detail = " " + AttackExplainer.Arithmetic(from, o);
            }
            rep.Lines.Add(new LethalLine
            {
                AttackerId = u.Id, Move = move?.Clone(), Action = action.Clone(),
                Text = $"{u.Def.Name} can {where}{what}.{detail}",
            });
        }
    }

    /// <summary>Plain-language account of one attack, from the same resolution the rules commit.</summary>
    public static class AttackExplainer
    {
        /// <summary>"7 raw − 2 armour − 1 cover = 4; 1 absorbed by shields; hull 5 → 2."</summary>
        public static string Arithmetic(MatchState s, AttackOutcome o)
        {
            var r = o.Roster;
            if (r == null) return $"{o.FinalDamage} damage; {o.TargetHpBefore} → {o.TargetHpAfter}.";
            var parts = new List<string> { $"{r.Raw} raw" };
            if (r.Armor > 0) parts.Add($"− {r.Armor} armour");
            if (r.Cover > 0) parts.Add($"− {r.Cover} cover");
            if (r.Reduction > 0) parts.Add($"− {r.Reduction} reduction");
            if (r.Redirected > 0) parts.Add($"− {r.Redirected} redirected");
            string text = string.Join(" ", parts) + $" = {r.Effective}";
            if (r.ShieldAbsorbed > 0) text += $"; {r.ShieldAbsorbed} absorbed by shields";
            var hit = s.Unit(r.TargetId);
            text += $"; {(hit != null ? hit.Def.Name + " hull" : "hull")} {o.TargetHpBefore} → {Math.Max(0, o.TargetHpAfter)}.";
            return text;
        }

        /// <summary>Full preview lines: who is hit, interception, the arithmetic, modifiers, and whether the battle ends.</summary>
        public static List<string> Explain(MatchState s, UnitState attacker, Hex targetHex, bool overclock = false)
        {
            var lines = new List<string>();
            var o = RosterCombat.ResolveBasic(s, attacker, targetHex, overclock: overclock);
            if (!o.Legal) { lines.Add(o.Reason); return lines; }
            var r = o.Roster;
            var aimed = s.UnitAt(targetHex);
            var hit = r != null ? s.Unit(r.TargetId) : aimed;
            if (r != null && r.GuardianId != 0 && aimed != null && r.GuardianId != aimed.Id)
                lines.Add($"{hit.Def.Name} intercepts this shot aimed at {aimed.Def.Name} ({r.GuardSource}). {aimed.Def.Name} takes nothing.");
            lines.Add($"{attacker.Weapon.Name}: {Arithmetic(s, o)}");
            var mods = (r?.Breakdown ?? o.Breakdown).Where(b => !b.Contains("absorbed by shields") && !b.StartsWith("−") && !b.Contains("intercepts")).ToList();
            if (mods.Count > 0) lines.Add("Modifiers: " + string.Join("; ", mods) + ".");
            if (o.RiposteDamage > 0) lines.Add($"{attacker.Def.Name} takes {o.RiposteDamage} back.");
            bool leader = hit != null && s.Players[hit.Owner].CommanderId == hit.Id;
            if (o.TargetDestroyed && leader) lines.Add($"This destroys {hit.Def.Name}, their leader. The battle ends immediately.");
            else if (o.TargetDestroyed) lines.Add($"This destroys {hit.Def.Name}.");
            else if (leader) lines.Add($"{hit.Def.Name} is their leader and survives this shot.");
            return lines;
        }
    }
}
