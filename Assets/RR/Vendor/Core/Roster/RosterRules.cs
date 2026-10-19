using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// The Full Roster (Unit Codex) rules: alternating single-unit activations, each surviving unit activating once per
    /// round; on activation a unit may move by its pattern, then make one basic attack OR use its active, or wait.
    /// Called by <see cref="RulesEngine"/> whenever <see cref="MatchConfig.Ruleset"/> is <see cref="Ruleset.FullRoster"/>.
    /// Deterministic: no randomness, no wall clock.
    /// </summary>
    public static class RosterRules
    {
        // ================================================================== setup

        public static MatchState CreateMatch(MatchConfig config, out string error) => CreateMatch(config, out error, validateArmies: true);

        /// <summary><paramref name="validateArmies"/> = false is for rules tests that place arbitrary units.</summary>
        public static MatchState CreateMatch(MatchConfig config, out string error, bool validateArmies)
        {
            error = null;
            var map = MapLibrary.Get(config.MapId);
            if (map.Domain != config.Domain)
            { error = $"'{map.Name}' is {(map.Domain == BattleDomain.Orbital ? "an orbital" : "a planetary")} battlefield; this is {(config.Domain == BattleDomain.Orbital ? "an orbital" : "a planetary")} battle."; return null; }
            for (int seat = 0; seat < 2 && validateArmies; seat++)
            {
                var p = config.Players[seat];
                var report = RosterArmy.Validate(p.Army, p.Caps, config.Domain, config.PointBudget, config.MaxUnits);
                if (!report.Valid) { error = $"{p.DisplayName}'s army is invalid: {report.Errors[0]}"; return null; }
            }
            var s = new MatchState { Config = config, Phase = MatchPhase.Deployment, ActiveSeat = config.FirstSeat };
            for (int seat = 0; seat < 2; seat++)
            {
                var p = config.Players[seat];
                var ps = s.Players[seat];
                ps.Faction = p.Army.Faction;
                ps.Doctrine = p.Army.Doctrine;
                ps.ClockRemainingMs = config.Timed ? config.ClockSeconds * 1000 : 0;
                foreach (var entry in p.Army.Units)
                {
                    var u = RosterArmy.CreateUnit(s.NextUnitId++, seat, entry.UnitId, p.Caps);
                    u.Equipment.AddRange(entry.Equipment);   // slice: contract, module and policy tokens (SliceRules)
                    if (entry.Equipment.Count > 0) { RosterArmy.Recompute(u, p.Caps); u.Hp = u.MaxHp; }
                    s.Units.Add(u);
                    if (u.R.IsLeaderRole && ps.CommanderId == 0) ps.CommanderId = u.Id;
                }
            }
            foreach (var h in RosterMaps.Objectives(map)) s.Objectives.Add(new ObjectiveState { Hex = h, Owner = -1 });
            return s;
        }

        // ================================================================== queries

        public static bool Done(MatchState s, UnitState u) => u.ActivatedRound >= s.Round;

        /// <summary>A unit that will never act this round (captured, boarded).</summary>
        public static bool Inert(UnitState u) => u.HasR(RStatusType.Captured) || u.HasR(RStatusType.Boarded);

        public static bool Eligible(MatchState s, UnitState u) => u.OnBoard && !Done(s, u) && !Inert(u);

        public static bool HasEligible(MatchState s, int seat)
        {
            foreach (var u in s.Units) if (u.Owner == seat && Eligible(s, u)) return true;
            return false;
        }

        public static bool CanActivate(MatchState s, UnitState u, out string reason)
        {
            reason = null;
            if (s.Phase != MatchPhase.Battle) { reason = s.Phase == MatchPhase.Ended ? "The match is over." : "The battle has not started."; return false; }
            if (u == null || !u.Alive) { reason = "That unit has been destroyed."; return false; }
            if (!u.Deployed) { reason = "That unit is not on the battlefield."; return false; }
            if (u.Owner != s.ActiveSeat) { reason = "It is not that unit's turn."; return false; }
            if (s.ActiveUnitId != 0 && s.ActiveUnitId != u.Id)
            { reason = $"{s.Unit(s.ActiveUnitId)?.Def.Name ?? "Another unit"} is mid-activation: finish it first (act or wait)."; return false; }
            if (u.HasR(RStatusType.Captured)) { reason = $"{u.Def.Name} is CAPTURED and cannot act until rescued."; return false; }
            if (u.HasR(RStatusType.Boarded)) { reason = $"{u.Def.Name} is BOARDED and cannot activate until rescued."; return false; }
            if (Done(s, u)) { reason = $"{u.Def.Name} has already activated this round."; return false; }
            return true;
        }

        /// <summary>Why the unit cannot use its action right now (null = it can). <paramref name="attack"/>: basic attack vs active.</summary>
        public static string CanAct(MatchState s, UnitState u, bool attack)
        {
            if (!CanActivate(s, u, out var why)) return why;
            if (u.ActedThisActivation) return $"{u.Def.Name} has already acted this activation.";
            if (u.HasR(RStatusType.Silenced)) return $"{u.Def.Name} cannot attack or use its active during this activation.";
            if (!attack)
            {
                if (u.R.IsToken) return "Tokens do not use active abilities.";
                if (u.HasR(RStatusType.Disabled)) return $"{u.Def.Name} is DISABLED: its active cannot be used this activation.";
                if (u.Cooldown > 0) return $"{u.R.ActiveName} is recharging ({u.Cooldown} more activation{(u.Cooldown == 1 ? "" : "s")}).";
            }
            return null;
        }

        // ================================================================== validation

        public static Validation Validate(MatchState s, Command c)
        {
            if (c.Type == CommandType.Guard) return Validation.Fail("Full Roster rules have no generic Guard action: use a GUARD ability.");
            if (c.Type == CommandType.Pass && c.UnitId == 0)
            {
                if (s.ActiveUnitId != 0) return Validation.Success;
                return HasEligible(s, c.Seat) ? Validation.Success : Validation.Fail("No unit left to activate.");
            }
            var u = s.Unit(c.UnitId);
            if (u == null) return Validation.Fail("No unit selected.");
            if (u.Owner != c.Seat) return Validation.Fail("That unit belongs to your opponent.");
            if (!CanActivate(s, u, out var why)) return Validation.Fail(why);
            switch (c.Type)
            {
                case CommandType.Pass: return Validation.Success;
                case CommandType.Move:
                {
                    if (c.Option == SliceRules.TurnOption)
                    {
                        var turn = SliceRules.ValidateTurn(s, u, c.Target);
                        return turn == null ? Validation.Success : Validation.Fail(turn);
                    }
                    if (c.Option != 0 && c.Option != SliceRules.OverclockOption) return Validation.Fail("Unknown move order.");
                    int extra = 0;
                    if (c.Option == SliceRules.OverclockOption)
                    {
                        var oc = SliceRules.CanOverclock(s, u);
                        if (oc != null) return Validation.Fail(oc);
                        if (u.MovedThisActivation || u.ActedThisActivation) return Validation.Fail("Only a ship's main move can be overclocked.");
                        extra = SliceRules.OverclockMove;
                    }
                    if (c.Target == u.Position) return Validation.Fail("The unit is already there.");
                    var view = Vision.FoggedView(s, u.Owner);
                    var vu = view.Unit(u.Id);
                    if (RosterMovement.Reachable(view, vu, extra).ContainsKey(c.Target)) return Validation.Success;
                    return Validation.Fail(RosterMovement.Explain(view, vu, c.Target));
                }
                case CommandType.Attack:
                {
                    if (c.Option == SliceRules.BoardOption)
                    {
                        var board = SliceRules.ValidateBoarding(s, u, c.Target);
                        return board == null ? Validation.Success : Validation.Fail(board);
                    }
                    if (c.Option != 0 && c.Option != SliceRules.OverclockOption) return Validation.Fail("Unknown attack order.");
                    bool overclock = c.Option == SliceRules.OverclockOption;
                    if (overclock && SliceRules.CanOverclock(s, u) is string oc) return Validation.Fail(oc);
                    var o = RosterCombat.ResolveBasic(s, u, c.Target, overclock: overclock);
                    return o.Legal ? Validation.Success : Validation.Fail(o.Reason);
                }
                case CommandType.Ability:
                    if (c.Ability == AbilityId.Rescue) return ValidateRescue(s, u, c.Target);
                    if (c.Ability != AbilityId.UnitActive) return Validation.Fail("That ability does not exist in Full Roster rules.");
                {
                    // Judged on what the player can see (a rejection would reveal a hidden unit); hidden units that
                    // turn out to be in the way are resolved as ambushes in RosterAbilities.Apply.
                    var view = Vision.FoggedView(s, u.Owner);
                    return RosterAbilities.Validate(view, view.Unit(u.Id), c.Picks, c.Option);
                }
            }
            return Validation.Fail("Unknown command.");
        }

        public static Validation ValidateRescue(MatchState s, UnitState u, Hex target)
        {
            var why = CanAct(s, u, attack: true);
            if (why != null) return Validation.Fail(why);
            var t = s.UnitAt(target);
            if (t == null || t.Owner != u.Owner || t.Id == u.Id) return Validation.Fail("Rescue an adjacent captured or boarded ally.");
            if (Hex.Distance(u.Position, t.Position) != 1) return Validation.Fail("Rescue needs an adjacent ally.");
            if (!t.HasR(RStatusType.Captured) && !t.HasR(RStatusType.Boarded)) return Validation.Fail($"{t.Def.Name} is not captured or boarded.");
            var b = t.GetR(RStatusType.Boarded);
            if (b.HasValue && b.Value.Mode == 1) return Validation.Fail($"{t.Def.Name} has been claimed for the rest of the battle.");
            if (t.HasR(RStatusType.Captured) && RosterPassives.RescueBlocked(s, t)) return Validation.Fail($"{t.Def.Name} is held next to a specimen keeper and cannot be freed.");
            return Validation.Success;
        }

        /// <summary>Abilities the UI offers for this unit (its active, and Rescue when an adjacent ally is captured or boarded).</summary>
        public static List<AbilityId> AbilitiesOf(MatchState s, UnitState u)
        {
            var list = new List<AbilityId>();
            if (!u.R.IsToken) list.Add(AbilityId.UnitActive);
            if (s != null && u.OnBoard && u.Position.Neighbors().Any(h => { var t = s.UnitAt(h); return t != null && t.Owner == u.Owner && Inert(t); }))
                list.Add(AbilityId.Rescue);
            return list;
        }

        // ================================================================== apply

        public static void Apply(MatchState s, Command c, List<GameEvent> ev)
        {
            int acting = c.Seat;
            UnitState u = c.UnitId != 0 ? s.Unit(c.UnitId) : (s.ActiveUnitId != 0 ? s.Unit(s.ActiveUnitId) : null);
            if (c.Type == CommandType.Pass && u == null)
                u = s.Units.Where(x => x.Owner == c.Seat && Eligible(s, x)).OrderBy(x => x.Id).First();
            if (s.ActiveUnitId != u.Id) BeginActivation(s, u, ev);

            switch (c.Type)
            {
                case CommandType.Pass:
                    ev.Add(new GameEvent(GameEventType.Passed) { Seat = c.Seat, UnitId = u.Id });
                    break;
                case CommandType.Move:
                {
                    if (c.Option == SliceRules.TurnOption)
                    {
                        int f = SliceRules.TurnFacing(u, c.Target);
                        SliceRules.SetFacing(u, f);
                        u.MovedThisActivation = true; u.MovedRound = s.Round;
                        ev.Add(new GameEvent(GameEventType.FacingChanged) { Seat = u.Owner, UnitId = u.Id, From = u.Position, To = u.Position, Amount = f, Text = "turns " + SliceRules.FacingNames[f] });
                        break;
                    }
                    bool free = u.ActedThisActivation || u.MovedThisActivation;
                    var view = Vision.FoggedView(s, u.Owner);
                    var opt = RosterMovement.Reachable(view, view.Unit(u.Id), c.Option == SliceRules.OverclockOption ? SliceRules.OverclockMove : 0)[c.Target];
                    bool jump = opt.Path.Count == 2 && Hex.Distance(opt.Path[0], opt.Path[1]) > 1;
                    if (free)
                    {
                        u.RStatuses.RemoveAll(x => x.Type == RStatusType.FreeStep);
                        ev.Add(new GameEvent(GameEventType.FreeMove) { Seat = u.Owner, UnitId = u.Id, From = u.Position, To = c.Target });
                    }
                    MoveAlong(s, u, opt.Path, jump, ev, own: true, planned: view);
                    if (u.Alive && u.Position == opt.Path[opt.Path.Count - 1])
                    {
                        int before = u.HasR(RStatusType.Facing) || SliceRules.HasArc(u) ? SliceRules.Facing(u) : -1;
                        SliceRules.FaceAlong(u, opt.Path);
                        if (before >= 0 && SliceRules.Facing(u) != before)
                            ev.Add(new GameEvent(GameEventType.FacingChanged) { Seat = u.Owner, UnitId = u.Id, From = u.Position, To = u.Position, Amount = SliceRules.Facing(u) });
                    }
                    if (c.Option == SliceRules.OverclockOption && u.Alive) SliceRules.AddHeat(s, u, SliceRules.OverclockHeat, ev);
                    if (!free) u.MovedThisActivation = true;
                    u.MovedRound = s.Round;
                    break;
                }
                case CommandType.Attack:
                {
                    if (c.Option == SliceRules.BoardOption) { SliceRules.ApplyBoarding(s, u, c.Target, ev); break; }
                    bool overclock = c.Option == SliceRules.OverclockOption;
                    var o = RosterCombat.ResolveBasic(s, u, c.Target, overclock: overclock);
                    BreakCloak(s, u, ev);
                    u.ActedThisActivation = true; u.AttackedThisActivation = true; u.AttackedRound = s.Round;
                    ev.Add(new GameEvent(GameEventType.AttackResolved)
                    {
                        Seat = u.Owner, UnitId = u.Id, TargetUnitId = o.Roster?.TargetId ?? 0, From = o.From, To = o.Roster != null ? s.Unit(o.Roster.TargetId).Position : o.TargetHex,
                        Amount = o.FinalDamage, FxKey = u.Weapon.FxKey, Text = u.Weapon.Name, Lethal = o.TargetDestroyed,
                    });
                    if (o.TargetIsBulwark)
                    {
                        var b = s.BarrierAt(o.TargetHex);
                        int before = b.Hp;
                        RosterCombat.DamageBarrier(s, b, o.FinalDamage, u, ev);
                        if (before - o.FinalDamage <= 0 && RosterPassives.Has(u, PassiveKind.OverkillThroughCover))
                        {
                            var line = Hex.Line(u.Position, o.TargetHex, 1);
                            var dir = o.TargetHex - line[line.Count - 2];
                            var t2 = s.UnitAt(o.TargetHex + dir);
                            if (t2 != null && t2.Owner != u.Owner)
                                RosterCombat.Hit(s, t2, new HitSpec { Attacker = u, Raw = 1, Cause = u.R.PassiveName, From = u.Position }, ev);
                        }
                    }
                    else RosterCombat.Apply(s, o.Roster, ev);
                    if (overclock && u.Alive) SliceRules.AddHeat(s, u, SliceRules.OverclockHeat, ev);
                    break;
                }
                case CommandType.Ability:
                    if (c.Ability == AbilityId.Rescue) ApplyRescue(s, u, s.UnitAt(c.Target), ev);
                    else RosterAbilities.Apply(s, u, c.Picks, c.Option, ev, Vision.FoggedView(s, u.Owner));
                    break;
            }

            CheckLeaders(s, acting, ev);
            if (s.IsOver) return;

            // The activation ends after the action (or a wait), unless a free step from a kill is pending.
            bool end = c.Type == CommandType.Pass || !u.Alive || !u.OnBoard;
            if (!end && u.ActedThisActivation && !u.HasR(RStatusType.FreeStep)) end = true;
            if (!end && c.Type == CommandType.Move && u.ActedThisActivation) end = true;   // the free step itself
            if (!end && c.Type == CommandType.Move && !CanStillDoAnything(s, u)) end = true;
            if (end) EndActivation(s, u, ev);
        }

        /// <summary>After moving: can the unit still attack, use its active or rescue? If not, its activation ends by itself.</summary>
        static bool CanStillDoAnything(MatchState s, UnitState u)
        {
            if (u.HasR(RStatusType.FreeStep)) return true;
            if (CanAct(s, u, attack: true) == null)
            {
                if (RosterCombat.LegalTargets(s, u).Count > 0) return true;
                if (AbilitiesOf(s, u).Contains(AbilityId.Rescue)) return true;
            }
            if (CanAct(s, u, attack: false) == null && RosterAbilities.AnyUse(s, u)) return true;
            return false;
        }

        static void ApplyRescue(MatchState s, UnitState u, UnitState t, List<GameEvent> ev)
        {
            u.ActedThisActivation = true;
            int need = t.HasR(RStatusType.Captured) ? RosterPassives.RescuesNeeded(s, t) : 1;
            t.RescueProgress++;
            if (t.RescueProgress < need)
            {
                ev.Add(new GameEvent(GameEventType.AbilityActivated) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, Ability = AbilityId.Rescue, Text = $"rescue {t.RescueProgress}/{need}" });
                return;
            }
            t.RescueProgress = 0;
            t.RStatuses.RemoveAll(x => x.Type == RStatusType.Captured || x.Type == RStatusType.Boarded || x.Type == RStatusType.Converting);
            ev.Add(new GameEvent(GameEventType.UnitRescued) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position });
        }

        // ================================================================== activation lifecycle

        public static void BeginActivation(MatchState s, UnitState u, List<GameEvent> ev)
        {
            s.ActiveUnitId = u.Id;
            u.MovedThisActivation = false; u.ActedThisActivation = false; u.AttackedThisActivation = false; u.UsedActiveThisActivation = false;
            foreach (var x in s.Units)
            {
                if (!x.Alive) continue;
                for (int i = x.RStatuses.Count - 1; i >= 0; i--)
                {
                    var st = x.RStatuses[i];
                    if (st.Expire == RExpire.SourceNextActivation && st.SourceId == u.Id)
                    {
                        x.RStatuses.RemoveAt(i);
                        ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = x.Owner, UnitId = u.Id, TargetUnitId = x.Id, To = x.Position, RStatus = st.Type });
                    }
                }
            }
            ev.Add(new GameEvent(GameEventType.ActivationStarted) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Amount = s.Round });
            RosterPassives.OnActivationStart(s, u, ev);
        }

        public static void EndActivation(MatchState s, UnitState u, List<GameEvent> ev)
        {
            if (u.Alive && u.OnBoard)
            {
                RosterPassives.OnActivationEnd(s, u, ev);
                if (u.HasR(RStatusType.Cloaked) && s.Units.Any(e => e.OnBoard && e.Owner != u.Owner && Hex.Distance(e.Position, u.Position) == 1))
                    BreakCloak(s, u, ev);
                if (!u.UsedActiveThisActivation && u.Cooldown > 0) u.Cooldown--;
                SliceRules.Cool(u, ev);
                for (int i = u.RStatuses.Count - 1; i >= 0; i--)
                {
                    var st = u.RStatuses[i];
                    if ((st.Expire == RExpire.AfterOwnActivation && st.ExpireAt <= u.Activations + 1) || st.Type == RStatusType.FreeStep)
                    {
                        u.RStatuses.RemoveAt(i);
                        if (st.Type != RStatusType.FreeStep)
                            ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, RStatus = st.Type });
                    }
                }
                int oi = s.ObjectiveIndex(u.Position);
                if (oi >= 0)
                {
                    var o = s.Objectives[oi];
                    if (o.Owner != u.Owner)
                    {
                        o.Owner = u.Owner;
                        ev.Add(new GameEvent(GameEventType.ObjectiveClaimed) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
                    }
                    o.EverHeld = true;
                    s.Objectives[oi] = o;
                }
            }
            u.Activations++;
            u.ActivatedRound = s.Round;
            u.LastActivationMoved = u.MovedThisActivation;
            u.MovedThisActivation = false; u.ActedThisActivation = false; u.AttackedThisActivation = false; u.UsedActiveThisActivation = false;
            s.ActiveUnitId = 0;
            // "For the next enemy activation" statuses end when an enemy of the affected unit completes an activation.
            foreach (var x in s.Units)
            {
                if (!x.Alive || x.Owner == u.Owner) continue;
                for (int i = x.RStatuses.Count - 1; i >= 0; i--)
                    if (x.RStatuses[i].Expire == RExpire.NextEnemyActivation)
                    {
                        var st = x.RStatuses[i];
                        x.RStatuses.RemoveAt(i);
                        ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = x.Owner, TargetUnitId = x.Id, To = x.Position, RStatus = st.Type });
                    }
            }
            ev.Add(new GameEvent(GameEventType.ActivationEnded) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
            s.ActivationIndex++;
            AdvanceTurn(s, u.Owner, ev);
        }

        static void AdvanceTurn(MatchState s, int lastSeat, List<GameEvent> ev)
        {
            if (s.IsOver) return;
            int other = 1 - lastSeat;
            if (HasEligible(s, other)) s.ActiveSeat = other;
            else if (HasEligible(s, lastSeat)) s.ActiveSeat = lastSeat;
            else
            {
                EndRound(s, lastSeat, ev);
                if (s.IsOver) return;
            }
            ev.Add(new GameEvent(GameEventType.TurnChanged) { Seat = s.ActiveSeat, Amount = s.ActivationIndex });
        }

        static void EndRound(MatchState s, int lastSeat, List<GameEvent> ev)
        {
            int round = s.Round;
            // Conversion Rites complete at the end of the round after they began, if uninterrupted.
            foreach (var t in s.Units.Where(x => x.Alive && x.HasR(RStatusType.Converting)).OrderBy(x => x.Id).ToList())
            {
                var st = t.GetR(RStatusType.Converting).Value;
                if (st.Amount > round) continue;
                var giver = s.Unit(st.SourceId);
                t.RStatuses.RemoveAll(x => x.Type == RStatusType.Converting);
                if (giver != null && giver.OnBoard && !Inert(giver) && t.HasR(RStatusType.Captured) && Hex.Distance(giver.Position, t.Position) == 1)
                    Convert(s, t, giver, ev);
            }
            foreach (var x in s.Units)
            {
                if (!x.Alive) continue;
                for (int i = x.RStatuses.Count - 1; i >= 0; i--)
                {
                    var st = x.RStatuses[i];
                    if (st.Expire == RExpire.RoundEnd && st.ExpireAt <= round)
                    {
                        x.RStatuses.RemoveAt(i);
                        ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = x.Owner, TargetUnitId = x.Id, To = x.Position, RStatus = st.Type });
                    }
                }
            }
            for (int i = s.Barriers.Count - 1; i >= 0; i--)
                if (s.Barriers[i].ExpireRound <= round)
                {
                    var b = s.Barriers[i];
                    s.Barriers.RemoveAt(i);
                    ev.Add(new GameEvent(GameEventType.BarrierExpired) { Seat = b.Owner, UnitId = b.SourceUnitId, To = b.Hex });
                }
            s.Ignited.RemoveAll(h => h.ExpireRound <= round);

            if (s.Config.RoundLimit > 0 && round >= s.Config.RoundLimit)
            {
                ev.Add(new GameEvent(GameEventType.RoundLimitReached) { Amount = round });
                ResolveRoundLimit(s, ev);
                return;
            }
            s.Round++;
            SliceRules.Arrivals(s, ev);
            s.KillsThisRound[0] = s.KillsThisRound[1] = 0;
            s.BoardsThisRound[0] = s.BoardsThisRound[1] = 0;
            int first = 1 - lastSeat;
            if (!HasEligible(s, first)) first = lastSeat;
            s.ActiveSeat = first;
            ev.Add(new GameEvent(GameEventType.RoundStarted) { Seat = first, Amount = s.Round });
        }

        /// <summary>Round limit: most objectives held wins; then surviving army value; then leader HP; then the defender.</summary>
        static void ResolveRoundLimit(MatchState s, List<GameEvent> ev)
        {
            int o0 = s.Objectives.Count(o => o.Owner == 0), o1 = s.Objectives.Count(o => o.Owner == 1);
            int winner;
            if (o0 != o1) winner = o0 > o1 ? 0 : 1;
            else
            {
                int v0 = ArmyValue(s, 0), v1 = ArmyValue(s, 1);
                if (v0 != v1) winner = v0 > v1 ? 0 : 1;
                else
                {
                    int h0 = s.Commander(0)?.Hp ?? 0, h1 = s.Commander(1)?.Hp ?? 0;
                    winner = h0 != h1 ? (h0 > h1 ? 0 : 1) : s.Config.DefenderSeat;
                }
            }
            RulesEngine.EndMatchInternal(s, winner, VictoryReason.RoundLimitObjectives, ev);
        }

        /// <summary>Point value of a seat's surviving, uncaptured units.</summary>
        public static int ArmyValue(MatchState s, int seat) =>
            s.Units.Where(u => u.Alive && u.Owner == seat && !u.HasR(RStatusType.Captured) && !u.HasR(RStatusType.Boarded)).Sum(u => RosterContent.Cost(u.Def));

        /// <summary>The acting seat wins if the enemy leader fell, even if its own also fell.</summary>
        public static void CheckLeaders(MatchState s, int actingSeat, List<GameEvent> ev)
        {
            if (s.IsOver || s.Phase != MatchPhase.Battle) return;
            bool enemyDead = !(s.Commander(1 - actingSeat)?.Alive ?? false);
            bool ownDead = !(s.Commander(actingSeat)?.Alive ?? false);
            if (enemyDead) RulesEngine.EndMatchInternal(s, actingSeat, VictoryReason.CommanderEliminated, ev);
            else if (ownDead) RulesEngine.EndMatchInternal(s, 1 - actingSeat, VictoryReason.CommanderEliminated, ev);
        }

        // ================================================================== state helpers (used by abilities and passives)

        static bool MidActivation(MatchState s, UnitState u) => s.ActiveUnitId == u.Id;

        /// <summary>Applies a status (strongest same-type instance wins; wards and immunities may stop it). Returns false if blocked.</summary>
        public static bool AddStatus(MatchState s, UnitState t, Grant g, int sourceId, List<GameEvent> ev, bool afterActivation = false, bool quiet = false)
        {
            if (t == null || !t.Alive) return false;
            var source = s.Unit(sourceId);
            var ward = RosterPassives.Ward(s, t, g.Type, source);
            if (ward != null)
            {
                ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = t.Owner, UnitId = sourceId, TargetUnitId = t.Id, To = t.Position, RStatus = g.Type, Text = ward });
                return false;
            }
            var st = new RStatus { Type = g.Type, Amount = g.Amount, Uses = g.Uses, SourceId = sourceId, Expire = g.Expire, Range = g.Range };
            switch (g.Expire)
            {
                case RExpire.AfterOwnActivation:
                    st.ExpireAt = t.Activations + Math.Max(1, g.Duration) + (afterActivation || MidActivation(s, t) ? 1 : 0);
                    break;
                case RExpire.RoundEnd: st.ExpireAt = s.Round + Math.Max(1, g.Duration) - 1; break;
            }
            if (g.Type == RStatusType.Converting) st.Amount = s.Round + 1;
            int existing = -1;
            for (int i = 0; i < t.RStatuses.Count; i++)
            {
                var x = t.RStatuses[i];
                if (x.Type != g.Type) continue;
                if ((g.Type == RStatusType.Protected || g.Type == RStatusType.Intercepted) && x.SourceId != sourceId) continue;
                existing = i; break;
            }
            if (existing >= 0)
            {
                var old = t.RStatuses[existing];
                bool stronger = g.Type == RStatusType.Shield || g.Type == RStatusType.AttackBuff || g.Type == RStatusType.Marked
                                || g.Type == RStatusType.ArmorBuff || g.Type == RStatusType.MoveBuff || g.Type == RStatusType.Exposed
                                ? st.Amount >= old.Amount : true;
                if (!stronger) return false;
                t.RStatuses[existing] = st;
            }
            else t.RStatuses.Add(st);
            if (!quiet)
                ev.Add(new GameEvent(GameEventType.RStatusApplied) { Seat = t.Owner, UnitId = sourceId, TargetUnitId = t.Id, To = t.Position, RStatus = g.Type, Amount = g.Amount });
            return true;
        }

        public static void Heal(MatchState s, UnitState u, int amount, UnitState source, List<GameEvent> ev, string cause)
        {
            if (u == null || !u.Alive || amount <= 0) return;
            int before = u.Hp;
            u.Hp = Math.Min(u.MaxHp, u.Hp + amount);
            if (u.Hp > before)
                ev.Add(new GameEvent(GameEventType.UnitHealed) { Seat = u.Owner, UnitId = source?.Id ?? 0, TargetUnitId = u.Id, To = u.Position, Amount = u.Hp - before, HpAfter = u.Hp, Text = cause });
        }

        public static void GrantFreeStep(MatchState s, UnitState u, string why, List<GameEvent> ev)
        {
            if (u.HasR(RStatusType.Rooted)) return;
            u.RStatuses.Add(new RStatus { Type = RStatusType.FreeStep, SourceId = u.Id, Expire = RExpire.AfterOwnActivation, ExpireAt = u.Activations + 1 });
            ev.Add(new GameEvent(GameEventType.RStatusApplied) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, RStatus = RStatusType.FreeStep, Text = why });
        }

        public static void BreakCloak(MatchState s, UnitState u, List<GameEvent> ev)
        {
            int i = u.RIndex(RStatusType.Cloaked);
            if (i < 0) return;
            u.RStatuses.RemoveAt(i);
            ev.Add(new GameEvent(GameEventType.RStatusExpired) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, RStatus = RStatusType.Cloaked, Text = "cloak broken" });
        }

        public static void Destroy(MatchState s, UnitState u, UnitState killer, List<GameEvent> ev)
        {
            if (!u.Alive) return;
            u.Alive = false;
            u.Hp = 0;
            u.RStatuses.Clear();
            ev.Add(new GameEvent(GameEventType.UnitDestroyed) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Text = u.Def.Name });
            if (RosterCombat.IsLeader(s, u))
                ev.Add(new GameEvent(GameEventType.CommanderKilled) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
            else if (!u.R.IsToken && s.WreckIndex(u.Position) < 0)
            {
                s.Wrecks.Add(new Wreck { Hex = u.Position, Faction = u.Def.Faction, Archetype = u.Def.Archetype, Round = s.Round });
                ev.Add(new GameEvent(GameEventType.WreckCreated) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
            }
            // Boarding and conversions held by the destroyed unit end with it.
            foreach (var x in s.Units)
            {
                if (!x.Alive) continue;
                int n = x.RStatuses.RemoveAll(st => (st.Type == RStatusType.Boarded || st.Type == RStatusType.Converting) && st.SourceId == u.Id);
                if (n > 0) ev.Add(new GameEvent(GameEventType.UnitRescued) { Seat = x.Owner, UnitId = u.Id, TargetUnitId = x.Id, To = x.Position, Text = "holder destroyed" });
            }
            RosterPassives.OnDestroyed(s, u, ev);
            RosterPassives.OnKill(s, killer, u, ev);
        }

        /// <summary>Conversion Rite: the captured unit becomes a 1-HP Cräigon auxiliary of the Giftbearer's side.</summary>
        static void Convert(MatchState s, UnitState t, UnitState giver, List<GameEvent> ev)
        {
            int oldOwner = t.Owner;
            t.Owner = giver.Owner;
            t.DefId = t.R.IsShip ? "token_craigon_auxiliary_orbital" : "token_craigon_auxiliary_planetary";
            RosterArmy.Recompute(t, s.Config.Players[t.Owner].Caps);
            t.Hp = 1;
            t.RStatuses.Clear();
            t.Converted = true;
            t.Cooldown = 0;
            t.ActivatedRound = s.Round;   // it acts from the next round
            // UnitId = TargetUnitId: the same unit changes sides (presentation rebuilds its view in the new colours).
            ev.Add(new GameEvent(GameEventType.UnitConverted) { Seat = giver.Owner, UnitId = t.Id, TargetUnitId = t.Id, To = t.Position, Amount = oldOwner, Text = giver.Def.Name });
        }

        /// <summary>Moves a unit along a planned path (own movement or a dash). Hidden enemies stop it short (ambush),
        /// the first enemy mine it enters detonates and stops it there; ending on a burning hex hurts.</summary>
        public static void MoveAlong(MatchState s, UnitState u, List<Hex> path, bool jump, List<GameEvent> ev, bool own, MatchState planned = null)
        {
            var from = u.Position;
            int stop = path.Count - 1;
            int ambusher = 0; Hex ambushHex = default;
            RosterMine mine = null;
            bool passer = jump || u.R.Pattern == MovePattern.Phase;
            for (int i = 1; i < path.Count; i++)
            {
                bool final = i == path.Count - 1;
                var occ = s.UnitAt(path[i]);
                if (occ != null && occ.Id != u.Id)
                {
                    // Planned routes only pass units the mover could see (allies, or anything for PHASE and jumps);
                    // a hidden enemy on the route, or anyone on the destination, stops the move just short of it.
                    bool hidden = occ.Owner != u.Owner && planned != null && !ReferenceEquals(planned, s) && planned.UnitAt(path[i]) == null;
                    bool canPass = !final && (passer || occ.Owner == u.Owner) && !(hidden && !passer);
                    if (!canPass)
                    {
                        if (occ.Owner != u.Owner) { ambusher = occ.Id; ambushHex = path[i]; }
                        stop = i - 1;
                        break;
                    }
                    continue;
                }
                if (jump && !final) continue;
                var m = s.Mines.FirstOrDefault(x => x.Hex == path[i] && x.Owner != u.Owner);
                if (m != null) { mine = m; stop = i; break; }
            }
            if (jump && stop < path.Count - 1) stop = 0;
            while (stop > 0 && s.UnitAt(path[stop]) != null && s.UnitAt(path[stop]).Id != u.Id) stop--;
            if (stop > 0)
            {
                u.Position = path[stop];
                if (jump)
                    ev.Add(new GameEvent(GameEventType.UnitJumped) { Seat = u.Owner, UnitId = u.Id, From = from, To = u.Position });
                else
                    ev.Add(new GameEvent(GameEventType.UnitMoved) { Seat = u.Owner, UnitId = u.Id, From = from, To = u.Position, Path = path.GetRange(0, stop + 1), Amount = stop });
            }
            if (ambusher != 0)
                ev.Add(new GameEvent(GameEventType.Ambushed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = ambusher, From = u.Position, To = ambushHex, Text = "ambush" });
            if (mine != null && u.Position == mine.Hex)
            {
                s.Mines.Remove(mine);
                ev.Add(new GameEvent(GameEventType.MineTriggered) { Seat = mine.Owner, UnitId = mine.SourceUnitId, TargetUnitId = u.Id, To = mine.Hex, Amount = mine.Damage });
                RosterCombat.Hit(s, u, new HitSpec { Attacker = null, Raw = mine.Damage, Cause = "mine", From = mine.Hex }, ev);
                if (!u.Alive) CreditKill(s, mine.Owner, u, ev);
            }
            if (stop > 0 && u.Alive) EndedOn(s, u, ev, own);
        }

        static void CreditKill(MatchState s, int seat, UnitState victim, List<GameEvent> ev)
        {
            if (victim.Owner != seat) s.KillsThisRound[seat]++;
        }

        /// <summary>Hazards and passives when a unit ends a move on a hex.</summary>
        public static void EndedOn(MatchState s, UnitState u, List<GameEvent> ev, bool own)
        {
            if (RosterTerrain.IsBurning(s, u.Position))
            {
                int dmg = RosterPassives.HazardDamage(s, u);
                if (dmg > 0)
                {
                    ev.Add(new GameEvent(GameEventType.HazardDamage) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Amount = dmg });
                    RosterCombat.Hit(s, u, new HitSpec { Raw = dmg, Pierce = 99, Cause = "hazard", From = u.Position }, ev);
                }
            }
            if (own && u.Alive) RosterPassives.OnMoveEnded(s, u, ev);
        }

        /// <summary>Forced movement of <paramref name="u"/> to <paramref name="to"/> by <paramref name="source"/>. Returns why it failed (null = moved).</summary>
        public static string ForceMove(MatchState s, UnitState u, Hex to, UnitState source, List<GameEvent> ev)
        {
            if (Hex.Distance(u.Position, to) == 0) return "no movement";
            var immune = RosterPassives.ForcedMoveImmune(s, u, source);
            if (immune != null) return immune;
            if (!RosterMovement.Open(s, to)) return "the destination is blocked";
            if (RosterCombat.IsLeader(s, u) && !s.Map.Contains(to)) return "a leader cannot be pushed off the board";
            var from = u.Position;
            u.Position = to;
            ev.Add(new GameEvent(GameEventType.UnitDisplaced) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = source?.Id ?? 0, From = from, To = to });
            var mine = s.Mines.FirstOrDefault(x => x.Hex == to && x.Owner != u.Owner);
            if (mine != null)
            {
                s.Mines.Remove(mine);
                ev.Add(new GameEvent(GameEventType.MineTriggered) { Seat = mine.Owner, UnitId = mine.SourceUnitId, TargetUnitId = u.Id, To = mine.Hex, Amount = mine.Damage });
                RosterCombat.Hit(s, u, new HitSpec { Raw = mine.Damage, Cause = "mine", From = mine.Hex }, ev);
            }
            if (u.Alive) EndedOn(s, u, ev, own: false);
            return null;
        }
    }
}
