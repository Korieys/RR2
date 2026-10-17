namespace Commandfall.Core
{
    public static partial class GameContent
    {
        // ================================================================== CRAIGON
        static void BuildCraigon()
        {
            Factions[FactionId.Craigon] = new FactionDef
            {
                Id = FactionId.Craigon, Name = "Craigon", ShortCode = "CRA", Folder = "Craigon",
                Motto = "Evolution / Control / The Gift", Tagline = "Adapt or Ascend",
                Lore = "The Craigon are a hyper-intelligent crustacean collective, united by telepathic coordination, ruthless " +
                       "experimentation, and the conversion of conquered enemies into willing components of a greater whole.",
                PassiveName = "Hive Link",
                PassiveDescription = "A Craigon unit adjacent to another Craigon unit gains +1 armor.",
                Signature = AbilityId.TheGift,
                Strengths = new[] { "Durable when grouped", "Converts weakened enemies", "Precise ranged lances" },
                Weaknesses = new[] { "Loses armor when isolated", "Expensive elite tech", "The Gift needs setup" },
                AccentColor = "#5AA2FF", SecondaryColor = "#C8343A", AudioFamily = "craigon",
            };
            var f = FactionId.Craigon;
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "Hierophant of the Shoal", Role = "Commander",
                Description = "A telepathic high priest who reshapes the defeated into kin.",
                Cost = 5, MaxHp = 5, Move = 1, Armor = 0,
                Weapon = W("Psionic Staff", 1, 1, 2, WeaponKind.Ranged, "craigon_bio_lance"),
                Abilities = new[] { new AbilityGrant(AbilityId.TheGift, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Shell Drone", Role = "Infantry",
                Description = "Bio-engineered frontline shell, never alone.", Cost = 2, MaxHp = 2, Move = 1,
                Weapon = Melee("Pincers", 1, "craigon_claw") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Riptide Stalker", Role = "Raider",
                Description = "A fast hunter that finishes what others start.", Cost = 3, MaxHp = 2, Move = 2,
                Weapon = Melee("Rending Claws", 1, "craigon_claw", WeaponTraits.Rend) });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Lance Weaver", Role = "Gunner",
                Description = "Fires coherent bio-plasma that bends around cover.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Bio-Lance", 2, 2, 3, WeaponKind.Ranged, "craigon_bio_lance", WeaponTraits.IgnoresCover) });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Carapace Warden", Role = "Guardian",
                Description = "A living shield that projects telepathic wards.", Cost = 3, MaxHp = 4, Move = 1, Armor = 1,
                Weapon = Melee("Crushing Claw", 1, "craigon_claw"),
                Abilities = new[] { new AbilityGrant(AbilityId.MindShield, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Brood Strider", Role = "Vehicle",
                Description = "Six-legged war-beast carrying a spinal bio-cannon.", Cost = 5, MaxHp = 4, Move = 2, Armor = 1,
                Weapon = W("Spinal Cannon", 2, 1, 2, WeaponKind.Ranged, "craigon_bio_lance") });

            AddEquip(new EquipmentDef { Id = "cra.u.assimilation_lattice", Name = "Assimilation Lattice", Category = EquipmentCategory.Utility,
                Tier = 3, Cost = 2, Faction = f, Allowed = new[] { Archetype.Infantry, Archetype.Raider, Archetype.Guardian },
                GrantsAbility = AbilityId.TheGift, GrantedCharges = 1,
                Description = "Grafts a conversion node: grants The Gift (1 charge).", Tradeoff = "2 points for a single, conditional use." });
            AddEquip(new EquipmentDef { Id = "cra.w.neural_lance", Name = "Neural Lance", Tier = 4, Cost = 2, Faction = f,
                Allowed = new[] { Archetype.Gunner, Archetype.Commander },
                Weapon = W("Neural Lance", 2, 1, 3, WeaponKind.Ranged, "craigon_bio_lance", WeaponTraits.Indirect, cooldown: 1),
                Description = "Telepathic strike that needs no line of sight.", Tradeoff = "Must recharge for one turn after firing." });
            AddEquip(new EquipmentDef { Id = "cra.u.synaptic_crown", Name = "Synaptic Crown", Category = EquipmentCategory.Utility,
                Tier = 5, Cost = 3, Faction = f, Allowed = new[] { Archetype.Commander }, Flags = EquipmentFlags.GreaterGift,
                Description = "The Gift may target enemies with 2 HP or less.", Tradeoff = "3 points; The Gift still has only its normal charges." });
        }

        // ================================================================== SILEX
        static void BuildSilex()
        {
            Factions[FactionId.Silex] = new FactionDef
            {
                Id = FactionId.Silex, Name = "Silex", ShortCode = "SIL", Folder = "Silex",
                Motto = "Endurance / Fortress / Resonance", Tagline = "The Forge Endures",
                Lore = "Massive crystalline beings whose fortresses have never fallen. The Silex grow their armour, sing their walls " +
                       "into being, and outlast every siege.",
                PassiveName = "Living Stone",
                PassiveDescription = "Silex units have +1 max HP over the standard profile (already included); Refractor gunners are the exception.",
                Signature = AbilityId.ResonantBulwark,
                Strengths = new[] { "Highest durability", "Raise walls that block sight", "Crushing melee" },
                Weaknesses = new[] { "Slow", "Fewer units per army", "Short-ranged" },
                AccentColor = "#B79BFF", SecondaryColor = "#C9A04E", AudioFamily = "silex",
            };
            var f = FactionId.Silex;
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "Forge-Lord", Role = "Commander",
                Description = "An ancient monolith-king whose voice raises walls.", Cost = 5, MaxHp = 6, Move = 1,
                Weapon = Melee("Crystal Maul", 2, "silex_maul"),
                Abilities = new[] { new AbilityGrant(AbilityId.ResonantBulwark, -1) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Shardling", Role = "Infantry",
                Description = "Young crystal soldier, harder than it looks.", Cost = 3, MaxHp = 3, Move = 1,
                Weapon = Melee("Crystal Fists", 1, "silex_maul") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Prism Stalker", Role = "Raider",
                Description = "A gliding blade of living quartz.", Cost = 3, MaxHp = 3, Move = 2,
                Weapon = Melee("Shard Blades", 1, "silex_maul") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Refractor", Role = "Gunner",
                Description = "Focuses starlight through its own body.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Refraction Beam", 2, 2, 3, WeaponKind.Ranged, "silex_crystal_beam") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Bastion Colossus", Role = "Guardian",
                Description = "A walking fortress wall.", Cost = 4, MaxHp = 5, Move = 1, Armor = 1,
                Weapon = Melee("Tectonic Fists", 2, "silex_maul"),
                Abilities = new[] { new AbilityGrant(AbilityId.ResonantBulwark, -1) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Crystal Walker", Role = "Vehicle",
                Description = "A four-legged siege engine bearing a resonance cannon.", Cost = 5, MaxHp = 5, Move = 1, Armor = 1,
                Weapon = W("Resonance Cannon", 2, 1, 3, WeaponKind.Ranged, "silex_crystal_beam") });

            AddEquip(new EquipmentDef { Id = "sil.w.geode_maul", Name = "Geode Maul", Tier = 3, Cost = 1, Faction = f,
                Allowed = new[] { Archetype.Commander, Archetype.Infantry, Archetype.Raider, Archetype.Guardian },
                Weapon = Melee("Geode Maul", 2, "silex_maul", WeaponTraits.Shatter, pierce: 1),
                Description = "2 melee damage, pierces 1 armor, +1 against Vehicles, Guardians and bulwarks.", Tradeoff = "Melee only." });
            AddEquip(new EquipmentDef { Id = "sil.u.resonance_core", Name = "Resonance Core", Category = EquipmentCategory.Utility,
                Tier = 4, Cost = 2, Faction = f, Allowed = HeavyFoot, Flags = EquipmentFlags.ResonanceCore,
                Description = "Bulwarks raised by this unit have +1 HP and last 3 activations longer.", Tradeoff = "Only useful with Resonant Bulwark." });
            AddEquip(new EquipmentDef { Id = "sil.d.monolith_heart", Name = "Monolith Heart", Category = EquipmentCategory.Defense,
                Tier = 5, Cost = 3, Faction = f, Allowed = HeavyFoot, Unbreakable = true,
                Description = "Unbreakable: the first lethal hit leaves this unit at 1 HP.", Tradeoff = "3 points; works once." });
        }

        // ================================================================== PYGMAEI
        static void BuildPygmaei()
        {
            Factions[FactionId.Pygmaei] = new FactionDef
            {
                Id = FactionId.Pygmaei, Name = "Pygmaei", ShortCode = "PYG", Folder = "Pygmaei",
                Motto = "Engineering / Vengeance / Firepower", Tagline = "Never Again",
                Lore = "Once peaceful engineering masters, the Pygmaei were nearly exterminated. The survivors rebuilt themselves into a " +
                       "ruthless war machine of oversized guns, overclocked rigs and grim resolve.",
                PassiveName = "Rapid Deployment",
                PassiveDescription = "Pygmaei Infantry and Raiders have +1 movement (included in their profiles).",
                Signature = AbilityId.OverclockBarrage,
                Strengths = new[] { "Fast", "Move-and-fire Overclock", "Heavy weapons" },
                Weaknesses = new[] { "Fragile", "Overheated units are exposed", "Low armor" },
                AccentColor = "#FF7A3D", SecondaryColor = "#8E1F1F", AudioFamily = "pygmaei",
            };
            var f = FactionId.Pygmaei;
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "Forgemaster", Role = "Commander",
                Description = "A grizzled war-engineer with a cannon bolted to his arm.", Cost = 5, MaxHp = 4, Move = 1,
                Weapon = W("Rivet Cannon", 2, 1, 2, WeaponKind.Ranged, "pygmaei_autocannon"),
                Abilities = new[] { new AbilityGrant(AbilityId.OverclockBarrage, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Scrapper", Role = "Infantry",
                Description = "Quick, mean and armed with a flak carbine.", Cost = 2, MaxHp = 2, Move = 2,
                Weapon = W("Flak Carbine", 1, 1, 2, WeaponKind.Ranged, "pygmaei_autocannon") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Rocketeer", Role = "Raider",
                Description = "Strapped to a rocket and a piston hammer.", Cost = 3, MaxHp = 2, Move = 3,
                Weapon = Melee("Piston Hammer", 1, "generic_melee"),
                Abilities = new[] { new AbilityGrant(AbilityId.OverclockBarrage, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Autocannon Crew", Role = "Gunner",
                Description = "One gnome, one very large gun.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Autocannon", 2, 2, 3, WeaponKind.Ranged, "pygmaei_autocannon"),
                Abilities = new[] { new AbilityGrant(AbilityId.OverclockBarrage, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Bulwark Engine", Role = "Guardian",
                Description = "A riveted shield-rig with smoke launchers.", Cost = 3, MaxHp = 3, Move = 1, Armor = 1,
                Weapon = Melee("Shield Ram", 1, "generic_melee"),
                Abilities = new[] { new AbilityGrant(AbilityId.Smoke, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Siege Crawler", Role = "Vehicle",
                Description = "Tracked mortar platform that never stops moving.", Cost = 5, MaxHp = 4, Move = 2,
                Weapon = W("Heavy Mortar", 2, 2, 3, WeaponKind.Ranged, "pygmaei_mortar", WeaponTraits.Indirect),
                Abilities = new[] { new AbilityGrant(AbilityId.OverclockBarrage, 2) } });

            AddEquip(new EquipmentDef { Id = "pyg.w.rotary_autocannon", Name = "Rotary Autocannon", Tier = 3, Cost = 1, Faction = f,
                Allowed = Shooters, Weapon = W("Rotary Autocannon", 2, 1, 3, WeaponKind.Ranged, "pygmaei_autocannon"),
                Description = "2 damage at range 1-3 — no blind spot.", Tradeoff = "Loses any Indirect fire." });
            AddEquip(new EquipmentDef { Id = "pyg.w.rocket_pods", Name = "Rocket Pods", Tier = 4, Cost = 2, Faction = f,
                Allowed = new[] { Archetype.Infantry, Archetype.Raider },
                Weapon = W("Rocket Pods", 2, 1, 2, WeaponKind.Ranged, "pygmaei_rocket", WeaponTraits.Splash, cooldown: 1),
                Description = "2 damage with splash at range 1-2.", Tradeoff = "Splash hits allies too; one turn to reload." });
            AddEquip(new EquipmentDef { Id = "pyg.w.hellforge_cannon", Name = "Hellforge Cannon", Tier = 5, Cost = 3, Faction = f,
                Allowed = Shooters, Weapon = W("Hellforge Cannon", 3, 2, 4, WeaponKind.Ranged, "pygmaei_mortar", pierce: 1, cooldown: 2),
                Description = "3 damage, pierce 1, range 2-4.", Tradeoff = "Two turns to recharge; cannot hit adjacent targets." });
        }

        // ================================================================== MEEPLES
        static void BuildMeeples()
        {
            Factions[FactionId.Meeples] = new FactionDef
            {
                Id = FactionId.Meeples, Name = "Meeples", ShortCode = "MEE", Folder = "Meeples",
                Motto = "Diplomacy / Unity / Council", Tagline = "Stronger Together",
                Lore = "The Meeples are a balanced and idealistic civilization devoted to peace, trade, and interstellar order. United by " +
                       "shared purpose, they build bridges between worlds and believe in a stronger galaxy through cooperation.",
                PassiveName = "Shared Command",
                PassiveDescription = "The Meeple Commander has +1 armor while adjacent to at least one ally.",
                Signature = AbilityId.CouncilFormation,
                Strengths = new[] { "Flexible ranged infantry", "Mass Guard formations", "Well-protected Commander" },
                Weaknesses = new[] { "No standout damage", "Formations need clumping", "Splash punishes clusters" },
                AccentColor = "#9BD14B", SecondaryColor = "#3A64B0", AudioFamily = "meeples",
            };
            var f = FactionId.Meeples;
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "High Councilor", Role = "Commander",
                Description = "Diplomat first, general when she must be.", Cost = 5, MaxHp = 5, Move = 1,
                Weapon = W("Orb Staff", 1, 1, 2, WeaponKind.Ranged, "meeples_rifle"),
                Abilities = new[] { new AbilityGrant(AbilityId.CouncilFormation, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Council Trooper", Role = "Infantry",
                Description = "Disciplined line soldier with a service rifle.", Cost = 2, MaxHp = 2, Move = 1,
                Weapon = W("Service Rifle", 1, 1, 2, WeaponKind.Ranged, "meeples_rifle") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Pathfinder", Role = "Raider",
                Description = "Scout-duelist of the Council frontier.", Cost = 3, MaxHp = 2, Move = 2,
                Weapon = Melee("Duelist Saber", 1, "meeples_blade", WeaponTraits.Rend) });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Lancer Team", Role = "Gunner",
                Description = "Pulse-lance specialists.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Pulse Lance", 2, 2, 3, WeaponKind.Ranged, "meeples_rifle") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Aegis Sentinel", Role = "Guardian",
                Description = "Halberdier of the Council guard.", Cost = 3, MaxHp = 4, Move = 1, Armor = 1,
                Weapon = Melee("Halberd", 1, "meeples_blade"),
                Abilities = new[] { new AbilityGrant(AbilityId.CouncilFormation, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Aegis Carrier", Role = "Vehicle",
                Description = "Hovering twin-cannon carrier.", Cost = 5, MaxHp = 4, Move = 2, Armor = 1,
                Weapon = W("Twin Cannons", 2, 1, 2, WeaponKind.Ranged, "meeples_rifle") });

            AddEquip(new EquipmentDef { Id = "mee.w.council_rifle", Name = "Council Longrifle", Tier = 3, Cost = 1, Faction = f,
                Allowed = Fighters, Weapon = W("Council Longrifle", 1, 1, 3, WeaponKind.Ranged, "meeples_rifle", WeaponTraits.IgnoresCover),
                Description = "1 damage at range 1-3, ignores cover.", Tradeoff = "Still only 1 damage." });
            AddEquip(new EquipmentDef { Id = "mee.d.council_aegis", Name = "Council Aegis", Category = EquipmentCategory.Defense,
                Tier = 4, Cost = 2, Faction = f, Allowed = HeavyFoot, ArmorBonus = 1, Flags = EquipmentFlags.CouncilAegis,
                Description = "+1 armor; adjacent allies count as in cover.", Tradeoff = "2 points." });
            AddEquip(new EquipmentDef { Id = "mee.u.unity_standard", Name = "Unity Standard", Category = EquipmentCategory.Utility,
                Tier = 5, Cost = 3, Faction = f, Allowed = new[] { Archetype.Commander },
                BoostsAbility = AbilityId.CouncilFormation, BoostCharges = 1, Flags = EquipmentFlags.UnityStandard,
                Description = "Council Formation gains +1 charge and reaches allies within 2 hexes.", Tradeoff = "3 points." });
        }

        // ================================================================== INHUMANUS
        static void BuildInhumanus()
        {
            Factions[FactionId.Inhumanus] = new FactionDef
            {
                Id = FactionId.Inhumanus, Name = "Inhumanus", ShortCode = "INH", Folder = "Inhumanus",
                Motto = "Commerce / Science / Dominion", Tagline = "Every Debt Is Collected",
                Lore = "The Inhumanus are a hyper-capitalist civilization of pale visionaries, mastering advanced science and economic power " +
                       "to shape the galaxy in their image. Order, progress, and profit are their true faiths.",
                PassiveName = "Proprietary Tech + Corporate Doctrine",
                PassiveDescription = "Tier III+ equipment costs 1 less (min 1). Choose a locked Doctrine: Assault (+1 damage vs full-HP " +
                                     "targets; T4-V weapons), Defense (start Guarded, Commander +1 HP; T4-V defense) or Control " +
                                     "(Lien +1 charge and +1 range, Liened units move at most 1; T4-V utility).",
                Signature = AbilityId.Lien,
                Strengths = new[] { "Cheap access to advanced tech", "Liens shut down key enemies", "Doctrine flexibility" },
                Weaknesses = new[] { "Very fragile infantry", "Locked into a doctrine", "Low base damage" },
                AccentColor = "#E8D7A8", SecondaryColor = "#1A1A1E", AudioFamily = "inhumanus",
            };
            var f = FactionId.Inhumanus;
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "Archon of the Exchange", Role = "Commander",
                Description = "Holds the debts of a hundred worlds.", Cost = 5, MaxHp = 5, Move = 1,
                Weapon = W("Orrery Scepter", 1, 1, 3, WeaponKind.Ranged, "inhumanus_gravity_lance"),
                Abilities = new[] { new AbilityGrant(AbilityId.Lien, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Contractor", Role = "Infantry",
                Description = "Indentured, expendable, invoiced in advance.", Cost = 1, MaxHp = 1, Move = 1,
                Weapon = W("Compliance Pistol", 1, 1, 2, WeaponKind.Ranged, "inhumanus_phase_rifle") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Arbitrage Skimmer", Role = "Raider",
                Description = "Low-gravity hover duelist.", Cost = 3, MaxHp = 2, Move = 3, Hover = true,
                Weapon = Melee("Mono-edge", 1, "generic_melee") });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Equity Lancer", Role = "Gunner",
                Description = "Collects with a gravity lance.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Gravity Lance", 2, 2, 3, WeaponKind.Ranged, "inhumanus_gravity_lance"),
                Abilities = new[] { new AbilityGrant(AbilityId.Lien, 1) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Liability Warden", Role = "Guardian",
                Description = "Hard-light bodyguard that relocates valuable assets.", Cost = 3, MaxHp = 3, Move = 1, Armor = 2,
                Weapon = Melee("Hard-light Baton", 1, "generic_melee"),
                Abilities = new[] { new AbilityGrant(AbilityId.AssetRelocation, 2) } });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Ascendancy Barge", Role = "Vehicle",
                Description = "Floating gravity platform.", Cost = 5, MaxHp = 4, Move = 2, Hover = true,
                Weapon = W("Gravity Projector", 2, 1, 3, WeaponKind.Ranged, "inhumanus_gravity_lance") });

            AddEquip(new EquipmentDef { Id = "inh.w.phase_rifle", Name = "Phase Rifle", Tier = 3, Cost = 2, Faction = f,
                Allowed = new[] { Archetype.Commander, Archetype.Infantry, Archetype.Gunner },
                Weapon = W("Phase Rifle", 2, 2, 4, WeaponKind.Ranged, "inhumanus_phase_rifle", WeaponTraits.Phase),
                Description = "2 damage at range 2-4; shots pass through units.", Tradeoff = "Cannot fire at adjacent targets." });
            AddEquip(new EquipmentDef { Id = "inh.u.debt_instrument", Name = "Debt Instrument", Category = EquipmentCategory.Utility,
                Tier = 4, Cost = 2, Faction = f, Allowed = new[] { Archetype.Commander, Archetype.Gunner },
                BoostsAbility = AbilityId.Lien, BoostCharges = 1, Flags = EquipmentFlags.DebtInstrument,
                Description = "Lien +1 charge; Liened enemies take +1 damage from Inhumanus attacks.", Tradeoff = "Requires Control doctrine." });
            AddEquip(new EquipmentDef { Id = "inh.w.singularity_ledger", Name = "Singularity Ledger", Tier = 5, Cost = 4, Faction = f,
                Allowed = new[] { Archetype.Commander, Archetype.Vehicle },
                Weapon = W("Singularity Ledger", 3, 1, 3, WeaponKind.Ranged, "inhumanus_gravity_lance", pierce: 2, cooldown: 2),
                Description = "3 damage, pierce 2, range 1-3.", Tradeoff = "Two turns to recharge; requires Assault doctrine." });
        }

        // ================================================================== SPACE PIRATES
        static void BuildSpacePirates()
        {
            Factions[FactionId.SpacePirates] = new FactionDef
            {
                Id = FactionId.SpacePirates, Name = "Space Pirates", ShortCode = "PIR", Folder = "SpacePirates",
                Motto = "Plunder / Salvage / Rule", Tagline = "Take What Isn't Yours",
                Lore = "A loose confederation of ruthless raiders and smugglers who thrive on cannon fire, scavenged warships, and dirty " +
                       "tactics. Where others build empires, they take what isn't theirs.",
                PassiveName = "Gang Up",
                PassiveDescription = "Pirate attacks deal +1 damage when another Pirate unit is adjacent to the target.",
                Signature = AbilityId.Salvage,
                Strengths = new[] { "Flanking damage", "Artillery broadsides", "Repairs from wrecks" },
                Weaknesses = new[] { "Splash can hit friends", "Needs wrecks to salvage", "Slow cannon rig" },
                AccentColor = "#E0453A", SecondaryColor = "#B08A4A", AudioFamily = "pirates",
            };
            var f = FactionId.SpacePirates;
            var salvage = new[] { new AbilityGrant(AbilityId.Salvage, 2) };
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Commander, Name = "Dread Captain", Role = "Commander",
                Description = "Cigar, hand-cannons and no mercy.", Cost = 5, MaxHp = 5, Move = 1,
                Weapon = W("Twin Hand-Cannons", 2, 1, 2, WeaponKind.Ranged, "pirates_hand_cannon"), Abilities = salvage });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Infantry, Name = "Deckhand", Role = "Infantry",
                Description = "Boarding axe and bad intentions.", Cost = 2, MaxHp = 2, Move = 1,
                Weapon = Melee("Boarding Axe", 1, "pirates_cutlass", WeaponTraits.Rend), Abilities = salvage });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Raider, Name = "Corsair", Role = "Raider",
                Description = "Cutlass in one hand, pistol in the other.", Cost = 3, MaxHp = 2, Move = 2,
                Weapon = W("Cutlass & Pistol", 1, 1, 2, WeaponKind.Ranged, "pirates_hand_cannon"), Abilities = salvage });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Gunner, Name = "Swivel-Gunner", Role = "Gunner",
                Description = "Hauls a ship's swivel gun into battle.", Cost = 3, MaxHp = 2, Move = 1,
                Weapon = W("Swivel Gun", 2, 2, 3, WeaponKind.Ranged, "pirates_hand_cannon"), Abilities = salvage });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Guardian, Name = "Ironhull Bruiser", Role = "Guardian",
                Description = "Wears a hull plate as a shield.", Cost = 3, MaxHp = 5, Move = 1, Armor = 1,
                Weapon = Melee("Anchor Flail", 1, "pirates_cutlass"), Abilities = salvage });
            AddUnit(new UnitDef { Faction = f, Archetype = Archetype.Vehicle, Name = "Scrap Cannon Rig", Role = "Vehicle",
                Description = "A salvaged ship cannon on stolen wheels.", Cost = 5, MaxHp = 4, Move = 1,
                Weapon = W("Broadside Cannon", 2, 2, 4, WeaponKind.Ranged, "pirates_broadside",
                           WeaponTraits.Indirect | WeaponTraits.Splash, cooldown: 1) });

            AddEquip(new EquipmentDef { Id = "pir.w.boarding_pistols", Name = "Boarding Pistols", Tier = 3, Cost = 1, Faction = f,
                Allowed = Fighters, Weapon = W("Boarding Pistols", 1, 1, 2, WeaponKind.Ranged, "pirates_hand_cannon", WeaponTraits.Rend),
                Description = "Range 1-2; +1 damage against damaged targets.", Tradeoff = "Base damage 1." });
            AddEquip(new EquipmentDef { Id = "pir.w.chain_shot", Name = "Chain-Shot Cannon", Tier = 4, Cost = 2, Faction = f,
                Allowed = new[] { Archetype.Gunner },
                Weapon = W("Chain-Shot Cannon", 2, 2, 3, WeaponKind.Ranged, "pirates_broadside", WeaponTraits.Expose),
                Description = "2 damage; the target becomes Exposed (+1 damage taken) until the end of your next activation.", Tradeoff = "2 points." });
            AddEquip(new EquipmentDef { Id = "pir.w.leviathan_broadside", Name = "Leviathan Broadside", Tier = 5, Cost = 3, Faction = f,
                Allowed = new[] { Archetype.Vehicle },
                Weapon = W("Leviathan Broadside", 3, 2, 5, WeaponKind.Ranged, "pirates_broadside",
                           WeaponTraits.Indirect | WeaponTraits.Splash, cooldown: 2),
                Description = "3 damage, range 2-5, indirect, splash.", Tradeoff = "Two turns to reload; splash hits allies." });
        }
    }
}
