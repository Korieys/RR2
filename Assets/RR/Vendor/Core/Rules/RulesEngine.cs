using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// The authoritative rules service. All state changes happen here, through <see cref="Apply"/>.
    /// Deterministic and engine-agnostic: the network host, local play, AI search, tutorial and replays all use it.
    /// </summary>
    public static partial class RulesEngine
    {
        public const int NonceMemory = 64;

        // ================================================================== setup

        /// <summary>Validates both armies and creates the initial (deployment-phase) state.</summary>
        public static MatchState CreateMatch(MatchConfig config, out string error)
        {
            error = null;
            if (config?.Players == null || config.Players.Length != 2) { error = "Match needs exactly two players."; return null; }
            // The battlefield was validated when it was registered (connectivity, dead ends, approaches); a layout that
            // failed even after repair never starts a battle.
            var map = MapLibrary.Get(config.MapId);
            if (map.Topology != null && !map.Topology.Passed)
            { error = $"The battlefield '{map.Name}' failed validation: {map.Topology.AllErrors().First()}"; return null; }
            if (config.IsRoster) return RosterRules.CreateMatch(config, out error);
            for (int seat = 0; seat < 2; seat++)
            {
                var p = config.Players[seat];
                var report = ArmyRules.Validate(p.Army, p.Caps, config.PointBudget, config.MaxUnits);
                if (!report.Valid) { error = $"{p.DisplayName}'s army is invalid: {report.Errors[0]}"; return null; }
            }
            var s = new MatchState { Config = config, Phase = MatchPhase.Deployment, ActiveSeat = config.FirstSeat };
            for (int seat = 0; seat < 2; seat++)
            {
                var p = config.Players[seat];
                var ps = s.Players[seat];
                ps.Faction = p.Army.Faction;
                ps.Doctrine = p.Army.Faction == FactionId.Inhumanus ? p.Army.Doctrine : Doctrine.None;
                ps.ClockRemainingMs = config.Timed ? config.ClockSeconds * 1000 : 0;
                foreach (var entry in p.Army.Units)
                {
                    var u = UnitFactory.Create(s.NextUnitId++, seat, entry, ps.Doctrine);
                    s.Units.Add(u);
                    if (u.IsCommander) ps.CommanderId = u.Id;
                }
            }
            return s;
        }

        public static MatchState CreateMatch(MatchConfig config)
        {
            var s = CreateMatch(config, out var err);
            if (s == null) throw new InvalidOperationException(err);
            return s;
        }

        // ================================================================== queries used by UI / AI

        public static bool CanActivate(MatchState s, UnitState u, out string reason)
        {
            if (s.IsRoster) return RosterRules.CanActivate(s, u, out reason);
            reason = null;
            if (s.Phase != MatchPhase.Battle) { reason = s.Phase == MatchPhase.Ended ? "The match is over." : "The battle has not started."; return false; }
            if (u == null || !u.Alive) { reason = "That unit has been destroyed."; return false; }
            if (!u.Deployed) { reason = "That unit is not on the battlefield."; return false; }
            if (u.Owner != s.ActiveSeat) { reason = "It is not that unit's turn."; return false; }
            if (u.HasStatus(StatusType.Dazed)) { reason = $"{u.Def.Name} is Dazed and cannot act this activation."; return false; }
            return true;
        }

        public static Validation CanGuard(MatchState s, UnitState u)
        {
            if (s.IsRoster) return Validation.Fail("Full Roster rules have no generic Guard action: use a GUARD ability.");
            if (!CanActivate(s, u, out var r)) return Validation.Fail(r);
            if (u.Guarded) return Validation.Fail("Already guarding.");
            if (u.HasStatus(StatusType.Overheated)) return Validation.Fail("Overheated units cannot Guard.");
            if (u.HasStatus(StatusType.Lien)) return Validation.Fail("A Lien prevents this unit from Guarding.");
            return Validation.Success;
        }

        /// <summary>Abilities the unit could use right now, including the implicit Settle Debt.</summary>
        /// <summary>Abilities the unit could use (Full Roster: its active, plus Rescue when an adjacent ally is captured or boarded).</summary>
        public static List<AbilityId> AbilitiesOf(MatchState s, UnitState u) => s.IsRoster ? RosterRules.AbilitiesOf(s, u) : AbilitiesOf(u);

        public static List<AbilityId> AbilitiesOf(UnitState u)
        {
            if (u.Def.IsRoster) return RosterRules.AbilitiesOf(null, u);
            var list = u.Abilities.Select(a => a.Ability).ToList();
            if (u.HasStatus(StatusType.Lien) && !list.Contains(AbilityId.SettleDebt)) list.Add(AbilityId.SettleDebt);
            return list;
        }

        public static int ChargesLeft(UnitState u, AbilityId id)
        {
            if (u.Def.IsRoster) return -1;
            if (id == AbilityId.SettleDebt) return -1;
            int i = u.AbilityIndex(id);
            return i < 0 ? 0 : u.Abilities[i].Charges;
        }

        // ================================================================== apply

        /// <summary>Validate and apply a command. Rejected commands leave the state untouched.</summary>
        public static CommandResult Apply(MatchState s, Command c)
        {
            var v = Validate(s, c);
            if (!v.Ok) return CommandResult.Reject(c, v.Reason);
            var result = new CommandResult { Accepted = true, Command = c };
            var ev = result.Events;

            if (c.Nonce != 0)
            {
                s.RecentNonces.Add(c.Nonce);
                if (s.RecentNonces.Count > NonceMemory) s.RecentNonces.RemoveAt(0);
            }
            // Decision clock: deduct the host-stamped elapsed time for battle actions.
            if (s.Phase == MatchPhase.Battle && s.Config.Timed && IsActivation(c.Type) && c.Seat == s.ActiveSeat)
                s.Players[c.Seat].ClockRemainingMs = Math.Max(0, s.Players[c.Seat].ClockRemainingMs - Math.Max(0, c.ElapsedMs));

            switch (c.Type)
            {
                case CommandType.CommitDeployment: ApplyDeployment(s, c.Seat, c.Placements, ev); break;
                case CommandType.AutoDeploy: ApplyDeployment(s, c.Seat, AutoDeployment(s, c.Seat), ev); break;
                case CommandType.Move when s.IsRoster:
                case CommandType.Attack when s.IsRoster:
                case CommandType.Ability when s.IsRoster:
                case CommandType.Pass when s.IsRoster:
                    RosterRules.Apply(s, c, ev);
                    break;
                case CommandType.Move: ApplyMove(s, s.Unit(c.UnitId), c.Target, ev); FinishActivation(s, ev); break;
                case CommandType.Attack: ApplyAttack(s, s.Unit(c.UnitId), c.Target, ev); FinishActivation(s, ev); break;
                case CommandType.Ability: ApplyAbility(s, c, ev); FinishActivation(s, ev); break;
                case CommandType.Guard:
                {
                    var u = s.Unit(c.UnitId);
                    u.Guarded = true;
                    ev.Add(new GameEvent(GameEventType.GuardApplied) { Seat = c.Seat, UnitId = u.Id, To = u.Position });
                    FinishActivation(s, ev);
                    break;
                }
                case CommandType.Pass:
                    ev.Add(new GameEvent(GameEventType.Passed) { Seat = c.Seat });
                    FinishActivation(s, ev);
                    break;
                case CommandType.Resign:
                    ev.Add(new GameEvent(GameEventType.PlayerResigned) { Seat = c.Seat });
                    EndMatch(s, 1 - c.Seat, VictoryReason.Resignation, ev);
                    break;
                case CommandType.ClockExpire:
                    s.Players[c.Seat].ClockRemainingMs = 0;
                    ev.Add(new GameEvent(GameEventType.ClockExpired) { Seat = c.Seat });
                    EndMatch(s, 1 - c.Seat, VictoryReason.ClockExpired, ev);
                    break;
                case CommandType.Forfeit:
                    ev.Add(new GameEvent(GameEventType.PlayerForfeited) { Seat = c.Seat });
                    EndMatch(s, 1 - c.Seat, VictoryReason.Disconnection, ev);
                    break;
            }
            s.Revision++;
            result.Revision = s.Revision;
            return result;
        }

        public static bool IsActivation(CommandType t) =>
            t == CommandType.Move || t == CommandType.Attack || t == CommandType.Ability || t == CommandType.Guard || t == CommandType.Pass;

        public static Validation Validate(MatchState s, Command c)
        {
            if (c == null) return Validation.Fail("Empty command.");
            if (s.IsOver) return Validation.Fail("The match is over.");
            if (c.Seat != 0 && c.Seat != 1) return Validation.Fail("Invalid seat.");
            if (c.ExpectedRevision >= 0 && c.ExpectedRevision != s.Revision) return Validation.Fail("Stale command: the battlefield changed. Please try again.");
            if (c.Nonce != 0 && s.RecentNonces.Contains(c.Nonce)) return Validation.Fail("Duplicate command ignored.");

            switch (c.Type)
            {
                case CommandType.CommitDeployment: return ValidateDeployment(s, c.Seat, c.Placements);
                case CommandType.AutoDeploy:
                    if (s.Phase != MatchPhase.Deployment) return Validation.Fail("Deployment is over.");
                    if (s.Players[c.Seat].DeploymentCommitted) return Validation.Fail("Deployment already committed.");
                    return Validation.Success;
                case CommandType.Resign:
                case CommandType.Forfeit:
                    return Validation.Success;
                case CommandType.ClockExpire:
                    if (!s.Config.Timed) return Validation.Fail("This match is untimed.");
                    if (s.Phase != MatchPhase.Battle || s.ActiveSeat != c.Seat) return Validation.Fail("Only the active player's clock can expire.");
                    return Validation.Success;
            }

            // ---- battle activations
            if (s.Phase != MatchPhase.Battle) return Validation.Fail("The battle has not started.");
            if (c.Seat != s.ActiveSeat) return Validation.Fail("It is not your turn.");
            if (s.Config.Timed && c.ElapsedMs >= s.Players[c.Seat].ClockRemainingMs) return Validation.Fail("Your clock has run out.");
            if (s.IsRoster) return RosterRules.Validate(s, c);
            if (c.Type == CommandType.Pass) return Validation.Success;

            var u = s.Unit(c.UnitId);
            if (u == null) return Validation.Fail("No unit selected.");
            if (u.Owner != c.Seat) return Validation.Fail("That unit belongs to your opponent.");
            if (!CanActivate(s, u, out var why)) return Validation.Fail(why);

            switch (c.Type)
            {
                case CommandType.Move:
                {
                    if (c.Target == u.Position) return Validation.Fail("The unit is already there.");
                    if (!s.Map.Contains(c.Target)) return Validation.Fail("That hex is outside the battlefield.");
                    // With fog of war the mover plans on what its side can see; hidden enemies are resolved as ambushes.
                    var view = Vision.FoggedView(s, u.Owner);
                    var vu = view.Unit(u.Id);
                    var reach = BoardQueries.Reachable(view, vu);
                    if (reach.ContainsKey(c.Target)) return Validation.Success;
                    return Validation.Fail(ExplainUnreachable(view, vu, c.Target));
                }
                case CommandType.Attack:
                {
                    var o = Combat.Resolve(s, u, c.Target);
                    return o.Legal ? Validation.Success : Validation.Fail(o.Reason);
                }
                case CommandType.Guard: return CanGuard(s, u);
                case CommandType.Ability: return ValidateAbility(s, u, c.Ability, c.Target, c.Option, c.Secondary);
            }
            return Validation.Fail("Unknown command.");
        }

        public static string ExplainUnreachable(MatchState s, UnitState u, Hex target)
        {
            if (s.IsRoster) return RosterMovement.Explain(s, u, target);
            if (!s.Map.Contains(target)) return "That hex is outside the battlefield.";
            if (s.Map.Terrain(target) == TerrainType.Obstacle) return "Impassable obstacle.";
            if (s.BulwarkAt(target) != null) return "A crystal bulwark blocks that hex.";
            var occ = s.UnitAt(target);
            if (occ != null) return occ.Owner == u.Owner ? "Another of your units is standing there." : "An enemy unit occupies that hex.";
            int move = BoardQueries.EffectiveMove(s, u);
            var all = BoardQueries.Reachable(s, u, 99);
            if (!all.ContainsKey(target)) return "No open path to that hex.";
            return $"Too far: needs {all[target].Cost} movement, {u.Def.Name} has {move}.";
        }

        // ================================================================== activation lifecycle

        static void RemoveGuardOnAction(UnitState u, List<GameEvent> ev)
        {
            if (!u.Guarded) return;
            u.Guarded = false;
            ev.Add(new GameEvent(GameEventType.GuardConsumed) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Text = "acted" });
        }

        static void FinishActivation(MatchState s, List<GameEvent> ev)
        {
            if (s.IsOver) return;
            s.ActivationIndex++;
            int idx = s.ActivationIndex;

            // Expire timed statuses and board effects.
            foreach (var u in s.Units)
            {
                if (!u.Alive) continue;
                for (int i = u.Statuses.Count - 1; i >= 0; i--)
                {
                    var st = u.Statuses[i];
                    if (st.ExpiresAt <= idx)
                    {
                        u.Statuses.RemoveAt(i);
                        ev.Add(new GameEvent(GameEventType.StatusExpired) { Seat = u.Owner, UnitId = u.Id, Status = st.Type, To = u.Position });
                    }
                }
            }
            for (int i = s.Bulwarks.Count - 1; i >= 0; i--)
            {
                if (s.Bulwarks[i].ExpiresAt <= idx)
                {
                    var b = s.Bulwarks[i];
                    s.Bulwarks.RemoveAt(i);
                    ev.Add(new GameEvent(GameEventType.BulwarkExpired) { Seat = b.Owner, UnitId = b.SourceUnitId, To = b.Hex });
                }
            }
            for (int i = s.Smoke.Count - 1; i >= 0; i--)
            {
                if (s.Smoke[i].ExpiresAt <= idx)
                {
                    ev.Add(new GameEvent(GameEventType.SmokeExpired) { To = s.Smoke[i].Hex });
                    s.Smoke.RemoveAt(i);
                }
            }

            if (s.Config.ActivationLimit > 0 && idx >= s.Config.ActivationLimit)
            {
                ev.Add(new GameEvent(GameEventType.ActivationLimitReached) { Amount = idx });
                ResolveStalemate(s, ev);
                return;
            }

            s.ActiveSeat = 1 - s.ActiveSeat;
            ev.Add(new GameEvent(GameEventType.TurnChanged) { Seat = s.ActiveSeat, Amount = idx });
        }

        static void ResolveStalemate(MatchState s, List<GameEvent> ev)
        {
            if (s.Config.Stalemate == StalemateRule.DefenderDenial)
            {
                // Denial has to be earned in the middle: if only the attacker's units stand in no-man's land when the
                // clock runs out, the attacker holds the field and wins. Any defender unit there contests it.
                int attacker = 1 - s.Config.DefenderSeat;
                if (HoldsNoMansLand(s, attacker)) { EndMatch(s, attacker, VictoryReason.ActivationLimitHeldGround, ev); return; }
                EndMatch(s, s.Config.DefenderSeat, VictoryReason.ActivationLimitDefender, ev);
                return;
            }
            int v0 = ArmyValue(s, 0), v1 = ArmyValue(s, 1);
            int winner;
            if (v0 != v1) winner = v0 > v1 ? 0 : 1;
            else
            {
                int h0 = s.Commander(0)?.Hp ?? 0, h1 = s.Commander(1)?.Hp ?? 0;
                winner = h0 != h1 ? (h0 > h1 ? 0 : 1) : 1 - s.Config.FirstSeat;
            }
            EndMatch(s, winner, VictoryReason.ActivationLimitTiebreak, ev);
        }

        /// <summary>True when <paramref name="seat"/> has units in no-man's land (the rows between the deployment
        /// territories) and the other side has none there. Uses the full state (fog of war never hides the result).</summary>
        public static bool HoldsNoMansLand(MatchState s, int seat)
        {
            bool mine = false;
            foreach (var u in s.Units)
            {
                if (!u.OnBoard || s.Map.ZoneSeat(u.Position) >= 0) continue;
                if (u.Owner != seat) return false;
                mine = true;
            }
            return mine;
        }

        /// <summary>Point value of a seat's surviving units (used by the exhibition tiebreak and analytics).</summary>
        public static int ArmyValue(MatchState s, int seat)
        {
            if (s.IsRoster) return RosterRules.ArmyValue(s, seat);
            int total = 0;
            foreach (var u in s.Units)
                if (u.Alive && u.Owner == seat)
                    total += ArmyRules.EntryCost(new RosterEntry { UnitId = u.DefId, Equipment = u.Equipment }, u.Def.Faction);
            return total;
        }

        internal static void EndMatchInternal(MatchState s, int winner, VictoryReason reason, List<GameEvent> ev) => EndMatch(s, winner, reason, ev);

        static void EndMatch(MatchState s, int winner, VictoryReason reason, List<GameEvent> ev)
        {
            if (s.IsOver) return;
            s.Phase = MatchPhase.Ended;
            s.Winner = winner;
            s.VictoryReason = reason;
            ev.Add(new GameEvent(GameEventType.MatchEnded) { Seat = winner, Reason = reason });
        }

        /// <summary>Checks Commander deaths after a resolution. The acting seat wins if the enemy Commander fell, even if its own also fell.</summary>
        static void CheckCommanders(MatchState s, int actingSeat, List<GameEvent> ev)
        {
            if (s.IsOver) return;
            bool enemyDead = !(s.Commander(1 - actingSeat)?.Alive ?? false);
            bool ownDead = !(s.Commander(actingSeat)?.Alive ?? false);
            if (enemyDead) EndMatch(s, actingSeat, VictoryReason.CommanderEliminated, ev);
            else if (ownDead) EndMatch(s, 1 - actingSeat, VictoryReason.CommanderEliminated, ev);
        }

        // ================================================================== damage / destruction

        static void DamageUnit(MatchState s, UnitState u, int amount, int sourceUnitId, List<GameEvent> ev, string cause)
        {
            if (!u.Alive || amount <= 0) return;
            if (amount >= u.Hp && u.HasStatus(StatusType.Unbreakable))
            {
                amount = u.Hp - 1;
                u.RemoveStatus(StatusType.Unbreakable);
                ev.Add(new GameEvent(GameEventType.UnbreakableTriggered) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
                if (amount <= 0) return;
            }
            u.Hp -= amount;
            bool lethal = u.Hp <= 0;
            ev.Add(new GameEvent(GameEventType.DamageApplied)
                { Seat = u.Owner, UnitId = sourceUnitId, TargetUnitId = u.Id, To = u.Position, Amount = amount, HpAfter = Math.Max(0, u.Hp), Lethal = lethal, Text = cause });
            if (lethal) DestroyUnit(s, u, ev, leaveWreck: true);
        }

        static void DestroyUnit(MatchState s, UnitState u, List<GameEvent> ev, bool leaveWreck)
        {
            u.Alive = false;
            u.Hp = 0;
            u.Guarded = false;
            u.Statuses.Clear();
            ev.Add(new GameEvent(GameEventType.UnitDestroyed) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Text = u.Def.Name });
            if (u.IsCommander)
                ev.Add(new GameEvent(GameEventType.CommanderKilled) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
            else if (leaveWreck && s.WreckIndex(u.Position) < 0)
            {
                s.Wrecks.Add(new Wreck { Hex = u.Position, Faction = u.Def.Faction, Archetype = u.Def.Archetype });
                ev.Add(new GameEvent(GameEventType.WreckCreated) { Seat = u.Owner, UnitId = u.Id, To = u.Position });
            }
            // A destroyed lien holder voids its liens.
            foreach (var other in s.Units)
            {
                if (!other.Alive) continue;
                var lien = other.GetStatus(StatusType.Lien);
                if (lien.HasValue && lien.Value.SourceUnitId == u.Id)
                {
                    other.RemoveStatus(StatusType.Lien);
                    ev.Add(new GameEvent(GameEventType.StatusExpired) { Seat = other.Owner, UnitId = other.Id, Status = StatusType.Lien, To = other.Position, Text = "holder destroyed" });
                }
            }
        }

        // ================================================================== move / attack

        static void ApplyMove(MatchState s, UnitState u, Hex to, List<GameEvent> ev)
        {
            var view = Vision.FoggedView(s, u.Owner);
            var opt = BoardQueries.Reachable(view, view.Unit(u.Id))[to];
            var path = opt.Path;
            int stop = path.Count - 1;
            int ambusher = 0; Hex ambushHex = default;
            if (!ReferenceEquals(view, s))
            {
                // Walk the planned path; the first hidden enemy on it springs an ambush and the mover halts just before it.
                for (int i = 1; i < path.Count; i++)
                {
                    var occ = s.UnitAt(path[i]);
                    if (occ != null && occ.Owner != u.Owner) { stop = i - 1; ambusher = occ.Id; ambushHex = path[i]; break; }
                }
                while (stop > 0 && s.UnitAt(path[stop]) != null) stop--;   // never stop on an ally it was passing through
            }
            RemoveGuardOnAction(u, ev);
            var from = u.Position;
            if (stop > 0)
            {
                u.Position = path[stop];
                ev.Add(new GameEvent(GameEventType.UnitMoved)
                    { Seat = u.Owner, UnitId = u.Id, From = from, To = u.Position, Path = path.GetRange(0, stop + 1), Amount = ambusher == 0 ? opt.Cost : stop });
            }
            if (ambusher != 0)
                ev.Add(new GameEvent(GameEventType.Ambushed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = ambusher, From = u.Position, To = ambushHex, Text = "ambush" });
            if (stop > 0) ApplyHazard(s, u, ev);
            CheckCommanders(s, u.Owner, ev);
        }

        static void ApplyHazard(MatchState s, UnitState u, List<GameEvent> ev)
        {
            if (!u.Alive || u.Hover || s.Map.Terrain(u.Position) != TerrainType.Hazard) return;
            ev.Add(new GameEvent(GameEventType.HazardDamage) { Seat = u.Owner, UnitId = u.Id, To = u.Position, Amount = 1 });
            DamageUnit(s, u, 1, 0, ev, "hazard");
        }

        static void ApplyAttack(MatchState s, UnitState attacker, Hex target, List<GameEvent> ev)
        {
            var o = Combat.Resolve(s, attacker, target);
            RemoveGuardOnAction(attacker, ev);
            ResolveAttackOutcome(s, attacker, o, ev);
            CheckCommanders(s, attacker.Owner, ev);
        }

        static void ResolveAttackOutcome(MatchState s, UnitState attacker, AttackOutcome o, List<GameEvent> ev)
        {
            var w = attacker.Weapon;
            if (w.CooldownTurns > 0) attacker.WeaponReadyAt = s.ActivationIndex + 2 * (w.CooldownTurns + 1);
            if (o.JuryRigConsumed) attacker.RemoveStatus(StatusType.JuryRigged);
            ev.Add(new GameEvent(GameEventType.AttackResolved)
            {
                Seat = attacker.Owner, UnitId = attacker.Id, TargetUnitId = o.TargetUnitId, From = o.From, To = o.TargetHex,
                Amount = o.FinalDamage, FxKey = w.FxKey, Text = w.Name, Lethal = o.TargetDestroyed,
            });

            if (o.TargetIsBulwark)
            {
                var b = s.BulwarkAt(o.TargetHex);
                b.Hp = o.TargetHpAfter;
                if (b.Hp <= 0)
                {
                    s.Bulwarks.Remove(b);
                    ev.Add(new GameEvent(GameEventType.BulwarkDestroyed) { Seat = b.Owner, UnitId = attacker.Id, To = b.Hex });
                }
                else ev.Add(new GameEvent(GameEventType.BulwarkDamaged) { Seat = b.Owner, UnitId = attacker.Id, To = b.Hex, Amount = o.FinalDamage, HpAfter = b.Hp });
            }
            else
            {
                var t = s.Unit(o.TargetUnitId);
                if (o.WardBlocked)
                {
                    t.RemoveStatus(StatusType.Ward);
                    ev.Add(new GameEvent(GameEventType.WardBlocked) { Seat = t.Owner, UnitId = attacker.Id, TargetUnitId = t.Id, To = t.Position });
                }
                if (o.GuardConsumed)
                {
                    t.Guarded = false;
                    ev.Add(new GameEvent(GameEventType.GuardConsumed) { Seat = t.Owner, UnitId = t.Id, To = t.Position, Text = "absorbed" });
                }
                if (o.UnbreakableTriggered)
                {
                    t.RemoveStatus(StatusType.Unbreakable);
                    ev.Add(new GameEvent(GameEventType.UnbreakableTriggered) { Seat = t.Owner, UnitId = t.Id, To = t.Position });
                }
                if (o.FinalDamage > 0)
                {
                    t.Hp = o.TargetHpAfter;
                    ev.Add(new GameEvent(GameEventType.DamageApplied)
                        { Seat = t.Owner, UnitId = attacker.Id, TargetUnitId = t.Id, To = t.Position, Amount = o.FinalDamage, HpAfter = Math.Max(0, t.Hp), Lethal = o.TargetDestroyed, Text = w.Name });
                    if (o.TargetDestroyed) DestroyUnit(s, t, ev, leaveWreck: true);
                }
                if (o.ExposeApplied && t.Alive)
                {
                    t.SetStatus(new StatusEffect(StatusType.Exposed, s.ActivationIndex + 3, attacker.Id));
                    ev.Add(new GameEvent(GameEventType.StatusApplied) { Seat = t.Owner, UnitId = attacker.Id, TargetUnitId = t.Id, Status = StatusType.Exposed, To = t.Position });
                }
                if (o.RiposteDamage > 0 && t.Alive)
                {
                    ev.Add(new GameEvent(GameEventType.Riposte) { Seat = t.Owner, UnitId = t.Id, TargetUnitId = attacker.Id, From = t.Position, To = attacker.Position, Amount = o.RiposteDamage });
                    DamageUnit(s, attacker, o.RiposteDamage, t.Id, ev, "riposte");
                }
            }
            foreach (var hit in o.Splash)
            {
                var u = s.Unit(hit.UnitId);
                if (u == null || !u.Alive) continue;
                DamageUnit(s, u, 1, attacker.Id, ev, "splash");
            }
        }
    }
}
