using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>
    /// The slice opponent (Prompt 5). It sees and submits exactly what a player could: legal commands from
    /// <see cref="LegalActions"/>, judged by applying them to copies with <see cref="RulesEngine.Apply"/>. In order:
    /// 1. take a legal lethal line on the enemy leader if one exists this activation;
    /// 2. if its own leader can be killed by the enemy's next activation, pick the candidate that leaves the fewest lethal
    ///    enemy lines (moving or turning the leader, declaring a guard, striking the threatening ship, screening);
    /// 3. otherwise follow the Commandfall Tactical planner, reseeded from the state hash so the choice depends only on
    ///    the state (no hidden randomness, identical after a reload);
    /// then adjust the plan with the slice's own orders (turning a capital ship that has nothing in its arc, and an
    /// overclocked shot when it is lethal and the ship is cool). No stat bonuses and no knowledge the side lacks.
    /// </summary>
    public sealed class SliceAI
    {
        /// <summary>Most candidate commands examined in a defensive search (bounded effort).</summary>
        public int DefenceBudget = 18;
        /// <summary>Most orders a defensive search may evaluate (a count, never a clock, so choices depend only on state).</summary>
        public int DefenceWork = 6000;
        public string LastReason { get; private set; } = "";
        public int LastEvaluations { get; private set; }

        public static int StableSeed(MatchState s) => (int)(Serialization.StateHash(s) & 0x7fffffff);

        public Command Choose(MatchState s)
        {
            LastEvaluations = 0;
            int me = s.ActiveSeat, enemy = 1 - me;

            // 1. A legal lethal line on the enemy leader: take it (its first order; the next call finishes the activation).
            var win = CommandThreat.Assess(s, enemy);
            LastEvaluations += win.CommandsChecked;
            if (win.Lines.Count > 0)
            {
                var line = win.Lines[0];
                LastReason = "Lethal line: " + line.Text;
                return (line.Move ?? line.Action).Clone();
            }

            var baseline = new RosterAI(AIDifficulty.Tactical, StableSeed(s)).ChooseAction(s);

            // 2. Our leader can be killed by the enemy's next activation: defend first.
            var danger = CommandThreat.Assess(s, me);
            LastEvaluations += danger.CommandsChecked;
            if (danger.Lines.Count > 0)
            {
                var pick = Defend(s, me, baseline);
                if (pick != null) return pick;
            }

            // 3. Our ships could destroy their leader over this round if all of them chose it: focus fire.
            var ours = CommandThreat.RoundExposure(s, enemy);
            LastEvaluations += ours.CommandsChecked;
            if (ours.Critical)
            {
                var focus = Focus(s, me);
                if (focus != null) return focus;
            }

            // 4. Their ships could do the same to ours: reduce the exposure.
            var theirs = CommandThreat.RoundExposure(s, me);
            LastEvaluations += theirs.CommandsChecked;
            if (theirs.Critical)
            {
                var pick = Defend(s, me, baseline);
                if (pick != null) return pick;
            }

            LastReason = "Tactical plan";
            return Refine(s, baseline);
        }

        /// <summary>The order that puts the most damage on the enemy leader this activation (directly, or by moving into a
        /// firing position); null when no ship of ours can reach it.</summary>
        Command Focus(MatchState s, int me)
        {
            var leader = s.Commander(1 - me);
            int hull = leader.Hp + leader.ShieldHp;
            Command best = null; int bestDmg = 0;
            var legal = LegalActions.EnumerateRoster(s, 64, 24);
            foreach (var c in legal.Where(c => c.Type == CommandType.Attack || c.Type == CommandType.Ability))
            {
                int d = Damage(s, c, leader.Id, hull);
                if (d > bestDmg) { bestDmg = d; best = c; }
            }
            foreach (var mv in legal.Where(c => c.Type == CommandType.Move && c.Option != SliceRules.TurnOption))
            {
                var moved = s.Clone();
                if (!RulesEngine.Apply(moved, mv.Clone()).Accepted || moved.IsOver || moved.ActiveUnitId != mv.UnitId) continue;
                LastEvaluations++;
                foreach (var a in LegalActions.EnumerateRoster(moved, 64, 24).Where(x => x.Type == CommandType.Attack || x.Type == CommandType.Ability))
                {
                    int d = Damage(moved, a, leader.Id, hull);
                    if (d > bestDmg) { bestDmg = d; best = mv; }
                }
            }
            if (best != null) LastReason = $"Focus fire on their leader ({bestDmg} damage this activation)";
            return best;
        }

        int Damage(MatchState from, Command c, int leaderId, int hull)
        {
            LastEvaluations++;
            var copy = from.Clone();
            if (!RulesEngine.Apply(copy, c.Clone()).Accepted) return 0;
            var l = copy.Unit(leaderId);
            return hull - (l == null || !l.Alive ? 0 : l.Hp + l.ShieldHp);
        }

        Command Defend(MatchState s, int me, Command baseline)
        {
            var leaderId = s.Players[me].CommanderId;
            var danger = CommandThreat.Assess(s, me);
            var exposure = CommandThreat.RoundExposure(s, me);
            var threatening = new HashSet<int>(danger.Lines.Select(l => l.AttackerId).Concat(exposure.ByShip.Select(x => x.unitId)));
            var legal = LegalActions.EnumerateRoster(s, 128, 24);
            var candidates = new List<Command>();
            void Take(IEnumerable<Command> cs) { foreach (var c in cs) if (candidates.Count < DefenceBudget && !candidates.Any(x => Same(x, c))) candidates.Add(c); }
            Take(new[] { baseline });
            Take(legal.Where(c => c.Type == CommandType.Attack && threatening.Contains(s.UnitAt(c.Target)?.Id ?? -1)));
            Take(legal.Where(c => c.Type == CommandType.Ability && IsGuard(s.Unit(c.UnitId)) && c.Picks.Contains(s.Unit(leaderId).Position)));
            Take(legal.Where(c => c.UnitId == leaderId && c.Type == CommandType.Move)
                      .OrderByDescending(c => threatening.Count == 0 ? 0 : threatening.Min(id => Hex.Distance(c.Target, s.Unit(id).Position))).Take(10));
            Take(legal.Where(c => c.Type == CommandType.Ability).Take(6));
            Take(legal.Where(c => c.Type == CommandType.Move && c.UnitId != leaderId)
                      .OrderBy(c => Hex.Distance(c.Target, s.Unit(leaderId).Position)));

            (int lines, int over, int total) Score(MatchState st)
            {
                var d = CommandThreat.Assess(st, me);
                var x = CommandThreat.RoundExposure(st, me);
                LastEvaluations += d.CommandsChecked + x.CommandsChecked;
                return (d.Lines.Count, Math.Max(0, x.Total - x.LeaderHull + 1), x.Total);
            }
            var start = (danger.Lines.Count, Math.Max(0, exposure.Total - exposure.LeaderHull + 1), exposure.Total);
            Command best = null; var bestScore = start;
            int work0 = LastEvaluations;
            foreach (var c in candidates)
            {
                if (LastEvaluations - work0 > DefenceWork) break;
                var after = s.Clone();
                if (!RulesEngine.Apply(after, c.Clone()).Accepted) continue;
                LastEvaluations++;
                if (after.IsOver) { if (after.Winner == me) return c; continue; }
                // If the activation continues, assume it ends with a wait (the cheapest finish) before the enemy acts.
                if (after.ActiveSeat == me && after.ActiveUnitId != 0) RulesEngine.Apply(after, Command.Wait(me, after.ActiveUnitId));
                if (after.IsOver) continue;
                var sc = Score(after);
                if (best == null ? sc.CompareTo(start) <= 0 : sc.CompareTo(bestScore) < 0) { best = c; bestScore = sc; }
                if (sc.Item1 == 0 && sc.Item2 == 0) break;
            }
            if (best == null) { LastReason = "Leader exposed; no candidate improves it"; return null; }
            LastReason = $"Defence: lethal lines {start.Item1} to {bestScore.Item1}, focus-fire margin {start.Item2} to {bestScore.Item2}";
            return best;
        }

        static bool IsGuard(UnitState u) => u != null && u.R.Active != null && (u.R.ActiveCode == EffectCode.GUARD || u.R.ActiveCode == EffectCode.INTERCEPT);

        static bool Same(Command a, Command b) =>
            a.Type == b.Type && a.UnitId == b.UnitId && a.Target == b.Target && a.Option == b.Option && a.Picks.SequenceEqual(b.Picks);

        /// <summary>Slice orders the base planner does not know: turn a capital ship whose plan wastes its activation, and
        /// overclock a shot that becomes lethal.</summary>
        Command Refine(MatchState s, Command plan)
        {
            var u = plan.UnitId != 0 ? s.Unit(plan.UnitId) : null;
            if (u == null) return plan;
            int me = u.Owner;
            if (plan.Type == CommandType.Attack && SliceRules.CanOverclock(s, u) == null && SliceRules.Heat(u) == 0)
            {
                var normal = RosterCombat.ResolveBasic(s, u, plan.Target);
                var hot = RosterCombat.ResolveBasic(s, u, plan.Target, overclock: true);
                if (normal.Legal && hot.Legal && !normal.TargetDestroyed && hot.TargetDestroyed)
                {
                    LastReason = "Overclocked shot is lethal";
                    return SliceRules.Order(CommandType.Attack, me, u.Id, plan.Target, SliceRules.OverclockOption);
                }
            }
            if (SliceRules.HasArc(u) && plan.Type == CommandType.Pass && RosterCombat.LegalTargets(s, u).Count == 0)
            {
                for (int f = 0; f < 12; f++)
                {
                    var turn = SliceRules.Order(CommandType.Move, me, u.Id, u.Position + SliceRules.FacingVector(f), SliceRules.TurnOption);
                    var after = s.Clone();
                    if (!RulesEngine.Apply(after, turn.Clone()).Accepted || after.ActiveUnitId != u.Id) continue;
                    if (RosterCombat.LegalTargets(after, after.Unit(u.Id)).Count > 0) { LastReason = "Turned to bring targets into its arc"; return turn; }
                }
            }
            return plan;
        }
    }
}
