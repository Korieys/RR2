using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    public static partial class RulesEngine
    {
        // ================================================================== deployment

        public static Validation ValidateDeployment(MatchState s, int seat, List<Placement> placements)
        {
            if (s.Phase != MatchPhase.Deployment) return Validation.Fail("Deployment is over.");
            if (s.Players[seat].DeploymentCommitted) return Validation.Fail("Deployment already committed.");
            var mine = s.Units.Where(u => u.Owner == seat && u.Alive).Select(u => u.Id).ToList();
            if (placements == null || placements.Count != mine.Count)
                return Validation.Fail($"Deploy all {mine.Count} units before confirming.");
            var usedHexes = new HashSet<Hex>();
            var usedUnits = new HashSet<int>();
            foreach (var p in placements)
            {
                if (!mine.Contains(p.UnitId)) return Validation.Fail("Unknown unit in deployment.");
                if (!usedUnits.Add(p.UnitId)) return Validation.Fail("A unit was placed twice.");
                if (!s.Map.Contains(p.Hex)) return Validation.Fail("A unit was placed off the battlefield.");
                if (!s.Map.IsDeploymentHex(seat, p.Hex))
                {
                    var t = s.Map.Terrain(p.Hex);
                    if (!TerrainRules.Deployable(t)) return Validation.Fail($"Units cannot deploy onto {t} terrain.");
                    return Validation.Fail("Units must deploy inside your deployment zone.");
                }
                if (!usedHexes.Add(p.Hex)) return Validation.Fail("Two units cannot share a hex.");
            }
            return Validation.Success;
        }

        /// <summary>Deterministic legal deployment used for timeouts, the AI and quick starts.</summary>
        public static List<Placement> AutoDeployment(MatchState s, int seat)
        {
            var zone = s.Map.DeploymentZone(seat);
            int back = seat == 0 ? s.Map.Radius : -s.Map.Radius;
            var units = s.Units.Where(u => u.Owner == seat && u.Alive)
                               .OrderBy(u => DeployPriority(u.Def.Archetype)).ThenBy(u => u.Id).ToList();
            var free = new List<Hex>(zone);
            var result = new List<Placement>();
            foreach (var u in units)
            {
                bool wantsBack = u.Def.Archetype == Archetype.Commander || u.Def.Archetype == Archetype.Gunner;
                Hex best = free[0]; int bestScore = int.MaxValue;
                foreach (var h in free)
                {
                    bool isBack = h.R == back;
                    int score = (wantsBack == isBack ? 0 : 100) + Math.Abs(2 * h.Q + h.R) * 10; // |2q+r| = horizontal offset from centre
                    // Deep territories: the line forms up on the front row (rows behind it are for the Commander and guns).
                    if (!wantsBack) score += (Math.Abs(h.R) - s.Map.DeploymentEdge) * 15;
                    if (s.Map.Terrain(h) == TerrainType.Elevated && u.Def.Archetype == Archetype.Gunner) score -= 25;
                    if (s.Map.Terrain(h) == TerrainType.Cover && u.Def.Archetype != Archetype.Vehicle) score -= 5;
                    if (score < bestScore || (score == bestScore && h.CompareTo(best) < 0)) { bestScore = score; best = h; }
                }
                free.Remove(best);
                result.Add(new Placement(u.Id, best));
            }
            return result;
        }

        static int DeployPriority(Archetype a) => a switch
        {
            Archetype.Commander => 0, Archetype.Gunner => 1, Archetype.Guardian => 2, Archetype.Vehicle => 3, Archetype.Infantry => 4, _ => 5,
        };

        static void ApplyDeployment(MatchState s, int seat, List<Placement> placements, List<GameEvent> ev)
        {
            foreach (var p in placements)
            {
                var u = s.Unit(p.UnitId);
                u.Position = p.Hex;
                u.Deployed = true;
            }
            s.Players[seat].DeploymentCommitted = true;
            ev.Add(new GameEvent(GameEventType.DeploymentCommitted) { Seat = seat, Amount = placements.Count });
            if (s.Players[0].DeploymentCommitted && s.Players[1].DeploymentCommitted) StartBattle(s, ev);
        }

        static void StartBattle(MatchState s, List<GameEvent> ev)
        {
            s.Phase = MatchPhase.Battle;
            s.ActivationIndex = 0;
            s.ActiveSeat = s.Config.FirstSeat;
            // Inhumanus Defense doctrine: the army begins the battle Guarded.
            for (int seat = 0; seat < 2 && !s.IsRoster; seat++)
            {
                if (s.Players[seat].Faction != FactionId.Inhumanus || s.Players[seat].Doctrine != Doctrine.Defense) continue;
                foreach (var u in s.LiveUnits(seat))
                {
                    u.Guarded = true;
                    ev.Add(new GameEvent(GameEventType.GuardApplied) { Seat = seat, UnitId = u.Id, To = u.Position, Text = "Indemnity" });
                }
            }
            if (s.IsRoster) { s.Round = 1; ev.Add(new GameEvent(GameEventType.RoundStarted) { Seat = s.ActiveSeat, Amount = 1 }); }
            ev.Add(new GameEvent(GameEventType.DeploymentCompleted));
            ev.Add(new GameEvent(GameEventType.MatchStarted) { Seat = s.ActiveSeat });
            ev.Add(new GameEvent(GameEventType.TurnChanged) { Seat = s.ActiveSeat, Amount = 0 });
        }

        // ================================================================== abilities

        public static int AbilityRange(MatchState s, UnitState u, AbilityId id)
        {
            var def = GameContent.Ability(id);
            int r = def?.Range ?? 1;
            if (id == AbilityId.Lien && s.Players[u.Owner].Doctrine == Doctrine.Control && u.Def.Faction == FactionId.Inhumanus) r += 1;
            if (id == AbilityId.CouncilFormation && (u.Flags & EquipmentFlags.UnityStandard) != 0) r = 2;
            return r;
        }

        /// <summary>Is the ability usable at all right now (ignoring the specific target)?</summary>
        public static Validation AbilityAvailable(MatchState s, UnitState u, AbilityId id)
        {
            if (s.IsRoster)
            {
                if (id == AbilityId.Rescue)
                {
                    var w = RosterRules.CanAct(s, u, attack: true);
                    return w == null ? Validation.Success : Validation.Fail(w);
                }
                var why2 = RosterRules.CanAct(s, u, attack: false);
                if (why2 != null) return Validation.Fail(why2);
                if (u.R.Active.RequiresNoMove && u.MovedThisActivation) return Validation.Fail($"{u.R.ActiveName} cannot be used after moving.");
                return RosterAbilities.AnyUse(s, u) ? Validation.Success : Validation.Fail($"No legal target for {u.R.ActiveName} right now.");
            }
            if (!CanActivate(s, u, out var why)) return Validation.Fail(why);
            var def = GameContent.Ability(id);
            if (def == null) return Validation.Fail("Unknown ability.");
            if (id == AbilityId.SettleDebt)
                return u.HasStatus(StatusType.Lien) ? Validation.Success : Validation.Fail("This unit carries no Lien.");
            int i = u.AbilityIndex(id);
            if (i < 0) return Validation.Fail($"{u.Def.Name} does not have {def.Name}.");
            var slot = u.Abilities[i];
            if (u.HasStatus(StatusType.Lien)) return Validation.Fail("A Lien blocks this unit's abilities. Settle Debt first.");
            if (slot.Charges == 0) return Validation.Fail($"{def.Name} has no charges left.");
            if (s.ActivationIndex < slot.ReadyAt)
            {
                int turns = (slot.ReadyAt - s.ActivationIndex + 1) / 2;
                return Validation.Fail($"{def.Name} is on cooldown ({turns} more turn{(turns == 1 ? "" : "s")}).");
            }
            if (id == AbilityId.OverclockBarrage && u.HasStatus(StatusType.Overheated)) return Validation.Fail("Already Overheated.");
            if (id == AbilityId.OverclockBarrage && s.ActivationIndex < u.WeaponReadyAt) return Validation.Fail($"{u.Weapon.Name} is recharging.");
            return Validation.Success;
        }

        /// <summary>Fog of war: an enemy standing on <paramref name="h"/> that <paramref name="u"/>'s side cannot see.</summary>
        static UnitState HiddenEnemyAt(MatchState s, UnitState u, Hex h)
        {
            if (!s.Config.FogOfWar) return null;
            var occ = s.UnitAt(h);
            return occ != null && occ.Owner != u.Owner && !Vision.CanSee(s, u.Owner, occ) ? occ : null;
        }

        public static Validation ValidateAbility(MatchState s, UnitState u, AbilityId id, Hex target, int option, Hex secondary)
        {
            var avail = AbilityAvailable(s, u, id);
            if (!avail.Ok) return avail;
            var def = GameContent.Ability(id);
            int range = AbilityRange(s, u, id);
            int dist = Hex.Distance(u.Position, target);

            switch (id)
            {
                case AbilityId.SettleDebt:
                case AbilityId.CouncilFormation:
                    return Validation.Success;

                case AbilityId.TheGift:
                {
                    var t = s.UnitAt(target);
                    if (t == null || t.Owner == u.Owner) return Validation.Fail("The Gift needs an enemy unit as its target.");
                    if (!Vision.CanSee(s, u.Owner, t)) return Validation.Fail("You can't see that target.");
                    if (t.IsCommander) return Validation.Fail("Commanders are immune to The Gift.");
                    if (dist > range) return Validation.Fail($"Out of range: The Gift reaches {range} hexes.");
                    int maxHp = (u.Flags & EquipmentFlags.GreaterGift) != 0 ? 2 : 1;
                    if (t.Hp > maxHp) return Validation.Fail($"The target must be at {(maxHp == 1 ? "exactly 1 HP" : "2 HP or less")} (it has {t.Hp}).");
                    if (t.Guarded) return Validation.Fail("A Guarded mind resists The Gift.");
                    if (s.LiveUnits(u.Owner).Count() >= s.Config.MaxUnits) return Validation.Fail($"Your army is already at the {s.Config.MaxUnits}-unit cap.");
                    return Validation.Success;
                }
                case AbilityId.ResonantBulwark:
                {
                    if (dist != 1) return Validation.Fail("Bulwarks must be raised on an adjacent hex.");
                    if (!s.Map.Contains(target)) return Validation.Fail("That hex is outside the battlefield.");
                    var tt = s.Map.Terrain(target);
                    if (tt == TerrainType.Obstacle) return Validation.Fail("Cannot raise a bulwark on an obstacle.");
                    if (tt == TerrainType.Hazard) return Validation.Fail("The unstable conduit rejects the crystal.");
                    if (HiddenEnemyAt(s, u, target) != null) return Validation.Success;   // resolves as an ambush
                    if (s.UnitAt(target) != null) return Validation.Fail("The hex must be empty.");
                    if (s.BulwarkAt(target) != null) return Validation.Fail("A bulwark already stands there.");
                    if (s.WreckIndex(target) >= 0) return Validation.Fail("Wreckage prevents the crystal from rooting.");
                    return Validation.Success;
                }
                case AbilityId.OverclockBarrage:
                {
                    if (target != u.Position)
                    {
                        if (dist != 1) return Validation.Fail("Overclock moves at most 1 hex.");
                        if (!BoardQueries.IsPassableTerrain(s, target)) return Validation.Fail("Cannot move there.");
                        if (HiddenEnemyAt(s, u, target) != null) return Validation.Success;   // resolves as an ambush
                        if (s.UnitAt(target) != null) return Validation.Fail("That hex is occupied.");
                    }
                    // Judge the plan by what the player could see; if the dash reveals a unit that now blocks the shot,
                    // resolution simply skips the attack (the move still happens).
                    var vs = Vision.FoggedView(s, u.Owner);
                    var vu = vs.Unit(u.Id);
                    var o = SimulateFrom(vs, vu, target, () => Combat.Resolve(vs, vu, secondary));
                    return o.Legal ? Validation.Success : Validation.Fail(o.Reason);
                }
                case AbilityId.Lien:
                {
                    var t = s.UnitAt(target);
                    if (t == null || t.Owner == u.Owner) return Validation.Fail("Lien needs an enemy unit as its target.");
                    if (!Vision.CanSee(s, u.Owner, t)) return Validation.Fail("You can't see that target.");
                    if (dist > range) return Validation.Fail($"Out of range: Lien reaches {range} hexes.");
                    var los = BoardQueries.LineOfSight(s, u.Position, target, false, u.Id, u.Owner);
                    if (los != LosState.Clear) return Validation.Fail(BoardQueries.LosText(los) + ".");
                    if (t.HasStatus(StatusType.Lien)) return Validation.Fail("That unit is already under a Lien.");
                    return Validation.Success;
                }
                case AbilityId.Salvage:
                {
                    if (dist > 1) return Validation.Fail("Salvage needs a wreck adjacent to (or under) the unit.");
                    if (s.WreckIndex(target) < 0) return Validation.Fail("There is no wreck there.");
                    if (option == (int)SalvageChoice.Repair && u.Hp >= u.MaxHp) return Validation.Fail("Already at full health — choose Rig Weapon instead.");
                    if (option != (int)SalvageChoice.Repair && option != (int)SalvageChoice.RigWeapon) return Validation.Fail("Choose Repair or Rig Weapon.");
                    return Validation.Success;
                }
                case AbilityId.MindShield:
                {
                    var t = s.UnitAt(target);
                    if (t == null || t.Owner != u.Owner || t.Id == u.Id) return Validation.Fail("Mind Shield targets an adjacent ally.");
                    if (dist > range) return Validation.Fail("The ally must be adjacent.");
                    if (t.HasStatus(StatusType.Ward)) return Validation.Fail("That ally is already Warded.");
                    return Validation.Success;
                }
                case AbilityId.PatchUp:
                {
                    var t = s.UnitAt(target);
                    if (t == null || t.Owner != u.Owner) return Validation.Fail("Patch Up targets this unit or an adjacent ally.");
                    if (dist > range) return Validation.Fail("The ally must be adjacent.");
                    if (t.Hp >= t.MaxHp) return Validation.Fail("Already at full health.");
                    return Validation.Success;
                }
                case AbilityId.Smoke:
                {
                    if (!s.Map.Contains(target)) return Validation.Fail("That hex is outside the battlefield.");
                    if (dist > range) return Validation.Fail($"Out of range: Smoke reaches {range} hexes.");
                    if (s.Map.Terrain(target) == TerrainType.Obstacle) return Validation.Fail("Smoke needs open ground.");
                    if (s.HasSmoke(target)) return Validation.Fail("That hex is already smoked.");
                    return Validation.Success;
                }
                case AbilityId.AssetRelocation:
                {
                    var t = s.UnitAt(target);
                    if (t == null || t.Owner != u.Owner || t.Id == u.Id) return Validation.Fail("Asset Relocation swaps with an adjacent ally.");
                    if (dist != 1) return Validation.Fail("The ally must be adjacent.");
                    return Validation.Success;
                }
            }
            return Validation.Fail($"{def.Name} cannot be used that way.");
        }

        /// <summary>Temporarily moves <paramref name="u"/> to <paramref name="at"/> while evaluating <paramref name="f"/>, then restores it.</summary>
        static T SimulateFrom<T>(MatchState s, UnitState u, Hex at, Func<T> f)
        {
            var original = u.Position;
            u.Position = at;
            try { return f(); }
            finally { u.Position = original; }
        }

        /// <summary>Preview of an Overclock Barrage attack from a prospective position (pure).</summary>
        public static AttackOutcome PreviewOverclock(MatchState s, UnitState u, Hex moveTo, Hex target) =>
            SimulateFrom(s, u, moveTo, () => Combat.Resolve(s, u, target));

        static void ConsumeAbility(MatchState s, UnitState u, AbilityId id)
        {
            int i = u.AbilityIndex(id);
            if (i < 0) return;
            var slot = u.Abilities[i];
            if (slot.Charges > 0) slot.Charges--;
            slot.ReadyAt = s.ActivationIndex + 2 * (GameContent.Ability(id).CooldownTurns + 1);
            u.Abilities[i] = slot;
        }

        static void ApplyAbility(MatchState s, Command c, List<GameEvent> ev)
        {
            var u = s.Unit(c.UnitId);
            var id = c.Ability;
            var def = GameContent.Ability(id);
            int idx = s.ActivationIndex;
            ConsumeAbility(s, u, id);
            RemoveGuardOnAction(u, ev);
            ev.Add(new GameEvent(GameEventType.AbilityActivated)
                { Seat = u.Owner, UnitId = u.Id, Ability = id, From = u.Position, To = c.Target, FxKey = def.FxKey, Text = def.Name, Amount = c.Option });

            switch (id)
            {
                case AbilityId.TheGift:
                {
                    var t = s.UnitAt(c.Target);
                    var newDef = GameContent.UnitFor(FactionId.Craigon, t.Def.Archetype);
                    t.Alive = false; t.Hp = 0; t.Guarded = false; t.Statuses.Clear();
                    var nu = UnitFactory.Create(s.NextUnitId++, u.Owner, new RosterEntry(newDef.Id), s.Players[u.Owner].Doctrine);
                    nu.Hp = 1;
                    nu.Deployed = true;
                    nu.Position = c.Target;
                    nu.Converted = true;
                    nu.SetStatus(new StatusEffect(StatusType.Dazed, idx + 3, u.Id));
                    s.Units.Add(nu);
                    ev.Add(new GameEvent(GameEventType.UnitConverted) { Seat = u.Owner, UnitId = t.Id, TargetUnitId = nu.Id, To = c.Target, Text = newDef.Name });
                    // Liens held by the converted unit are void.
                    foreach (var other in s.Units)
                    {
                        var lien = other.Alive ? other.GetStatus(StatusType.Lien) : null;
                        if (lien.HasValue && lien.Value.SourceUnitId == t.Id) other.RemoveStatus(StatusType.Lien);
                    }
                    break;
                }
                case AbilityId.ResonantBulwark:
                {
                    var lurker = HiddenEnemyAt(s, u, c.Target);
                    if (lurker != null)
                    {
                        ev.Add(new GameEvent(GameEventType.Ambushed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = lurker.Id, From = u.Position, To = c.Target, Text = "the crystal found no room" });
                        break;
                    }
                    var old = s.Bulwarks.FirstOrDefault(b => b.SourceUnitId == u.Id);
                    if (old != null)
                    {
                        s.Bulwarks.Remove(old);
                        ev.Add(new GameEvent(GameEventType.BulwarkDestroyed) { Seat = old.Owner, UnitId = u.Id, To = old.Hex, Text = "replaced" });
                    }
                    bool core = (u.Flags & EquipmentFlags.ResonanceCore) != 0;
                    var nb = new Bulwark { Hex = c.Target, Owner = u.Owner, SourceUnitId = u.Id, Hp = core ? 3 : 2, MaxHp = core ? 3 : 2, ExpiresAt = idx + (core ? 7 : 4) };
                    s.Bulwarks.Add(nb);
                    s.Bulwarks.Sort((a, b) => a.Hex.CompareTo(b.Hex));
                    ev.Add(new GameEvent(GameEventType.BulwarkRaised) { Seat = u.Owner, UnitId = u.Id, To = c.Target, Amount = nb.Hp });
                    break;
                }
                case AbilityId.OverclockBarrage:
                {
                    var lurker = c.Target != u.Position ? HiddenEnemyAt(s, u, c.Target) : null;
                    if (lurker != null)
                    {
                        ev.Add(new GameEvent(GameEventType.Ambushed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = lurker.Id, From = u.Position, To = c.Target, Text = "overclock dash blocked" });
                        CheckCommanders(s, u.Owner, ev);
                        break;
                    }
                    if (c.Target != u.Position)
                    {
                        var from = u.Position;
                        u.Position = c.Target;
                        ev.Add(new GameEvent(GameEventType.UnitMoved) { Seat = u.Owner, UnitId = u.Id, From = from, To = c.Target, Path = new List<Hex> { from, c.Target }, Amount = 1, Text = "overclock" });
                        ApplyHazard(s, u, ev);
                    }
                    if (u.Alive)
                    {
                        var o = Combat.Resolve(s, u, c.Secondary);
                        if (o.Legal) ResolveAttackOutcome(s, u, o, ev);
                    }
                    if (u.Alive)
                    {
                        u.SetStatus(new StatusEffect(StatusType.Overheated, idx + 3, u.Id));
                        ev.Add(new GameEvent(GameEventType.StatusApplied) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, Status = StatusType.Overheated, To = u.Position });
                    }
                    CheckCommanders(s, u.Owner, ev);
                    break;
                }
                case AbilityId.CouncilFormation:
                {
                    int range = AbilityRange(s, u, id);
                    var targets = s.BoardUnits().Where(x => x.Owner == u.Owner && Hex.Distance(x.Position, u.Position) <= range)
                                   .OrderBy(x => x.Id).ToList();
                    foreach (var t in targets)
                    {
                        if (t.Guarded || t.HasStatus(StatusType.Lien) || t.HasStatus(StatusType.Overheated)) continue;
                        t.Guarded = true;
                        ev.Add(new GameEvent(GameEventType.GuardApplied) { Seat = t.Owner, UnitId = t.Id, To = t.Position, Text = "Council Formation" });
                    }
                    break;
                }
                case AbilityId.Lien:
                {
                    var t = s.UnitAt(c.Target);
                    if (t.Guarded)
                    {
                        t.Guarded = false;
                        ev.Add(new GameEvent(GameEventType.GuardConsumed) { Seat = t.Owner, UnitId = t.Id, To = t.Position, Text = "lien" });
                    }
                    t.SetStatus(new StatusEffect(StatusType.Lien, idx + 5, u.Id));
                    ev.Add(new GameEvent(GameEventType.StatusApplied) { Seat = t.Owner, UnitId = u.Id, TargetUnitId = t.Id, Status = StatusType.Lien, To = t.Position });
                    break;
                }
                case AbilityId.SettleDebt:
                {
                    u.RemoveStatus(StatusType.Lien);
                    ev.Add(new GameEvent(GameEventType.StatusExpired) { Seat = u.Owner, UnitId = u.Id, Status = StatusType.Lien, To = u.Position, Text = "settled" });
                    break;
                }
                case AbilityId.Salvage:
                {
                    int wi = s.WreckIndex(c.Target);
                    s.Wrecks.RemoveAt(wi);
                    ev.Add(new GameEvent(GameEventType.WreckSalvaged) { Seat = u.Owner, UnitId = u.Id, To = c.Target, Amount = c.Option });
                    if (c.Option == (int)SalvageChoice.Repair)
                    {
                        int before = u.Hp;
                        u.Hp = Math.Min(u.MaxHp, u.Hp + 2);
                        ev.Add(new GameEvent(GameEventType.UnitHealed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, Amount = u.Hp - before, HpAfter = u.Hp });
                    }
                    else
                    {
                        u.SetStatus(new StatusEffect(StatusType.JuryRigged, idx + 6, u.Id));
                        ev.Add(new GameEvent(GameEventType.StatusApplied) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, Status = StatusType.JuryRigged, To = u.Position });
                    }
                    break;
                }
                case AbilityId.MindShield:
                {
                    var t = s.UnitAt(c.Target);
                    t.SetStatus(new StatusEffect(StatusType.Ward, idx + 4, u.Id));
                    ev.Add(new GameEvent(GameEventType.StatusApplied) { Seat = t.Owner, UnitId = u.Id, TargetUnitId = t.Id, Status = StatusType.Ward, To = t.Position });
                    break;
                }
                case AbilityId.PatchUp:
                {
                    var t = s.UnitAt(c.Target);
                    int before = t.Hp;
                    t.Hp = Math.Min(t.MaxHp, t.Hp + 1);
                    ev.Add(new GameEvent(GameEventType.UnitHealed) { Seat = t.Owner, UnitId = u.Id, TargetUnitId = t.Id, To = t.Position, Amount = t.Hp - before, HpAfter = t.Hp });
                    break;
                }
                case AbilityId.Smoke:
                {
                    s.Smoke.Add(new SmokeCloud { Hex = c.Target, ExpiresAt = idx + 4 });
                    s.Smoke.Sort((a, b) => a.Hex.CompareTo(b.Hex));
                    ev.Add(new GameEvent(GameEventType.SmokeDeployed) { Seat = u.Owner, UnitId = u.Id, To = c.Target });
                    break;
                }
                case AbilityId.AssetRelocation:
                {
                    var t = s.UnitAt(c.Target);
                    var a = u.Position;
                    u.Position = t.Position;
                    t.Position = a;
                    ev.Add(new GameEvent(GameEventType.UnitsSwapped) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, From = a, To = u.Position });
                    break;
                }
            }
        }
    }
}
