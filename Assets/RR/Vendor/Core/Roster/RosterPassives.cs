using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>Bit flags for once-per-round (<see cref="UnitState.RoundFlags"/>) and once-per-battle (<see cref="UnitState.BattleFlags"/>) bookkeeping.</summary>
    public static class RosterFlags
    {
        // round
        public const int InterceptA = 1 << 0, InterceptB = 1 << 1;
        public const int HitAny = 1 << 2, HitDirect = 1 << 3, HitBasic = 1 << 4, HitRanged = 1 << 5, HitByFighter = 1 << 6;
        public const int AuraHealed = 1 << 7, HazardWard = 1 << 8, DisableWard = 1 << 9, SuppressWard = 1 << 10, ShieldOnActivate = 1 << 11;
        public const int FreeStepUsed = 1 << 12, RepairBonus = 1 << 13, MarketFlex = 1 << 14, ShadowContract = 1 << 15, SelfHealed = 1 << 16;
        public const int MedicalHold = 1 << 17;
        // battle
        public const int FirstAttackSurvived = 1 << 0, DebuffWard = 1 << 1, MarkWard = 1 << 2, RootWard = 1 << 3, FirstAttackTaken = 1 << 4;
        public const int VehicleDisableWard = 1 << 5, FirstObjective = 1 << 6;
    }

    /// <summary>
    /// Executes the passive abilities (and faction rules) of the Full Roster units. Every hook switches on the unit's
    /// <see cref="PassiveKind"/>; the per-unit <see cref="PassiveSpec.Engine"/> line documents each one in plain words.
    /// </summary>
    public static class RosterPassives
    {
        public static bool Has(UnitState u, PassiveKind k) => u?.Def?.Roster?.Passive != null && u.R.Passive.Kind == k;
        static PassiveSpec Spec(UnitState u) => u.R.Passive;

        static IEnumerable<UnitState> Allies(MatchState s, UnitState u) =>
            s.Units.Where(x => x.OnBoard && x.Owner == u.Owner && x.Id != u.Id && !x.HasR(RStatusType.Captured));
        static IEnumerable<UnitState> Enemies(MatchState s, UnitState u) =>
            s.Units.Where(x => x.OnBoard && x.Owner != u.Owner);
        static int Dist(UnitState a, UnitState b) => Hex.Distance(a.Position, b.Position);

        /// <summary>Holders of an aura passive within its range of <paramref name="u"/> (same side; optionally including u itself).</summary>
        static IEnumerable<UnitState> AuraHolders(MatchState s, UnitState u, PassiveKind k, bool includeSelf = false)
        {
            foreach (var x in s.Units)
            {
                if (!x.OnBoard || x.Owner != u.Owner || !Has(x, k) || x.HasR(RStatusType.Captured)) continue;
                if (x.Id == u.Id && !includeSelf) continue;
                if (Dist(x, u) <= Math.Max(1, Spec(x).Range)) yield return x;
            }
        }

        static bool RoleMatch(UnitState t, UnitRole? role)
        {
            if (role == null) return true;
            if (role == UnitRole.Line) return t.R.IsInfantry;
            if (role == UnitRole.Fighter) return t.R.IsSmallCraft;
            return t.R.Role == role.Value;
        }

        /// <summary>Stationary for defensive passives: it has not moved this activation and did not move in its last one.</summary>
        public static bool Stationary(UnitState u) => !u.MovedThisActivation && !u.LastActivationMoved;

        static bool OnObjective(MatchState s, UnitState u) => s.ObjectiveIndex(u.Position) >= 0;

        // ================================================================== movement

        public static int MoveBonus(MatchState s, UnitState u)
        {
            bool aura = false, hangar = false, ramp = false;
            foreach (var x in s.Units)
            {
                if (!x.OnBoard || x.Owner != u.Owner || x.Id == u.Id || x.R.Passive == null || x.HasR(RStatusType.Captured)) continue;
                var p = x.R.Passive; int d = Dist(x, u);
                switch (p.Kind)
                {
                    case PassiveKind.AuraMove: if (d <= p.Range) aura = true; break;
                    case PassiveKind.AuraMoveSmallCraft: if (d <= p.Range && (u.R.IsSmallCraft || u.R.IsToken)) hangar = true; break;
                    case PassiveKind.AuraMoveForCorsairs: if (d == 1 && u.DefId == "pirates_boarding_corsair") ramp = true; break;
                }
            }
            int b = (aura ? 1 : 0) + (hangar ? 1 : 0) + (ramp ? 1 : 0);
            if (Has(u, PassiveKind.MoveIfAllyKill) && s.KillsThisRound[u.Owner] > 0) b += 1;
            if (Has(u, PassiveKind.MoveIfAllyBoarded) && s.BoardsThisRound[u.Owner] > 0) b += 1;
            return b;
        }

        public static int MovePenalty(MatchState s, UnitState u)
        {
            if (u.R.IsSiege || u.R.Role == UnitRole.Elite) return 0;
            foreach (var e in Enemies(s, u))
                if (Has(e, PassiveKind.AuraEnemySlow) && Dist(e, u) == 1 && !e.HasR(RStatusType.Captured)) return 1;
            return 0;
        }

        public static bool IgnoresRough(MatchState s, UnitState u) => Has(u, PassiveKind.IgnoreRough);
        public static bool IgnoresFirstRough(MatchState s, UnitState u) => Has(u, PassiveKind.IgnoreRoughOnce);
        public static bool MayPassAlly(MatchState s, UnitState u) => Has(u, PassiveKind.PassThroughAlly);

        public static bool NebulaImmune(MatchState s, UnitState u)
        {
            if (Has(u, PassiveKind.NebulaImmune)) return true;
            foreach (var x in AuraHolders(s, u, PassiveKind.AuraNebulaImmune)) return true;
            return false;
        }

        // ================================================================== targeting and range

        public static int RangeBonus(MatchState s, UnitState u) =>
            Has(u, PassiveKind.RangeIfStationary) && !u.MovedThisActivation && u.Weapon.MaxRange < 5 ? 1 : 0;

        public static bool NoAttackAfterMove(UnitState u) => Has(u, PassiveKind.NoAttackAfterMove);

        public static int CloakSightBonus(MatchState s, UnitState attacker) =>
            AuraHolders(s, attacker, PassiveKind.AuraCloakSight, includeSelf: true).Any() ? 1 : 0;

        /// <summary>Shared spotting: an allied spotter near the attacker can see the cloaked target (within 2 of it).</summary>
        public static bool Spotted(MatchState s, UnitState attacker, UnitState target)
        {
            foreach (var x in AuraHolders(s, attacker, PassiveKind.Spotter, includeSelf: true))
                if (Dist(x, target) <= 2) return true;
            return false;
        }

        /// <summary>Hushed Synapse: targetable only from this close (0 = no concealment).</summary>
        public static int ConcealedBeyond(MatchState s, UnitState target) =>
            Has(target, PassiveKind.HiddenBeyond) && target.AttackedRound != s.Round ? Spec(target).Range : 0;

        public static int? UntargetableByCapitalBeyond(MatchState s, UnitState target) =>
            Has(target, PassiveKind.UntargetableByCapitalBeyond) ? Spec(target).Range : (int?)null;

        public static Func<UnitState, bool> LosPassFilter(MatchState s, UnitState u, Hex from)
        {
            if (Has(u, PassiveKind.LosThroughFriendlySmallCraft)) return x => x.R.IsSmallCraft || x.R.IsToken;
            if (Has(u, PassiveKind.LosThroughAdjacentAlly)) return x => x.R.IsShip && Hex.Distance(x.Position, from) == 1;
            return null;
        }

        /// <summary>Long Synapse: an allied scout with a clear sight line (4 hexes) to the target lets this unit fire without its own LOS.</summary>
        public static bool SpotterSees(MatchState s, UnitState attacker, UnitState target)
        {
            if (!Has(attacker, PassiveKind.AttackViaSpotter)) return false;
            foreach (var x in Allies(s, attacker))
            {
                var r = x.R.Role;
                if (r != UnitRole.Scout && r != UnitRole.VehicleScout && r != UnitRole.SpaceScout) continue;
                if (Dist(x, target) > 4) continue;
                if (RosterTerrain.LineOfSight(s, x.Position, target.Position, false, x.Id, x.Owner) == LosState.Clear) return true;
            }
            return false;
        }

        public static bool CanBenefitFromBuffs(UnitState a) => !Has(a, PassiveKind.NeutralEmblem);

        // ================================================================== damage in

        public static int ArmorBonus(MatchState s, UnitState t, HitSpec hit, List<string> why)
        {
            int b = 0;
            void Add(int v, string text) { if (v <= 0) return; b += v; why?.Add($"+{v} ARM ({text})"); }
            var p = t.R.Passive;
            if (p != null)
            {
                switch (p.Kind)
                {
                    case PassiveKind.ArmorIfAdjacentAlly:
                        if (Allies(s, t).Any(x => Dist(x, t) == 1 && x.Def.Faction == t.Def.Faction && x.R.IsInfantry)) Add(1, t.R.PassiveName);
                        break;
                    case PassiveKind.ArmorIfStationary: if (Stationary(t)) Add(1, t.R.PassiveName); break;
                    case PassiveKind.ArmorIfAdjacentAllyStationary:
                        if (Stationary(t) && Allies(s, t).Any(x => Dist(x, t) == 1 && x.Def.Faction == t.Def.Faction)) Add(1, t.R.PassiveName);
                        break;
                    case PassiveKind.ArmorOnCoverOrObjective:
                        if (RosterTerrain.CoverAt(s, t.Position) != null || OnObjective(s, t)) Add(1, t.R.PassiveName);
                        break;
                    case PassiveKind.ArmorIfAdjacentBigShip:
                        if (Allies(s, t).Any(x => Dist(x, t) == 1 && x.R.IsShip && x.MaxHp >= 10)) Add(1, t.R.PassiveName);
                        break;
                    case PassiveKind.ArmorStationaryVsUnmarkedRanged:
                        if (hit.Basic && Hex.Distance(hit.From, t.Position) >= 2 && !t.HasR(RStatusType.Marked) && Stationary(t)) Add(1, t.R.PassiveName);
                        break;
                    case PassiveKind.ArmorFirstHitNearLeader:
                        if (!t.RoundFlag(s.Round, RosterFlags.HitAny) && Allies(s, t).Any(x => Dist(x, t) == 1 && RosterCombat.IsLeader(s, x))) Add(1, t.R.PassiveName);
                        break;
                }
            }
            foreach (var x in Allies(s, t))
            {
                var xp = x.R.Passive; if (xp == null) continue;
                int d = Dist(x, t);
                switch (xp.Kind)
                {
                    case PassiveKind.AuraArmorWhileStationary:
                        if (d == 1 && t.R.IsShip && Stationary(x)) Add(1, xp.Name(x));
                        break;
                    case PassiveKind.AuraArmorInfantryUnmoved:
                        if (d <= xp.Range && t.R.IsInfantry && t.MovedRound != s.Round) Add(1, xp.Name(x));
                        break;
                    case PassiveKind.AuraArmorOnObjective:
                        int oi = s.ObjectiveIndex(t.Position);
                        if (d == 1 && oi >= 0 && s.Objectives[oi].Owner == t.Owner) Add(1, xp.Name(x));
                        break;
                }
            }
            return b;
        }

        public static bool AuraCover(MatchState s, UnitState t)
        {
            foreach (var x in Allies(s, t))
                if (Has(x, PassiveKind.AuraCover) && Dist(x, t) == 1 && RoleMatch(t, Spec(x).Role)) return true;
            return false;
        }

        /// <summary>Raw-damage reductions on the defender ("deals 1 less raw damage").</summary>
        public static int RawReduction(MatchState s, UnitState t, UnitState a, HitSpec hit, List<string> why)
        {
            int r = 0;
            if (t.R.Role == UnitRole.Carrier && Allies(s, t).Any(x => Has(x, PassiveKind.AuraReduceForCarrier) && Dist(x, t) == 1))
            { r += 1; why.Add("−1 raw (Escort Protocol)"); }
            return r;
        }

        /// <summary>Flat reductions after ARM and cover ("takes 1 less damage"). Each is checked against the target's
        /// once-per-round/battle hit flags, which <see cref="RosterCombat.Apply"/> sets for every enemy hit.</summary>
        public static int FlatReduction(MatchState s, UnitState t, UnitState a, HitSpec hit, int distance, HitResult r)
        {
            var p = t.R.Passive;
            if (p == null || a == null || a.Owner == t.Owner) return 0;
            bool ranged = distance >= 2;
            bool attack = hit.Direct || hit.Basic;
            bool apply = false;
            switch (p.Kind)
            {
                case PassiveKind.ReduceFromDistance: apply = ranged && distance >= p.Range; break;
                case PassiveKind.ReduceFirstFighterHit: apply = (a.R.IsSmallCraft || a.R.IsToken) && !t.RoundFlag(s.Round, RosterFlags.HitByFighter); break;
                case PassiveKind.ReduceFirstRangedIfStationary: apply = ranged && Stationary(t) && !t.RoundFlag(s.Round, RosterFlags.HitRanged); break;
                case PassiveKind.ReduceFirstDirectHit: apply = hit.Direct && !t.RoundFlag(s.Round, RosterFlags.HitDirect); break;
                case PassiveKind.ReduceFirstBasicPerRound: apply = hit.Basic && !t.RoundFlag(s.Round, RosterFlags.HitBasic); break;
                case PassiveKind.ReduceFirstAttackBattle: apply = attack && (t.BattleFlags & RosterFlags.FirstAttackTaken) == 0; break;
                case PassiveKind.ReduceNextRangedIfMoved:
                    apply = ranged && t.MovedRound == s.Round && !t.RoundFlag(s.Round, RosterFlags.MarketFlex);
                    if (apply) r.Flags.Add((t.Id, RosterFlags.MarketFlex, false));
                    break;
            }
            if (!apply) return 0;
            r.Breakdown.Add($"−1 {p.Name(t)}");
            return 1;
        }

        public static int PierceBonus(MatchState s, UnitState a, UnitState t, HitSpec hit, Hex from, List<string> why)
        {
            var p = a.R.Passive;
            if (p == null) return 0;
            bool hit1 = false;
            switch (p.Kind)
            {
                case PassiveKind.PierceVsFortified:
                    hit1 = t.R.Role == UnitRole.Defender || t.R.Role == UnitRole.SpaceBastion || RosterTerrain.CoverAt(s, t.Position) != null;
                    break;
                case PassiveKind.PierceIfUnmarked: hit1 = a.RoundFlag(s.Round, RosterFlags.ShadowContract) && !a.AttackedThisActivation && s.ActiveUnitId == a.Id; break;
                case PassiveKind.PierceVsMarked: hit1 = t.HasR(RStatusType.Marked); break;
                case PassiveKind.PierceVsShieldedOrFortified: hit1 = hit.Basic && (t.ShieldHp > 0 || t.R.Role == UnitRole.Defender || t.R.Role == UnitRole.SpaceBastion); break;
                case PassiveKind.PierceFromCloak: hit1 = a.HasR(RStatusType.Cloaked); break;
                case PassiveKind.PierceVsBoarded: hit1 = hit.Basic && t.HasR(RStatusType.Boarded); break;
                case PassiveKind.PierceVsCapitalIfStationary: hit1 = t.R.IsCapital && !a.MovedThisActivation; break;
            }
            if (!hit1) return 0;
            why.Add($"pierce 1 ({p.Name(a)})");
            return 1;
        }

        public static int IgnoreCover(MatchState s, UnitState a, UnitState t, HitSpec hit, Hex from)
        {
            int n = 0;
            if (Has(a, PassiveKind.IgnoreCover) && hit.Basic) n = 1;
            if (Has(a, PassiveKind.IgnoreCoverFromCover) && RosterTerrain.CoverAt(s, from) != null) n = 1;
            if (Allies(s, a).Any(x => Has(x, PassiveKind.AuraIgnoreCover) && Hex.Distance(x.Position, from) == 1)) n = 1;
            if (t.HasR(RStatusType.Marked) && AuraHolders(s, a, PassiveKind.AuraIgnoreCoverVsMarked, includeSelf: true).Any()) n = 1;
            if (Has(a, PassiveKind.IgnoreCover) && hit.Basic) n = Math.Max(n, 1);
            return n;
        }

        public static int BarrierDamageBonus(MatchState s, UnitState a, bool basic)
        {
            int b = 0;
            if (basic && Has(a, PassiveKind.DamageVsBarrier)) b += Spec(a).Amount;
            if (Has(a, PassiveKind.PierceVsFortified)) b += 1;
            return b;
        }

        /// <summary>Raw damage bonuses from the attacker's and its allies' passives.</summary>
        public static int DamageBonus(MatchState s, UnitState a, UnitState t, HitSpec hit, Hex from, int distance, List<string> why)
        {
            int b = 0;
            void Add(int v, string text) { if (v <= 0) return; b += v; why.Add($"+{v} {text}"); }
            var p = a.R.Passive;
            if (p != null && !(p.BasicOnly && !hit.Basic))
            {
                string n = p.Name(a);
                switch (p.Kind)
                {
                    case PassiveKind.DamageIfAdjacentRole:
                        if (Allies(s, a).Any(x => x.R.Role == p.Role && Hex.Distance(x.Position, from) == 1)) Add(1, n);
                        break;
                    case PassiveKind.DamageVsSmallCraft: if (t.R.IsSmallCraft || (t.R.IsToken && t.R.IsShip)) Add(1, n); break;
                    case PassiveKind.DamageAfterLeap: if (a.MovedThisActivation && !a.AttackedThisActivation) Add(1, n); break;
                    case PassiveKind.DamageIfNotMoved: if (!a.MovedThisActivation) Add(1, n); break;
                    case PassiveKind.AnchorGunners: if (!a.MovedThisActivation) Add(1, n); break;
                    case PassiveKind.DamageVsMovedTarget: if (t.MovedRound == s.Round) Add(1, n); break;
                    case PassiveKind.DamageIfAllyNearTarget: if (Allies(s, a).Any(x => Dist(x, t) <= p.Range)) Add(1, n); break;
                    case PassiveKind.DamageAtDistance: if (distance >= p.Range) Add(1, n); break;
                    case PassiveKind.DamageVsMarked: if (hit.Basic && t.HasR(RStatusType.Marked)) Add(1, n); break;
                    case PassiveKind.DamageVsIsolated: if (!s.Units.Any(x => x.OnBoard && x.Owner == t.Owner && x.Id != t.Id && Dist(x, t) == 1)) Add(1, n); break;
                    case PassiveKind.GangUp: if (Allies(s, a).Any(x => Dist(x, t) == 1)) Add(1, n); break;
                    case PassiveKind.DamageAdjacent: if (distance == 1) Add(1, n); break;
                    case PassiveKind.DamageIfAdjacentFrigate: if (Allies(s, a).Any(x => x.R.Role == UnitRole.Frigate && Hex.Distance(x.Position, from) == 1)) Add(1, n); break;
                    case PassiveKind.DamageVsLeaderThreat:
                    {
                        var leader = s.Commander(a.Owner);
                        if (hit.Basic && leader != null && leader.OnBoard && Dist(t, leader) <= t.Weapon.MaxRange) Add(1, n);
                        break;
                    }
                }
            }
            // ally auras
            foreach (var x in s.Units)
            {
                if (!x.OnBoard || x.Owner != a.Owner || x.R.Passive == null || x.HasR(RStatusType.Captured)) continue;
                var xp = x.R.Passive; int d = Hex.Distance(x.Position, from);
                switch (xp.Kind)
                {
                    case PassiveKind.AuraDamageToRole:
                        if (x.Id != a.Id && d == 1 && a.R.Role == xp.Role) Add(1, xp.Name(x));
                        break;
                    case PassiveKind.AuraDamageVsMarked:
                        if (d <= xp.Range && t.HasR(RStatusType.Marked)) Add(1, xp.Name(x));
                        break;
                    case PassiveKind.AuraDamageVsObjectiveHolder:
                        if (hit.Basic && d <= xp.Range && OnObjective(s, t)) Add(1, xp.Name(x));
                        break;
                    case PassiveKind.AuraDamageVsBoarded:
                        if (d <= xp.Range && t.HasR(RStatusType.Boarded)) Add(1, xp.Name(x));
                        break;
                }
            }
            return b;
        }

        /// <summary>Counter damage actually taken by <paramref name="a"/> (Weighted Prow reduces it on its own activation).</summary>
        public static int CounterTaken(MatchState s, UnitState a, int amount)
        {
            if (Has(a, PassiveKind.CounterReduction) && s.ActiveUnitId == a.Id) amount = Math.Max(0, amount - 1);
            return amount;
        }

        // ================================================================== after hits, kills and deaths

        public static void AfterHitSurvived(MatchState s, UnitState t, UnitState a, HitResult r, List<GameEvent> ev)
        {
            var spec = r.Spec;
            int dist = Hex.Distance(spec.From, t.Position);
            if (spec.Direct || spec.Basic)
            {
                t.SetRoundFlag(s.Round, RosterFlags.HitAny);
                if (spec.Direct) t.SetRoundFlag(s.Round, RosterFlags.HitDirect);
                if (spec.Basic) t.SetRoundFlag(s.Round, RosterFlags.HitBasic);
                if (dist >= 2) t.SetRoundFlag(s.Round, RosterFlags.HitRanged);
                if (a.R.IsSmallCraft || a.R.IsToken) t.SetRoundFlag(s.Round, RosterFlags.HitByFighter);
                t.BattleFlags |= RosterFlags.FirstAttackTaken;
            }
            if (Has(t, PassiveKind.ShieldAfterFirstHitSurvived) && (t.BattleFlags & RosterFlags.FirstAttackSurvived) == 0 && (spec.Direct || spec.Basic))
            {
                t.BattleFlags |= RosterFlags.FirstAttackSurvived;
                RosterRules.AddStatus(s, t, new Grant(RStatusType.Shield, 1, RExpire.RoundEnd, 2), t.Id, ev);
            }
            if (Has(t, PassiveKind.MarkAttackerIfGuardNear) && (spec.Direct || spec.Basic)
                && Allies(s, t).Any(x => x.DefId == "meeples_council_guard" && Dist(x, t) <= 2))
                RosterRules.AddStatus(s, a, new Grant(RStatusType.Marked, 1, RExpire.RoundEnd, 2, uses: 1), t.Id, ev);
        }

        public static void OnKill(MatchState s, UnitState killer, UnitState victim, List<GameEvent> ev)
        {
            if (killer == null || killer.Owner == victim.Owner) return;
            s.KillsThisRound[killer.Owner]++;
            bool midActivation = s.ActiveUnitId == killer.Id && killer.Alive;
            // Pygmaei War Momentum: the first kill of the round grants the killer a free step.
            if (killer.Def.Faction == FactionId.Pygmaei && s.MomentumRound[killer.Owner] != s.Round && midActivation)
            {
                s.MomentumRound[killer.Owner] = s.Round;
                RosterRules.GrantFreeStep(s, killer, "War Momentum", ev);
            }
            else if (Has(killer, PassiveKind.FreeStepOnKill) && !killer.RoundFlag(s.Round, RosterFlags.FreeStepUsed) && midActivation)
            {
                killer.SetRoundFlag(s.Round, RosterFlags.FreeStepUsed);
                RosterRules.GrantFreeStep(s, killer, killer.R.PassiveName, ev);
            }
            if (killer.Alive)
            {
                foreach (var m in s.Units.Where(x => x.OnBoard && x.Owner == killer.Owner && Has(x, PassiveKind.AllyKillMoveAura) && Dist(x, killer) <= Spec(x).Range).Take(1))
                    foreach (var ally in Allies(s, killer).Where(x => Dist(x, killer) == 1).OrderBy(x => x.Id))
                        RosterRules.AddStatus(s, ally, new Grant(RStatusType.MoveBuff, 1), m.Id, ev);
                if (Has(killer, PassiveKind.ShieldOnScoutKill) && (victim.R.Role == UnitRole.Scout || victim.R.Role == UnitRole.VehicleScout || victim.R.Role == UnitRole.SpaceScout))
                    RosterRules.AddStatus(s, killer, new Grant(RStatusType.Shield, 1, RExpire.RoundEnd, 2), killer.Id, ev);
            }
        }

        public static void OnDestroyed(MatchState s, UnitState victim, List<GameEvent> ev)
        {
            if (Has(victim, PassiveKind.DeathBlast))
            {
                int dmg = Spec(victim).Amount;
                foreach (var n in s.Units.Where(x => x.OnBoard && Dist(x, victim) == 1).OrderBy(x => x.Id).ToList())
                    RosterCombat.Hit(s, n, new HitSpec { Raw = dmg, Area = true, Cause = victim.R.PassiveName, From = victim.Position }, ev);
            }
        }

        // ================================================================== activation hooks

        public static void OnActivationStart(MatchState s, UnitState u, List<GameEvent> ev)
        {
            var p = u.R.Passive;
            if (p != null)
            {
                switch (p.Kind)
                {
                    case PassiveKind.RevealMinesAtStart:
                        foreach (var m in s.Mines.Where(m => m.Owner != u.Owner && Hex.Distance(m.Hex, u.Position) <= p.Range))
                        {
                            m.RevealedUntilRound = s.Round;
                            ev.Add(new GameEvent(GameEventType.UnitsRevealed) { Seat = u.Owner, UnitId = u.Id, To = m.Hex, Text = "mine" });
                        }
                        break;
                    case PassiveKind.DisarmMinesAtStart:
                        foreach (var m in s.Mines.Where(m => m.Owner != u.Owner && Hex.Distance(m.Hex, u.Position) <= 1).ToList())
                        {
                            s.Mines.Remove(m);
                            ev.Add(new GameEvent(GameEventType.MineDisarmed) { Seat = u.Owner, UnitId = u.Id, To = m.Hex });
                        }
                        break;
                    case PassiveKind.ShieldAtStartIfNotAttacked:
                        if (u.AttackedByEnemyRound != s.Round) RosterRules.AddStatus(s, u, new Grant(RStatusType.Shield, p.Amount, RExpire.SourceNextActivation), u.Id, ev);
                        break;
                    case PassiveKind.NeutralEmblem:
                        if (!u.LastActivationMoved) RosterRules.AddStatus(s, u, new Grant(RStatusType.Shield, p.Amount, RExpire.SourceNextActivation), u.Id, ev);
                        break;
                    case PassiveKind.AuraHealAtStart:
                        foreach (var x in Allies(s, u).Where(x => Dist(x, u) == 1 && RoleMatch(x, p.Role)).OrderBy(x => x.Id))
                            RosterRules.Heal(s, x, 1, u, ev, p.Name(u));
                        break;
                    case PassiveKind.PierceIfUnmarked:
                        if (!u.HasR(RStatusType.Marked)) u.SetRoundFlag(s.Round, RosterFlags.ShadowContract);
                        break;
                }
            }
            // Shields granted to an activating unit by nearby carriers / ledgers.
            foreach (var x in Allies(s, u))
            {
                var xp = x.R.Passive;
                if (xp == null || xp.Kind != PassiveKind.AuraShieldOnActivate || Dist(x, u) > xp.Range || !RoleMatch(u, xp.Role)) continue;
                if (xp.Role == null && (!u.R.IsShip || u.RoundFlag(s.Round, RosterFlags.ShieldOnActivate))) continue;
                if (xp.Role == UnitRole.Fighter && !(u.R.IsSmallCraft || u.R.IsToken)) continue;
                u.SetRoundFlag(s.Round, RosterFlags.ShieldOnActivate);
                RosterRules.AddStatus(s, u, new Grant(RStatusType.Shield, 1, RExpire.SourceNextActivation), u.Id, ev);
                break;
            }
        }

        public static void OnActivationEnd(MatchState s, UnitState u, List<GameEvent> ev)
        {
            var p = u.R.Passive;
            if (p != null)
            {
                switch (p.Kind)
                {
                    case PassiveKind.AuraHealAfterActivation:
                        foreach (var x in Allies(s, u).Where(x => Dist(x, u) == 1).OrderBy(x => x.Id))
                        {
                            if (p.Role == UnitRole.Fighter ? !(x.R.IsSmallCraft || x.R.IsToken) : x.Def.Faction != FactionId.Craigon) continue;
                            if (p.Role == null && x.AttackedRound == s.Round) continue;
                            if (x.RoundFlag(s.Round, RosterFlags.AuraHealed) || x.Hp >= x.MaxHp) continue;
                            x.SetRoundFlag(s.Round, RosterFlags.AuraHealed);
                            RosterRules.Heal(s, x, 1, u, ev, p.Name(u));
                        }
                        break;
                    case PassiveKind.SelfHealIfIdle:
                        if (!u.MovedThisActivation && !u.AttackedThisActivation && !u.RoundFlag(s.Round, RosterFlags.SelfHealed))
                        {
                            u.SetRoundFlag(s.Round, RosterFlags.SelfHealed);
                            RosterRules.Heal(s, u, 1, u, ev, p.Name(u));
                        }
                        break;
                    case PassiveKind.ArmorIfNoAlliedKill:
                        if (s.KillsThisRound[u.Owner] == 0) RosterRules.AddStatus(s, u, new Grant(RStatusType.ArmorBuff, 1, RExpire.SourceNextActivation), u.Id, ev);
                        break;
                    case PassiveKind.MoveIfFreshWreck:
                        if (s.Wrecks.Any(w => w.Round >= s.Round - 1 && Hex.Distance(w.Hex, u.Position) <= 1))
                            RosterRules.AddStatus(s, u, new Grant(RStatusType.MoveBuff, 1), u.Id, ev, afterActivation: true);
                        break;
                    case PassiveKind.AnchorGunners:
                        if (u.AttackedThisActivation) RosterRules.AddStatus(s, u, new Grant(RStatusType.Suppressed, 1), u.Id, ev, afterActivation: true);
                        break;
                }
            }
            // Silex faction rule, Living Stone: +1 guard armor until the next activation if it did not move.
            if (u.Def.Faction == FactionId.Silex && !u.MovedThisActivation && !u.R.IsToken)
                RosterRules.AddStatus(s, u, new Grant(RStatusType.ArmorBuff, 1, RExpire.SourceNextActivation), u.Id, ev, quiet: true);
        }

        /// <summary>After a unit ends its own move (not forced movement).</summary>
        public static void OnMoveEnded(MatchState s, UnitState u, List<GameEvent> ev)
        {
            if (u.R.IsInfantry && !u.RoundFlag(s.Round, RosterFlags.MedicalHold))
            {
                var carrier = Allies(s, u).FirstOrDefault(x => Has(x, PassiveKind.HealAllyEndingAdjacent) && Dist(x, u) == 1);
                if (carrier != null && u.Hp < u.MaxHp)
                {
                    u.SetRoundFlag(s.Round, RosterFlags.MedicalHold);
                    RosterRules.Heal(s, u, 1, carrier, ev, carrier.R.PassiveName);
                }
            }
            int oi = s.ObjectiveIndex(u.Position);
            if (oi >= 0)
            {
                if (Has(u, PassiveKind.ShieldOnObjectiveMove))
                    RosterRules.AddStatus(s, u, new Grant(RStatusType.Shield, 1, RExpire.SourceNextActivation), u.Id, ev);
                if (Has(u, PassiveKind.ShieldOnFirstObjective) && !s.Objectives[oi].EverHeld && (u.BattleFlags & RosterFlags.FirstObjective) == 0)
                {
                    u.BattleFlags |= RosterFlags.FirstObjective;
                    RosterRules.AddStatus(s, u, new Grant(RStatusType.Shield, 1, RExpire.RoundEnd, 2), u.Id, ev);
                }
            }
        }

        // ================================================================== wards and immunities

        /// <summary>Returns why a status is ignored by the target's (or an ally's) passive, or null if it applies.
        /// Wards that are "once per round/battle" are consumed here.</summary>
        public static string Ward(MatchState s, UnitState target, RStatusType type, UnitState source, bool consume = true)
        {
            bool hostile = source != null && source.Owner != target.Owner;
            if (!hostile) return type == RStatusType.Shield && target.HasR(RStatusType.NoShieldGain) ? "cannot gain shields this round" :
                                 type == RStatusType.AttackBuff && Has(target, PassiveKind.NeutralEmblem) ? "Neutral Emblem: cannot gain attack buffs" : null;
            var p = target.R.Passive;
            bool debuff = type == RStatusType.Rooted || type == RStatusType.Disabled || type == RStatusType.Silenced || type == RStatusType.Exposed
                          || type == RStatusType.Suppressed || type == RStatusType.RangeDebuff || type == RStatusType.AttackDebuff || type == RStatusType.Marked;
            if (type == RStatusType.Rooted && p != null)
            {
                if (p.Kind == PassiveKind.RootImmuneBelowTier4 && source.R.Tier < 4) return $"{p.Name(target)}: cannot be ROOTED by units below tier 4";
                if (p.Kind == PassiveKind.ForcedMoveAndRootImmuneBelowElite && !source.R.IsEliteClass) return $"{p.Name(target)}: immune to ROOT from units below elite class";
                if (p.Kind == PassiveKind.InertialMass && source.R.IsSmallCraft) return $"{p.Name(target)}: immune to fighter-class ROOT";
                if (p.Kind == PassiveKind.RootWardOnce && (target.BattleFlags & RosterFlags.RootWard) == 0)
                { if (consume) target.BattleFlags |= RosterFlags.RootWard; return $"{p.Name(target)}: first ROOT of the battle ignored"; }
            }
            if (type == RStatusType.Marked && p != null && p.Kind == PassiveKind.MarkWardOnce && (target.BattleFlags & RosterFlags.MarkWard) == 0)
            { if (consume) target.BattleFlags |= RosterFlags.MarkWard; return $"{p.Name(target)}: first MARK of the battle ignored"; }
            if (type == RStatusType.Disabled)
            {
                var harmonic = s.Units.FirstOrDefault(x => x.OnBoard && x.Owner == target.Owner && Has(x, PassiveKind.AuraDisableWard) && Dist(x, target) <= 1 && target.Def.Faction == FactionId.Silex);
                if (harmonic != null && !target.RoundFlag(s.Round, RosterFlags.DisableWard))
                { if (consume) target.SetRoundFlag(s.Round, RosterFlags.DisableWard); return $"{harmonic.R.PassiveName}: first DISABLE this round ignored"; }
                var jury = s.Units.FirstOrDefault(x => x.OnBoard && x.Owner == target.Owner && Has(x, PassiveKind.AuraDisableWardVehiclesBattle) && Dist(x, target) <= 1 && target.R.IsVehicle);
                if (jury != null && (target.BattleFlags & RosterFlags.VehicleDisableWard) == 0)
                { if (consume) target.BattleFlags |= RosterFlags.VehicleDisableWard; return $"{jury.R.PassiveName}: first DISABLE of the battle ignored"; }
            }
            if (type == RStatusType.Suppressed)
            {
                var stand = s.Units.FirstOrDefault(x => x.OnBoard && x.Owner == target.Owner && x.Id != target.Id && Has(x, PassiveKind.AuraSuppressWard) && Dist(x, target) == 1
                                                         && !x.RoundFlag(s.Round, RosterFlags.SuppressWard));
                if (stand != null) { if (consume) stand.SetRoundFlag(s.Round, RosterFlags.SuppressWard); return $"{stand.R.PassiveName}: SUPPRESS ignored"; }
            }
            if (debuff && p != null && p.Kind == PassiveKind.DebuffWardOnce && (target.BattleFlags & RosterFlags.DebuffWard) == 0)
            { if (consume) target.BattleFlags |= RosterFlags.DebuffWard; return $"{p.Name(target)}: first debuff of the battle ignored"; }
            return null;
        }

        /// <summary>Why a hostile forced move (push, pull, displacement) fails against <paramref name="target"/> (null = allowed).</summary>
        public static string ForcedMoveImmune(MatchState s, UnitState target, UnitState source)
        {
            if (source == null || source.Owner == target.Owner) return null;
            var p = target.R.Passive;
            if (p == null) return null;
            string n = p.Name(target);
            switch (p.Kind)
            {
                case PassiveKind.ForcedMoveImmune: return source.R.IsSiege ? null : $"{n}: cannot be moved except by siege units";
                case PassiveKind.ForcedMoveImmuneVsInfantry: return source.R.IsInfantry && !source.R.IsSiege ? $"{n}: infantry cannot move it" : null;
                case PassiveKind.ForcedMoveAndRootImmuneBelowElite: return source.R.IsEliteClass ? null : $"{n}: only elite-class units can move it";
                case PassiveKind.ForcedMoveImmuneSmallSource: return source.R.ShipSize < 5 ? $"{n}: ships smaller than a cruiser cannot move it" : null;
                case PassiveKind.InertialMass: return $"{n}: unaffected by forced movement";
                case PassiveKind.ForcedMoveImmuneIfStationary: return target.MovedRound != s.Round ? $"{n}: immune while it has not moved this round" : null;
            }
            return null;
        }

        /// <summary>Range bonus a friendly passive gives to this unit's active (Bio-Laboratory).</summary>
        public static int ActiveRangeBonus(MatchState s, UnitState u)
        {
            if (u.R.ActiveCode == EffectCode.CAPTURE && u.Def.Faction == FactionId.Craigon
                && AuraHolders(s, u, PassiveKind.AuraAbilityRange).Any()) return 1;
            return 0;
        }

        /// <summary>Captured units adjacent to one of these cannot be rescued.</summary>
        public static bool RescueBlocked(MatchState s, UnitState captive)
        {
            foreach (var x in s.Units)
                if (x.OnBoard && x.Owner != captive.Owner && Has(x, PassiveKind.BlockRescue) && Dist(x, captive) == 1 && !x.HasR(RStatusType.Captured)) return true;
            return false;
        }

        public static int RescuesNeeded(MatchState s, UnitState captive)
        {
            foreach (var x in s.Units)
                if (x.OnBoard && x.Owner != captive.Owner && Has(x, PassiveKind.RescueDouble) && Dist(x, captive) == 1) return 2;
            return 1;
        }

        public static int HazardDamage(MatchState s, UnitState u)
        {
            if (Has(u, PassiveKind.HazardImmune)) return 0;
            var ward = s.Units.FirstOrDefault(x => x.OnBoard && x.Owner == u.Owner && Has(x, PassiveKind.AuraHazardWard) && Dist(x, u) <= 1);
            if (ward != null && !u.RoundFlag(s.Round, RosterFlags.HazardWard)) { u.SetRoundFlag(s.Round, RosterFlags.HazardWard); return 0; }
            return 1;
        }

        public static int RepairBonus(MatchState s, UnitState target)
        {
            if (!target.R.IsVehicle || target.RoundFlag(s.Round, RosterFlags.RepairBonus)) return 0;
            if (!s.Units.Any(x => x.OnBoard && x.Owner == target.Owner && Has(x, PassiveKind.RepairBonusAura) && Dist(x, target) == 1)) return 0;
            target.SetRoundFlag(s.Round, RosterFlags.RepairBonus);
            return 1;
        }

        // ------------------------------------------------------------------ helpers

        static string Name(this PassiveSpec p, UnitState holder) => holder.R.PassiveName;
    }
}
