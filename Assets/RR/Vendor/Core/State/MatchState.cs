using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    [Serializable]
    public struct StatusEffect
    {
        public StatusType Type;
        /// <summary>The status is removed when the match's activation index reaches this value. int.MaxValue = until consumed.</summary>
        public int ExpiresAt;
        public int SourceUnitId;

        public StatusEffect(StatusType type, int expiresAt, int source = 0) { Type = type; ExpiresAt = expiresAt; SourceUnitId = source; }
    }

    [Serializable]
    public struct AbilitySlot
    {
        public AbilityId Ability;
        public int Charges;   // -1 = unlimited
        public int ReadyAt;   // activation index at which it can next be used
    }

    /// <summary>A live unit on (or waiting to enter) the battlefield.</summary>
    [Serializable]
    public sealed class UnitState
    {
        public int Id;
        public int Owner;
        public string DefId;
        public List<string> Equipment = new List<string>();
        public int Hp;
        public bool Alive = true;
        public bool Deployed;
        public Hex Position;
        public bool Guarded;
        public bool Converted;
        public int WeaponReadyAt;
        public List<StatusEffect> Statuses = new List<StatusEffect>();
        public List<AbilitySlot> Abilities = new List<AbilitySlot>();

        // ---- Full Roster (Unit Codex) state; unused by Classic matches ----
        public List<RStatus> RStatuses = new List<RStatus>();
        /// <summary>Completed activations.</summary>
        public int Activations;
        /// <summary>Round in which it last completed an activation (equal to the match round: done for this round).</summary>
        public int ActivatedRound;
        /// <summary>Own activations its active ability remains unusable.</summary>
        public int Cooldown;
        public bool MovedThisActivation;
        public bool ActedThisActivation;
        public bool AttackedThisActivation;
        public bool UsedActiveThisActivation;
        /// <summary>Did it move during its most recent completed activation ("stationary" passives check this).</summary>
        public bool LastActivationMoved;
        public int MovedRound;
        public int AttackedRound;
        public int AttackedByEnemyRound;
        /// <summary>Once-per-battle passive flags (bits of <see cref="RosterFlags"/>).</summary>
        public int BattleFlags;
        /// <summary>Once-per-round flags; valid while <see cref="RoundFlagsRound"/> equals the match round.</summary>
        public int RoundFlags;
        public int RoundFlagsRound;
        /// <summary>The carrier that spawned this token (0 = not a token).</summary>
        public int SpawnedBy;
        /// <summary>Rescue actions received while captured (Seizure Rig needs two).</summary>
        public int RescueProgress;

        // ---- Derived, fixed for the life of the unit (computed by UnitFactory) ----
        public int MaxHp;
        public int BaseMove;
        public int BaseArmor;
        public bool Hover;
        public EquipmentFlags Flags;
        public WeaponProfile Weapon;

        public UnitDef Def => _def != null && _def.Id == DefId ? _def : (_def = GameContent.Unit(DefId));
        [NonSerialized] UnitDef _def;
        public RosterProfile R => Def.Roster;
        public bool IsCommander => Def.Archetype == Archetype.Commander;
        public bool OnBoard => Alive && Deployed;

        public bool HasStatus(StatusType t) => Statuses.Any(s => s.Type == t);
        public StatusEffect? GetStatus(StatusType t)
        {
            foreach (var s in Statuses) if (s.Type == t) return s;
            return null;
        }

        public void SetStatus(StatusEffect s)
        {
            Statuses.RemoveAll(x => x.Type == s.Type);
            Statuses.Add(s);
            Statuses.Sort((a, b) => a.Type.CompareTo(b.Type));
        }

        public bool RemoveStatus(StatusType t) => Statuses.RemoveAll(x => x.Type == t) > 0;

        public int AbilityIndex(AbilityId id)
        {
            for (int i = 0; i < Abilities.Count; i++) if (Abilities[i].Ability == id) return i;
            return -1;
        }

        public bool HasR(RStatusType t)
        {
            for (int i = 0; i < RStatuses.Count; i++) if (RStatuses[i].Type == t) return true;
            return false;
        }

        public int RIndex(RStatusType t)
        {
            for (int i = 0; i < RStatuses.Count; i++) if (RStatuses[i].Type == t) return i;
            return -1;
        }

        public RStatus? GetR(RStatusType t)
        {
            for (int i = 0; i < RStatuses.Count; i++) if (RStatuses[i].Type == t) return RStatuses[i];
            return null;
        }

        /// <summary>Total of a status's amount over all instances (only Shield, AttackBuff and similar can coexist).</summary>
        public int RAmount(RStatusType t)
        {
            int best = 0;
            for (int i = 0; i < RStatuses.Count; i++) if (RStatuses[i].Type == t && RStatuses[i].Amount > best) best = RStatuses[i].Amount;
            return best;
        }

        public int ShieldHp => RAmount(RStatusType.Shield);

        public bool RoundFlag(int round, int bit) => RoundFlagsRound == round && (RoundFlags & bit) != 0;
        public void SetRoundFlag(int round, int bit)
        {
            if (RoundFlagsRound != round) { RoundFlagsRound = round; RoundFlags = 0; }
            RoundFlags |= bit;
        }

        public UnitState Clone()
        {
            var c = (UnitState)MemberwiseClone();
            c.Equipment = new List<string>(Equipment);
            c.Statuses = new List<StatusEffect>(Statuses);
            c.Abilities = new List<AbilitySlot>(Abilities);
            c.RStatuses = new List<RStatus>(RStatuses);
            return c; // WeaponProfile is treated as immutable after creation
        }
    }

    [Serializable]
    public sealed class Bulwark
    {
        public Hex Hex;
        public int Owner;
        public int SourceUnitId;
        public int Hp;
        public int MaxHp;
        public int ExpiresAt;
        public Bulwark Clone() => (Bulwark)MemberwiseClone();
    }

    [Serializable]
    public struct SmokeCloud { public Hex Hex; public int ExpiresAt; }

    [Serializable]
    public struct Wreck { public Hex Hex; public FactionId Faction; public Archetype Archetype; /// <summary>Round it was created (Full Roster).</summary>
        public int Round; }

    /// <summary>A Full Roster objective hex: claimed by the last side to end an activation on it.</summary>
    [Serializable]
    public struct ObjectiveState { public Hex Hex; public int Owner; public bool EverHeld; }

    [Serializable]
    public sealed class PlayerState
    {
        public int Seat;
        public FactionId Faction;
        public Doctrine Doctrine;
        public bool DeploymentCommitted;
        public int ClockRemainingMs;
        public int CommanderId;
        public PlayerState Clone() => (PlayerState)MemberwiseClone();
    }

    /// <summary>
    /// Complete authoritative match state. Mutated only by <see cref="RulesEngine"/>. Deterministic: no randomness, no wall clock.
    /// </summary>
    [Serializable]
    public sealed class MatchState
    {
        public MatchConfig Config;
        public MatchPhase Phase = MatchPhase.Deployment;
        /// <summary>Number of completed battle activations (both players). Statuses and cooldowns are measured in this clock.</summary>
        public int ActivationIndex;
        public int ActiveSeat;
        /// <summary>Incremented by every accepted command. Clients must quote it to avoid stale actions.</summary>
        public int Revision;
        public int NextUnitId = 1;
        public List<UnitState> Units = new List<UnitState>();
        public List<Bulwark> Bulwarks = new List<Bulwark>();
        public List<SmokeCloud> Smoke = new List<SmokeCloud>();
        public List<Wreck> Wrecks = new List<Wreck>();
        public PlayerState[] Players = { new PlayerState { Seat = 0 }, new PlayerState { Seat = 1 } };
        public int Winner = -1;
        public VictoryReason VictoryReason;
        /// <summary>Recently accepted command nonces per seat (duplicate-delivery protection).</summary>
        public List<uint> RecentNonces = new List<uint>();

        // ---- Full Roster (Unit Codex) state; unused by Classic matches ----
        /// <summary>Current round (1-based). Each surviving unit activates once per round.</summary>
        public int Round = 1;
        /// <summary>The unit in the middle of its activation (it has moved but not yet acted), 0 = none.</summary>
        public int ActiveUnitId;
        public List<RosterBarrier> Barriers = new List<RosterBarrier>();
        public List<RosterMine> Mines = new List<RosterMine>();
        public List<IgnitedHex> Ignited = new List<IgnitedHex>();
        public List<ObjectiveState> Objectives = new List<ObjectiveState>();
        /// <summary>Enemies destroyed by each seat this round.</summary>
        public int[] KillsThisRound = new int[2];
        /// <summary>Ships boarded by each seat this round.</summary>
        public int[] BoardsThisRound = new int[2];
        /// <summary>Round in which each seat used War Momentum (Pygmaei), −1 = not yet.</summary>
        public int[] MomentumRound = { -1, -1 };

        public bool IsRoster => Config != null && Config.Ruleset == Ruleset.FullRoster;

        [NonSerialized] MapDefinition _map;
        public MapDefinition Map => _map ??= MapLibrary.Get(Config.MapId);

        public bool IsOver => Phase == MatchPhase.Ended;

        public UnitState Unit(int id)
        {
            for (int i = 0; i < Units.Count; i++) if (Units[i].Id == id) return Units[i];
            return null;
        }

        public UnitState UnitAt(Hex h)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.OnBoard && u.Position == h) return u;
            }
            return null;
        }

        public RosterBarrier BarrierAt(Hex h)
        {
            for (int i = 0; i < Barriers.Count; i++) if (Barriers[i].Hex == h) return Barriers[i];
            return null;
        }

        public int ObjectiveIndex(Hex h)
        {
            for (int i = 0; i < Objectives.Count; i++) if (Objectives[i].Hex == h) return i;
            return -1;
        }

        public Bulwark BulwarkAt(Hex h)
        {
            for (int i = 0; i < Bulwarks.Count; i++) if (Bulwarks[i].Hex == h) return Bulwarks[i];
            return null;
        }

        public bool HasSmoke(Hex h)
        {
            for (int i = 0; i < Smoke.Count; i++) if (Smoke[i].Hex == h) return true;
            return false;
        }

        public int WreckIndex(Hex h)
        {
            for (int i = 0; i < Wrecks.Count; i++) if (Wrecks[i].Hex == h) return i;
            return -1;
        }

        public IEnumerable<UnitState> LiveUnits(int seat) => Units.Where(u => u.Alive && u.Owner == seat);
        public IEnumerable<UnitState> BoardUnits() => Units.Where(u => u.OnBoard);
        public UnitState Commander(int seat) => Unit(Players[seat].CommanderId);

        public MatchState Clone()
        {
            var c = (MatchState)MemberwiseClone();
            c.Units = Units.Select(u => u.Clone()).ToList();
            c.Bulwarks = Bulwarks.Select(b => b.Clone()).ToList();
            c.Smoke = new List<SmokeCloud>(Smoke);
            c.Wrecks = new List<Wreck>(Wrecks);
            c.Players = Players.Select(p => p.Clone()).ToArray();
            c.RecentNonces = new List<uint>(RecentNonces);
            c.Barriers = Barriers.Select(b => b.Clone()).ToList();
            c.Mines = Mines.Select(m => m.Clone()).ToList();
            c.Ignited = new List<IgnitedHex>(Ignited);
            c.Objectives = new List<ObjectiveState>(Objectives);
            c.KillsThisRound = (int[])KillsThisRound.Clone();
            c.BoardsThisRound = (int[])BoardsThisRound.Clone();
            c.MomentumRound = (int[])MomentumRound.Clone();
            return c; // Config is immutable during a match and shared
        }
    }
}
