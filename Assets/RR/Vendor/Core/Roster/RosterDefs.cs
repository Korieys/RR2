using System;
using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>Which rules a match is played under. Classic is the original archetype/equipment game; Full Roster is the
    /// 156-unit Unit Codex ruleset (Docs/UnitCodex), with planetary and orbital battles.</summary>
    public enum Ruleset : byte { Classic = 0, FullRoster = 1 }

    public enum BattleDomain : byte { Planetary = 0, Orbital = 1 }

    /// <summary>Codex movement geometries (Docs/UnitCodex "Movement patterns").</summary>
    public enum MovePattern : byte { Step = 0, Ray = 1, Zigzag = 2, Diag = 3, Knight = 4, Leap = 5, Phase = 6 }

    /// <summary>Codex effect vocabulary. Every active ability is one of these; <see cref="RosterAbilities"/> has one handler per code.</summary>
    public enum EffectCode : byte
    {
        AOE, ARMOR_BREAK, BARRAGE, BARRIER, BOARD, BOMBARD, CAPTURE, CHARGE, CLOAK, CONVERT, COUNTER, DAMAGE, DASH, DEBUFF,
        DISABLE, GUARD, HEAL, INTERCEPT, LEAP, MARK, MINE, PIERCE, PIN, PULL, RALLY, REPAIR, REPOSITION, REVEAL, SHIELD,
        SPAWN, SUPPRESS, TELEPORT,
    }

    public enum UnitRole : byte
    {
        Line, Defender, Scout, Saboteur, Heavy, Marksman, Support, Capture, Engineer, Commander, Assault, Elite,
        VehicleSupport, VehicleAssault, VehicleSiege, VehicleScout,
        Fighter, Interceptor, SpaceScout, Corvette, Frigate, Destroyer, Cruiser, Carrier, SpaceSupport, Battleship,
        SpaceBastion, Ark, Dreadnought, Flagship,
        /// <summary>A SPAWN training token (2 HP, no active ability).</summary>
        Token,
    }

    /// <summary>Statuses of the Full Roster rules (Docs/UnitCodex "Status effects"), plus the bookkeeping buffs the
    /// abilities need. Stored on <see cref="UnitState.RStatuses"/>.</summary>
    public enum RStatusType : byte
    {
        Rooted = 0,        // cannot move
        Disabled = 1,      // cannot use its active ability
        Silenced = 2,      // cannot attack or use its active ability (may move)
        Exposed = 3,       // −Amount ARM
        Marked = 4,        // the next qualifying friendly attack(s) deal +Amount raw damage (Uses)
        Cloaked = 5,       // untargetable by enemies farther than 2 hexes unless Revealed
        Revealed = 6,      // cloak and passive concealment do not apply
        Suppressed = 7,    // −Amount movement (minimum 1)
        Shield = 8,        // Amount temporary shield HP, absorbed before HP
        Protected = 9,     // guarded: SourceId (guardian) intercepts the next eligible direct attack; Amount = raw reduction; Range = protection range
        Guardian = 10,     // may intercept for an adjacent ally (Uses intercepts)
        AttackBuff = 11,   // +Amount raw damage on the next attack (Uses), or every attack while it lasts when Uses = 0
        MoveBuff = 12,     // +Amount movement
        ArmorBuff = 13,    // +Amount ARM
        RangeDebuff = 14,  // −Amount basic attack range (minimum 1)
        AttackDebuff = 15, // −Amount raw damage on the next attack (minimum raw 1)
        Reflect = 16,      // the next ranged basic attack that damages this unit takes Amount damage back
        Redirect = 17,     // the next Amount damage this unit receives is sent back to the attacker instead
        Captured = 18,     // cannot act, move or protect; may be rescued
        Boarded = 19,      // ship cannot activate until rescued or the boarder is destroyed
        NoShieldGain = 20, // cannot gain temporary shields
        Converting = 21,   // a captured unit under a Conversion Rite (SourceId = the Giftbearer, Amount = round it completes)
        Intercepted = 22,  // protected only against attacks by small craft (fighters, interceptors, tokens)
        FreeStep = 23,     // may make one free 1-hex step before the activation ends (War Momentum, Breakthrough Pace)
        Ignited = 24,      // (hex marker carried on the unit standing in fire; reserved)
        DashBuff = 25,     // reserved
        Facing = 26,       // slice: a capital ship's facing (Amount = 0..11, clockwise from East); see SliceRules
        Heat = 27,         // slice: Pygmaei overclock heat (Amount); Mode 1 = overclocked during the current activation
    }

    /// <summary>When a roster status ends.</summary>
    public enum RExpire : byte
    {
        /// <summary>When the affected unit completes activation number <see cref="RStatus.ExpireAt"/> (its "next activation").</summary>
        AfterOwnActivation = 0,
        /// <summary>When the source unit begins its next activation ("until your next activation").</summary>
        SourceNextActivation = 1,
        /// <summary>At the end of round <see cref="RStatus.ExpireAt"/>.</summary>
        RoundEnd = 2,
        /// <summary>Lasts until consumed or the battle ends.</summary>
        Battle = 3,
        /// <summary>When the next enemy unit completes an activation.</summary>
        NextEnemyActivation = 4,
    }

    [Serializable]
    public struct RStatus
    {
        public RStatusType Type;
        public int Amount;
        /// <summary>Remaining uses (consumed by attacks, intercepts…); 0 = not use-limited.</summary>
        public int Uses;
        public int SourceId;
        public RExpire Expire;
        public int ExpireAt;
        /// <summary>Protection range for <see cref="RStatusType.Protected"/>; reach for other statuses (0 when unused).</summary>
        public int Range;
        /// <summary>Protection filter: 0 any direct attack, 1 ranged only, 2 basic only, 3 attacks by small craft only.</summary>
        public byte Mode;

        public override string ToString() => $"{Type}({Amount}{(Uses > 0 ? $"x{Uses}" : "")} src{SourceId} {Expire}@{ExpireAt})";
    }

    /// <summary>A temporary battlefield structure (BARRIER). Blocking barriers stop movement and sight; cover fields
    /// can be stood in and give low cover.</summary>
    [Serializable]
    public sealed class RosterBarrier
    {
        public Hex Hex;
        public int Owner;
        public int SourceUnitId;
        public int Hp;
        public int MaxHp;
        public bool Blocking;
        /// <summary>Removed at the end of this round.</summary>
        public int ExpireRound;
        public RosterBarrier Clone() => (RosterBarrier)MemberwiseClone();
    }

    /// <summary>A hidden MINE: visible to its owner, hidden to the opponent until revealed.</summary>
    [Serializable]
    public sealed class RosterMine
    {
        public Hex Hex;
        public int Owner;
        public int SourceUnitId;
        public int Damage;
        /// <summary>Revealed to the opponent until the end of this round (−1 = hidden).</summary>
        public int RevealedUntilRound = -1;
        public RosterMine Clone() => (RosterMine)MemberwiseClone();
    }

    /// <summary>A burning hex (Fireline Sweep): a unit that ends a move on it takes 1 damage. Removed at the end of ExpireRound.</summary>
    [Serializable]
    public struct IgnitedHex { public Hex Hex; public int ExpireRound; }

    /// <summary>Codex data plus derived classifications for one Full Roster unit. Executable ability parameters are in
    /// <see cref="Active"/> and <see cref="Passive"/> (hand-authored in RosterSpecs.cs, never parsed from prose).</summary>
    public sealed class RosterProfile
    {
        public string FactionKey;
        public bool Established;
        public BattleDomain Domain;
        public UnitRole Role;
        public int Tier;
        public int CatalogIndex;
        public MovePattern Pattern;
        public int MoveHexes;
        public bool AttackNeedsLos = true;
        public string ActiveName;
        public EffectCode ActiveCode;
        public int ActiveRange;
        public int ActiveCooldown;
        public string ActiveText;
        public string PassiveName;
        public string PassiveText;
        public string[] Tags = Array.Empty<string>();
        public bool IsToken;
        public ActiveSpec Active;
        public PassiveSpec Passive;

        public bool HasTag(string t) => Array.IndexOf(Tags, t) >= 0;
        public bool IsShip => Domain == BattleDomain.Orbital;
        public bool IsVehicle => Role == UnitRole.VehicleSupport || Role == UnitRole.VehicleAssault || Role == UnitRole.VehicleSiege || Role == UnitRole.VehicleScout;
        public bool IsInfantry => Domain == BattleDomain.Planetary && !IsVehicle;
        public bool IsLeaderRole => Role == UnitRole.Commander || Role == UnitRole.Flagship;

        /// <summary>Ship size class: 1 small craft … 8 dreadnought/flagship; 0 for ground units.</summary>
        public int ShipSize => Role switch
        {
            UnitRole.Fighter => 1, UnitRole.Interceptor => 1, UnitRole.SpaceScout => 1,
            UnitRole.Token => Domain == BattleDomain.Orbital ? 1 : 0,
            UnitRole.Corvette => 2, UnitRole.Frigate => 3, UnitRole.Destroyer => 4, UnitRole.Cruiser => 5,
            UnitRole.Carrier => 6, UnitRole.SpaceSupport => 6, UnitRole.Battleship => 7, UnitRole.SpaceBastion => 7, UnitRole.Ark => 7,
            UnitRole.Dreadnought => 8, UnitRole.Flagship => 8,
            _ => 0,
        };

        /// <summary>Fighters, interceptors, space scouts and orbital tokens.</summary>
        public bool IsSmallCraft => IsShip && ShipSize == 1;
        /// <summary>Cruisers and larger.</summary>
        public bool IsCapital => IsShip && ShipSize >= 5;
        /// <summary>"Heavy" for forced-movement filters: heavies, defenders, elites, commanders, vehicles and ships larger than a frigate.</summary>
        public bool IsHeavy => Role == UnitRole.Heavy || Role == UnitRole.Defender || Role == UnitRole.Elite || Role == UnitRole.Commander
                               || IsVehicle || (IsShip && ShipSize >= 4);
        public bool IsSiege => Role == UnitRole.VehicleSiege;
        /// <summary>Elite-class or better for "below Elite" immunities: elites, commanders, siege vehicles, capital ships.</summary>
        public bool IsEliteClass => Role == UnitRole.Elite || Role == UnitRole.Commander || IsSiege || IsCapital;
    }

    // ===================================================================== executable ability parameters

    public enum PickKind : byte { Unit = 0, Hex = 1 }

    /// <summary>One targeting step of an active ability (the UI asks for these in order; the command carries them as Picks).</summary>
    public sealed class PickStep
    {
        public PickKind Kind;
        public string Prompt;
        /// <summary>The step may be skipped (the player confirms with fewer picks).</summary>
        public bool Optional;
        public PickStep(PickKind kind, string prompt, bool optional = false) { Kind = kind; Prompt = prompt; Optional = optional; }
    }

    /// <summary>Who an active may target.</summary>
    public enum TargetKind : byte
    {
        /// <summary>No target (the unit itself).</summary>
        Self = 0,
        Enemy = 1,
        Ally = 2,
        /// <summary>A hex (AOE/BOMBARD centre, barrier, mine, dash destination…).</summary>
        Hex = 3,
    }

    /// <summary>Where the dash/charge/teleport part of an active may go.</summary>
    public enum DashKind : byte
    {
        None = 0,
        /// <summary>Any empty hex within N, skipping units (and low cover); walls still block the route.</summary>
        Leap = 1,
        /// <summary>A straight line along one hex axis, through empty hexes.</summary>
        Straight = 2,
        /// <summary>A connected path of empty hexes.</summary>
        Path = 3,
        /// <summary>Any empty, non-blocking hex within N in line of sight, ignoring units and barriers.</summary>
        Teleport = 4,
    }

    public delegate bool UnitFilter(MatchState s, UnitState source, UnitState target);

    /// <summary>A status an active applies.</summary>
    public struct Grant
    {
        public RStatusType Type;
        public int Amount;
        public int Uses;
        public RExpire Expire;
        /// <summary>For RoundEnd: how many rounds (1 = this round). For AfterOwnActivation: how many of the target's activations (1 = next).</summary>
        public int Duration;
        public int Range;
        public Grant(RStatusType type, int amount = 0, RExpire expire = RExpire.AfterOwnActivation, int duration = 1, int uses = 0, int range = 0)
        { Type = type; Amount = amount; Expire = expire; Duration = duration; Uses = uses; Range = range; }
    }

    /// <summary>
    /// The executable form of one unit's active ability. Fields are interpreted by the handler for <see cref="Code"/>;
    /// <see cref="Special"/> names the few behaviours the generic handlers cannot express.
    /// </summary>
    public sealed class ActiveSpec
    {
        public EffectCode Code;
        public TargetKind Target = TargetKind.Self;
        public int Range;
        public int MinRange;
        public int Cooldown;
        public bool NeedsLos;
        /// <summary>Maximum unit targets (RALLY "up to three", BARRAGE two…). For Hex targets, the number of hexes.</summary>
        public int MaxTargets = 1;
        /// <summary>Minimum unit targets ("two different enemies" = 2).</summary>
        public int MinTargets = 1;
        /// <summary>Extra eligibility (class, faction, HP…); null = any.</summary>
        public UnitFilter Filter;
        public string FilterText;
        public bool AllowSelf;

        public int Damage;
        /// <summary>ARM ignored; 99 = all armor.</summary>
        public int Pierce;
        /// <summary>Ignores ARM, cover and shields (Foreclosure Claw).</summary>
        public bool Unavoidable;
        /// <summary>Damage to every unit on the hexes adjacent to the centre (AOE/BOMBARD).</summary>
        public int Splash;
        /// <summary>Splash/area damage spares allies.</summary>
        public bool EnemiesOnly;
        public bool IgnoresCover;

        public Grant[] Grants = Array.Empty<Grant>();
        public Grant[] SelfGrants = Array.Empty<Grant>();
        public int Heal;
        public int HealVehicle;
        public int SelfDamage;

        public DashKind Dash;
        public int DashRange;
        /// <summary>The dash ends with a strike of this much damage against one adjacent enemy (0 = no strike).</summary>
        public int StrikeDamage;
        public int StrikePierce;
        /// <summary>The ability may only be used if the unit has not moved this activation.</summary>
        public bool RequiresNoMove;

        /// <summary>HP threshold for CAPTURE/BOARD.</summary>
        public int HpThreshold;
        public int BarrierHp;
        public int BarrierRounds;
        public bool BarrierBlocking;
        public int TokenCap;
        public string TokenName;

        /// <summary>Number of selectable options (0/1 = none) and their labels.</summary>
        public string[] Options = Array.Empty<string>();
        public string Special;

        /// <summary>A plain-language line describing exactly how the rules engine executes this ability.</summary>
        public string Engine;
    }

    /// <summary>How each passive is executed. The engine calls <see cref="RosterPassives"/> hooks that switch on Kind.</summary>
    public enum PassiveKind : byte
    {
        /// <summary>No in-battle effect under these rules (campaign, post-battle or inert); Note explains why.</summary>
        Inert = 0,
        ArmorIfAdjacentAlly, ArmorIfStationary, ArmorIfAdjacentAllyStationary, ArmorOnCoverOrObjective, ArmorIfAdjacentBigShip,
        ArmorStationaryVsUnmarkedRanged, AuraArmorWhileStationary, AuraArmorInfantryUnmoved, AuraArmorOnObjective,
        ArmorFirstHitNearLeader,
        IgnoreRoughOnce, IgnoreRough, HiddenBeyond, GuardLeader, AuraCover, AttackViaSpotter, AuraIgnoreCover, MercifulKill,
        BlockRescue, AuraHealAfterActivation, LeaderGuardAura, AuraAbilityRange, IgnoreCover, NebulaImmune, DamageVsSmallCraft,
        DamageIfAdjacentRole, LosThroughFriendlySmallCraft, Spotter, AuraNebulaImmune, ForcedMoveImmune, ForcedMoveImmuneVsInfantry,
        ForcedMoveAndRootImmuneBelowElite, AuraDamageToRole, RevealMinesAtStart, CounterReduction, RangeIfStationary,
        AuraDisableWard, AuraCloakSight, PierceVsFortified, ReduceFirstFighterHit, ForcedMoveImmuneSmallSource, SelfHealIfIdle,
        InertialMass, FreeStepOnKill, DamageAfterLeap, HazardImmune, DamageVsBarrier, DamageIfNotMoved, DisarmMinesAtStart,
        ReduceFirstRangedIfStationary, AuraHazardWard, RootImmuneBelowTier4, AllyKillMoveAura, ReduceFromDistance, OverkillThroughCover,
        DamageVsMovedTarget, MoveAfterIntercept, UntargetableByCapitalBeyond, MoveIfAllyKill, DamageIfAllyNearTarget,
        PierceVsCapitalIfStationary, NoAttackAfterMove, DeathBlast, AuraMove, PassThroughAlly, IgnoreCoverFromCover,
        GuardReductionForLeader, AuraSuppressWard, NoDamagingActiveOnCaptured, ShieldAtStartIfNotAttacked, MarkAttackerIfGuardNear,
        RepairBonusAura, ArmorIfNoAlliedKill, HealAllyEndingAdjacent, DamageVsLeaderThreat, AuraReduceForCarrier, DebuffWardOnce,
        AuraHealAtStart, DamageIfAdjacentFrigate, NeutralEmblem, ShieldAfterFirstHitSurvived, PierceIfUnmarked, DamageAtDistance,
        AuraEnemySlow, PierceVsMarked, AuraIgnoreCoverVsMarked, ReduceFirstDirectHit, MarkWardOnce, RescueDouble,
        ForcedMoveImmuneIfStationary, PierceVsShieldedOrFortified, ShieldOnFirstObjective, ReduceNextRangedIfMoved,
        ReduceFirstAttackBattle, DamageVsMarked, RootWardOnce, AuraShieldOnActivate, DamageVsIsolated, AuraDamageVsMarked,
        MoveIfFreshWreck, ShieldOnObjectiveMove, GangUp, PierceFromCloak, AuraDisableWardVehiclesBattle, AuraDamageVsObjectiveHolder,
        ShieldOnScoutKill, AuraMoveForCorsairs, ReduceFirstBasicPerRound, DamageAdjacent, MoveIfAllyBoarded, PierceVsBoarded,
        LosThroughAdjacentAlly, AuraMoveSmallCraft, AnchorGunners, AuraDamageVsBoarded,
    }

    public sealed class PassiveSpec
    {
        public PassiveKind Kind;
        public int Amount = 1;
        /// <summary>Radius for auras (1 = adjacent).</summary>
        public int Range = 1;
        /// <summary>Optional class filter for auras (Line = infantry only, Fighter = small craft only, other roles = exact role).</summary>
        public UnitRole? Role;
        /// <summary>The bonus applies to basic attacks only.</summary>
        public bool BasicOnly;
        /// <summary>How the engine executes the passive, or why it has no in-battle effect.</summary>
        public string Engine;
    }
}
