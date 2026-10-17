using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>Codex faction text for the Full Roster ruleset (identity and faction rule).</summary>
    public sealed class RosterFaction
    {
        public string Key;
        public FactionId Id;
        public string Display;
        public string Identity;
        public string Rule;
        public string RuleName => Rule.Contains(":") ? Rule.Substring(0, Rule.IndexOf(':')) : Rule;
        public string RuleBody => Rule.Contains(":") ? Rule.Substring(Rule.IndexOf(':') + 1).Trim() : Rule;
    }

    /// <summary>
    /// Registry of the 156 Full Roster units (generated from the codex JSON by Tools/Roster/gen_roster.py) plus the SPAWN
    /// tokens and conversion auxiliaries. Unit ids are the codex ids (stable keys for art, saves and replays).
    /// </summary>
    public static partial class RosterContent
    {
        public const string ContentVersion = "cf-roster-0.1";

        public static readonly Dictionary<string, RosterFaction> Factions = new Dictionary<string, RosterFaction>();
        public static readonly Dictionary<string, UnitDef> Units = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
        /// <summary>Catalog order (faction, then planetary 1–16, then orbital 1–10).</summary>
        public static readonly List<UnitDef> Ordered = new List<UnitDef>();
        public static readonly Dictionary<string, UnitDef> Tokens = new Dictionary<string, UnitDef>(StringComparer.Ordinal);

        static RosterContent()
        {
            BuildCatalog();
            BuildTokens();
            RosterSpecs.Attach();
        }

        /// <summary>Forces the static registry to build (cheap after the first call).</summary>
        public static int Count => Units.Count;

        public static UnitDef Unit(string id) =>
            id != null && (Units.TryGetValue(id, out var u) || Tokens.TryGetValue(id, out u)) ? u : null;

        public static RosterFaction Faction(FactionId f) => Factions.Values.First(x => x.Id == f);

        public static IEnumerable<UnitDef> For(FactionId f, BattleDomain d) =>
            Ordered.Where(u => u.Faction == f && u.Roster.Domain == d);

        public static IEnumerable<UnitDef> Leaders(FactionId f, BattleDomain d) => For(f, d).Where(u => u.Roster.IsLeaderRole);

        /// <summary>Army point cost of a roster unit: unlock tier + 1 (T1 = 2 … T5 = 6), plus 1 for each point of printed ARM
        /// above 1. A balance proposal, not codex canon: with the codex's low damage values (2–3 raw for most line units),
        /// max(0, raw − ARM) makes armor far more valuable than tier alone suggests (AI harness, Docs/UNIT_CODEX_IMPLEMENTATION.md).</summary>
        public static int Cost(UnitDef u) => u.Roster.IsToken ? 0 : u.Roster.Tier + 1 + Math.Max(0, u.Armor - 1);

        // ------------------------------------------------------------------ catalog builders (called by the generated file)

        static void Faction(string key, FactionId id, string display, string identity, string rule) =>
            Factions[key] = new RosterFaction { Key = key, Id = id, Display = display, Identity = identity, Rule = rule };

        static void Unit(string id, string faction, string name, bool established, BattleDomain domain, UnitRole role, int tier, int index,
            int hp, int armor, MovePattern pattern, int move, string attack, int damage, int minRange, int maxRange, bool los,
            string active, EffectCode code, int activeRange, int cooldown, string activeText, string passive, string passiveText, string[] tags)
        {
            var f = Factions[faction];
            var profile = new RosterProfile
            {
                FactionKey = faction, Established = established, Domain = domain, Role = role, Tier = tier, CatalogIndex = index,
                Pattern = pattern, MoveHexes = move, AttackNeedsLos = los,
                ActiveName = active, ActiveCode = code, ActiveRange = activeRange, ActiveCooldown = cooldown, ActiveText = activeText,
                PassiveName = passive, PassiveText = passiveText, Tags = tags,
            };
            var def = new UnitDef
            {
                Id = id, Faction = f.Id, Name = name, Role = RoleName(role), Roster = profile, MaxHp = hp, Armor = armor, Move = move,
                Description = $"{RoleName(role)} · Tier {ArmyRules.Roman(tier)} · {(domain == BattleDomain.Orbital ? "orbital" : "planetary")}",
                Weapon = Weapon(f.Id, attack, damage, minRange, maxRange, profile),
            };
            def.Archetype = ArchetypeFor(profile);
            def.ModelKey = ModelKeyFor(def);
            def.Hover = domain == BattleDomain.Orbital;
            def.Cost = Cost(def);
            Units.Add(id, def);
            Ordered.Add(def);
        }

        static WeaponProfile Weapon(FactionId f, string name, int damage, int min, int max, RosterProfile p)
        {
            bool melee = max <= 1;
            bool heavy = damage >= 5 || p.IsVehicle || p.ShipSize >= 4;
            return new WeaponProfile
            {
                Name = name, Damage = damage, MinRange = min, MaxRange = max,
                Kind = melee ? WeaponKind.Melee : WeaponKind.Ranged, FxKey = FxFor(f, melee, heavy),
            };
        }

        static string FxFor(FactionId f, bool melee, bool heavy)
        {
            switch (f)
            {
                case FactionId.Craigon: return melee ? "craigon_claw" : "craigon_bio_lance";
                case FactionId.Silex: return melee ? "silex_maul" : "silex_crystal_beam";
                case FactionId.Pygmaei: return melee ? "generic_melee" : heavy ? "pygmaei_mortar" : "pygmaei_autocannon";
                case FactionId.Meeples: return melee ? "meeples_blade" : "meeples_rifle";
                case FactionId.Inhumanus: return melee ? "generic_melee" : heavy ? "inhumanus_gravity_lance" : "inhumanus_phase_rifle";
                default: return melee ? "pirates_cutlass" : heavy ? "pirates_broadside" : "pirates_hand_cannon";
            }
        }

        /// <summary>The classic archetype a roster unit borrows its model family, animation and audio from.</summary>
        static Archetype ArchetypeFor(RosterProfile p)
        {
            if (p.IsLeaderRole) return Archetype.Commander;
            if (p.IsShip)
            {
                if (p.ShipSize == 1) return Archetype.Raider;
                return Archetype.Vehicle;
            }
            switch (p.Role)
            {
                case UnitRole.Line: case UnitRole.Capture: case UnitRole.Engineer: case UnitRole.Token: return Archetype.Infantry;
                case UnitRole.Assault: case UnitRole.Scout: case UnitRole.Saboteur: return Archetype.Raider;
                case UnitRole.Marksman: case UnitRole.Support: return Archetype.Gunner;
                case UnitRole.Heavy: case UnitRole.Defender: case UnitRole.Elite: return Archetype.Guardian;
                default: return p.IsVehicle ? Archetype.Vehicle : Archetype.Infantry;
            }
        }

        /// <summary>Hull family for orbital models: Fighter, Escort, Warship, Capital, Flagship.</summary>
        public static string HullFor(RosterProfile p)
        {
            if (p.Role == UnitRole.Flagship) return "Flagship";
            int size = p.ShipSize;
            if (size <= 1) return "Fighter";
            if (size <= 3) return "Escort";
            if (size <= 5) return "Warship";
            return "Capital";
        }

        /// <summary>Every roster unit and token has its own model, keyed by its codex id
        /// (Art/Characters/Roster/&lt;Faction&gt;/&lt;id&gt;.fbx for ground units, Art/Characters/Ships/&lt;id&gt;.fbx for ships;
        /// Tools/Blender/roster_units.py and ships_all.py).</summary>
        static string ModelKeyFor(UnitDef def) => def.Id;

        /// <summary>The model a roster unit falls back to when its own model is missing: the faction's generic hull class
        /// for ships, the faction's classic model for the archetype it borrows on the ground.</summary>
        public static string FallbackModelKey(UnitDef def)
        {
            string code = GameContent.Faction(def.Faction).ShortCode;
            return def.Roster.IsShip ? $"SHIP_{code}_{HullFor(def.Roster)}" : ClassicModelKey(def);
        }

        /// <summary>Last-resort fallback: the faction's classic model for the borrowed archetype.</summary>
        public static string ClassicModelKey(UnitDef def) => $"{GameContent.Faction(def.Faction).ShortCode}_{def.Archetype}";

        public static string RoleName(UnitRole r) => r switch
        {
            UnitRole.VehicleSupport => "Vehicle Support", UnitRole.VehicleAssault => "Vehicle Assault",
            UnitRole.VehicleSiege => "Vehicle Siege", UnitRole.VehicleScout => "Vehicle Scout",
            UnitRole.SpaceScout => "Space Scout", UnitRole.SpaceSupport => "Space Support", UnitRole.SpaceBastion => "Space Bastion",
            _ => r.ToString(),
        };

        // ------------------------------------------------------------------ tokens

        /// <summary>SPAWN tokens: 2 HP, 0 ARM, basic attack 2 at range 1–2, STEP 2, no active ability (codex "Training Tokens").
        /// Conversion auxiliaries are 1-HP Cräigon bio-forms with the same profile.</summary>
        static void BuildTokens()
        {
            AddToken("token_craigon_sporewing", FactionId.Craigon, "Sporewing", BattleDomain.Orbital, 2, "Spore Bolts");
            AddToken("token_meeples_fighter", FactionId.Meeples, "Council Fighter", BattleDomain.Orbital, 2, "Fighter Lasers");
            AddToken("token_inhumanus_escort", FactionId.Inhumanus, "Mercenary Escort", BattleDomain.Orbital, 2, "Escort Guns");
            AddToken("token_inhumanus_fighter", FactionId.Inhumanus, "Autonomous Fighter", BattleDomain.Orbital, 2, "Drone Pulses");
            AddToken("token_pirates_skiff", FactionId.SpacePirates, "Boarding Skiff", BattleDomain.Orbital, 2, "Skiff Guns");
            AddToken("token_craigon_auxiliary_planetary", FactionId.Craigon, "Cräigon Auxiliary", BattleDomain.Planetary, 1, "Grafted Claws");
            AddToken("token_craigon_auxiliary_orbital", FactionId.Craigon, "Cräigon Auxiliary Hulk", BattleDomain.Orbital, 1, "Grafted Spines");
        }

        static void AddToken(string id, FactionId f, string name, BattleDomain domain, int hp, string weapon)
        {
            var profile = new RosterProfile
            {
                FactionKey = Factions.Values.First(x => x.Id == f).Key, Established = false, Domain = domain, Role = UnitRole.Token, Tier = 0,
                Pattern = MovePattern.Step, MoveHexes = 2, IsToken = true, ActiveName = "—", PassiveName = "—",
                ActiveText = "Tokens do not use active abilities.", PassiveText = "Despawns after battle; cannot be captured for economy; never a leader.",
                Tags = new[] { "TOKEN" },
            };
            var def = new UnitDef
            {
                Id = id, Faction = f, Name = name, Role = "Token", Roster = profile, MaxHp = hp, Armor = 0, Move = 2,
                Description = "Deployable token", Weapon = Weapon(f, weapon, 2, 1, 2, profile), Hover = domain == BattleDomain.Orbital,
            };
            def.Archetype = domain == BattleDomain.Orbital ? Archetype.Raider : Archetype.Infantry;
            def.ModelKey = id;
            Tokens.Add(id, def);
        }

        // ------------------------------------------------------------------ Inhumanus Corporate Charter

        /// <summary>The four Corporate Charter specialisations. Naval is the orbital charter.</summary>
        public static readonly string[] CharterNames = { "Assault", "Bulwark", "Commerce", "Naval" };

        /// <summary>Doctrine values reused for the charter: Assault = Assault, Bulwark = Defense, Commerce = Control,
        /// Naval = None on an orbital army (the only charter there).</summary>
        public static string CharterName(Doctrine d, BattleDomain domain) =>
            domain == BattleDomain.Orbital ? "Naval" : d switch { Doctrine.Assault => "Assault", Doctrine.Defense => "Bulwark", Doctrine.Control => "Commerce", _ => "—" };

        /// <summary>Planetary units that belong to a charter's specialisation (the codex: "the battle roster reflects that
        /// choice"). Proposal: at least half of an Inhumanus planetary army's non-leader units must come from it.</summary>
        public static bool InCharter(UnitDef u, Doctrine charter)
        {
            var p = u.Roster;
            switch (charter)
            {
                case Doctrine.Assault:
                    return p.Role == UnitRole.Line || p.Role == UnitRole.Saboteur || p.Role == UnitRole.Marksman || p.Role == UnitRole.Elite
                           || p.Role == UnitRole.VehicleAssault || p.Role == UnitRole.VehicleScout;
                case Doctrine.Defense:
                    return p.Role == UnitRole.Line || p.Role == UnitRole.Defender || p.Role == UnitRole.Support
                           || p.Role == UnitRole.VehicleSupport || p.Role == UnitRole.VehicleSiege;
                case Doctrine.Control:
                    return p.Role == UnitRole.Capture || p.Role == UnitRole.Support || p.Role == UnitRole.Scout || p.Role == UnitRole.Saboteur
                           || p.Role == UnitRole.VehicleAssault;
                default: return false;
            }
        }
    }
}
