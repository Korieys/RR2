using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    [Serializable]
    public struct Placement
    {
        public int UnitId;
        public Hex Hex;
        public Placement(int unitId, Hex hex) { UnitId = unitId; Hex = hex; }
    }

    /// <summary>
    /// The single authoritative action interface. Humans (local or remote), the AI, the tutorial, replays and the network host
    /// all submit these. Selection and hovering are presentation-only and never produce commands.
    /// </summary>
    [Serializable]
    public sealed class Command
    {
        public CommandType Type;
        public int Seat;
        public int UnitId;
        /// <summary>Move destination, attack/ability target hex, or Overclock move destination.</summary>
        public Hex Target;
        /// <summary>Overclock attack target.</summary>
        public Hex Secondary;
        public AbilityId Ability;
        /// <summary>Ability option (e.g. <see cref="SalvageChoice"/>).</summary>
        public int Option;
        public List<Placement> Placements = new List<Placement>();
        /// <summary>Full Roster active abilities: the target picks in order (units are picked by their hex).</summary>
        public List<Hex> Picks = new List<Hex>();
        /// <summary>State revision the issuer saw; −1 skips the check (host/system commands).</summary>
        public int ExpectedRevision = -1;
        /// <summary>Client-generated id used to drop duplicate deliveries. 0 = none.</summary>
        public uint Nonce;
        /// <summary>Decision time consumed by the acting player, stamped by the host clock.</summary>
        public int ElapsedMs;

        public static Command Move(int seat, int unitId, Hex to) => new Command { Type = CommandType.Move, Seat = seat, UnitId = unitId, Target = to };
        public static Command Attack(int seat, int unitId, Hex target) => new Command { Type = CommandType.Attack, Seat = seat, UnitId = unitId, Target = target };
        public static Command Guard(int seat, int unitId) => new Command { Type = CommandType.Guard, Seat = seat, UnitId = unitId };
        public static Command Pass(int seat) => new Command { Type = CommandType.Pass, Seat = seat };
        /// <summary>Full Roster: end (or spend) this unit's activation without acting ("wait").</summary>
        public static Command Wait(int seat, int unitId) => new Command { Type = CommandType.Pass, Seat = seat, UnitId = unitId };
        /// <summary>Full Roster: use the unit's active ability with the given target picks and option.</summary>
        public static Command Active(int seat, int unitId, IEnumerable<Hex> picks, int option = 0)
        {
            var list = picks?.ToList() ?? new List<Hex>();
            return new Command { Type = CommandType.Ability, Seat = seat, UnitId = unitId, Ability = AbilityId.UnitActive, Option = option, Picks = list,
                                 Target = list.Count > 0 ? list[0] : default, Secondary = list.Count > 1 ? list[1] : default };
        }
        /// <summary>Full Roster: free an adjacent captured or boarded ally.</summary>
        public static Command Rescue(int seat, int unitId, Hex ally) =>
            new Command { Type = CommandType.Ability, Seat = seat, UnitId = unitId, Ability = AbilityId.Rescue, Target = ally, Picks = new List<Hex> { ally } };
        public static Command Resign(int seat) => new Command { Type = CommandType.Resign, Seat = seat };
        public static Command UseAbility(int seat, int unitId, AbilityId ability, Hex target, int option = 0, Hex secondary = default) =>
            new Command { Type = CommandType.Ability, Seat = seat, UnitId = unitId, Ability = ability, Target = target, Option = option, Secondary = secondary };
        public static Command Overclock(int seat, int unitId, Hex moveTo, Hex attackTarget) =>
            new Command { Type = CommandType.Ability, Seat = seat, UnitId = unitId, Ability = AbilityId.OverclockBarrage, Target = moveTo, Secondary = attackTarget };
        public static Command Deploy(int seat, IEnumerable<Placement> placements) =>
            new Command { Type = CommandType.CommitDeployment, Seat = seat, Placements = placements.ToList() };
        public static Command AutoDeploy(int seat) => new Command { Type = CommandType.AutoDeploy, Seat = seat };
        public static Command ClockExpire(int seat) => new Command { Type = CommandType.ClockExpire, Seat = seat };
        public static Command Forfeit(int seat) => new Command { Type = CommandType.Forfeit, Seat = seat };

        public Command Clone()
        {
            var c = (Command)MemberwiseClone();
            c.Placements = new List<Placement>(Placements);
            c.Picks = new List<Hex>(Picks);
            return c;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case CommandType.Move: return $"S{Seat} Move #{UnitId} -> {Target}";
                case CommandType.Attack: return $"S{Seat} Attack #{UnitId} -> {Target}";
                case CommandType.Ability when Ability == AbilityId.UnitActive || Ability == AbilityId.Rescue:
                    return $"S{Seat} {Ability} #{UnitId} -> [{string.Join(" ", Picks)}] opt{Option}";
                case CommandType.Ability: return $"S{Seat} {Ability} #{UnitId} -> {Target}{(Ability == AbilityId.OverclockBarrage ? " / " + Secondary : "")} opt{Option}";
                case CommandType.Pass when UnitId != 0: return $"S{Seat} Wait #{UnitId}";
                case CommandType.CommitDeployment: return $"S{Seat} Deploy x{Placements.Count}";
                default: return $"S{Seat} {Type}" + (UnitId != 0 ? $" #{UnitId}" : "");
            }
        }
    }

    public enum GameEventType : byte
    {
        MatchStarted, DeploymentCommitted, DeploymentCompleted, UnitMoved, AttackResolved, DamageApplied, WardBlocked,
        GuardApplied, GuardConsumed, Riposte, AbilityActivated, StatusApplied, StatusExpired, UnitDestroyed, UnitConverted,
        UnitHealed, BulwarkRaised, BulwarkDamaged, BulwarkDestroyed, BulwarkExpired, SmokeDeployed, SmokeExpired,
        WreckCreated, WreckSalvaged, UnitsSwapped, HazardDamage, Passed, TurnChanged, CommanderKilled, ClockExpired,
        PlayerResigned, PlayerForfeited, ActivationLimitReached, MatchEnded, UnbreakableTriggered,
        /// <summary>Fog of war: a move ran into a hidden enemy (TargetUnitId at To) and stopped at From.</summary>
        Ambushed,

        // ---- Full Roster (Unit Codex) events
        /// <summary>A roster status was applied (RStatus, Amount) to TargetUnitId by UnitId.</summary>
        RStatusApplied,
        RStatusExpired,
        /// <summary>A guardian (UnitId) took a direct attack aimed at TargetUnitId.</summary>
        Intercepted,
        /// <summary>Temporary shields on TargetUnitId absorbed Amount damage.</summary>
        ShieldAbsorbed,
        /// <summary>Counter or reflected damage from UnitId to TargetUnitId.</summary>
        Countered,
        /// <summary>Forced movement (pull, reposition) of UnitId From → To.</summary>
        UnitDisplaced,
        /// <summary>A jump, dash or teleport by UnitId From → To (no path).</summary>
        UnitJumped,
        TokenSpawned,
        BarrierRaised,
        BarrierDamaged,
        BarrierDestroyed,
        BarrierExpired,
        MineLaid,
        MineTriggered,
        MineDisarmed,
        UnitCaptured,
        UnitRescued,
        UnitBoarded,
        ConversionStarted,
        ObjectiveClaimed,
        RoundStarted,
        ActivationStarted,
        ActivationEnded,
        UnitsRevealed,
        HexIgnited,
        /// <summary>A free step or free move granted by a rule (War Momentum, All Hands).</summary>
        FreeMove,
        RoundLimitReached,
    }

    /// <summary>Typed, resolved gameplay event. Presentation (animation, VFX, audio, UI) subscribes to these; it never decides outcomes.</summary>
    [Serializable]
    public sealed class GameEvent
    {
        public GameEventType Type;
        public int Seat = -1;
        public int UnitId;
        public int TargetUnitId;
        public Hex From;
        public Hex To;
        public int Amount;
        public int HpAfter;
        public bool Lethal;
        public AbilityId Ability;
        public StatusType Status;
        public VictoryReason Reason;
        public string FxKey;
        public string Text;
        public List<Hex> Path;
        /// <summary>Full Roster status for RStatusApplied / RStatusExpired.</summary>
        public RStatusType RStatus;

        public GameEvent(GameEventType type) { Type = type; }
        public override string ToString() => $"{Type} s{Seat} u{UnitId}->u{TargetUnitId} {From}->{To} amt{Amount} hp{HpAfter}{(Lethal ? " LETHAL" : "")} {Text}";
    }

    /// <summary>Structured validation outcome. Reason is player-facing text.</summary>
    public readonly struct Validation
    {
        public readonly bool Ok;
        public readonly string Reason;
        Validation(bool ok, string reason) { Ok = ok; Reason = reason; }
        public static readonly Validation Success = new Validation(true, null);
        public static Validation Fail(string reason) => new Validation(false, reason);
        public override string ToString() => Ok ? "OK" : Reason;
    }

    public sealed class CommandResult
    {
        public bool Accepted;
        public string Reason;
        public Command Command;
        public List<GameEvent> Events = new List<GameEvent>();
        public int Revision;
        public static CommandResult Reject(Command c, string reason) => new CommandResult { Accepted = false, Reason = reason, Command = c };
    }
}
