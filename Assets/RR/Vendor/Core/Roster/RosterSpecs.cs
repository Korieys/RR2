using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Hand-authored executable parameters for every Full Roster active and passive ability (the codex handoff: "feed each
    /// unit's rules_text into explicit parameters … do not parse prose at runtime"). The ability code, range and cooldown
    /// come from the generated catalog; everything else is set here, one entry per unit id, in RosterSpecs.&lt;Faction&gt;.cs.
    /// Each entry carries an <c>Engine</c> line: the exact behaviour the rules engine executes, shown in the unit card.
    /// </summary>
    public static partial class RosterSpecs
    {
        static readonly Dictionary<string, ActiveSpec> Actives = new Dictionary<string, ActiveSpec>(StringComparer.Ordinal);
        static readonly Dictionary<string, PassiveSpec> Passives = new Dictionary<string, PassiveSpec>(StringComparer.Ordinal);

        /// <summary>Problems found while attaching (unit ids without specs, specs for unknown ids); empty when consistent.</summary>
        public static readonly List<string> Problems = new List<string>();

        internal static void Attach()
        {
            Craigon(); Silex(); Pygmaei(); Meeples(); Inhumanus(); Pirates();
            foreach (var def in RosterContent.Ordered)
            {
                var p = def.Roster;
                if (!Actives.TryGetValue(def.Id, out var a)) { Problems.Add($"{def.Id}: no active spec"); a = new ActiveSpec { Engine = "Not implemented." }; }
                if (!Passives.TryGetValue(def.Id, out var ps)) { Problems.Add($"{def.Id}: no passive spec"); ps = Inert("Not implemented."); }
                a.Code = p.ActiveCode;
                a.Cooldown = p.ActiveCooldown;
                if (a.Range == 0) a.Range = p.ActiveRange;
                p.Active = a;
                p.Passive = ps;
            }
            foreach (var id in Actives.Keys.Concat(Passives.Keys).Distinct())
                if (!RosterContent.Units.ContainsKey(id)) Problems.Add($"{id}: spec for an unknown unit");
            foreach (var t in RosterContent.Tokens.Values)
            {
                t.Roster.Active = new ActiveSpec { Code = EffectCode.DAMAGE, Engine = "Tokens do not use active abilities." };
                t.Roster.Passive = Inert("Tokens have no passive.");
            }
        }

        // ------------------------------------------------------------------ authoring helpers

        static void Def(string id, ActiveSpec a, PassiveSpec p)
        {
            if (Actives.ContainsKey(id)) Problems.Add($"{id}: duplicate spec");
            Actives[id] = a;
            Passives[id] = p;
        }

        static PassiveSpec P(PassiveKind k, string engine, int amount = 1, int range = 1, UnitRole? role = null, bool basicOnly = false) =>
            new PassiveSpec { Kind = k, Engine = engine, Amount = amount, Range = range, Role = role, BasicOnly = basicOnly };

        static PassiveSpec Inert(string why) => new PassiveSpec { Kind = PassiveKind.Inert, Engine = why };

        static Grant[] G(params Grant[] g) => g;

        // Common statuses.
        static Grant Root => new Grant(RStatusType.Rooted);
        static Grant Disable => new Grant(RStatusType.Disabled);
        static Grant Silence => new Grant(RStatusType.Silenced);
        static Grant Expose(int n) => new Grant(RStatusType.Exposed, n);
        static Grant Suppress(int n) => new Grant(RStatusType.Suppressed, n);
        /// <summary>MARK: +n on the next qualifying friendly attack(s); lasts until used or the end of the next round.</summary>
        static Grant MarkG(int n, int uses = 1, int rounds = 2) => new Grant(RStatusType.Marked, n, RExpire.RoundEnd, rounds, uses);
        static Grant ShieldUntilSource(int n) => new Grant(RStatusType.Shield, n, RExpire.SourceNextActivation);
        static Grant ShieldOwnNext(int n) => new Grant(RStatusType.Shield, n);
        static Grant ShieldRound(int n, int rounds = 1) => new Grant(RStatusType.Shield, n, RExpire.RoundEnd, rounds);
        static Grant NextAttack(int n, RExpire expire = RExpire.AfterOwnActivation) => new Grant(RStatusType.AttackBuff, n, expire, 1, uses: 1);
        static Grant AttackThisRound(int n) => new Grant(RStatusType.AttackBuff, n, RExpire.RoundEnd, 1);
        static Grant MoveNext(int n) => new Grant(RStatusType.MoveBuff, n);
        static Grant ArmorUntilSource(int n) => new Grant(RStatusType.ArmorBuff, n, RExpire.SourceNextActivation);
        static Grant ProtectBy(int range, int reduction = 0, RExpire expire = RExpire.SourceNextActivation, int duration = 1) =>
            new Grant(RStatusType.Protected, reduction, expire, duration, uses: 1, range: range);
        static Grant InterceptBy(int range) => new Grant(RStatusType.Intercepted, 0, RExpire.SourceNextActivation, 1, uses: 1, range: range);
        static Grant GuardianRole(RExpire expire = RExpire.AfterOwnActivation) => new Grant(RStatusType.Guardian, 0, expire, 1, uses: 1, range: 1);

        // Common filters.
        internal static bool IsLeader(MatchState s, UnitState u) => u != null && s.Players[u.Owner].CommanderId == u.Id;
        static readonly UnitFilter Infantry = (s, src, t) => t.Def.Roster.IsInfantry;
        static readonly UnitFilter Vehicle = (s, src, t) => t.Def.Roster.IsVehicle;
        static readonly UnitFilter Ship = (s, src, t) => t.Def.Roster.IsShip;
        static readonly UnitFilter NonHeavy = (s, src, t) => !t.Def.Roster.IsHeavy;
        static readonly UnitFilter NonLeader = (s, src, t) => !IsLeader(s, t);
        static readonly UnitFilter LeaderOnly = (s, src, t) => IsLeader(s, t);
        static readonly UnitFilter SmallerThanCruiser = (s, src, t) => t.Def.Roster.IsShip && t.Def.Roster.ShipSize < 5;
        static readonly UnitFilter CorvetteOrSmaller = (s, src, t) => t.Def.Roster.IsShip && t.Def.Roster.ShipSize <= 2;
        static readonly UnitFilter CorvetteOrFrigate = (s, src, t) => t.Def.Roster.Role == UnitRole.Corvette || t.Def.Roster.Role == UnitRole.Frigate;
        static readonly UnitFilter SmallerThanSelf = (s, src, t) => t.Def.Roster.IsShip && t.Def.Roster.ShipSize < src.Def.Roster.ShipSize;
        static readonly UnitFilter NonSiege = (s, src, t) => !t.Def.Roster.IsSiege;
        static readonly UnitFilter SmallCraft = (s, src, t) => t.Def.Roster.IsSmallCraft;
        static UnitFilter Both(UnitFilter a, UnitFilter b) => (s, src, t) => a(s, src, t) && b(s, src, t);
        static UnitFilter Either(UnitFilter a, UnitFilter b) => (s, src, t) => a(s, src, t) || b(s, src, t);
        static UnitFilter RoleIs(params UnitRole[] roles) => (s, src, t) => Array.IndexOf(roles, t.Def.Roster.Role) >= 0;
    }
}
