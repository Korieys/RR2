using System;
using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>Immutable description of a weapon's combat profile.</summary>
    public sealed class WeaponProfile
    {
        public string Name;
        public int Damage;
        public int MinRange = 1;
        public int MaxRange = 1;
        public WeaponKind Kind = WeaponKind.Melee;
        public WeaponTraits Traits;
        public int Pierce;          // ignores this much armour
        public int CooldownTurns;   // own turns the weapon must skip after firing (0 = fire every turn)
        public string FxKey = "generic_melee"; // presentation hook (projectile/impact/audio family)

        public bool Has(WeaponTraits t) => (Traits & t) != 0;

        public WeaponProfile Clone() => (WeaponProfile)MemberwiseClone();

        public string RangeText => MinRange == MaxRange ? MaxRange.ToString() : $"{MinRange}-{MaxRange}";
    }

    [Flags]
    public enum EquipmentFlags : ushort
    {
        None = 0,
        GreaterGift = 1 << 0,      // The Gift may target units with ≤ 2 HP
        ResonanceCore = 1 << 1,    // bulwarks: +1 HP, +2 activations duration
        DebtInstrument = 1 << 2,   // Lien +1 charge; Liened enemies take +1 damage from Inhumanus attacks
        UnityStandard = 1 << 3,    // Council Formation reaches allies within 2
        CouncilAegis = 1 << 4,     // adjacent allies count as in cover
        Hover = 1 << 5,            // ignores elevation cost and hazard damage
    }

    public sealed class EquipmentDef
    {
        public string Id;
        public string Name;
        public string Description;
        public EquipmentCategory Category;
        public SlotType Slot;
        public int Tier = 1;
        public int Cost;
        /// <summary>Null = available to every faction.</summary>
        public FactionId? Faction;
        public Archetype[] Allowed = Array.Empty<Archetype>();
        public WeaponProfile Weapon;          // only for Weapon slot items
        public int MaxHpBonus;
        public int ArmorBonus;
        public int MoveBonus;
        public int RangeBonus;                // added to ranged weapons' max range
        public int DamageBonus;               // added to the unit's weapon damage
        public bool StartsWithWard;
        public bool Unbreakable;
        public AbilityId GrantsAbility;
        public int GrantedCharges;
        public AbilityId BoostsAbility;
        public int BoostCharges;
        public EquipmentFlags Flags;
        /// <summary>Tradeoff text shown in the army builder.</summary>
        public string Tradeoff;

        public bool IsAllowedFor(Archetype a) => Array.IndexOf(Allowed, a) >= 0;
        public bool Has(EquipmentFlags f) => (Flags & f) != 0;
    }

    public struct AbilityGrant
    {
        public AbilityId Ability;
        public int Charges; // -1 = unlimited
        public AbilityGrant(AbilityId ability, int charges) { Ability = ability; Charges = charges; }
    }

    public sealed class UnitDef
    {
        public string Id;
        public FactionId Faction;
        public Archetype Archetype;
        public string Name;
        public string Role;
        public string Description;
        public int Cost;
        public int MaxHp;
        public int Move;
        public int Armor;
        public WeaponProfile Weapon;
        public SlotType[] Slots = Array.Empty<SlotType>();
        public AbilityGrant[] Abilities = Array.Empty<AbilityGrant>();
        public bool Hover;
        public string ModelKey;
        /// <summary>Vehicle chassis require the player's Vehicle technology cap to be at least this tier.</summary>
        public int RequiredVehicleTier;
        /// <summary>Full Roster (Unit Codex) data; null for Classic units.</summary>
        public RosterProfile Roster;

        public bool IsRoster => Roster != null;
        public bool HasSlot(SlotType s) => Array.IndexOf(Slots, s) >= 0;
    }

    public sealed class AbilityDef
    {
        public AbilityId Id;
        public string Name;
        public string Description;
        public AbilityTargeting Targeting;
        public int Range = 1;
        public int CooldownTurns;
        public bool IgnoresLineOfSight = true;
        public bool CanTargetSelf;
        public string FxKey;
        public string Hotkey;
    }

    public sealed class FactionDef
    {
        public FactionId Id;
        public string Name;
        public string ShortCode;   // CRA, SIL, PYG, MEE, INH, PIR
        public string Folder;      // art folder name
        public string Motto;       // e.g. "Evolution / Control / The Gift"
        public string Tagline;     // HUD line under the faction name
        public string Lore;
        public string PassiveName;
        public string PassiveDescription;
        public AbilityId Signature;
        public string[] Strengths = Array.Empty<string>();
        public string[] Weaknesses = Array.Empty<string>();
        public string AccentColor;     // UI accent (hex)
        public string SecondaryColor;  // UI secondary (hex)
        public string AudioFamily;     // e.g. "craigon"
    }
}
