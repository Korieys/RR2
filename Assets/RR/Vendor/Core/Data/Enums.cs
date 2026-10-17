using System;

namespace Commandfall.Core
{
    public enum FactionId : byte { Craigon = 0, Silex = 1, Pygmaei = 2, Meeples = 3, Inhumanus = 4, SpacePirates = 5 }

    public enum Archetype : byte { Commander = 0, Infantry = 1, Raider = 2, Gunner = 3, Guardian = 4, Vehicle = 5 }

    public enum TerrainType : byte { Open = 0, Cover = 1, Elevated = 2, Obstacle = 3, Hazard = 4 }

    public enum EquipmentCategory : byte { Weapon = 0, Defense = 1, Utility = 2, Vehicle = 3 }

    /// <summary>Equipment slots. A unit definition lists which slots it has.</summary>
    public enum SlotType : byte { Weapon = 0, Defense = 1, Utility = 2, VehicleUpgrade = 3 }

    public enum WeaponKind : byte { Melee = 0, Ranged = 1 }

    [Flags]
    public enum WeaponTraits : ushort
    {
        None = 0,
        IgnoresCover = 1 << 0,   // cover gives no mitigation
        Indirect = 1 << 1,       // ignores line of sight entirely (artillery, telepathy)
        Phase = 1 << 2,          // shots pass through units (terrain still blocks)
        Splash = 1 << 3,         // 1 damage to every other unit adjacent to the target hex (friend or foe)
        Rend = 1 << 4,           // +1 damage against targets already below max HP
        Shatter = 1 << 5,        // +1 damage against Vehicles, Guardians and bulwarks
        Expose = 1 << 6,         // target becomes Exposed (+1 damage taken) until the end of the attacker's next activation
    }

    public enum Doctrine : byte { None = 0, Assault = 1, Defense = 2, Control = 3 }

    public enum TechMode : byte { Balanced = 0, Advanced = 1, Asymmetric = 2 }

    public enum StalemateRule : byte
    {
        /// <summary>When the activation limit is reached, the designated defender wins by successful denial.</summary>
        DefenderDenial = 0,
        /// <summary>Symmetric tiebreak: surviving army value, then Commander HP, then the seat that moved second.</summary>
        ExhibitionTiebreak = 1,
    }

    public enum MatchPhase : byte { Deployment = 0, Battle = 1, Ended = 2 }

    public enum VictoryReason : byte
    {
        None = 0,
        CommanderEliminated = 1,
        ClockExpired = 2,
        Resignation = 3,
        Disconnection = 4,
        ActivationLimitDefender = 5,
        ActivationLimitTiebreak = 6,
        /// <summary>Full Roster round limit: decided on objectives held, then surviving army value.</summary>
        RoundLimitObjectives = 8,
        /// <summary>Defender denial broken: at the activation limit the attacker held no-man's land (only its units there).</summary>
        ActivationLimitHeldGround = 7,
    }

    public enum StatusType : byte
    {
        Ward = 0,        // the next attack against this unit deals 0 damage (explicit temporary immunity), then consumed
        Overheated = 1,  // takes +1 damage from attacks; cannot Guard or Overclock
        Lien = 2,        // cannot use Abilities or Guard; cleared by Settle Debt or by the holder's destruction
        JuryRigged = 3,  // next attack deals +1 damage, then consumed
        Dazed = 4,       // cannot be activated
        Exposed = 5,     // takes +1 damage from attacks
        Unbreakable = 6, // the first lethal hit leaves this unit at 1 HP instead, then consumed
    }

    public enum AbilityId : byte
    {
        None = 0,
        TheGift = 1,
        ResonantBulwark = 2,
        OverclockBarrage = 3,
        CouncilFormation = 4,
        Lien = 5,
        SettleDebt = 6,
        Salvage = 7,
        MindShield = 8,
        PatchUp = 9,
        Smoke = 10,
        AssetRelocation = 11,
        /// <summary>Full Roster: the unit's own codex active ability (name and effect come from its profile).</summary>
        UnitActive = 100,
        /// <summary>Full Roster: free an adjacent captured or boarded ally (spends the action).</summary>
        Rescue = 101,
    }

    public enum AbilityTargeting : byte
    {
        Self = 0,          // no target
        EnemyUnit = 1,
        AllyUnit = 2,      // includes self only if the ability says so
        EmptyHex = 3,
        WreckHex = 4,
        MoveThenAttack = 5,
        AnyHex = 6,
    }

    public enum CommandType : byte
    {
        None = 0,
        CommitDeployment = 1,
        AutoDeploy = 2,
        Move = 3,
        Attack = 4,
        Ability = 5,
        Guard = 6,
        Pass = 7,
        Resign = 8,
        ClockExpire = 9,
        Forfeit = 10,     // host-issued (disconnection)
    }

    public enum AIDifficulty : byte { Beginner = 0, Tactical = 1 }

    public enum SalvageChoice : byte { Repair = 0, RigWeapon = 1 }
}
