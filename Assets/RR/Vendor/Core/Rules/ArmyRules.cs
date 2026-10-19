using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    public sealed class ArmyReport
    {
        public bool Valid => Errors.Count == 0;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public int TotalCost;
        public int UnitCount;
        /// <summary>Slice: ships under an Inhumanus fleet contract (SliceRules).</summary>
        public int Contracts;
        public int Budget;
        public int MaxUnits;
        public List<int> EntryCosts = new List<int>();
        /// <summary>Per-entry problems (index aligned with roster.Units) for inline UI explanations.</summary>
        public List<List<string>> EntryErrors = new List<List<string>>();
        public int Remaining => Budget - TotalCost;
    }

    public static class ArmyRules
    {
        /// <summary>Effective point cost of an equipment item for a faction (Inhumanus Proprietary Tech discount).</summary>
        public static int EquipmentCost(EquipmentDef e, FactionId faction)
        {
            if (e == null) return 0;
            if (faction == FactionId.Inhumanus && e.Tier >= 3) return Math.Max(1, e.Cost - 1);
            return e.Cost;
        }

        public static int EntryCost(RosterEntry entry, FactionId faction)
        {
            var def = GameContent.Unit(entry.UnitId);
            int cost = def?.Cost ?? 0;
            foreach (var id in entry.Equipment) cost += EquipmentCost(GameContent.Equip(id), faction);
            return cost;
        }

        /// <summary>Doctrine an Inhumanus army needs for a high-tier item, or None when unrestricted.</summary>
        public static Doctrine RequiredDoctrine(EquipmentDef e, FactionId faction)
        {
            if (faction != FactionId.Inhumanus || e.Tier < 4) return Doctrine.None;
            switch (e.Category)
            {
                case EquipmentCategory.Weapon: return Doctrine.Assault;
                case EquipmentCategory.Defense: return Doctrine.Defense;
                case EquipmentCategory.Utility: return Doctrine.Control;
                default: return Doctrine.None;
            }
        }

        /// <summary>Why a specific item cannot be mounted on a unit (null if it can). Used by the army builder.</summary>
        public static string WhyNotEquippable(UnitDef unit, EquipmentDef e, TechCaps caps, Doctrine doctrine)
        {
            if (e == null) return "Unknown equipment.";
            if (e.Faction != null && e.Faction != unit.Faction) return $"{e.Name} is {GameContent.Faction(e.Faction.Value).Name} technology.";
            if (!e.IsAllowedFor(unit.Archetype)) return $"{e.Name} cannot be mounted on a {unit.Archetype}.";
            if (!unit.HasSlot(e.Slot)) return $"A {unit.Archetype} has no {e.Slot} slot.";
            int cap = caps.CapFor(e.Category);
            if (e.Tier > cap) return $"Tier {Roman(e.Tier)} exceeds your {e.Category} technology cap (Tier {Roman(cap)}).";
            var need = RequiredDoctrine(e, unit.Faction);
            if (need != Doctrine.None && doctrine != need) return $"Requires the {need} doctrine.";
            return null;
        }

        public static ArmyReport Validate(ArmyRoster army, TechCaps caps, int budget = 18, int maxUnits = 7)
        {
            var r = new ArmyReport { Budget = budget, MaxUnits = maxUnits };
            if (army == null) { r.Errors.Add("No army selected."); return r; }
            r.UnitCount = army.Units.Count;
            int commanders = 0;
            if (army.Faction == FactionId.Inhumanus && army.Doctrine == Doctrine.None)
                r.Errors.Add("Inhumanus must choose a Corporate Doctrine (Assault, Defense or Control).");

            for (int i = 0; i < army.Units.Count; i++)
            {
                var entry = army.Units[i];
                var errs = new List<string>();
                var def = GameContent.Unit(entry.UnitId);
                if (def == null) { errs.Add($"Unknown unit '{entry.UnitId}'."); r.EntryErrors.Add(errs); r.EntryCosts.Add(0); r.Errors.AddRange(errs); continue; }
                if (def.Faction != army.Faction) errs.Add($"{def.Name} does not serve the {GameContent.Faction(army.Faction).Name}.");
                if (def.Archetype == Archetype.Commander) commanders++;
                if (def.Archetype == Archetype.Vehicle && caps.Vehicle < def.RequiredVehicleTier)
                    errs.Add($"{def.Name} needs Vehicle technology Tier {Roman(def.RequiredVehicleTier)} (cap is {Roman(caps.Vehicle)}).");

                var usedSlots = new HashSet<SlotType>();
                foreach (var eqId in entry.Equipment)
                {
                    var e = GameContent.Equip(eqId);
                    var why = WhyNotEquippable(def, e, caps, army.Doctrine);
                    if (why != null) { errs.Add($"{def.Name}: {why}"); continue; }
                    if (!usedSlots.Add(e.Slot)) errs.Add($"{def.Name}: only one {e.Slot} item allowed.");
                }
                int cost = EntryCost(entry, army.Faction);
                r.EntryCosts.Add(cost);
                r.TotalCost += cost;
                r.EntryErrors.Add(errs);
                r.Errors.AddRange(errs);
            }

            if (commanders == 0) r.Errors.Add("The army needs exactly one Commander.");
            else if (commanders > 1) r.Errors.Add($"Only one Commander allowed ({commanders} selected).");
            if (r.UnitCount > maxUnits) r.Errors.Add($"Too many units: {r.UnitCount}/{maxUnits}.");
            if (r.TotalCost > budget) r.Errors.Add($"Over budget: {r.TotalCost}/{budget} points.");
            if (r.Valid && r.TotalCost < budget) r.Warnings.Add($"{budget - r.TotalCost} point(s) unspent.");
            return r;
        }

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
    }

    /// <summary>Creates live unit instances from roster entries, computing their fixed derived stats.</summary>
    public static class UnitFactory
    {
        public static UnitState Create(int id, int owner, RosterEntry entry, Doctrine doctrine)
        {
            var def = GameContent.Unit(entry.UnitId) ?? throw new ArgumentException($"Unknown unit {entry.UnitId}");
            if (def.IsRoster) return RosterArmy.CreateUnit(id, owner, def.Id, new TechCaps(1));
            var u = new UnitState { Id = id, Owner = owner, DefId = def.Id, Equipment = new List<string>(entry.Equipment) };
            Recompute(u, doctrine);
            u.Hp = u.MaxHp;
            foreach (var g in def.Abilities) u.Abilities.Add(new AbilitySlot { Ability = g.Ability, Charges = g.Charges, ReadyAt = 0 });
            foreach (var e in Items(u))
            {
                if (e.GrantsAbility != AbilityId.None)
                {
                    int idx = u.AbilityIndex(e.GrantsAbility);
                    if (idx < 0) u.Abilities.Add(new AbilitySlot { Ability = e.GrantsAbility, Charges = e.GrantedCharges });
                    else { var s = u.Abilities[idx]; if (s.Charges >= 0) s.Charges += e.GrantedCharges; u.Abilities[idx] = s; }
                }
                if (e.BoostsAbility != AbilityId.None)
                {
                    int idx = u.AbilityIndex(e.BoostsAbility);
                    if (idx >= 0) { var s = u.Abilities[idx]; if (s.Charges >= 0) s.Charges += e.BoostCharges; u.Abilities[idx] = s; }
                }
                if (e.StartsWithWard) u.SetStatus(new StatusEffect(StatusType.Ward, int.MaxValue));
                if (e.Unbreakable) u.SetStatus(new StatusEffect(StatusType.Unbreakable, int.MaxValue));
            }
            // Inhumanus Control doctrine: +1 Lien charge.
            if (def.Faction == FactionId.Inhumanus && doctrine == Doctrine.Control)
            {
                int idx = u.AbilityIndex(AbilityId.Lien);
                if (idx >= 0) { var s = u.Abilities[idx]; if (s.Charges >= 0) s.Charges += 1; u.Abilities[idx] = s; }
            }
            // Every unit can Settle Debt if it is ever Liened (handled as an implicit ability by the rules engine).
            u.Abilities.Sort((a, b) => a.Ability.CompareTo(b.Ability));
            return u;
        }

        public static IEnumerable<EquipmentDef> Items(UnitState u) => u.Equipment.Select(GameContent.Equip).Where(e => e != null);

        public static void Recompute(UnitState u, Doctrine doctrine)
        {
            var def = u.Def;
            var items = Items(u).ToList();
            u.MaxHp = def.MaxHp + items.Sum(e => e.MaxHpBonus);
            if (def.Faction == FactionId.Inhumanus && doctrine == Doctrine.Defense && def.Archetype == Archetype.Commander) u.MaxHp += 1;
            u.BaseMove = Math.Max(1, def.Move + items.Sum(e => e.MoveBonus));
            u.BaseArmor = def.Armor + items.Sum(e => e.ArmorBonus);
            u.Hover = def.Hover || items.Any(e => e.Has(EquipmentFlags.Hover));
            u.Flags = items.Aggregate(EquipmentFlags.None, (f, e) => f | e.Flags);
            var weaponItem = items.FirstOrDefault(e => e.Weapon != null);
            var w = (weaponItem?.Weapon ?? def.Weapon).Clone();
            int dmgBonus = items.Sum(e => e.DamageBonus);
            w.Damage += dmgBonus;
            if (w.Kind == WeaponKind.Ranged) w.MaxRange += items.Sum(e => e.RangeBonus);
            u.Weapon = w;
        }
    }
}
