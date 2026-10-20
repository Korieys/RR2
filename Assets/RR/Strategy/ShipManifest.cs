using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>
    /// A read-only reference manifest of the slice's ships, built from the live registry (RosterContent, SliceRoster,
    /// SliceContent) so it cannot drift from the game (campaign alpha Prompt 19). It supports discussion of a token-based
    /// physical test with the co-creator; it is not a miniature specification or a finished tabletop rule set, and every
    /// number in it is a provisional (codex 0.1 draft or slice experimental) value.
    /// </summary>
    public static class ShipManifest
    {
        public sealed class Row
        {
            public string Id, Faction, Class, Role, Silhouette, Dimensions, Configuration, Movement, Attack, Defences, Leadership, Signature;
            public int Tier, Cost, Hull, Armor;
        }

        /// <param name="assetExists">Checks a project-relative path exists (the tools pass File.Exists; null skips the check).</param>
        public static List<Row> Build(Func<string, bool> assetExists = null)
        {
            var rows = new List<Row>();
            foreach (var f in new[] { FactionId.Pygmaei, FactionId.Inhumanus })
                foreach (var id in SliceRoster.Classes(f))
                {
                    var d = RosterContent.Unit(id);
                    var p = d.Roster;
                    var model = $"Assets/RR/Art/Models/{id}.fbx";
                    bool leader = p.IsLeaderRole;
                    var config = new List<string>();
                    if (leader) config.Add("modules, one per slot: " + string.Join(", ", SliceContent.Modules.Select(m => $"{m.Id} ({m.Slot})")));
                    if (f == FactionId.Pygmaei) config.Add("industrial overclock (heat); doctrine variants: coolant discipline or furnace doctrine");
                    if (f == FactionId.Inhumanus && !leader) config.Add("may be contracted (arrives in round 3; doctrine variants: round 4 cheaper or round 2 full price)");
                    rows.Add(new Row
                    {
                        Id = id, Faction = f.ToString(), Class = d.Name, Role = p.Role.ToString(), Tier = p.Tier, Cost = RosterContent.Cost(d), Hull = d.MaxHp, Armor = d.Armor,
                        Silhouette = assetExists == null ? model : assetExists(model) ? model : "none (no model)",
                        Dimensions = "not measured for play: one hex per ship; relative silhouettes in Docs/vertical-slice/DESIGN_REFERENCE.md",
                        Configuration = config.Count == 0 ? "fixed" : string.Join("; ", config),
                        Movement = $"{p.Pattern} {p.MoveHexes} hexes" + (SliceRules.HasArcRole(p.Role) ? "; capital arc: the main battery is blind in the 120° astern, a turn costs the move" : ""),
                        Attack = $"{d.Weapon.Name}: {d.Weapon.Damage} damage, range {d.Weapon.MinRange}-{d.Weapon.MaxRange}" + (p.AttackNeedsLos ? ", needs line of sight" : ""),
                        Defences = $"hull {d.MaxHp}, armour {d.Armor}",
                        Leadership = leader ? "flagship: its legal destruction or capture ends a battle at once; no proximity shield" : "may be named acting flagship by succession between operation engagements",
                        Signature = p.ActiveName != null ? $"{p.ActiveName}: {p.ActiveText}" : "",
                    });
                }
            return rows;
        }

        public static string RulesStamp => $"{RulesVersion.Id}; content {RulesVersion.ContentFingerprint}; modules {SliceContent.Version}";

        public static string ToMarkdown(List<Row> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Ship reference manifest (read-only, generated)");
            sb.AppendLine();
            sb.AppendLine($"Rules: `{RulesStamp}`. Generated from the live registry by `Tools/SliceTests` (`manifest` mode); do not edit by hand.");
            sb.AppendLine("Every value is provisional (codex 0.1 draft or slice experimental). This supports discussing a token-based physical test; it is not a miniature specification or a finished tabletop rule set.");
            sb.AppendLine();
            sb.AppendLine("| Id | Faction | Class | Role | Tier | Points | Hull | Armour | Movement | Attack | Leadership | Configuration | Silhouette |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| `{r.Id}` | {r.Faction} | {r.Class} | {r.Role} | {r.Tier} | {r.Cost} | {r.Hull} | {r.Armor} | {r.Movement} | {r.Attack} | {r.Leadership} | {r.Configuration} | `{r.Silhouette}` |");
            sb.AppendLine();
            sb.AppendLine("Dimensions: " + (rows.FirstOrDefault()?.Dimensions ?? "") + ".");
            return sb.ToString();
        }

        public static string ToJson(List<Row> rows) => Json.Write(new Dictionary<string, object>
        {
            ["schema"] = "rr.ship.manifest", ["rules"] = RulesStamp, ["provisional"] = true,
            ["ships"] = rows.Select(r => (object)new Dictionary<string, object>
            {
                ["id"] = r.Id, ["faction"] = r.Faction, ["class"] = r.Class, ["role"] = r.Role, ["tier"] = r.Tier, ["points"] = r.Cost, ["hull"] = r.Hull, ["armor"] = r.Armor,
                ["movement"] = r.Movement, ["attack"] = r.Attack, ["defences"] = r.Defences, ["leadership"] = r.Leadership, ["configuration"] = r.Configuration,
                ["silhouette"] = r.Silhouette, ["dimensions"] = r.Dimensions, ["signature"] = r.Signature,
            }).ToList(),
        });
    }

    /// <summary>Runtime checks on the generated content against the live roster (the same rules as the generator).</summary>
    public static class SliceContentCheck
    {
        static readonly string[] Slots = { "propulsion", "protection", "firepower" };
        static readonly string[] Visuals = { "afterburners", "ablative_plating", "heavy_battery" };

        public static List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (var m in SliceContent.Modules)
            {
                if (!ids.Add(m.Id)) errors.Add($"module {m.Id}: duplicate id");
                if (!Slots.Contains(m.Slot)) errors.Add($"module {m.Id}: unknown slot {m.Slot}");
                if (!Visuals.Contains(m.Visual)) errors.Add($"module {m.Id}: unknown visual {m.Visual}");
                var deltas = new[] { m.Move, m.Armor, m.Hull, m.Damage, m.Range };
                if (!deltas.Any(x => x > 0) || !deltas.Any(x => x < 0)) errors.Add($"module {m.Id}: not a sidegrade (needs a gain and a cost)");
                if (deltas.Any(x => Math.Abs(x) > 3)) errors.Add($"module {m.Id}: a change larger than 3");
                if (m.CostEnergy + m.CostMaterials + m.CostCredits <= 0) errors.Add($"module {m.Id}: no cost");
            }
            foreach (var l in SliceContent.Loadouts)
            {
                var army = new ArmyRoster { Faction = l.Faction, Name = l.Name };
                foreach (var u in l.Units) army.Units.Add(new RosterEntry(u));
                var rep = RosterArmy.Validate(army, BattleBridge.Caps, BattleDomain.Orbital);
                if (!rep.Valid) errors.Add($"loadout {l.Id}: {rep.Errors.First()}");
            }
            return errors;
        }
    }
}
