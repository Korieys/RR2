using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    public sealed class AbilityTarget
    {
        public Hex Hex;
        public bool Valid;
        public string Reason;
        public int Option;
    }

    /// <summary>Enumerates legal commands. Used by the AI, the balance harness and the UI target highlighting.</summary>
    public static class LegalActions
    {
        /// <summary>Candidate hexes the UI should evaluate for an ability (valid ones are highlighted, invalid ones explain why on hover).</summary>
        public static List<AbilityTarget> AbilityTargets(MatchState s, UnitState u, AbilityId id, int option = 0)
        {
            var result = new List<AbilityTarget>();
            if (s.IsRoster)
            {
                if (!u.OnBoard) return result;
                if (id == AbilityId.Rescue)
                {
                    foreach (var h in u.Position.Neighbors())
                    {
                        if (!s.Map.Contains(h)) continue;
                        var v = RosterRules.ValidateRescue(s, u, h);
                        result.Add(new AbilityTarget { Hex = h, Valid = v.Ok, Reason = v.Reason });
                    }
                    return result;
                }
                return RosterAbilities.Candidates(s, u, new List<Hex>(), option);
            }
            var def = GameContent.Ability(id);
            if (def == null || !u.OnBoard) return result;
            IEnumerable<Hex> candidates;
            int range = RulesEngine.AbilityRange(s, u, id);
            switch (def.Targeting)
            {
                case AbilityTargeting.Self: candidates = new[] { u.Position }; break;
                case AbilityTargeting.WreckHex: candidates = s.Wrecks.Select(w => w.Hex).Where(h => Hex.Distance(h, u.Position) <= 1); break;
                case AbilityTargeting.MoveThenAttack:
                    candidates = new[] { u.Position }.Concat(u.Position.Neighbors().Where(h => BoardQueries.IsPassableTerrain(s, h) && s.UnitAt(h) == null));
                    break;
                default: candidates = s.Map.AllHexes.Where(h => Hex.Distance(h, u.Position) <= Math.Max(1, range) + 1); break;
            }
            foreach (var h in candidates)
            {
                if (def.Targeting == AbilityTargeting.MoveThenAttack)
                {
                    // A move destination is valid if at least one attack is possible from it.
                    bool any = Combat.LegalTargets(s, u, h).Count > 0 && RulesEngine.AbilityAvailable(s, u, id).Ok;
                    result.Add(new AbilityTarget { Hex = h, Valid = any, Reason = any ? null : "No target in range from there." });
                    continue;
                }
                var v = RulesEngine.ValidateAbility(s, u, id, h, option, default);
                result.Add(new AbilityTarget { Hex = h, Valid = v.Ok, Reason = v.Reason, Option = option });
            }
            return result;
        }

        /// <summary>All legal activations for the active seat (always includes Pass).</summary>
        public static List<Command> Enumerate(MatchState s, bool includeLowValue = true)
        {
            var list = new List<Command>();
            if (s.Phase != MatchPhase.Battle) return list;
            if (s.IsRoster) return EnumerateRoster(s);
            int seat = s.ActiveSeat;
            list.Add(Command.Pass(seat));
            foreach (var u in s.Units.ToList())
            {
                if (u.Owner != seat || !RulesEngine.CanActivate(s, u, out _)) continue;
                foreach (var kv in BoardQueries.Reachable(s, u).OrderBy(k => k.Key))
                    list.Add(Command.Move(seat, u.Id, kv.Key));
                foreach (var o in Combat.LegalTargets(s, u))
                    list.Add(Command.Attack(seat, u.Id, o.TargetHex));
                if (RulesEngine.CanGuard(s, u).Ok) list.Add(Command.Guard(seat, u.Id));
                foreach (var ab in RulesEngine.AbilitiesOf(u))
                {
                    if (!RulesEngine.AbilityAvailable(s, u, ab).Ok) continue;
                    if (ab == AbilityId.Smoke && !includeLowValue) continue;
                    AddAbilityCommands(s, u, ab, list);
                }
            }
            return list;
        }

        /// <summary>Full Roster: every legal command for the active seat. Mid-activation, only the active unit's options.</summary>
        public static List<Command> EnumerateRoster(MatchState s, int abilityCap = 48, int abilityWidth = 12)
        {
            var list = new List<Command>();
            int seat = s.ActiveSeat;
            var units = s.ActiveUnitId != 0 ? new List<UnitState> { s.Unit(s.ActiveUnitId) }
                                           : s.Units.Where(u => u.Owner == seat && RosterRules.Eligible(s, u)).OrderBy(u => u.Id).ToList();
            var view = Vision.FoggedView(s, seat);
            foreach (var u in units)
            {
                if (!RulesEngine.CanActivate(s, u, out _)) continue;
                list.Add(Command.Wait(seat, u.Id));
                foreach (var kv in RosterMovement.Reachable(view, view.Unit(u.Id)).OrderBy(k => k.Key))
                    list.Add(Command.Move(seat, u.Id, kv.Key));
                if (RosterRules.CanAct(s, u, attack: true) == null)
                {
                    foreach (var o in RosterCombat.LegalTargets(s, u)) list.Add(Command.Attack(seat, u.Id, o.TargetHex));
                    foreach (var h in u.Position.Neighbors())
                        if (s.Map.Contains(h) && RosterRules.ValidateRescue(s, u, h).Ok) list.Add(Command.Rescue(seat, u.Id, h));
                }
                SliceRules.ExtraOrders(s, view, u, list);
                foreach (var (picks, opt) in RosterAbilities.Enumerate(s, u, abilityCap, abilityWidth))
                    list.Add(Command.Active(seat, u.Id, picks, opt));
            }
            return list;
        }

        static void AddAbilityCommands(MatchState s, UnitState u, AbilityId ab, List<Command> list)
        {
            int seat = u.Owner;
            var def = GameContent.Ability(ab);
            switch (def.Targeting)
            {
                case AbilityTargeting.Self:
                    list.Add(Command.UseAbility(seat, u.Id, ab, u.Position));
                    return;
                case AbilityTargeting.MoveThenAttack:
                {
                    var dests = new List<Hex> { u.Position };
                    dests.AddRange(u.Position.Neighbors().Where(h => BoardQueries.IsPassableTerrain(s, h) && s.UnitAt(h) == null).OrderBy(h => h));
                    foreach (var d in dests)
                        foreach (var o in RulesEngine.PreviewOverclockTargets(s, u, d))
                            list.Add(Command.Overclock(seat, u.Id, d, o.TargetHex));
                    return;
                }
                case AbilityTargeting.WreckHex:
                    foreach (var w in s.Wrecks)
                        for (int opt = 0; opt <= 1; opt++)
                            if (RulesEngine.ValidateAbility(s, u, ab, w.Hex, opt, default).Ok)
                                list.Add(Command.UseAbility(seat, u.Id, ab, w.Hex, opt));
                    return;
                default:
                    foreach (var t in AbilityTargets(s, u, ab))
                        if (t.Valid) list.Add(Command.UseAbility(seat, u.Id, ab, t.Hex, t.Option));
                    return;
            }
        }
    }

    public static partial class RulesEngine
    {
        public static List<AttackOutcome> PreviewOverclockTargets(MatchState s, UnitState u, Hex moveTo)
        {
            var original = u.Position;
            u.Position = moveTo;
            try { return Combat.LegalTargets(s, u); }
            finally { u.Position = original; }
        }
    }

    /// <summary>Ready-made legal armies for quick play and as starting points in the army builder.</summary>
    public static class ArmyPresets
    {
        public static List<ArmyRoster> For(FactionId f)
        {
            string C(Archetype a) => GameContent.UnitFor(f, a).Id;
            var list = new List<ArmyRoster>();
            ArmyRoster Make(string name, Doctrine d, params RosterEntry[] units) => new ArmyRoster { Name = name, Faction = f, Doctrine = d, Units = units.ToList() };
            var doctrine = f == FactionId.Inhumanus ? Doctrine.Assault : Doctrine.None;

            switch (f)
            {
                case FactionId.Silex:
                    list.Add(Make("Bastion Host", doctrine, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Guardian)),
                        new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Raider)), new RosterEntry(C(Archetype.Infantry))));
                    list.Add(Make("Crystal Siege", doctrine, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Vehicle)),
                        new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Infantry), "gen.d.plating")));
                    break;
                case FactionId.Inhumanus:
                    list.Add(Make("Hostile Takeover", Doctrine.Assault, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Guardian)),
                        new RosterEntry(C(Archetype.Gunner), "inh.w.phase_rifle"), new RosterEntry(C(Archetype.Raider)),
                        new RosterEntry(C(Archetype.Infantry)), new RosterEntry(C(Archetype.Infantry)), new RosterEntry(C(Archetype.Infantry))));
                    list.Add(Make("Indemnified Holdings", Doctrine.Defense, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Guardian)),
                        new RosterEntry(C(Archetype.Vehicle)), new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Infantry)),
                        new RosterEntry(C(Archetype.Infantry))));
                    list.Add(Make("Leveraged Control", Doctrine.Control, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Guardian)),
                        new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Raider)), new RosterEntry(C(Archetype.Infantry)),
                        new RosterEntry(C(Archetype.Infantry)), new RosterEntry(C(Archetype.Infantry))));
                    break;
                default:
                    list.Add(Make("Standard Battle Group", doctrine, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Guardian)),
                        new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Raider)), new RosterEntry(C(Archetype.Infantry)),
                        new RosterEntry(C(Archetype.Infantry))));
                    list.Add(Make("Armored Spearhead", doctrine, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Vehicle)),
                        new RosterEntry(C(Archetype.Guardian)), new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Infantry))));
                    list.Add(Make("Skirmish Line", doctrine, new RosterEntry(C(Archetype.Commander)), new RosterEntry(C(Archetype.Raider)),
                        new RosterEntry(C(Archetype.Raider)), new RosterEntry(C(Archetype.Gunner)), new RosterEntry(C(Archetype.Infantry)),
                        new RosterEntry(C(Archetype.Infantry))));
                    break;
            }
            return list;
        }

        public static ArmyRoster Default(FactionId f) => For(f)[0];
    }
}
