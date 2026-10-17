using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace Commandfall.AI
{
    /// <summary>
    /// Opponent for the Full Roster rules. It plans whole activations: for each unit that may activate it tries waiting,
    /// acting in place, and every move followed by the unit's best action, scores the resulting state, and plays the first
    /// command of the best plan (the next call continues the activation). Only commands from
    /// <see cref="LegalActions.EnumerateRoster"/> are ever submitted, through the same rules interface as a human.
    /// Beginner adds noise; Tactical also discounts plans that leave its units exposed to the opponent's next activation.
    /// </summary>
    public sealed class RosterAI
    {
        public AIDifficulty Difficulty;
        readonly Random _rng;
        public int Evaluations { get; private set; }
        /// <summary>Moves considered per unit (closest-to-goal first); keeps decisions fast on the 115-hex boards.</summary>
        public int MoveBreadth = 10;

        public RosterAI(AIDifficulty difficulty, int seed = 1729)
        {
            Difficulty = difficulty;
            _rng = new Random(seed);
        }

        public Command ChooseAction(MatchState real)
        {
            Evaluations = 0;
            int me = real.ActiveSeat;
            var s = Vision.PlanningView(real, me);   // only what this side knows: visible units, its own and revealed mines
            var plans = new List<(Command first, double score)>();
            var units = s.ActiveUnitId != 0 ? new List<UnitState> { s.Unit(s.ActiveUnitId) }
                                           : s.Units.Where(u => u.Owner == me && RosterRules.Eligible(s, u)).OrderBy(u => u.Id).ToList();
            foreach (var u in units)
            {
                if (!RulesEngine.CanActivate(s, u, out _)) continue;
                foreach (var plan in PlansFor(s, u, me)) plans.Add(plan);
            }
            if (plans.Count == 0) return Command.Pass(me);
            plans.Sort((a, b) => b.score.CompareTo(a.score));
            if (Difficulty == AIDifficulty.Beginner && plans.Count > 1)
            {
                double r = _rng.NextDouble();
                int idx = r < 0.6 ? 0 : r < 0.85 ? 1 : Math.Min(plans.Count - 1, 2 + _rng.Next(3));
                return plans[Math.Min(idx, plans.Count - 1)].first;
            }
            return plans[0].first;
        }

        IEnumerable<(Command, double)> PlansFor(MatchState s, UnitState u, int me)
        {
            int seat = u.Owner;
            var results = new List<(Command, double)>();
            // Act from where it stands (or wait).
            foreach (var (cmd, score) in Actions(s, u, me)) results.Add((cmd, score));
            // Move, then act.
            var moves = RosterMovement.Reachable(s, u);
            if (moves.Count > 0)
            {
                var goal = Goal(s, u);
                foreach (var h in moves.Keys.OrderBy(h => s.Map.Paths.Distance(h, goal)).ThenBy(h => h).Take(MoveBreadth)
                                        .Concat(moves.Keys.Where(h => s.ObjectiveIndex(h) >= 0)).Distinct())
                {
                    var mv = Command.Move(seat, u.Id, h);
                    var after = s.Clone();
                    var r = RulesEngine.Apply(after, mv.Clone());
                    if (!r.Accepted) continue;
                    if (after.IsOver) { results.Add((mv, after.Winner == me ? 1e6 : -1e6)); continue; }
                    var au = after.Unit(u.Id);
                    double best;
                    if (after.ActiveUnitId != u.Id || !au.Alive) best = Evaluate(after, me);
                    else best = Actions(after, au, me).Select(x => x.Item2).DefaultIfEmpty(Evaluate(after, me)).Max();
                    results.Add((mv, best + Noise()));
                }
            }
            return results;
        }

        /// <summary>The unit's possible actions from its current hex (attack, active, rescue, wait), each scored.</summary>
        List<(Command, double)> Actions(MatchState s, UnitState u, int me)
        {
            var list = new List<(Command, double)>();
            int seat = u.Owner;
            var cmds = new List<Command> { Command.Wait(seat, u.Id) };
            if (RosterRules.CanAct(s, u, attack: true) == null)
            {
                foreach (var o in RosterCombat.LegalTargets(s, u)) cmds.Add(Command.Attack(seat, u.Id, o.TargetHex));
                foreach (var h in u.Position.Neighbors())
                    if (s.Map.Contains(h) && RosterRules.ValidateRescue(s, u, h).Ok) cmds.Add(Command.Rescue(seat, u.Id, h));
            }
            foreach (var (picks, opt) in RosterAbilities.Enumerate(s, u, Difficulty == AIDifficulty.Beginner ? 12 : 24))
                cmds.Add(Command.Active(seat, u.Id, picks, opt));
            foreach (var c in cmds)
            {
                var after = s.Clone();
                var r = RulesEngine.Apply(after, c.Clone());
                if (!r.Accepted) continue;
                double score;
                if (after.IsOver) score = after.Winner == me ? 1e6 : -1e6;
                else
                {
                    // A pending free step (War Momentum): take the best one.
                    if (after.ActiveUnitId == u.Id && after.Unit(u.Id).HasR(RStatusType.FreeStep))
                    {
                        double bestStep = Evaluate(after, me);
                        foreach (var h in RosterMovement.Reachable(after, after.Unit(u.Id)).Keys)
                        {
                            var a2 = after.Clone();
                            if (RulesEngine.Apply(a2, Command.Move(seat, u.Id, h)).Accepted) bestStep = Math.Max(bestStep, Evaluate(a2, me));
                        }
                        score = bestStep;
                    }
                    else score = Evaluate(after, me);
                }
                if (c.Type == CommandType.Pass) score -= 1.5;   // prefer doing something useful
                list.Add((c, score + Noise()));
            }
            return list;
        }

        double Noise() => _rng.NextDouble() * (Difficulty == AIDifficulty.Beginner ? 3.0 : 0.05);

        /// <summary>Where a unit wants to be: the enemy leader for fighters, the nearest objective for the rest when objectives are contested.</summary>
        static Hex Goal(MatchState s, UnitState u)
        {
            var leader = s.Commander(1 - u.Owner);
            if (RosterCombat.IsLeader(s, u))
            {
                // Leaders hang back behind their army, near the closest objective they own or the home row.
                var home = s.Units.Where(x => x.OnBoard && x.Owner == u.Owner && x.Id != u.Id).Select(x => x.Position).DefaultIfEmpty(u.Position);
                var c = home.Aggregate(new Hex(0, 0), (acc, h) => acc + h);
                int n = Math.Max(1, home.Count());
                return new Hex(c.Q / n, c.R / n + (u.Owner == 0 ? 1 : -1));
            }
            if (leader != null && leader.OnBoard) return leader.Position;
            return s.Objectives.Count > 0 ? s.Objectives[0].Hex : Hex.Zero;
        }

        // ------------------------------------------------------------------ evaluation

        public double Evaluate(MatchState s, int me)
        {
            Evaluations++;
            if (s.IsOver) return s.Winner == me ? 1e6 : -1e6;
            double score = Side(s, me) - Side(s, 1 - me);
            // Objectives matter at the round limit; weight grows as the limit nears.
            double late = s.Config.RoundLimit > 0 ? Math.Min(1.0, (double)s.Round / s.Config.RoundLimit) : 0.3;
            foreach (var o in s.Objectives)
                if (o.Owner >= 0) score += (o.Owner == me ? 1 : -1) * (8 + 30 * late * late);
            return score;
        }

        double Side(MatchState s, int seat)
        {
            double v = 0;
            var enemyLeader = s.Commander(1 - seat);
            var ownLeader = s.Commander(seat);
            foreach (var u in s.Units)
            {
                if (u.Owner != seat || !u.Alive) continue;
                bool leader = ownLeader != null && u.Id == ownLeader.Id;
                double worth = leader ? 60 : RosterContent.Cost(u.Def) * 6 + 2;
                double frac = u.MaxHp > 0 ? (double)u.Hp / u.MaxHp : 0;
                double unit = worth * (0.45 + 0.55 * frac);
                if (u.HasR(RStatusType.Captured) || u.HasR(RStatusType.Boarded)) unit *= 0.25;
                unit += u.ShieldHp * 0.8;
                if (u.HasR(RStatusType.Protected) || u.HasR(RStatusType.Intercepted)) unit += leader ? 6 : 2;
                if (u.HasR(RStatusType.Rooted)) unit -= 1.5;
                if (u.HasR(RStatusType.Disabled) || u.HasR(RStatusType.Silenced)) unit -= 2;
                if (u.HasR(RStatusType.Marked)) unit -= 1.5;
                if (u.HasR(RStatusType.Exposed)) unit -= 1.5;
                if (u.HasR(RStatusType.AttackBuff)) unit += 1.5;
                if (u.Cooldown == 0 && !u.R.IsToken) unit += 0.5;
                if (u.OnBoard && !leader && enemyLeader != null && enemyLeader.OnBoard)
                {
                    int d = s.Map.Paths.Distance(u.Position, enemyLeader.Position);
                    int reach = u.Weapon.MaxRange + Math.Max(1, u.R.MoveHexes);
                    unit += Math.Max(0, 12 - Math.Max(0, d - reach)) * 0.45;
                    if (d <= u.Weapon.MaxRange) unit += 4;
                }
                if (u.OnBoard && s.ObjectiveIndex(u.Position) >= 0) unit += 3;
                if (leader && u.OnBoard)
                {
                    // Leader danger: enemy units that could hit it now.
                    int threats = s.Units.Count(e => e.OnBoard && e.Owner != seat && !RosterRules.Inert(e)
                                                     && Hex.Distance(e.Position, u.Position) <= e.Weapon.MaxRange + Math.Max(1, e.R.MoveHexes));
                    unit -= threats * 7;
                    int guards = u.Position.Neighbors().Count(n => { var x = s.UnitAt(n); return x != null && x.Owner == seat; });
                    unit += Math.Min(guards, 3) * 3;
                }
                v += unit;
            }
            return v;
        }
    }
}
