using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Full Roster army building: validation, unit creation with research upgrades, and preset armies.
    /// Army rules (balance proposals, not codex canon; see Docs/UNIT_CODEX_IMPLEMENTATION.md): exactly one leader
    /// (Commander on the ground, Flagship in orbit); units up to the army's unlock tier (the leader is exempt); cost =
    /// tier + 1; 30 points and 8 units by default.
    /// </summary>
    public static class RosterArmy
    {
        // ------------------------------------------------------------------ research (codex "Equipment Upgrades")

        /// <summary>Weapon research: tier 3 adds +1 base damage, tier 5 a further +1.</summary>
        public static int WeaponBonus(int weaponTier) => (weaponTier >= 3 ? 1 : 0) + (weaponTier >= 5 ? 1 : 0);
        /// <summary>Armor research: tiers 2, 3, 4 and 5 each add +1 printed HP.</summary>
        public static int HpBonus(int armorTier) => Math.Max(0, Math.Min(5, armorTier) - 1);
        /// <summary>Armor research: tier 3 adds +1 ARM, tier 5 another +1 (cap 4).</summary>
        public static int ArmorBonus(int armorTier) => (armorTier >= 3 ? 1 : 0) + (armorTier >= 5 ? 1 : 0);

        public static UnitState CreateUnit(int id, int owner, string defId, TechCaps caps)
        {
            var def = RosterContent.Unit(defId) ?? throw new ArgumentException($"Unknown roster unit {defId}");
            var u = new UnitState { Id = id, Owner = owner, DefId = def.Id };
            Recompute(u, caps);
            u.Hp = u.MaxHp;
            return u;
        }

        /// <summary>Derived stats: printed values plus research (tokens never get research).</summary>
        public static void Recompute(UnitState u, TechCaps caps)
        {
            var def = u.Def;
            bool research = caps != null && !def.Roster.IsToken;
            u.MaxHp = def.MaxHp + (research ? HpBonus(caps.Defense) : 0);
            u.BaseArmor = Math.Min(RosterCombat.ArmorCap, def.Armor + (research ? ArmorBonus(caps.Defense) : 0));
            u.BaseMove = def.Move;
            u.Hover = def.Hover;
            u.Flags = EquipmentFlags.None;
            var w = def.Weapon.Clone();
            if (research) w.Damage += WeaponBonus(caps.Weapon);
            u.Weapon = w;
            SliceRules.ApplyModules(u);   // slice: fitted capital-ship modules
        }

        /// <summary>Tech for a Full Roster army: Balanced = printed codex stats (unlock tier III, no research upgrades);
        /// Advanced = everything unlocked and fully researched (tier V).</summary>
        public static TechCaps CapsFor(TechMode mode) =>
            mode == TechMode.Advanced ? new TechCaps(5) : new TechCaps(1) { Unlock = 3 };

        // ------------------------------------------------------------------ ruleset-aware helpers (UI, network)

        /// <summary>Tech caps for a match's technology mode under its ruleset.</summary>
        public static TechCaps CapsFor(MatchConfig c) => c.IsRoster ? CapsFor(c.TechMode == TechMode.Advanced ? TechMode.Advanced : TechMode.Balanced) : TechCaps.ForMode(c.TechMode);

        /// <summary>Validates an army under the match's ruleset.</summary>
        public static ArmyReport ValidateFor(MatchConfig c, ArmyRoster army, TechCaps caps) =>
            c.IsRoster ? Validate(army, caps, c.Domain, c.PointBudget, c.MaxUnits) : ArmyRules.Validate(army, caps, c.PointBudget, c.MaxUnits);

        /// <summary>Switches a config between Classic (0), Full Roster planetary (1) and orbital (2) with the default limits.</summary>
        public static void SetRuleset(MatchConfig c, int v)
        {
            if (v == 0)
            {
                c.Ruleset = Ruleset.Classic; c.Domain = BattleDomain.Planetary; c.PointBudget = 18; c.MaxUnits = 7;
                if (MapLibrary.Get(c.MapId).Domain != BattleDomain.Planetary) c.MapId = MapLibrary.DefaultMapId;
            }
            else c.UseRoster(v == 2 ? BattleDomain.Orbital : BattleDomain.Planetary);
        }

        public static int RulesetIndex(MatchConfig c) => !c.IsRoster ? 0 : c.Domain == BattleDomain.Orbital ? 2 : 1;
        public static string RulesetName(MatchConfig c) => RulesetIndex(c) switch { 0 => "Classic", 1 => "Full Roster — planetary", _ => "Full Roster — orbital" };

        // ------------------------------------------------------------------ validation

        public static ArmyReport Validate(ArmyRoster army, TechCaps caps, BattleDomain domain, int budget = MatchConfig.RosterBudget, int maxUnits = MatchConfig.RosterMaxUnits)
        {
            var r = new ArmyReport { Budget = budget, MaxUnits = maxUnits };
            if (army == null) { r.Errors.Add("No army selected."); return r; }
            r.UnitCount = army.Units.Count;
            int leaders = 0, charterUnits = 0, nonLeaders = 0;
            for (int i = 0; i < army.Units.Count; i++)
            {
                var entry = army.Units[i];
                var errs = new List<string>();
                var def = RosterContent.Unit(entry.UnitId);
                if (def == null || def.Roster.IsToken) { errs.Add($"Unknown unit '{entry.UnitId}'."); r.EntryErrors.Add(errs); r.EntryCosts.Add(0); r.Errors.AddRange(errs); continue; }
                if (def.Faction != army.Faction) errs.Add($"{def.Name} does not serve the {GameContent.Faction(army.Faction).Name}.");
                if (def.Roster.Domain != domain) errs.Add($"{def.Name} is a{(def.Roster.IsShip ? "n orbital" : " planetary")} unit; this is a{(domain == BattleDomain.Orbital ? "n orbital" : " planetary")} battle.");
                if (!def.Roster.Established) errs.Add($"{def.Name} is a proposed unit and is disabled.");
                if (def.Roster.IsLeaderRole) leaders++;
                else
                {
                    nonLeaders++;
                    if (def.Roster.Tier > caps.Unlock) errs.Add($"{def.Name} unlocks at tier {ArmyRules.Roman(def.Roster.Tier)} (your unlock tier is {ArmyRules.Roman(caps.Unlock)}).");
                    if (army.Faction == FactionId.Inhumanus && domain == BattleDomain.Planetary && RosterContent.InCharter(def, army.Doctrine)) charterUnits++;
                }
                var contract = SliceRules.ValidateContract(army, entry, domain);
                if (contract != null) errs.Add(contract);
                if (SliceRules.ContractLane(entry) != null) r.Contracts++;
                int cost = SliceRules.EntryCost(entry, RosterContent.Cost(def));
                r.EntryCosts.Add(cost);
                r.TotalCost += cost;
                r.EntryErrors.Add(errs);
                r.Errors.AddRange(errs);
            }
            string leaderName = domain == BattleDomain.Orbital ? "Flagship" : "Commander";
            if (leaders == 0) r.Errors.Add($"The army needs exactly one {leaderName} (the leader whose loss ends the battle).");
            else if (leaders > 1) r.Errors.Add($"Only one {leaderName} allowed ({leaders} selected).");
            if (army.Faction == FactionId.Inhumanus && domain == BattleDomain.Planetary)
            {
                if (army.Doctrine == Doctrine.None) r.Errors.Add("Inhumanus must lock a Corporate Charter (Assault, Bulwark or Commerce) before deployment.");
                else if (charterUnits * 2 < nonLeaders)
                    r.Errors.Add($"Corporate Charter ({RosterContent.CharterName(army.Doctrine, domain)}): at least half of the non-leader units must be charter specialists ({charterUnits}/{nonLeaders}).");
            }
            if (r.Contracts > SliceRules.MaxContracts) r.Errors.Add($"At most {SliceRules.MaxContracts} ships may be contracted ({r.Contracts} are).");
            if (r.UnitCount > maxUnits) r.Errors.Add($"Too many units: {r.UnitCount}/{maxUnits}.");
            if (r.TotalCost > budget) r.Errors.Add($"Over budget: {r.TotalCost}/{budget} points.");
            if (r.Valid && r.TotalCost < budget && r.UnitCount < maxUnits) r.Warnings.Add($"{budget - r.TotalCost} point(s) unspent.");
            return r;
        }

        // ------------------------------------------------------------------ presets

        static readonly UnitRole[] PlanetaryPriority =
        {
            UnitRole.Line, UnitRole.Defender, UnitRole.Marksman, UnitRole.Heavy, UnitRole.Assault, UnitRole.Support, UnitRole.Scout,
            UnitRole.Engineer, UnitRole.Saboteur, UnitRole.Capture, UnitRole.VehicleAssault, UnitRole.VehicleSupport, UnitRole.Elite,
            UnitRole.VehicleScout, UnitRole.VehicleSiege,
        };

        static readonly UnitRole[] OrbitalPriority =
        {
            UnitRole.Corvette, UnitRole.Frigate, UnitRole.Destroyer, UnitRole.Fighter, UnitRole.Interceptor, UnitRole.Cruiser,
            UnitRole.SpaceScout, UnitRole.Carrier, UnitRole.SpaceSupport, UnitRole.Battleship, UnitRole.SpaceBastion, UnitRole.Ark, UnitRole.Dreadnought,
        };

        /// <summary>A legal default army: the strongest leader, then one unit per role in priority order (second copies
        /// of line units if points remain), within the unlock tier, budget and unit cap.</summary>
        public static ArmyRoster Preset(FactionId f, BattleDomain domain, int unlock = 3, int budget = MatchConfig.RosterBudget, int maxUnits = MatchConfig.RosterMaxUnits)
        {
            var army = new ArmyRoster { Faction = f, Name = domain == BattleDomain.Orbital ? "Codex Battle Fleet" : "Codex Battle Group" };
            if (f == FactionId.Inhumanus && domain == BattleDomain.Planetary) army.Doctrine = Doctrine.Assault;
            var all = RosterContent.For(f, domain).ToList();
            var leader = all.Where(u => u.Roster.IsLeaderRole).OrderByDescending(u => u.Roster.Tier).ThenBy(u => u.Roster.CatalogIndex).First();
            army.Units.Add(new RosterEntry(leader.Id));
            int points = budget - RosterContent.Cost(leader);
            var priority = domain == BattleDomain.Orbital ? OrbitalPriority : PlanetaryPriority;
            var pool = all.Where(u => !u.Roster.IsLeaderRole && u.Roster.Tier <= unlock).ToList();
            bool charter = army.Doctrine != Doctrine.None;
            var picked = new List<UnitDef>();
            for (int pass = 0; pass < 2 && army.Units.Count < maxUnits; pass++)
            {
                foreach (var role in priority)
                {
                    if (army.Units.Count >= maxUnits) break;
                    foreach (var u in pool.Where(x => x.Roster.Role == role).OrderBy(x => x.Roster.Tier).ThenBy(x => x.Roster.CatalogIndex))
                    {
                        if (army.Units.Count >= maxUnits) break;
                        if (pass == 0 && picked.Contains(u)) continue;
                        if (pass == 1 && role != priority[0] && role != priority[1]) continue;
                        if (charter && !RosterContent.InCharter(u, army.Doctrine) && picked.Count(p => !RosterContent.InCharter(p, army.Doctrine)) * 2 + 2 > maxUnits - 1) continue;
                        int c = RosterContent.Cost(u);
                        if (c > points) continue;
                        // Keep enough points to fill the remaining slots with the cheapest unit.
                        int slotsAfter = maxUnits - army.Units.Count - 1;
                        if (pass == 0 && points - c < Math.Min(slotsAfter, 2) * 2) continue;
                        army.Units.Add(new RosterEntry(u.Id));
                        picked.Add(u);
                        points -= c;
                        break;
                    }
                }
            }
            return army;
        }

        public static List<ArmyRoster> Presets(FactionId f, BattleDomain domain, int unlock = 3) => new List<ArmyRoster> { Preset(f, domain, unlock) };
    }

    /// <summary>Objective hexes of a battlefield under Full Roster rules: the centre (an orbital gate in space) and one
    /// point on each wing of the centre row, chosen deterministically and point-symmetric.</summary>
    public static class RosterMaps
    {
        public static List<Hex> Objectives(MapDefinition map)
        {
            var list = new List<Hex>();
            bool Ok(Hex h) => map.Contains(h) && !TerrainRules.BlocksMovement(map.Terrain(h)) && map.Terrain(h) != TerrainType.Hazard;
            // centre: (0,0) or the nearest passable self-symmetric pair is impossible, so fall back to the nearest passable hex
            // and its rotation as two objectives.
            if (Ok(Hex.Zero)) list.Add(Hex.Zero);
            else
            {
                var c = Hex.Spiral(Hex.Zero, 2).Where(h => h != Hex.Zero && Ok(h)).OrderBy(h => h.Length).ThenBy(h => h).First();
                list.Add(c); list.Add(c.Rotate180());
            }
            int wing = Math.Max(2, map.HalfWidth - 3);
            var w = Enumerable.Range(0, wing + 1).Select(k => new Hex(wing - k, 0)).Concat(Enumerable.Range(1, 3).Select(k => new Hex(wing + k, 0)))
                              .FirstOrDefault(h => h.Q > 0 && Ok(h) && Ok(h.Rotate180()) && !list.Contains(h));
            if (w != default) { list.Add(w); list.Add(w.Rotate180()); }
            return list.OrderBy(h => h).ToList();
        }
    }
}
