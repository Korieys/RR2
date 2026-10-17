using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Data registry for factions, units, abilities and equipment. All balance numbers live here (see BALANCE_NOTES.md).
    /// Definitions are pure data; rules code in <see cref="RulesEngine"/> interprets them.
    /// </summary>
    public static partial class GameContent
    {
        public const string ContentVersion = "cf-content-1.3";   // 1.2: the 115-hex battlefields; 1.3: limit 90, no-man's land breaks denial (0.14)

        public static readonly Dictionary<FactionId, FactionDef> Factions = new Dictionary<FactionId, FactionDef>();
        public static readonly Dictionary<string, UnitDef> Units = new Dictionary<string, UnitDef>();
        public static readonly Dictionary<AbilityId, AbilityDef> Abilities = new Dictionary<AbilityId, AbilityDef>();
        public static readonly Dictionary<string, EquipmentDef> Equipment = new Dictionary<string, EquipmentDef>();

        public static readonly FactionId[] AllFactions =
            { FactionId.Craigon, FactionId.Silex, FactionId.Pygmaei, FactionId.Meeples, FactionId.Inhumanus, FactionId.SpacePirates };

        public static readonly Archetype[] AllArchetypes =
            { Archetype.Commander, Archetype.Infantry, Archetype.Raider, Archetype.Gunner, Archetype.Guardian, Archetype.Vehicle };

        static readonly Archetype[] Foot = { Archetype.Commander, Archetype.Infantry, Archetype.Raider, Archetype.Gunner, Archetype.Guardian };
        static readonly Archetype[] Fighters = { Archetype.Commander, Archetype.Infantry, Archetype.Raider };
        static readonly Archetype[] HeavyFoot = { Archetype.Commander, Archetype.Guardian };
        static readonly Archetype[] Shooters = { Archetype.Gunner, Archetype.Vehicle };

        static GameContent()
        {
            BuildAbilities();
            BuildGenericEquipment();
            BuildCraigon();
            BuildSilex();
            BuildPygmaei();
            BuildMeeples();
            BuildInhumanus();
            BuildSpacePirates();
        }

        public static UnitDef Unit(string id) => id != null && Units.TryGetValue(id, out var u) ? u : RosterContent.Unit(id);
        public static EquipmentDef Equip(string id) => id != null && Equipment.TryGetValue(id, out var e) ? e : null;
        public static AbilityDef Ability(AbilityId id) => Abilities.TryGetValue(id, out var a) ? a : null;
        public static FactionDef Faction(FactionId id) => Factions[id];

        public static UnitDef UnitFor(FactionId faction, Archetype archetype) =>
            Units.Values.First(u => u.Faction == faction && u.Archetype == archetype);

        public static IEnumerable<UnitDef> UnitsOf(FactionId faction) =>
            AllArchetypes.Select(a => UnitFor(faction, a));

        /// <summary>Equipment a given unit definition could ever mount (ignores tech caps / doctrine / budget).</summary>
        public static IEnumerable<EquipmentDef> EquipmentFor(UnitDef unit, SlotType slot) =>
            Equipment.Values.Where(e => e.Slot == slot && e.IsAllowedFor(unit.Archetype)
                                        && (e.Faction == null || e.Faction == unit.Faction))
                     .OrderBy(e => e.Tier).ThenBy(e => e.Cost).ThenBy(e => e.Id, StringComparer.Ordinal);

        // ------------------------------------------------------------------ helpers

        static WeaponProfile W(string name, int dmg, int min, int max, WeaponKind kind, string fx,
                               WeaponTraits traits = WeaponTraits.None, int pierce = 0, int cooldown = 0) =>
            new WeaponProfile { Name = name, Damage = dmg, MinRange = min, MaxRange = max, Kind = kind, FxKey = fx,
                                Traits = traits, Pierce = pierce, CooldownTurns = cooldown };

        static WeaponProfile Melee(string name, int dmg, string fx, WeaponTraits traits = WeaponTraits.None, int pierce = 0) =>
            W(name, dmg, 1, 1, WeaponKind.Melee, fx, traits, pierce);

        static void AddUnit(UnitDef u)
        {
            u.Id = $"{Factions[u.Faction].ShortCode.ToLowerInvariant()}.{u.Archetype.ToString().ToLowerInvariant()}";
            u.ModelKey = $"{Factions[u.Faction].ShortCode}_{u.Archetype}";
            if (u.Slots.Length == 0)
            {
                switch (u.Archetype)
                {
                    case Archetype.Commander: u.Slots = new[] { SlotType.Weapon, SlotType.Defense, SlotType.Utility }; break;
                    case Archetype.Infantry: u.Slots = new[] { SlotType.Weapon, SlotType.Defense }; break;
                    case Archetype.Raider: u.Slots = new[] { SlotType.Weapon, SlotType.Utility }; break;
                    case Archetype.Gunner: u.Slots = new[] { SlotType.Weapon, SlotType.Utility }; break;
                    case Archetype.Guardian: u.Slots = new[] { SlotType.Weapon, SlotType.Defense, SlotType.Utility }; break;
                    case Archetype.Vehicle: u.Slots = new[] { SlotType.Weapon, SlotType.VehicleUpgrade }; break;
                }
            }
            if (u.Archetype == Archetype.Vehicle && u.RequiredVehicleTier == 0) u.RequiredVehicleTier = 2;
            Units.Add(u.Id, u);
        }

        static void AddEquip(EquipmentDef e)
        {
            if (e.Weapon != null) e.Slot = SlotType.Weapon;
            else if (e.Category == EquipmentCategory.Defense) e.Slot = SlotType.Defense;
            else if (e.Category == EquipmentCategory.Utility) e.Slot = SlotType.Utility;
            else if (e.Category == EquipmentCategory.Vehicle) e.Slot = SlotType.VehicleUpgrade;
            if (e.Weapon != null) e.Category = EquipmentCategory.Weapon;
            Equipment.Add(e.Id, e);
        }

        // ------------------------------------------------------------------ abilities

        static void BuildAbilities()
        {
            void A(AbilityDef d) => Abilities.Add(d.Id, d);
            A(new AbilityDef { Id = AbilityId.TheGift, Name = "The Gift", Targeting = AbilityTargeting.EnemyUnit, Range = 2, CooldownTurns = 1,
                FxKey = "ability_gift",
                Description = "Assimilate an enemy non-Commander unit at exactly 1 HP within 2 hexes (telepathic: no line of sight needed). " +
                              "It becomes a Craigon unit of the same archetype with 1 HP and standard Craigon equipment, Dazed for your next activation. " +
                              "Guarded minds resist. Cannot exceed 7 units." });
            A(new AbilityDef { Id = AbilityId.ResonantBulwark, Name = "Resonant Bulwark", Targeting = AbilityTargeting.EmptyHex, Range = 1, CooldownTurns = 2,
                FxKey = "ability_bulwark",
                Description = "Raise a 2 HP crystal bulwark on an adjacent empty, non-hazard hex for 4 activations. It blocks movement and line of sight, " +
                              "and allied units next to it count as in cover. One bulwark per unit — raising another shatters the old one. Enemies can destroy it." });
            A(new AbilityDef { Id = AbilityId.OverclockBarrage, Name = "Overclock Barrage", Targeting = AbilityTargeting.MoveThenAttack, Range = 1, CooldownTurns = 2,
                FxKey = "ability_overclock",
                Description = "Move up to 1 hex, then attack with this unit's weapon in the same activation. Afterwards the unit is Overheated " +
                              "(+1 damage taken, cannot Guard) until the end of your next activation." });
            A(new AbilityDef { Id = AbilityId.CouncilFormation, Name = "Council Formation", Targeting = AbilityTargeting.Self, Range = 1, CooldownTurns = 2,
                FxKey = "ability_council",
                Description = "This unit and every adjacent ally gain a Guard token (−1 damage from the next attack, and melee attackers suffer 1 riposte damage)." });
            A(new AbilityDef { Id = AbilityId.Lien, Name = "Lien", Targeting = AbilityTargeting.EnemyUnit, Range = 3, CooldownTurns = 1, IgnoresLineOfSight = false,
                FxKey = "ability_lien",
                Description = "Place a Lien on an enemy unit in line of sight within 3: it loses its Guard token and cannot Guard or use abilities for 5 activations. " +
                              "Counterplay: the unit may spend an activation to Settle Debt, and destroying the lien holder voids it." });
            A(new AbilityDef { Id = AbilityId.SettleDebt, Name = "Settle Debt", Targeting = AbilityTargeting.Self, Range = 0, CooldownTurns = 0,
                FxKey = "ability_lien",
                Description = "Spend this unit's activation to remove a Lien from it." });
            A(new AbilityDef { Id = AbilityId.Salvage, Name = "Battlefield Salvage", Targeting = AbilityTargeting.WreckHex, Range = 1, CooldownTurns = 0,
                FxKey = "ability_salvage",
                Description = "Strip an adjacent wreck (or the wreck underfoot). Choose: Repair (restore 2 HP) or Rig Weapon (next attack deals +1 damage). The wreck is consumed." });
            A(new AbilityDef { Id = AbilityId.MindShield, Name = "Mind Shield", Targeting = AbilityTargeting.AllyUnit, Range = 1, CooldownTurns = 2,
                FxKey = "ability_gift",
                Description = "Grant an adjacent ally a Ward for 4 activations: the next attack against it deals no damage." });
            A(new AbilityDef { Id = AbilityId.PatchUp, Name = "Patch Up", Targeting = AbilityTargeting.AllyUnit, Range = 1, CooldownTurns = 0, CanTargetSelf = true,
                FxKey = "ability_heal",
                Description = "Restore 1 HP to this unit or an adjacent ally." });
            A(new AbilityDef { Id = AbilityId.Smoke, Name = "Smoke Screen", Targeting = AbilityTargeting.AnyHex, Range = 2, CooldownTurns = 0,
                FxKey = "explosion_small",
                Description = "Fill a hex within 2 with smoke for 4 activations. Smoke blocks line of sight through it, and a unit standing in smoke counts as in cover." });
            A(new AbilityDef { Id = AbilityId.AssetRelocation, Name = "Asset Relocation", Targeting = AbilityTargeting.AllyUnit, Range = 1, CooldownTurns = 1,
                FxKey = "inhumanus_gravity_lance",
                Description = "Swap places with an adjacent ally." });
        }

        // ------------------------------------------------------------------ generic equipment

        static void BuildGenericEquipment()
        {
            AddEquip(new EquipmentDef { Id = "gen.w.marksman_rifle", Name = "Marksman Rifle", Tier = 2, Cost = 1, Allowed = Fighters,
                Weapon = W("Marksman Rifle", 1, 1, 3, WeaponKind.Ranged, "meeples_rifle"),
                Description = "Turns a frontline unit into a light skirmisher.", Tradeoff = "Damage stays at 1; loses melee-only perks." });
            AddEquip(new EquipmentDef { Id = "gen.w.heavy_blade", Name = "Heavy Blade", Tier = 2, Cost = 1,
                Allowed = new[] { Archetype.Commander, Archetype.Infantry, Archetype.Raider, Archetype.Guardian },
                Weapon = Melee("Heavy Blade", 2, "generic_melee"),
                Description = "A two-handed cutting weapon: 2 melee damage.", Tradeoff = "Melee only." });
            AddEquip(new EquipmentDef { Id = "gen.w.long_lance", Name = "Long Lance", Tier = 3, Cost = 2, Allowed = new[] { Archetype.Gunner },
                Weapon = W("Long Lance", 2, 2, 4, WeaponKind.Ranged, "meeples_rifle", WeaponTraits.IgnoresCover),
                Description = "Precision long-range weapon that ignores cover.", Tradeoff = "Cannot fire at adjacent targets." });

            AddEquip(new EquipmentDef { Id = "gen.d.plating", Name = "Reinforced Plating", Category = EquipmentCategory.Defense, Tier = 1, Cost = 1, Allowed = Foot,
                MaxHpBonus = 1, Description = "+1 max HP.", Tradeoff = "Costs a point you could spend on firepower." });
            AddEquip(new EquipmentDef { Id = "gen.d.ablative", Name = "Ablative Armor", Category = EquipmentCategory.Defense, Tier = 2, Cost = 1, Allowed = Foot,
                ArmorBonus = 1, Description = "+1 armor (reduces each hit by 1, never below 1).", Tradeoff = "Armor does nothing against 1-damage attacks." });
            AddEquip(new EquipmentDef { Id = "gen.d.deflector", Name = "Deflector Shield", Category = EquipmentCategory.Defense, Tier = 3, Cost = 2, Allowed = Foot,
                StartsWithWard = true, Description = "Begins the battle with a Ward: the first attack against this unit deals no damage.",
                Tradeoff = "Single use." });
            AddEquip(new EquipmentDef { Id = "gen.d.aegis_field", Name = "Aegis Field Generator", Category = EquipmentCategory.Defense, Tier = 4, Cost = 3, Allowed = HeavyFoot,
                ArmorBonus = 1, StartsWithWard = true, Description = "+1 armor and a starting Ward.", Tradeoff = "Expensive; heavy units only." });

            AddEquip(new EquipmentDef { Id = "gen.u.medkit", Name = "Field Medkit", Category = EquipmentCategory.Utility, Tier = 1, Cost = 1, Allowed = Foot,
                GrantsAbility = AbilityId.PatchUp, GrantedCharges = 1, Description = "Grants Patch Up (1 charge): restore 1 HP to self or an adjacent ally.",
                Tradeoff = "Uses an activation." });
            AddEquip(new EquipmentDef { Id = "gen.u.smoke", Name = "Smoke Canisters", Category = EquipmentCategory.Utility, Tier = 2, Cost = 1, Allowed = Foot,
                GrantsAbility = AbilityId.Smoke, GrantedCharges = 1, Description = "Grants Smoke Screen (1 charge).", Tradeoff = "Smoke also blocks your own shots." });
            AddEquip(new EquipmentDef { Id = "gen.u.jump_pack", Name = "Jump Pack", Category = EquipmentCategory.Utility, Tier = 2, Cost = 2, Allowed = Fighters,
                MoveBonus = 1, Description = "+1 movement.", Tradeoff = "2 points." });
            AddEquip(new EquipmentDef { Id = "gen.u.targeting_array", Name = "Targeting Array", Category = EquipmentCategory.Utility, Tier = 3, Cost = 1,
                Allowed = new[] { Archetype.Gunner, Archetype.Commander }, RangeBonus = 1,
                Description = "+1 maximum range for ranged weapons.", Tradeoff = "No effect on melee weapons." });

            AddEquip(new EquipmentDef { Id = "gen.v.reinforced_hull", Name = "Reinforced Hull", Category = EquipmentCategory.Vehicle, Tier = 2, Cost = 1,
                Allowed = new[] { Archetype.Vehicle }, MaxHpBonus = 1, Description = "+1 max HP.", Tradeoff = "—" });
            AddEquip(new EquipmentDef { Id = "gen.v.overdrive", Name = "Overdrive Engine", Category = EquipmentCategory.Vehicle, Tier = 3, Cost = 1,
                Allowed = new[] { Archetype.Vehicle }, MoveBonus = 1, Description = "+1 movement.", Tradeoff = "—" });
            AddEquip(new EquipmentDef { Id = "gen.v.heavy_ordnance", Name = "Heavy Ordnance", Category = EquipmentCategory.Vehicle, Tier = 4, Cost = 2,
                Allowed = new[] { Archetype.Vehicle }, DamageBonus = 1, MoveBonus = -1,
                Description = "+1 weapon damage.", Tradeoff = "−1 movement (minimum 1)." });
        }
    }
}
