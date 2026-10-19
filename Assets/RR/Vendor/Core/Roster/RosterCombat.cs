using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>One damage instance to resolve (a basic attack, an ability hit, a splash hit, a mine…).</summary>
    public sealed class HitSpec
    {
        public UnitState Attacker;
        public int Raw;
        /// <summary>ARM ignored (99 = all).</summary>
        public int Pierce;
        /// <summary>Ignores ARM, cover, shields and guards.</summary>
        public bool Unavoidable;
        /// <summary>A single-target attack: GUARD can intercept it, buffs and marks apply.</summary>
        public bool Direct;
        public bool Basic;
        /// <summary>Area damage (AOE/BOMBARD splash and centres): never intercepted.</summary>
        public bool Area;
        public bool IgnoreCover;
        public string Cause;
        public string FxKey;
        public Hex From;
        /// <summary>The planned target hex (for preview text); the actual hit may be intercepted.</summary>
        public Hex TargetHex;
        public bool NoCounter;
    }

    /// <summary>A fully resolved (not yet applied) damage instance. Previews and the authoritative result share it.</summary>
    public sealed class HitResult
    {
        public HitSpec Spec;
        public int OriginalTargetId;
        public int TargetId;
        public int GuardianId;
        /// <summary>How the interception was granted (status index on the original target, Guardian status, passive…).</summary>
        public string GuardSource;
        public int ProtectedStatusIndex = -1;
        public bool GuardianStatusConsumed;
        public int Raw;
        public int Armor;
        public int Cover;
        public int Reduction;
        public int Effective;
        public int Redirected;
        public int ShieldAbsorbed;
        public int HpDamage;
        public int HpAfter;
        public bool Lethal;
        public bool Merciful;
        public int ReflectDamage;
        /// <summary>Statuses consumed by the attack (marks, buffs) so Apply can remove them.</summary>
        public List<(int unitId, RStatusType type)> Consumed = new List<(int, RStatusType)>();
        /// <summary>Once-per-round / once-per-battle passive flags to set on Apply.</summary>
        public List<(int unitId, int bit, bool battle)> Flags = new List<(int, int, bool)>();
        public List<string> Breakdown = new List<string>();
        public int Total => ShieldAbsorbed + HpDamage;
    }

    /// <summary>
    /// The codex damage pipeline: legal target, range and line of sight; GUARD interception; raw damage with buffs, marks
    /// and passives; max(0, raw − ARM − cover); flat reductions; shields first, then HP. No randomness anywhere.
    /// </summary>
    public static class RosterCombat
    {
        public const int ArmorCap = 4;

        // ------------------------------------------------------------------ stats in context

        /// <summary>Effective armor of a unit against a hit (0..4): printed + research + passives + buffs − EXPOSED.</summary>
        public static int Armor(MatchState s, UnitState t, HitSpec hit, List<string> why = null)
        {
            int a = t.BaseArmor;
            a += RosterPassives.ArmorBonus(s, t, hit, why);
            int buff = t.RAmount(RStatusType.ArmorBuff);
            if (buff > 0) { a += buff; why?.Add($"+{buff} ARM (buff)"); }
            int exp = t.RAmount(RStatusType.Exposed);
            if (exp > 0) { a -= exp; why?.Add($"−{exp} ARM (EXPOSED)"); }
            int heat = SliceRules.HeatArmorPenalty(t);
            if (heat > 0) { a -= heat; why?.Add($"−{heat} ARM (OVERHEATED)"); }
            return Math.Max(0, Math.Min(ArmorCap, a));
        }

        /// <summary>Basic attack range of a unit standing on <paramref name="from"/>.</summary>
        public static (int min, int max) BasicRange(MatchState s, UnitState u, Hex from)
        {
            var w = u.Weapon;
            int max = w.MaxRange + RosterPassives.RangeBonus(s, u);
            var deb = u.GetR(RStatusType.RangeDebuff);
            if (deb.HasValue) max -= deb.Value.Amount;
            if (RosterTerrain.IsNebula(s, from) && !RosterPassives.NebulaImmune(s, u)) max -= 1;
            max = Math.Max(1, max);
            return (w.MinRange, Math.Max(w.MinRange, max));
        }

        public static bool IsLeader(MatchState s, UnitState u) => u != null && s.Players[u.Owner].CommanderId == u.Id;

        // ------------------------------------------------------------------ targeting

        /// <summary>Can <paramref name="attacker"/> pick <paramref name="target"/> at all (cloak, concealment, fog)?</summary>
        public static string TargetBlocked(MatchState s, UnitState attacker, UnitState target, Hex from, bool basic)
        {
            if (!Vision.CanSee(s, attacker.Owner, target)) return "You can't see that target.";
            int d = Hex.Distance(from, target.Position);
            bool revealed = target.HasR(RStatusType.Revealed);
            if (!revealed)
            {
                int cloakReach = 2 + RosterPassives.CloakSightBonus(s, attacker);
                bool spotted = RosterPassives.Spotted(s, attacker, target);
                if (target.HasR(RStatusType.Cloaked) && d > cloakReach && !spotted) return $"{target.Def.Name} is CLOAKED: it can only be targeted from {cloakReach} hexes or closer until revealed.";
                var hidden = RosterPassives.ConcealedBeyond(s, target);
                if (hidden > 0 && d > hidden && !spotted) return $"{target.Def.Name} cannot be targeted from more than {hidden} hexes away until it attacks or is revealed.";
                if (basic && attacker.R.IsCapital && RosterPassives.UntargetableByCapitalBeyond(s, target) is int far && far > 0 && d > far)
                    return $"Capital ships cannot target {target.Def.Name} with basic attacks from more than {far} hexes until it is revealed.";
            }
            return null;
        }

        /// <summary>Line of sight for an attack by <paramref name="u"/> (passives that see through friendly ships included).</summary>
        public static LosState LineOfSight(MatchState s, UnitState u, Hex from, Hex to)
        {
            return RosterTerrain.LineOfSight(s, from, to, false, u.Id, u.Owner, RosterPassives.LosPassFilter(s, u, from));
        }

        // ------------------------------------------------------------------ basic attack (preview = authoritative)

        /// <summary>Resolves a basic attack by <paramref name="attacker"/> against the unit or barrier on <paramref name="targetHex"/>.
        /// Pure: never mutates the state. The returned outcome carries the roster <see cref="HitResult"/> in Roster.</summary>
        public static AttackOutcome ResolveBasic(MatchState s, UnitState attacker, Hex targetHex, Hex? fromOverride = null, bool checkAction = true, bool overclock = false)
        {
            var o = new AttackOutcome { AttackerId = attacker.Id, TargetHex = targetHex, Weapon = attacker.Weapon };
            var from = fromOverride ?? attacker.Position;
            o.From = from;
            o.Distance = Hex.Distance(from, targetHex);
            var w = attacker.Weapon;
            if (checkAction)
            {
                var act = RosterRules.CanAct(s, attacker, attack: true);
                if (act != null) return Fail(o, act);
                if (attacker.MovedThisActivation && RosterPassives.NoAttackAfterMove(attacker)) return Fail(o, $"{attacker.Def.Name} cannot make its basic attack in an activation in which it moved (Fixed Battery).");
            }
            var target = s.UnitAt(targetHex);
            if (target != null && target.Id == attacker.Id) target = null;
            var barrier = target == null ? s.BarrierAt(targetHex) : null;
            if (target == null && barrier == null) return Fail(o, "No target there.");
            if (target != null && target.Owner == attacker.Owner) return Fail(o, "You cannot attack your own units.");
            if (barrier != null && barrier.Owner == attacker.Owner) return Fail(o, "You cannot attack your own barrier.");
            o.TargetUnitId = target?.Id ?? 0;
            o.TargetIsBulwark = barrier != null;
            if (target != null)
            {
                var blocked = TargetBlocked(s, attacker, target, from, basic: true);
                if (blocked != null) return Fail(o, blocked);
            }
            var (min, max) = BasicRange(s, attacker, from);
            o.InRange = o.Distance >= min && o.Distance <= max;
            if (!o.InRange)
            {
                if (o.Distance < min) return Fail(o, $"Too close: {w.Name} needs range {min}-{max}.");
                return Fail(o, $"Out of range: {w.Name} reaches {min}-{max}, target is {o.Distance} away.");
            }
            var arc = SliceRules.ArcBlock(attacker, from, targetHex);
            if (arc != null) return Fail(o, arc);
            o.Los = LosState.Clear;
            if (attacker.R.AttackNeedsLos && o.Distance > 1)
            {
                o.Los = LineOfSight(s, attacker, from, targetHex);
                if (o.Los != LosState.Clear && target != null && RosterPassives.SpotterSees(s, attacker, target)) { o.Los = LosState.Clear; o.Breakdown.Add("Long Synapse: a linked scout sees the target"); }
                if (o.Los != LosState.Clear) return Fail(o, BoardQueries.LosText(o.Los) + ".");
            }

            var hit = new HitSpec
            {
                Attacker = attacker, Raw = w.Damage + (overclock ? SliceRules.OverclockDamage : 0), Direct = true, Basic = true, From = from, TargetHex = targetHex, Cause = w.Name, FxKey = w.FxKey,
            };
            if (barrier != null)
            {
                int dmg = Math.Max(0, hit.Raw + RosterPassives.BarrierDamageBonus(s, attacker, true));
                o.BaseDamage = w.Damage;
                o.FinalDamage = dmg;
                o.TargetHpBefore = barrier.Hp;
                o.TargetHpAfter = Math.Max(0, barrier.Hp - dmg);
                o.TargetDestroyed = o.TargetHpAfter <= 0;
                o.Breakdown.Add($"{dmg} damage to the barrier ({barrier.Hp} HP)");
                o.Legal = true;
                return o;
            }
            var r = Compute(s, target, hit, fromOverride);
            if (overclock) r.Breakdown.Insert(0, $"+{SliceRules.OverclockDamage} overclock");
            o.Roster = r;
            o.BaseDamage = w.Damage;
            o.BonusDamage = r.Raw - w.Damage;
            o.Armor = r.Armor; o.Cover = r.Cover;
            o.FinalDamage = r.Total;
            var finalT = s.Unit(r.TargetId);
            o.TargetHpBefore = finalT.Hp;
            o.TargetHpAfter = r.HpAfter;
            o.TargetDestroyed = r.Lethal;
            o.GuardConsumed = r.GuardianId != 0;
            o.RiposteDamage = r.ReflectDamage + r.Redirected;
            o.AttackerHpAfter = attacker.Hp - o.RiposteDamage;
            o.Breakdown.AddRange(r.Breakdown);
            o.Legal = true;
            return o;
        }

        static AttackOutcome Fail(AttackOutcome o, string reason) { o.Legal = false; o.Reason = reason; return o; }

        public static List<AttackOutcome> LegalTargets(MatchState s, UnitState attacker, Hex? from = null, bool checkAction = true)
        {
            var list = new List<AttackOutcome>();
            foreach (var u in s.Units)
            {
                if (!u.OnBoard || u.Owner == attacker.Owner) continue;
                var o = ResolveBasic(s, attacker, u.Position, from, checkAction);
                if (o.Legal) list.Add(o);
            }
            foreach (var b in s.Barriers)
            {
                if (b.Owner == attacker.Owner || s.UnitAt(b.Hex) != null) continue;
                var o = ResolveBasic(s, attacker, b.Hex, from, checkAction);
                if (o.Legal) list.Add(o);
            }
            return list;
        }

        // ------------------------------------------------------------------ the pipeline

        /// <summary>Resolves one hit on <paramref name="target"/> (pure).</summary>
        public static HitResult Compute(MatchState s, UnitState target, HitSpec hit, Hex? attackerFrom = null)
        {
            var r = new HitResult { Spec = hit, OriginalTargetId = target.Id, TargetId = target.Id };
            var a = hit.Attacker;
            var from = attackerFrom ?? (a != null ? a.Position : hit.From);
            int raw = hit.Raw;

            // ---- GUARD interception (direct, avoidable attacks only)
            var t = target;
            int guardReduction = 0;
            if (hit.Direct && !hit.Area && !hit.Unavoidable && a != null)
            {
                var g = FindGuardian(s, target, a, hit, out var src, out int statusIndex, out guardReduction, out bool guardianStatus);
                if (g != null)
                {
                    r.GuardianId = g.Id; r.TargetId = g.Id; r.GuardSource = src; r.ProtectedStatusIndex = statusIndex; r.GuardianStatusConsumed = guardianStatus;
                    t = g;
                    r.Breakdown.Add($"{g.Def.Name} intercepts the attack ({src})");
                    if (guardReduction > 0) { raw -= guardReduction; r.Breakdown.Add($"−{guardReduction} raw ({src})"); }
                }
            }
            int distance = Hex.Distance(from, t.Position);
            bool ranged = distance >= 2;

            // ---- raw damage: buffs, marks, passives, debuffs, research
            if (a != null && (hit.Direct || hit.Basic) && !hit.Unavoidable)
            {
                raw += AttackerBonuses(s, a, t, hit, from, distance, r);
                if (t.Id == target.Id)
                {
                    var mark = t.GetR(RStatusType.Marked);
                    if (mark.HasValue && mark.Value.Amount > 0 && RosterPassives.CanBenefitFromBuffs(a))
                    { raw += mark.Value.Amount; r.Breakdown.Add($"+{mark.Value.Amount} MARKED"); r.Consumed.Add((t.Id, RStatusType.Marked)); }
                    else if (mark.HasValue) r.Consumed.Add((t.Id, RStatusType.Marked));
                }
                var deb = a.GetR(RStatusType.AttackDebuff);
                if (deb.HasValue && raw > 1)
                {
                    int before = raw; raw = Math.Max(1, raw - deb.Value.Amount);
                    r.Breakdown.Add($"−{before - raw} (Feedback Field)");
                    r.Consumed.Add((a.Id, RStatusType.AttackDebuff));
                }
            }
            int rawReduce = hit.Unavoidable ? 0 : RosterPassives.RawReduction(s, t, a, hit, r.Breakdown);
            raw = Math.Max(0, raw - rawReduce);
            r.Raw = raw;

            // ---- ARM and cover
            int effective;
            if (hit.Unavoidable)
            {
                effective = raw;
                r.Breakdown.Add("unavoidable: ignores ARM, cover and shields");
            }
            else
            {
                var why = new List<string>();
                int armor = Armor(s, t, hit, why);
                int pierce = hit.Pierce + (a != null ? RosterPassives.PierceBonus(s, a, t, hit, from, why) : 0);
                int armorAfter = Math.Max(0, armor - pierce);
                r.Armor = armorAfter;
                r.Breakdown.AddRange(why);
                if (armorAfter > 0) r.Breakdown.Add($"−{armorAfter} ARM" + (pierce > 0 ? $" (pierce {Math.Min(pierce, 99)})" : ""));
                else if (armor > 0) r.Breakdown.Add($"ARM {armor} pierced");
                int cover = 0;
                if (ranged && !hit.IgnoreCover && !(hit.Area && t.Id != target.Id))
                {
                    var coverSrc = RosterTerrain.CoverAt(s, t.Position);
                    if (coverSrc != null) cover = 1;
                    if (RosterPassives.AuraCover(s, t)) cover = Math.Min(2, cover + 1);
                    if (a != null && cover > 0)
                    {
                        int ignore = RosterPassives.IgnoreCover(s, a, t, hit, from);
                        cover = Math.Max(0, cover - ignore);
                    }
                    if (cover > 0) r.Breakdown.Add($"−{cover} cover" + (coverSrc != null ? $" ({coverSrc})" : " (aura)"));
                }
                r.Cover = cover;
                effective = Math.Max(0, raw - armorAfter - cover);
                int flat = RosterPassives.FlatReduction(s, t, a, hit, distance, r);
                if (r.GuardianId != 0 && IsLeader(s, target) && RosterPassives.Has(t, PassiveKind.GuardReductionForLeader))
                { flat += 1; r.Breakdown.Add("−1 Shield Discipline"); }
                if (flat > 0) { effective = Math.Max(0, effective - flat); r.Reduction = flat; }
            }

            // ---- Redirect (Angle Reflect) sends damage back instead of taking it
            var redirect = t.GetR(RStatusType.Redirect);
            if (!hit.Unavoidable && redirect.HasValue && effective > 0 && a != null && !hit.NoCounter)
            {
                int back = Math.Min(redirect.Value.Amount, effective);
                effective -= back;
                r.Redirected = RosterPassives.CounterTaken(s, a, back);
                r.Consumed.Add((t.Id, RStatusType.Redirect));
                r.Breakdown.Add($"Angle Reflect: {back} damage sent back to the attacker");
            }
            r.Effective = effective;

            // ---- shields, then HP
            int shield = hit.Unavoidable ? 0 : t.ShieldHp;
            r.ShieldAbsorbed = Math.Min(shield, effective);
            r.HpDamage = effective - r.ShieldAbsorbed;
            if (r.ShieldAbsorbed > 0) r.Breakdown.Add($"{r.ShieldAbsorbed} absorbed by shields");
            r.HpAfter = t.Hp - r.HpDamage;
            r.Lethal = r.HpAfter <= 0;
            if (r.Lethal && a != null && !IsLeader(s, t) && RosterPassives.Has(a, PassiveKind.MercifulKill) && !t.R.IsToken)
            {
                r.Lethal = false; r.Merciful = true; r.HpAfter = 1; r.HpDamage = t.Hp - 1;
                r.Breakdown.Add("Merciful Procedure: left at 1 HP and ROOTED");
            }

            // ---- Reflect (Prism Mirror): the next ranged basic attack that damages the unit
            var reflect = t.GetR(RStatusType.Reflect);
            if (reflect.HasValue && hit.Basic && ranged && r.Total > 0 && a != null && !hit.NoCounter)
            {
                r.ReflectDamage = RosterPassives.CounterTaken(s, a, reflect.Value.Amount);
                r.Consumed.Add((t.Id, RStatusType.Reflect));
                r.Breakdown.Add($"Prism Mirror: attacker takes {r.ReflectDamage}");
            }
            return r;
        }

        static int AttackerBonuses(MatchState s, UnitState a, UnitState t, HitSpec hit, Hex from, int distance, HitResult r)
        {
            int bonus = 0;
            if (!RosterPassives.CanBenefitFromBuffs(a)) return 0;
            // Strongest same-type buff; per-attack buffs are consumed, round buffs persist.
            RStatus? best = null; int bestIdx = -1;
            for (int i = 0; i < a.RStatuses.Count; i++)
            {
                var st = a.RStatuses[i];
                if (st.Type != RStatusType.AttackBuff) continue;
                if (best == null || st.Amount > best.Value.Amount) { best = st; bestIdx = i; }
            }
            if (best.HasValue)
            {
                bonus += best.Value.Amount;
                r.Breakdown.Add($"+{best.Value.Amount} attack buff");
                if (best.Value.Uses > 0) r.Consumed.Add((a.Id, RStatusType.AttackBuff));
            }
            bonus += RosterPassives.DamageBonus(s, a, t, hit, from, distance, r.Breakdown);
            return bonus;
        }

        /// <summary>Finds who intercepts a direct attack on <paramref name="target"/> (null = nobody).</summary>
        public static UnitState FindGuardian(MatchState s, UnitState target, UnitState attacker, HitSpec hit, out string source, out int statusIndex,
            out int reduction, out bool guardianStatus)
        {
            source = null; statusIndex = -1; reduction = 0; guardianStatus = false;
            int distance = Hex.Distance(hit.From, target.Position);
            bool ranged = distance >= 2;
            // a) explicit protection on the target, in the order it was applied
            for (int i = 0; i < target.RStatuses.Count; i++)
            {
                var st = target.RStatuses[i];
                if (st.Type != RStatusType.Protected && st.Type != RStatusType.Intercepted) continue;
                byte mode = st.Type == RStatusType.Intercepted ? (byte)3 : st.Mode;
                if (mode == 1 && !ranged) continue;
                if (mode == 2 && !hit.Basic) continue;
                if (mode == 3 && !attacker.R.IsSmallCraft) continue;
                var g = s.Unit(st.SourceId);
                if (!CanIntercept(s, g, target, st.Range)) continue;
                source = g.R.ActiveName; statusIndex = i; reduction = st.Amount;
                return g;
            }
            var allies = s.Units.Where(u => u.OnBoard && u.Owner == target.Owner && u.Id != target.Id).OrderBy(u => u.Id).ToList();
            // b) Meeples Coalition Cover: an adjacent Meeples guardian with a live guard may donate its intercept.
            foreach (var g in allies)
            {
                if (g.Def.Faction != FactionId.Meeples || Hex.Distance(g.Position, target.Position) != 1) continue;
                if (!HasLiveGuard(s, g) || !CanIntercept(s, g, target, 1)) continue;
                source = "Coalition Cover";
                return g;
            }
            // c) units granted GUARD (Castle Doctrine, Protect Civilians) protect adjacent allies
            foreach (var g in allies)
            {
                var gs = g.GetR(RStatusType.Guardian);
                if (!gs.HasValue || gs.Value.Uses <= 0 || Hex.Distance(g.Position, target.Position) != 1) continue;
                if (!CanIntercept(s, g, target, 1)) continue;
                source = "granted GUARD"; guardianStatus = true;
                return g;
            }
            // d) leader protection passives
            if (IsLeader(s, target))
            {
                foreach (var g in allies)
                {
                    if (RosterPassives.Has(g, PassiveKind.GuardLeader) && CanIntercept(s, g, target, 1)) { source = g.R.PassiveName; return g; }
                }
                var aura = target.R.Passive;
                if (aura != null && aura.Kind == PassiveKind.LeaderGuardAura)
                    foreach (var g in allies)
                        if (GuardCapable(g) && CanIntercept(s, g, target, aura.Range)) { source = target.R.PassiveName; return g; }
            }
            return null;
        }

        static bool CanIntercept(MatchState s, UnitState g, UnitState target, int range)
        {
            if (g == null || !g.OnBoard || g.Owner != target.Owner || g.Id == target.Id) return false;
            if (g.HasR(RStatusType.Captured) || g.HasR(RStatusType.Boarded) || g.HasR(RStatusType.Disabled)) return false;
            if (Hex.Distance(g.Position, target.Position) > Math.Max(1, range)) return false;
            return InterceptsLeft(s, g) > 0;
        }

        /// <summary>Units with a GUARD/INTERCEPT active or a guard passive.</summary>
        public static bool GuardCapable(UnitState u) =>
            u.R.Active != null && (u.R.ActiveCode == EffectCode.GUARD || u.R.ActiveCode == EffectCode.INTERCEPT) || RosterPassives.Has(u, PassiveKind.GuardLeader);

        static bool HasLiveGuard(MatchState s, UnitState g)
        {
            foreach (var u in s.Units)
                if (u.OnBoard && u.Owner == g.Owner)
                    foreach (var st in u.RStatuses)
                        if ((st.Type == RStatusType.Protected || st.Type == RStatusType.Intercepted) && st.SourceId == g.Id) return true;
            return false;
        }

        public static int InterceptsLeft(MatchState s, UnitState g)
        {
            int max = g.R.Active != null && g.R.Active.Special == "EachIntercepts" ? 2 : 1;
            int used = (g.RoundFlag(s.Round, RosterFlags.InterceptA) ? 1 : 0) + (g.RoundFlag(s.Round, RosterFlags.InterceptB) ? 1 : 0);
            return max - used;
        }

        // ------------------------------------------------------------------ apply

        /// <summary>Applies a computed hit. Destroys units, consumes statuses and guards, applies counters.</summary>
        public static void Apply(MatchState s, HitResult r, List<GameEvent> ev)
        {
            var a = r.Spec.Attacker;
            var t = s.Unit(r.TargetId);
            var orig = s.Unit(r.OriginalTargetId);
            if (t == null || !t.Alive) return;
            if (r.GuardianId != 0)
            {
                var g = t;
                g.SetRoundFlag(s.Round, g.RoundFlag(s.Round, RosterFlags.InterceptA) ? RosterFlags.InterceptB : RosterFlags.InterceptA);
                if (r.ProtectedStatusIndex >= 0 && r.ProtectedStatusIndex < orig.RStatuses.Count)
                {
                    var st = orig.RStatuses[r.ProtectedStatusIndex];
                    st.Uses--;
                    if (st.Uses <= 0) orig.RStatuses.RemoveAt(r.ProtectedStatusIndex); else orig.RStatuses[r.ProtectedStatusIndex] = st;
                }
                if (r.GuardianStatusConsumed)
                {
                    int gi = g.RIndex(RStatusType.Guardian);
                    if (gi >= 0) g.RStatuses.RemoveAt(gi);
                }
                ev.Add(new GameEvent(GameEventType.Intercepted) { Seat = g.Owner, UnitId = g.Id, TargetUnitId = orig.Id, From = orig.Position, To = g.Position, Text = r.GuardSource });
                if (RosterPassives.Has(g, PassiveKind.MoveAfterIntercept))
                    RosterRules.AddStatus(s, g, new Grant(RStatusType.MoveBuff, 1), g.Id, ev);
            }
            foreach (var (uid, type) in r.Consumed)
            {
                var u = s.Unit(uid);
                if (u == null) continue;
                int i = u.RIndex(type);
                if (i < 0) continue;
                var st = u.RStatuses[i];
                if (st.Uses > 1) { st.Uses--; u.RStatuses[i] = st; }
                else u.RStatuses.RemoveAt(i);
            }
            foreach (var (uid, bit, battle) in r.Flags)
            {
                var u = s.Unit(uid);
                if (u == null) continue;
                if (battle) u.BattleFlags |= bit; else u.SetRoundFlag(s.Round, bit);
            }
            if (a != null && a.Owner != t.Owner) t.AttackedByEnemyRound = s.Round;

            if (r.ShieldAbsorbed > 0)
            {
                int si = t.RIndex(RStatusType.Shield);
                if (si >= 0)
                {
                    var st = t.RStatuses[si];
                    st.Amount -= r.ShieldAbsorbed;
                    if (st.Amount <= 0) t.RStatuses.RemoveAt(si); else t.RStatuses[si] = st;
                }
                ev.Add(new GameEvent(GameEventType.ShieldAbsorbed) { Seat = t.Owner, UnitId = a?.Id ?? 0, TargetUnitId = t.Id, To = t.Position, Amount = r.ShieldAbsorbed, HpAfter = t.Hp });
            }
            if (r.HpDamage > 0 || r.Total == 0)
            {
                t.Hp = Math.Max(0, t.Hp - r.HpDamage);
                if (r.Merciful) t.Hp = 1;
                ev.Add(new GameEvent(GameEventType.DamageApplied)
                {
                    Seat = t.Owner, UnitId = a?.Id ?? 0, TargetUnitId = t.Id, To = t.Position, Amount = r.HpDamage, HpAfter = Math.Max(0, t.Hp),
                    Lethal = r.Lethal, Text = r.Spec.Cause,
                });
            }
            if (r.Merciful) RosterRules.AddStatus(s, t, new Grant(RStatusType.Rooted), a?.Id ?? 0, ev);
            if (r.Lethal) RosterRules.Destroy(s, t, a, ev);
            else if (a != null && a.Owner != t.Owner) RosterPassives.AfterHitSurvived(s, t, a, r, ev);

            int back = r.ReflectDamage + r.Redirected;
            if (back > 0 && a != null && a.Alive)
            {
                ev.Add(new GameEvent(GameEventType.Countered) { Seat = t.Owner, UnitId = t.Id, TargetUnitId = a.Id, From = t.Position, To = a.Position, Amount = back });
                var counter = new HitSpec { Attacker = null, Raw = back, Pierce = 99, Cause = "counter", From = t.Position, NoCounter = true };
                var cr = Compute(s, a, counter);
                Apply(s, cr, ev);
            }
        }

        /// <summary>Resolve-and-apply convenience for ability hits.</summary>
        public static HitResult Hit(MatchState s, UnitState target, HitSpec hit, List<GameEvent> ev)
        {
            var r = Compute(s, target, hit);
            Apply(s, r, ev);
            return r;
        }

        /// <summary>Damages a barrier; destroys it at 0 HP.</summary>
        public static void DamageBarrier(MatchState s, RosterBarrier b, int amount, UnitState source, List<GameEvent> ev)
        {
            if (b == null || amount <= 0) return;
            b.Hp -= amount;
            if (b.Hp <= 0)
            {
                s.Barriers.Remove(b);
                ev.Add(new GameEvent(GameEventType.BarrierDestroyed) { Seat = b.Owner, UnitId = source?.Id ?? 0, To = b.Hex, Amount = amount });
            }
            else ev.Add(new GameEvent(GameEventType.BarrierDamaged) { Seat = b.Owner, UnitId = source?.Id ?? 0, To = b.Hex, Amount = amount, HpAfter = b.Hp });
        }
    }
}
