using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace Commandfall.AI
{
    /// <summary>
    /// Heuristic opponent. It only ever produces commands from <see cref="LegalActions.Enumerate"/> and submits them through the
    /// same rules interface as a human, so it can never perform an illegal action.
    /// Beginner: one-ply evaluation with deliberate imprecision. Tactical: two-ply (considers the opponent's best reply).
    /// </summary>
    public sealed class TacticalAI
    {
        public AIDifficulty Difficulty;
        /// <summary>Search depth for Tactical (2 = considers the opponent's best reply; 1 = greedy, used by the fast balance harness).</summary>
        public int Depth = 2;
        readonly Random _rng;
        public int Evaluations { get; private set; }

        public TacticalAI(AIDifficulty difficulty, int seed = 1729)
        {
            Difficulty = difficulty;
            _rng = new Random(seed);
        }

        // ------------------------------------------------------------------ deployment

        public Command ChooseDeployment(MatchState s, int seat)
        {
            var placements = RulesEngine.AutoDeployment(s, seat);
            // Small, legal variation: occasionally swap two non-Commander placements so mirror matches differ.
            if (placements.Count > 3 && _rng.NextDouble() < 0.5)
            {
                int a = 1 + _rng.Next(placements.Count - 1), b = 1 + _rng.Next(placements.Count - 1);
                var ua = s.Unit(placements[a].UnitId); var ub = s.Unit(placements[b].UnitId);
                if (!ua.IsCommander && !ub.IsCommander)
                {
                    var ha = placements[a].Hex;
                    placements[a] = new Placement(placements[a].UnitId, placements[b].Hex);
                    placements[b] = new Placement(placements[b].UnitId, ha);
                }
            }
            return Command.Deploy(seat, placements);
        }

        // ------------------------------------------------------------------ battle

        RosterAI _roster;

        public Command ChooseAction(MatchState s)
        {
            if (s.IsRoster)
            {
                _roster ??= new RosterAI(Difficulty, _rng.Next());
                var c = _roster.ChooseAction(s);
                Evaluations = _roster.Evaluations;
                return c;
            }
            Evaluations = 0;
            int me = s.ActiveSeat;
            _urgency = Urgency(s);
            s = Vision.FoggedView(s, me);   // with fog of war the AI plans only on what its army can see
            var actions = LegalActions.Enumerate(s, includeLowValue: Difficulty == AIDifficulty.Tactical);
            if (actions.Count == 1) return actions[0];

            // Score every action by one-ply evaluation.
            var scored = new List<(Command cmd, double score, MatchState after)>(actions.Count);
            foreach (var a in actions)
            {
                var after = s.Clone();
                var r = RulesEngine.Apply(after, a.Clone());
                if (!r.Accepted) continue; // defensive; Enumerate only yields legal commands
                if (after.IsOver && after.Winner == me) return a; // immediate win
                scored.Add((a, Evaluate(after, me) + TieBreak(a) + Backtrack(s, a), after));
            }
            if (scored.Count == 0) return Command.Pass(me);
            scored.Sort((x, y) => y.score.CompareTo(x.score));

            if (Difficulty == AIDifficulty.Beginner)
            {
                // Pick among the top few with noise; occasionally miss the best move.
                int pool = Math.Min(scored.Count, 4);
                double r = _rng.NextDouble();
                int idx = r < 0.55 ? 0 : r < 0.80 ? 1 : r < 0.93 ? 2 : pool - 1;
                return Remember(s, scored[Math.Min(idx, scored.Count - 1)].cmd);
            }

            if (Depth <= 1) return Remember(s, scored[0].cmd);

            // Tactical: minimax over the best candidates against the opponent's strongest replies.
            int breadth = Math.Min(scored.Count, 14);
            double best = double.NegativeInfinity;
            Command bestCmd = scored[0].cmd;
            for (int i = 0; i < breadth; i++)
            {
                var (cmd, oneply, after) = scored[i];
                double worst = oneply;
                if (!after.IsOver)
                {
                    worst = double.PositiveInfinity;
                    var replies = LegalActions.Enumerate(after, includeLowValue: false);
                    var replyScores = new List<double>(replies.Count);
                    foreach (var rep in replies)
                    {
                        var after2 = after.Clone();
                        var rr = RulesEngine.Apply(after2, rep.Clone());
                        if (!rr.Accepted) continue;
                        replyScores.Add(Evaluate(after2, me));
                    }
                    if (replyScores.Count == 0) worst = oneply;
                    else worst = replyScores.Min();
                }
                // Blend so the AI still values its own immediate gains.
                double score = worst * 0.85 + oneply * 0.15;
                if (score > best) { best = score; bestCmd = cmd; }
            }
            return Remember(s, bestCmd);
        }

        /// <summary>
        /// The attacker's urgency, fixed for the whole decision: the share of the remaining activations its army needs just
        /// to walk into striking range of the enemy Commander (routes around terrain, one unit per own activation). On a
        /// small board this is near zero and the clock alone drives aggression; on the 115-hex battlefields the approach
        /// itself takes a large part of the activation limit, so the attacker must start pressing earlier.
        /// </summary>
        double _urgency;

        static double Urgency(MatchState s)
        {
            int loser = StalemateLoser(s);
            if (loser < 0 || s.Config.ActivationLimit <= 0) return 0;
            var target = s.Commander(1 - loser);
            if (target == null || !target.OnBoard) return 0;
            int needOwn = 0;
            foreach (var u in s.Units)
            {
                if (!u.OnBoard || u.Owner != loser || u.IsCommander) continue;
                int gap = s.Map.Paths.Distance(u.Position, target.Position) - Math.Max(1, u.Weapon.MaxRange);
                int move = Math.Max(1, BoardQueries.EffectiveMove(s, u));
                if (gap > 0) needOwn += (gap + move - 1) / move;
            }
            int remaining = Math.Max(1, s.Config.ActivationLimit - s.ActivationIndex);
            return Math.Min(1.0, 2.0 * needOwn / remaining);
        }

        // Short memory against shuttling: one-ply search has no notion of history, so a unit can bounce between two hexes
        // forever when each looks better from the other. Moving a unit straight back to where it just came from costs a
        // little. Lives in the AI instance only: replays re-apply commands, never re-run the AI.
        readonly Dictionary<int, Hex> _cameFrom = new Dictionary<int, Hex>();

        double Backtrack(MatchState s, Command a) =>
            a.Type == CommandType.Move && _cameFrom.TryGetValue(a.UnitId, out var h) && h == a.Target ? -8.0 : 0.0;

        Command Remember(MatchState s, Command c)
        {
            if (c.Type == CommandType.Move && s.Unit(c.UnitId) is UnitState u) _cameFrom[c.UnitId] = u.Position;
            else if (c.Type != CommandType.Pass) _cameFrom.Remove(c.UnitId);
            return c;
        }

        double TieBreak(Command a) => _rng.NextDouble() * 0.01 + (a.Type == CommandType.Pass ? -0.5 : 0);

        // ------------------------------------------------------------------ evaluation

        /// <summary>Static evaluation from <paramref name="me"/>'s perspective. Positive is good for me.</summary>
        public double Evaluate(MatchState s, int me)
        {
            Evaluations++;
            if (s.IsOver) return s.Winner == me ? 100000 : s.Winner < 0 ? 0 : -100000;
            int them = 1 - me;
            double score = 0;
            score += SideValue(s, me) - SideValue(s, them);

            // Anti-stalemate awareness: the side that would lose when the activation limit hits must press harder over time.
            int stalemateLoser = StalemateLoser(s);
            double progress = s.Config.ActivationLimit > 0 ? Math.Min(1.0, (double)s.ActivationIndex / s.Config.ActivationLimit) : 0;
            progress = Math.Max(progress, _urgency);
            // The attacker (who loses a stalemate) presses from the start and harder as the limit nears; the defender stays patient.
            double myAggression = me == stalemateLoser ? 1.35 + 2.5 * progress * progress : 0.9;
            double theirAggression = them == stalemateLoser ? 1.35 + 2.5 * progress * progress : 0.9;

            // No-man's land decides defender denial: the attacker who holds it alone at the limit wins, so as the clock runs
            // down the attacker wants units there and the defender wants to contest it (or clear it).
            if (s.Config.Stalemate == StalemateRule.DefenderDenial && s.Config.ActivationLimit > 0 && stalemateLoser >= 0)
            {
                double clock = Math.Min(1.0, (double)s.ActivationIndex / s.Config.ActivationLimit);
                int atkIn = 0, defIn = 0;
                foreach (var u in s.Units)
                    if (u.OnBoard && s.Map.ZoneSeat(u.Position) < 0) { if (u.Owner == stalemateLoser) atkIn++; else defIn++; }
                double hold = atkIn == 0 ? 0 : defIn == 0 ? 1.0 + 0.15 * Math.Min(atkIn, 3) : 0.25;
                double w = 25 + 140 * clock * clock;
                score += (me == stalemateLoser ? 1 : -1) * w * hold;
            }

            // Distances are routes, not straight lines: around blocking terrain (static, cached per map) and around the
            // other side's units, which nobody can walk through — so a clogged centre makes a flank shorter.
            var routeTo = new int[2][];
            for (int seat = 0; seat < 2; seat++)
            {
                var c = s.Commander(seat);
                if (c != null && c.OnBoard) routeTo[seat] = RouteField(s, c.Position, seat);
            }

            // Commander danger: who can hit each Commander right now, and who could reach striking range next activation.
            var myCmd = s.Commander(me);
            var theirCmd = s.Commander(them);
            score -= CommanderDanger(s, myCmd, them, routeTo[me]) * 1.15 * theirAggression;
            score += CommanderDanger(s, theirCmd, me, routeTo[them]) * myAggression;

            // Units exposed to immediate attacks by the side to move.
            int mover = s.ActiveSeat;
            foreach (var u in s.BoardUnits())
            {
                if (u.IsCommander) continue;
                double risk = ImmediateRisk(s, u, 1 - u.Owner);
                double val = UnitWorth(u);
                double weight = u.Owner == mover ? 0.35 : 0.8; // the side to move can still react
                if (u.Owner == me) score -= risk * val * weight / myAggression; else score += risk * val * weight / theirAggression;
            }

            // Positional nudges.
            foreach (var u in s.BoardUnits())
            {
                double p = 0;
                var t = s.Map.Terrain(u.Position);
                if (t == TerrainType.Elevated && u.Weapon.Kind == WeaponKind.Ranged) p += 3;
                if (BoardQueries.CoverSource(s, u) != null) p += 1.5;
                if (u.Guarded) p += 2.5;
                if (u.HasStatus(StatusType.Ward)) p += 3;
                if (u.HasStatus(StatusType.Overheated)) p -= 2;
                if (u.HasStatus(StatusType.Lien)) p -= 3;
                foreach (var a in u.Abilities) if (a.Charges != 0) p += 1;
                var enemyCmd = s.Commander(1 - u.Owner);
                double aggression = u.Owner == me ? myAggression : theirAggression;
                if (enemyCmd != null && enemyCmd.OnBoard && !u.IsCommander)
                    p -= RouteDistance(s, routeTo[enemyCmd.Owner], u.Position, enemyCmd.Position) * 0.9 * aggression * aggression; // pressure the enemy Commander
                else if (!u.IsCommander && s.Config.FogOfWar)
                    p -= s.Map.Paths.Distance(u.Position, EnemyHome(s, 1 - u.Owner)) * 0.7 * aggression * aggression; // fog: advance to find it
                if (u.IsCommander)
                {
                    int guards = u.Position.Neighbors().Count(n => { var x = s.UnitAt(n); return x != null && x.Owner == u.Owner; });
                    p += Math.Min(guards, 3) * 2.5;
                }
                score += u.Owner == me ? p : -p;
            }
            return score;
        }

        /// <summary>
        /// Steps from every hex to <paramref name="target"/> for units attacking it: blocking terrain and bulwarks are
        /// walls (from the cached static topology), and so are the units of <paramref name="defenderSeat"/>, which
        /// attackers cannot pass. One breadth-first search over the board per side per evaluation (≈115 hexes).
        /// </summary>
        public static int[] RouteField(MatchState s, Hex target, int defenderSeat)
        {
            var paths = s.Map.Paths;
            int n = paths.Count, start = paths.IndexOf(target);
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = int.MaxValue;
            if (start < 0) return dist;
            var blocked = new bool[n];
            foreach (var u in s.Units)
                if (u.OnBoard && u.Owner == defenderSeat) { int i = paths.IndexOf(u.Position); if (i >= 0) blocked[i] = true; }
            foreach (var b in s.Bulwarks) { int i = paths.IndexOf(b.Hex); if (i >= 0) blocked[i] = true; }
            var queue = new int[n];
            int head = 0, tail = 0;
            dist[start] = 0; queue[tail++] = start;
            while (head < tail)
            {
                int v = queue[head++];
                for (int d = 0; d < 6; d++)
                {
                    int w = paths.Adjacent[v * 6 + d];
                    if (w < 0 || dist[w] != int.MaxValue) continue;
                    dist[w] = dist[v] + 1;
                    // Attackers can step next to a blocker (and attack from there) but never through it.
                    if (paths.Walkable[w] && !blocked[w]) queue[tail++] = w;
                }
            }
            return dist;
        }

        static int RouteDistance(MatchState s, int[] field, Hex from, Hex to)
        {
            if (field != null)
            {
                int i = s.Map.Paths.IndexOf(from);
                if (i >= 0 && field[i] != int.MaxValue) return field[i];
            }
            return s.Map.Paths.Distance(from, to) + 4;   // walled off by units right now: the static route, plus a detour
        }

        /// <summary>Fog of war: where an unseen enemy army most likely is — the middle of its deployment zone.</summary>
        static Hex EnemyHome(MatchState s, int seat)
        {
            var zone = s.Map.DeploymentZone(seat);
            if (zone.Count == 0) return Hex.Zero;
            int q = 0, r = 0;
            foreach (var h in zone) { q += h.Q; r += h.R; }
            var c = new Hex(q / zone.Count, r / zone.Count);
            return zone.OrderBy(h => Hex.Distance(h, c)).ThenBy(h => h).First();
        }

        /// <summary>Seat that would lose if the activation limit were reached now.</summary>
        static int StalemateLoser(MatchState s)
        {
            if (s.Config.ActivationLimit <= 0) return -1;
            if (s.Config.Stalemate == StalemateRule.DefenderDenial) return 1 - s.Config.DefenderSeat;
            int v0 = RulesEngine.ArmyValue(s, 0), v1 = RulesEngine.ArmyValue(s, 1);
            if (v0 != v1) return v0 > v1 ? 1 : 0;
            int h0 = s.Commander(0)?.Hp ?? 0, h1 = s.Commander(1)?.Hp ?? 0;
            if (h0 != h1) return h0 > h1 ? 1 : 0;
            return s.Config.FirstSeat;
        }

        static double UnitWorth(UnitState u) => u.Def.Cost * 10 * (0.45 + 0.55 * u.Hp / Math.Max(1, u.MaxHp));

        static double SideValue(MatchState s, int seat)
        {
            double v = 0;
            foreach (var u in s.Units)
            {
                if (!u.Alive || u.Owner != seat) continue;
                v += u.IsCommander ? 60 + u.Hp * 45 : UnitWorth(u);
            }
            return v;
        }

        /// <summary>Expected fraction of a unit lost to immediate legal attacks by <paramref name="attackerSeat"/>.</summary>
        static double ImmediateRisk(MatchState s, UnitState target, int attackerSeat)
        {
            int dmg = 0;
            foreach (var a in s.Units)
            {
                if (!a.OnBoard || a.Owner != attackerSeat || a.HasStatus(StatusType.Dazed)) continue;
                var o = Combat.Resolve(s, a, target.Position);
                if (o.Legal) dmg = Math.Max(dmg, o.FinalDamage);
            }
            if (dmg <= 0) return 0;
            return dmg >= target.Hp ? 1.0 : (double)dmg / Math.Max(1, target.Hp) * 0.6;
        }

        static double CommanderDanger(MatchState s, UnitState cmd, int enemySeat, int[] routeToCmd)
        {
            if (cmd == null || !cmd.OnBoard) return 0;
            double danger = 0;
            int lethal = 0;
            foreach (var e in s.Units)
            {
                if (!e.OnBoard || e.Owner != enemySeat) continue;
                var o = Combat.Resolve(s, e, cmd.Position);
                if (o.Legal)
                {
                    danger += 18 + o.FinalDamage * 14;
                    lethal += o.FinalDamage;
                    continue;
                }
                // Could it reach a firing position with one move? It has to walk there (around rocks and our units).
                var (min, max) = (e.Weapon.MinRange, e.Weapon.MaxRange + 1);
                int d = Hex.Distance(e.Position, cmd.Position);
                int route = RouteDistance(s, routeToCmd, e.Position, cmd.Position);
                int reach = BoardQueries.EffectiveMove(s, e);
                if (route - reach <= max && d + reach >= min) danger += 9 + e.Weapon.Damage * 5;
            }
            if (lethal >= cmd.Hp) danger += 120;
            return danger;
        }
    }
}
