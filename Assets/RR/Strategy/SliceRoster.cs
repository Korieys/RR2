using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>What a player needs to know about one slice class, from the rules data plus an authored counter.</summary>
    public sealed class ClassProfile
    {
        public string Id, Name, Role, Movement, Attack, Defences, Signature, SignatureText, Passive, Counter;
        public int Cost;
        public bool Leader;
        /// <summary>Codex 0.1 values: every number is a draft, and slice doctrine numbers are experimental.</summary>
        public bool Experimental = true;
    }

    public sealed class Composition
    {
        public string Name;
        public ArmyRoster Army;
        public string Tradeoff;
    }

    /// <summary>
    /// The focused two-faction orbital roster for the vertical slice (Prompt 4). Classes are existing codex units, by their
    /// established ids and names; nothing here renames or rebalances the wider roster. Selection rules: one flagship, plus
    /// protection or support, manoeuvring strike craft and heavy firepower, all legal under the campaign's Balanced unlock
    /// (tier III for non-leaders). Doctrines are in <see cref="SliceRules"/>.
    /// </summary>
    public static class SliceRoster
    {
        static readonly Dictionary<FactionId, string[]> ClassIds = new Dictionary<FactionId, string[]>
        {
            [FactionId.Pygmaei] = new[] { "pygmaei_revenant_command_ship", "pygmaei_furnace_cruiser", "pygmaei_annihilator_destroyer", "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor" },
            [FactionId.Inhumanus] = new[] { "inhumanus_executive_flagship", "inhumanus_parallax_cruiser", "inhumanus_arbitration_destroyer", "inhumanus_proxy_frigate", "inhumanus_mercator_corvette" },
        };

        static readonly Dictionary<string, (string role, string counter)> Authored = new Dictionary<string, (string, string)>
        {
            ["pygmaei_revenant_command_ship"] = ("Flagship and leader: the battle is lost the moment it is destroyed.",
                "Slow (two hexes in a straight line), blind astern, and Total Offensive leaves its targets' allies EXPOSED next activation. Flank it and strike after it commits."),
            ["pygmaei_furnace_cruiser"] = ("Support: Heat Flood slows the enemy's next move so the line can catch it.",
                "Armour 1 and a slow zigzag move; destroyers at range 4 outreach its broadside."),
            ["pygmaei_annihilator_destroyer"] = ("Heavy firepower: 5 at range 2 to 4, Saturation Strike for clustered ships.",
                "Armour 0 and a minimum range of 2: a corvette that closes to one hex cannot be shot by its main array."),
            ["pygmaei_skirmish_frigate"] = ("Manoeuvring strike craft: Flanking Run and Wolfpack reward attacking from two sides.",
                "Nine hull and no armour: it must keep to range 2 to 3 and dies to focused fire."),
            ["pygmaei_rift_interceptor"] = ("Screen: Snap Intercept protects one ship from the next small-craft attack.",
                "It stops only fighter and interceptor attacks; frigates, destroyers and abilities go straight through. Five hull."),
            ["inhumanus_executive_flagship"] = ("Flagship and leader: the battle is lost the moment it is destroyed.",
                "Moves one hex in a straight line and is blind astern: once flanked it cannot turn and fire in the same activation."),
            ["inhumanus_parallax_cruiser"] = ("Heavy hull and repositioning: armour 2, Gravity Fold moves itself or a corvette out of a bad line.",
                "Range 2 to 4 only, and Gravity Fold needs line of sight to its destination."),
            ["inhumanus_arbitration_destroyer"] = ("Control: Injunction shuts down an enemy active ability for its next activation.",
                "Injunction does not stop basic guns, and the destroyer moves only two hexes."),
            ["inhumanus_proxy_frigate"] = ("Protection: Subcontract places a mercenary escort token that can body-block a firing line.",
                "The escort token has 2 hull; the frigate cannot shoot adjacent targets."),
            ["inhumanus_mercator_corvette"] = ("Manoeuvring strike craft: Arbitrage Run dashes three hexes and sharpens the next shot.",
                "Seven hull; Market Flex only helps after it has repositioned this round."),
        };

        public static IEnumerable<string> Classes(FactionId f) => ClassIds.TryGetValue(f, out var ids) ? ids : Array.Empty<string>();
        public static IEnumerable<FactionId> Factions => ClassIds.Keys;

        static ArmyRoster Army(string name, FactionId f, params string[] entries)
        {
            var a = new ArmyRoster { Name = name, Faction = f };
            foreach (var e in entries)
            {
                var parts = e.Split('@');
                var re = new RosterEntry(parts[0]);
                if (parts.Length > 1) re.Equipment.Add(SliceRules.ContractToken + parts[1]);
                a.Units.Add(re);
            }
            return a;
        }

        public static IEnumerable<Composition> Compositions(FactionId f)
        {
            if (f == FactionId.Pygmaei)
            {
                yield return new Composition
                {
                    Name = "Furnace Line",
                    Army = Army("Furnace Line", f, "pygmaei_revenant_command_ship", "pygmaei_annihilator_destroyer", "pygmaei_annihilator_destroyer",
                                "pygmaei_furnace_cruiser", "pygmaei_skirmish_frigate", "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor", "pygmaei_rift_interceptor"),
                    Tradeoff = "Two Annihilators and a Furnace Cruiser hit hard at range 4 and slow the enemy, so the line wins a straight fight for the passage. " +
                               "It overheats if it overclocks every turn, and it has only two fast hulls to reach a flagship that hides.",
                };
                yield return new Composition
                {
                    Name = "Wolfpack Raid",
                    Army = Army("Wolfpack Raid", f, "pygmaei_revenant_command_ship", "pygmaei_skirmish_frigate", "pygmaei_skirmish_frigate", "pygmaei_skirmish_frigate",
                                "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor", "pygmaei_rift_interceptor", "pygmaei_annihilator_destroyer"),
                    Tradeoff = "Four frigates threaten a flagship from several sides at once and overclock around the wreck banks. " +
                               "With little armour and one heavy gun, they lose a slugging match if the enemy screens its flagship and holds the passage.",
                };
            }
            else if (f == FactionId.Inhumanus)
            {
                yield return new Composition
                {
                    Name = "Bulwark Charter",
                    Army = Army("Bulwark Charter", f, "inhumanus_executive_flagship", "inhumanus_parallax_cruiser", "inhumanus_arbitration_destroyer",
                                "inhumanus_proxy_frigate", "inhumanus_proxy_frigate", "inhumanus_mercator_corvette", "inhumanus_mercator_corvette"),
                    Tradeoff = "Everything deploys at once: two Proxy Frigates can place escort tokens around the flagship from the first round. " +
                               "It pays full price, so it fields fewer heavy hulls.",
                };
                yield return new Composition
                {
                    Name = "Escort Contract",
                    Army = Army("Escort Contract", f, "inhumanus_executive_flagship", "inhumanus_parallax_cruiser", "inhumanus_parallax_cruiser",
                                "inhumanus_arbitration_destroyer", "inhumanus_mercator_corvette", "inhumanus_proxy_frigate@west", "inhumanus_mercator_corvette@east"),
                    Tradeoff = "Two contracted escorts cost a point less each, which buys a second Parallax Cruiser. They arrive only at the start of round 3, " +
                               "on back-row hexes both sides can see, so the flagship is thinly screened early and a blockade of the entry hexes delays them.",
                };
            }
        }

        public static string MovementNote(string id)
        {
            var p = RosterContent.Unit(id).Roster;
            string n = p.Pattern switch
            {
                MovePattern.Ray => $"up to {p.MoveHexes} hexes in a straight line",
                MovePattern.Zigzag => $"up to {p.MoveHexes} hexes with one 60° turn",
                MovePattern.Leap => $"up to {p.MoveHexes} hexes, jumping over ships",
                MovePattern.Phase => $"up to {p.MoveHexes} hexes, passing through ships",
                MovePattern.Diag => $"up to {p.MoveHexes} diagonal hops; diagonal hops can only ever land on one hex in three",
                MovePattern.Knight => "a fixed jump of three",
                _ => $"up to {p.MoveHexes} hexes",
            };
            return n;
        }

        public static ClassProfile Profile(string id)
        {
            var d = RosterContent.Unit(id);
            var p = d.Roster;
            Authored.TryGetValue(id, out var a);
            return new ClassProfile
            {
                Id = id, Name = d.Name, Leader = p.IsLeaderRole, Cost = RosterContent.Cost(d), Role = a.role ?? p.Role.ToString(),
                Movement = MovementNote(id) + (SliceRules.HasArcRole(p.Role) ? "; main battery blind in the 120° astern" : ""),
                Attack = $"{d.Weapon.Name}: {d.Weapon.Damage} at range {d.Weapon.MinRange} to {d.Weapon.MaxRange}",
                Defences = $"{d.MaxHp} hull, armour {d.Armor}",
                Signature = p.ActiveName, SignatureText = p.Active?.Engine ?? p.ActiveText,
                Passive = p.PassiveName + ": " + (p.Passive?.Engine ?? p.PassiveText),
                Counter = a.counter ?? "",
            };
        }

        public static string DoctrineCard(FactionId f) => f == FactionId.Pygmaei
            ? $"Industrial Overclock (experimental). Any Pygmaei ship may overclock its main move (+{SliceRules.OverclockMove} hexes) or its basic attack " +
              $"(+{SliceRules.OverclockDamage} damage); each costs {SliceRules.OverclockHeat} heat, shown on the ship. At the end of an activation in which it did not " +
              $"overclock, a ship sheds {SliceRules.CoolPerActivation} heat. At {SliceRules.OverheatAt} heat it is OVERHEATED: armour −{SliceRules.OverheatArmor} and no " +
              "overclocking until it cools below the threshold. There are no random malfunctions."
            : f == FactionId.Inhumanus
            ? $"Fleet Contracts (experimental; the orbital form of the Corporate Charter). Before the battle, up to {SliceRules.MaxContracts} escorts may be contracted: " +
              $"each costs {SliceRules.ContractDiscount} point less and arrives at the start of round {SliceRules.ContractArrivalRound} on a back-row hex chosen in advance " +
              "(west, centre or east) and shown to both sides. If that hex is occupied, the escort waits for the next round. Contracted ships still count toward the " +
              "eight-ship limit and the 30-point budget."
            : "";

        /// <summary>Load-time content check: every slice class exists, is orbital, belongs to its faction, is established,
        /// has executable specs, and every composition is legal. Empty when consistent.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var kv in ClassIds)
            {
                foreach (var id in kv.Value)
                {
                    var d = RosterContent.Unit(id);
                    if (d == null) { problems.Add($"{id}: unknown unit id"); continue; }
                    if (d.Faction != kv.Key) problems.Add($"{id}: belongs to {d.Faction}, not {kv.Key}");
                    if (!d.Roster.IsShip) problems.Add($"{id}: not an orbital unit");
                    if (!d.Roster.Established) problems.Add($"{id}: not an established codex unit");
                    if (d.Roster.Active == null || d.Roster.Passive == null) problems.Add($"{id}: missing executable ability specs");
                    if (!Authored.ContainsKey(id)) problems.Add($"{id}: no authored role and counter");
                    problems.AddRange(RosterSpecs.Problems.Where(x => x.StartsWith(id + ":", StringComparison.Ordinal)));
                }
                if (kv.Value.Count(id => RosterContent.Unit(id)?.Roster.IsLeaderRole == true) != 1) problems.Add($"{kv.Key}: the slice roster needs exactly one flagship class");
                foreach (var c in Compositions(kv.Key))
                {
                    var rep = RosterArmy.Validate(c.Army, BattleBridge.Caps, BattleDomain.Orbital);
                    if (!rep.Valid) problems.Add($"{c.Name}: {string.Join("; ", rep.Errors)}");
                }
            }
            if (!MapLibrary.Maps.TryGetValue("trade_passage", out var m) || m.Topology == null || !m.Topology.Passed) problems.Add("trade_passage: the battlefield is missing or fails validation");
            return problems;
        }
    }
}
