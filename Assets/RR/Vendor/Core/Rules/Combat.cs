using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    public sealed class SplashHit
    {
        public int UnitId;
        public int Damage;
        public int HpAfter;
        public bool Destroyed;
        public bool UnbreakableTriggered;
    }

    /// <summary>
    /// Fully resolved (but not yet applied) attack. The same object drives the UI preview and the authoritative result,
    /// so the preview always matches the outcome.
    /// </summary>
    public sealed class AttackOutcome
    {
        public bool Legal;
        public string Reason;
        public int AttackerId;
        public Hex From;
        public Hex TargetHex;
        public int TargetUnitId;        // 0 when the target is a bulwark
        public bool TargetIsBulwark;
        public int Distance;
        public bool InRange;
        public LosState Los;
        public WeaponProfile Weapon;

        public int BaseDamage;
        public int BonusDamage;
        public int Armor;
        public int Cover;
        public int GuardMitigation;
        public int FinalDamage;
        public bool WardBlocked;
        public bool GuardConsumed;
        public bool UnbreakableTriggered;
        public int TargetHpBefore;
        public int TargetHpAfter;
        public bool TargetDestroyed;
        public int RiposteDamage;
        public int AttackerHpAfter;
        public bool AttackerDestroyed;
        public bool ExposeApplied;
        public bool JuryRigConsumed;
        public List<SplashHit> Splash = new List<SplashHit>();
        /// <summary>Human-readable breakdown lines for tooltips ("+1 Gang Up", "−1 cover (ruins)").</summary>
        public List<string> Breakdown = new List<string>();
        /// <summary>Full Roster: the resolved hit (guard interception, shields, counters), null for Classic.</summary>
        public HitResult Roster;
    }

    public static class Combat
    {
        static bool WeaponReady(MatchState s, UnitState u) => s.ActivationIndex >= u.WeaponReadyAt;

        /// <summary>
        /// Resolve an attack by <paramref name="attacker"/> standing on <paramref name="from"/> against <paramref name="targetHex"/>.
        /// Pure: never mutates the state.
        /// </summary>
        public static AttackOutcome Resolve(MatchState s, UnitState attacker, Hex targetHex, Hex? fromOverride = null)
        {
            if (s.IsRoster) return RosterCombat.ResolveBasic(s, attacker, targetHex, fromOverride);
            var o = new AttackOutcome { AttackerId = attacker.Id, TargetHex = targetHex, Weapon = attacker.Weapon };
            var from = fromOverride ?? attacker.Position;
            o.From = from;
            var w = attacker.Weapon;
            o.Distance = Hex.Distance(from, targetHex);

            var targetUnit = s.UnitAt(targetHex);
            if (targetUnit != null && targetUnit.Id == attacker.Id) targetUnit = null;
            var bulwark = targetUnit == null ? s.BulwarkAt(targetHex) : null;
            if (targetUnit == null && bulwark == null) return Fail(o, "No target there.");
            if (targetUnit != null && targetUnit.Owner == attacker.Owner) return Fail(o, "You cannot attack your own units.");
            if (bulwark != null && bulwark.Owner == attacker.Owner) return Fail(o, "You cannot attack your own bulwark.");
            o.TargetUnitId = targetUnit?.Id ?? 0;
            o.TargetIsBulwark = bulwark != null;
            if (targetUnit != null && !Vision.CanSee(s, attacker.Owner, targetUnit)) return Fail(o, "You can't see that target.");

            var (min, max) = BoardQueries.WeaponRange(s, attacker, from);
            o.InRange = o.Distance >= min && o.Distance <= max;
            bool elevated = s.Map.Terrain(from) == TerrainType.Elevated;
            if (w.Has(WeaponTraits.Indirect)) o.Los = LosState.Clear;
            else o.Los = BoardQueries.LineOfSight(s, from, targetHex, w.Has(WeaponTraits.Phase) || elevated, attacker.Id, attacker.Owner);

            if (!o.InRange)
            {
                if (o.Distance < min) return Fail(o, $"Too close: {w.Name} needs range {min}-{max}.");
                return Fail(o, $"Out of range: {w.Name} reaches {min}-{max}, target is {o.Distance} away.");
            }
            if (o.Los != LosState.Clear) return Fail(o, BoardQueries.LosText(o.Los) + ".");
            if (!WeaponReady(s, attacker))
            {
                int turns = (attacker.WeaponReadyAt - s.ActivationIndex + 1) / 2;
                return Fail(o, $"{w.Name} is recharging ({turns} more turn{(turns == 1 ? "" : "s")}).");
            }

            // ---- damage
            o.BaseDamage = w.Damage;
            int bonus = 0;
            void Add(int v, string text) { bonus += v; o.Breakdown.Add($"+{v} {text}"); }
            if (attacker.HasStatus(StatusType.JuryRigged)) { Add(1, "jury-rigged weapon"); o.JuryRigConsumed = true; }
            if (w.Has(WeaponTraits.Shatter) && (bulwark != null || (targetUnit != null &&
                (targetUnit.Def.Archetype == Archetype.Vehicle || targetUnit.Def.Archetype == Archetype.Guardian))))
                Add(1, "Shatter");
            if (targetUnit != null)
            {
                if (w.Has(WeaponTraits.Rend) && targetUnit.Hp < targetUnit.MaxHp) Add(1, "Rend (damaged target)");
                if (attacker.Def.Faction == FactionId.SpacePirates && GangUp(s, attacker, targetHex)) Add(1, "Gang Up");
                if (attacker.Def.Faction == FactionId.Inhumanus && s.Players[attacker.Owner].Doctrine == Doctrine.Assault
                    && targetUnit.Hp == targetUnit.MaxHp) Add(1, "Hostile Takeover");
                var lien = targetUnit.GetStatus(StatusType.Lien);
                if (attacker.Def.Faction == FactionId.Inhumanus && lien.HasValue)
                {
                    var holder = s.Unit(lien.Value.SourceUnitId);
                    if (holder != null && (holder.Flags & EquipmentFlags.DebtInstrument) != 0) Add(1, "debt collection");
                }
                if (targetUnit.HasStatus(StatusType.Overheated)) Add(1, "target Overheated");
                if (targetUnit.HasStatus(StatusType.Exposed)) Add(1, "target Exposed");
            }
            o.BonusDamage = bonus;
            int raw = o.BaseDamage + bonus;

            if (bulwark != null)
            {
                o.FinalDamage = Math.Max(1, raw);
                o.TargetHpBefore = bulwark.Hp;
                o.TargetHpAfter = Math.Max(0, bulwark.Hp - o.FinalDamage);
                o.TargetDestroyed = o.TargetHpAfter <= 0;
            }
            else
            {
                int armor = Math.Max(0, BoardQueries.EffectiveArmor(s, targetUnit) - w.Pierce);
                int cover = 0;
                string coverSrc = BoardQueries.CoverSource(s, targetUnit);
                if (o.Distance >= 2 && coverSrc != null && !w.Has(WeaponTraits.IgnoresCover)) cover = 1;
                int guard = targetUnit.Guarded ? (targetUnit.Def.Archetype == Archetype.Guardian ? 2 : 1) : 0;
                o.Armor = armor; o.Cover = cover; o.GuardMitigation = guard;
                if (armor > 0) o.Breakdown.Add($"−{armor} armor" + (w.Pierce > 0 ? $" (after pierce {w.Pierce})" : ""));
                if (cover > 0) o.Breakdown.Add($"−1 cover ({coverSrc})");
                if (guard > 0) o.Breakdown.Add($"−{guard} Guard");
                int dmg = raw - armor - cover - guard;
                if (dmg < 1 && raw > 0) { dmg = 1; if (armor + cover + guard > 0) o.Breakdown.Add("minimum 1 damage"); }
                o.TargetHpBefore = targetUnit.Hp;
                if (targetUnit.HasStatus(StatusType.Ward))
                {
                    o.WardBlocked = true; dmg = 0; o.Breakdown.Add("Ward absorbs the hit");
                }
                else if (targetUnit.Guarded) o.GuardConsumed = true;
                if (dmg >= targetUnit.Hp && dmg > 0 && targetUnit.HasStatus(StatusType.Unbreakable))
                {
                    dmg = targetUnit.Hp - 1; o.UnbreakableTriggered = true; o.Breakdown.Add("Unbreakable: survives at 1 HP");
                }
                o.FinalDamage = dmg;
                o.TargetHpAfter = targetUnit.Hp - dmg;
                o.TargetDestroyed = o.TargetHpAfter <= 0;

                // Riposte: a Guard token punishes melee attackers that fail to kill.
                if (o.GuardConsumed && w.Kind == WeaponKind.Melee && o.Distance == 1 && !o.TargetDestroyed)
                {
                    o.RiposteDamage = 1;
                    if (attacker.Hp - 1 <= 0 && attacker.HasStatus(StatusType.Unbreakable)) o.RiposteDamage = 0;
                    o.Breakdown.Add("Riposte: attacker takes 1");
                }
                if (w.Has(WeaponTraits.Expose) && !o.TargetDestroyed) o.ExposeApplied = true;
            }
            o.AttackerHpAfter = attacker.Hp - o.RiposteDamage;
            o.AttackerDestroyed = o.AttackerHpAfter <= 0;

            if (w.Has(WeaponTraits.Splash))
            {
                foreach (var n in targetHex.Neighbors())
                {
                    var u = s.UnitAt(n);
                    if (u == null || u.Id == attacker.Id) continue;
                    var hit = new SplashHit { UnitId = u.Id, Damage = 1 };
                    if (u.Hp <= 1 && u.HasStatus(StatusType.Unbreakable)) { hit.Damage = 0; hit.UnbreakableTriggered = true; }
                    hit.HpAfter = u.Hp - hit.Damage;
                    hit.Destroyed = hit.HpAfter <= 0;
                    o.Splash.Add(hit);
                }
                if (o.Splash.Count > 0) o.Breakdown.Add($"Splash: 1 damage to {o.Splash.Count} adjacent unit(s)");
            }
            o.Legal = true;
            return o;
        }

        static AttackOutcome Fail(AttackOutcome o, string reason) { o.Legal = false; o.Reason = reason; return o; }

        public static bool GangUp(MatchState s, UnitState attacker, Hex targetHex)
        {
            foreach (var n in targetHex.Neighbors())
            {
                var u = s.UnitAt(n);
                if (u != null && u.Id != attacker.Id && u.Owner == attacker.Owner && u.Def.Faction == FactionId.SpacePirates) return true;
            }
            return false;
        }

        /// <summary>Every enemy unit/bulwark hex the attacker could legally hit from its position (or from <paramref name="from"/>).</summary>
        public static List<AttackOutcome> LegalTargets(MatchState s, UnitState attacker, Hex? from = null)
        {
            if (s.IsRoster) return RosterCombat.LegalTargets(s, attacker, from);
            var list = new List<AttackOutcome>();
            foreach (var u in s.Units)
            {
                if (!u.OnBoard || u.Owner == attacker.Owner) continue;
                var o = Resolve(s, attacker, u.Position, from);
                if (o.Legal) list.Add(o);
            }
            foreach (var b in s.Bulwarks)
            {
                if (b.Owner == attacker.Owner) continue;
                var o = Resolve(s, attacker, b.Hex, from);
                if (o.Legal) list.Add(o);
            }
            return list;
        }
    }
}
