using System.Linq;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    public class CombatTests
    {
        [Test]
        public void Damage_IsDeterministic_AndPreviewMatchesResult()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int tgt = b.Add(1, FactionId.Pygmaei, Archetype.Guardian, 0, 0);
            var s = b.Build();
            var attacker = s.Unit(b.Id(gun)); var target = s.Unit(b.Id(tgt));
            var preview = Combat.Resolve(s, attacker, target.Position);
            var again = Combat.Resolve(s, attacker, target.Position);
            Assert.IsTrue(preview.Legal);
            Assert.AreEqual(1, preview.FinalDamage, "2 damage - 1 armor");
            Assert.AreEqual(again.FinalDamage, preview.FinalDamage);
            Scenario.Ok(s, Command.Attack(0, attacker.Id, target.Position));
            Assert.AreEqual(preview.TargetHpAfter, target.Hp);
        }

        [Test]
        public void Lethal_Attack_RemovesUnit_AndLeavesWreck()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int tgt = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, 0);
            var s = b.Build();
            var r = Scenario.Do(s, Command.Attack(0, b.Id(gun), new Hex(0, 0)));
            Assert.IsTrue(r.Accepted);
            Assert.IsFalse(s.Unit(b.Id(tgt)).Alive);
            Assert.IsNull(s.UnitAt(new Hex(0, 0)));
            Assert.AreEqual(0, s.WreckIndex(new Hex(0, 0)));
            Assert.IsTrue(r.Events.Any(e => e.Type == GameEventType.UnitDestroyed && e.UnitId == b.Id(tgt)));
        }

        [Test]
        public void Cover_ReducesRangedButNotMelee_AndDamageNeverDropsBelowOne()
        {
            var b = new Scenario(Scenario.LosMap);
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, -2, 3);
            int melee = b.Add(0, FactionId.Meeples, Archetype.Guardian, -1, 1);
            int tgt = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, -2, 1); // on cover
            var s = b.Build();
            var ranged = Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(-2, 1));
            Assert.IsTrue(ranged.Legal, ranged.Reason);
            Assert.AreEqual(1, ranged.Cover);
            Assert.AreEqual(1, ranged.FinalDamage);
            var close = Combat.Resolve(s, s.Unit(b.Id(melee)), new Hex(-2, 1));
            Assert.AreEqual(0, close.Cover);

            var b2 = new Scenario();
            int weak = b2.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 1);
            b2.Add(1, FactionId.Inhumanus, Archetype.Guardian, 0, 0); // armor 2
            var s2 = b2.Build();
            Assert.AreEqual(1, Combat.Resolve(s2, s2.Unit(b2.Id(weak)), new Hex(0, 0)).FinalDamage);
        }

        [Test]
        public void Guard_TokenLifecycle()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int tgt = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, 0);
            b.FirstSeat = 1;
            var s = b.Build();
            var target = s.Unit(b.Id(tgt));
            Scenario.Ok(s, Command.Guard(1, target.Id));
            Assert.IsTrue(target.Guarded);
            var o = Combat.Resolve(s, s.Unit(b.Id(gun)), target.Position);
            Assert.AreEqual(1, o.FinalDamage);
            Assert.IsTrue(o.GuardConsumed);
            Scenario.Ok(s, Command.Attack(0, b.Id(gun), target.Position));
            Assert.IsFalse(target.Guarded, "Guard is consumed by the first attack");
            Assert.AreEqual(1, target.Hp);
        }

        [Test]
        public void Guard_IsRemovedWhenTheUnitActs()
        {
            var b = new Scenario();
            int inf = b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var s = b.Build();
            var u = s.Unit(b.Id(inf));
            Scenario.Ok(s, Command.Guard(0, u.Id));
            Scenario.PassTurn(s);
            Scenario.Ok(s, Command.Move(0, u.Id, new Hex(0, 1)));
            Assert.IsFalse(u.Guarded);
        }

        [Test]
        public void GuardianGuard_MitigatesTwo_AndRipostesMelee()
        {
            var b = new Scenario();
            int axe = b.Add(0, FactionId.SpacePirates, Archetype.Infantry, 0, 1);
            int wall = b.Add(1, FactionId.Silex, Archetype.Guardian, 0, 0);
            b.FirstSeat = 1;
            var s = b.Build();
            Scenario.Ok(s, Command.Guard(1, b.Id(wall)));
            var o = Combat.Resolve(s, s.Unit(b.Id(axe)), new Hex(0, 0));
            Assert.AreEqual(1, o.FinalDamage, "minimum 1");
            Assert.AreEqual(1, o.RiposteDamage);
            Scenario.Ok(s, Command.Attack(0, b.Id(axe), new Hex(0, 0)));
            Assert.AreEqual(1, s.Unit(b.Id(axe)).Hp, "riposte hurt the attacker");
        }

        [Test]
        public void Ward_NegatesOneAttack_AndIsConsumed()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int tgt = b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0, "gen.d.deflector");
            var s = b.Build();
            var t = s.Unit(b.Id(tgt));
            Assert.IsTrue(t.HasStatus(StatusType.Ward));
            var o = Combat.Resolve(s, s.Unit(b.Id(gun)), t.Position);
            Assert.IsTrue(o.WardBlocked);
            Assert.AreEqual(0, o.FinalDamage);
            Scenario.Ok(s, Command.Attack(0, b.Id(gun), t.Position));
            Assert.AreEqual(t.MaxHp, t.Hp);
            Assert.IsFalse(t.HasStatus(StatusType.Ward));
        }

        [Test]
        public void Splash_HitsFriendAndFoe_AndGangUpAddsDamage()
        {
            var b = new Scenario();
            int rig = b.Add(0, FactionId.SpacePirates, Archetype.Vehicle, 0, 3);
            int own = b.Add(0, FactionId.SpacePirates, Archetype.Guardian, 0, 1);
            int tgt = b.Add(1, FactionId.Silex, Archetype.Guardian, 0, 0);
            int foe = b.Add(1, FactionId.Silex, Archetype.Infantry, 1, 0);
            var s = b.Build();
            var o = Combat.Resolve(s, s.Unit(b.Id(rig)), new Hex(0, 0));
            Assert.IsTrue(o.Legal, o.Reason);
            Assert.That(o.Breakdown.Any(x => x.Contains("Gang Up")));
            Assert.AreEqual(2, o.FinalDamage, "2 + 1 gang up - 1 armor");
            Assert.AreEqual(2, o.Splash.Count);
            int ownHp = s.Unit(b.Id(own)).Hp, foeHp = s.Unit(b.Id(foe)).Hp;
            Scenario.Ok(s, Command.Attack(0, b.Id(rig), new Hex(0, 0)));
            Assert.AreEqual(ownHp - 1, s.Unit(b.Id(own)).Hp, "friendly fire");
            Assert.AreEqual(foeHp - 1, s.Unit(b.Id(foe)).Hp);
        }

        [Test]
        public void CommanderDeath_EndsTheMatchImmediately()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int cmd = b.Add(1, FactionId.Pygmaei, Archetype.Commander, 0, 0);
            b.WithHp(cmd, 2);
            var s = b.Build();
            var r = Scenario.Do(s, Command.Attack(0, b.Id(gun), new Hex(0, 0)));
            Assert.IsTrue(r.Accepted);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
            Assert.IsTrue(r.Events.Any(e => e.Type == GameEventType.CommanderKilled));
            Assert.IsFalse(r.Events.Any(e => e.Type == GameEventType.TurnChanged), "no turn after a terminal result");
        }

        [Test]
        public void SimultaneousCommanderDeath_ActingPlayerWins()
        {
            var b = new Scenario();
            int rig = b.Add(0, FactionId.SpacePirates, Archetype.Vehicle, 0, 3);
            int myCmd = b.Add(0, FactionId.SpacePirates, Archetype.Commander, 1, 0);
            int theirCmd = b.Add(1, FactionId.Meeples, Archetype.Commander, 0, 0);
            b.WithHp(myCmd, 1).WithHp(theirCmd, 1);
            var s = b.Build();
            Scenario.Ok(s, Command.Attack(0, b.Id(rig), new Hex(0, 0)));
            Assert.IsFalse(s.Unit(b.Id(myCmd)).Alive);
            Assert.IsFalse(s.Unit(b.Id(theirCmd)).Alive);
            Assert.AreEqual(0, s.Winner);
        }

        [Test]
        public void SelfInflictedCommanderDeath_LosesTheMatch()
        {
            var b = new Scenario();
            int cmd = b.Add(0, FactionId.SpacePirates, Archetype.Commander, 0, 1);
            int wall = b.Add(1, FactionId.Silex, Archetype.Guardian, 0, 0);
            b.WithHp(cmd, 1);
            b.FirstSeat = 1;
            var s = b.Build();
            Scenario.Ok(s, Command.Guard(1, b.Id(wall)));
            // Commander's hand-cannons are ranged: attack from range 1 with a ranged weapon does not trigger riposte.
            var o = Combat.Resolve(s, s.Unit(b.Id(cmd)), new Hex(0, 0));
            Assert.AreEqual(0, o.RiposteDamage);
            s.Unit(b.Id(cmd)).Weapon = new WeaponProfile { Name = "Fists", Damage = 1, Kind = WeaponKind.Melee, FxKey = "generic_melee" };
            Scenario.Ok(s, Command.Attack(0, b.Id(cmd), new Hex(0, 0)));
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(1, s.Winner, "riposte killed the attacking Commander");
        }

        [Test]
        public void ConditionalBonuses_RendHostileTakeoverExposure()
        {
            var b = new Scenario();
            int stalker = b.Add(0, FactionId.Craigon, Archetype.Raider, 0, 1);
            int tgt = b.Add(1, FactionId.Meeples, Archetype.Guardian, 0, 0);
            b.WithHp(tgt, 3);
            var s = b.Build();
            var o = Combat.Resolve(s, s.Unit(b.Id(stalker)), new Hex(0, 0));
            Assert.That(o.Breakdown.Any(x => x.Contains("Rend")));
            Assert.AreEqual(1, o.FinalDamage, "1 + 1 rend - 1 armor");

            var b2 = new Scenario();
            int lancer = b2.Add(0, FactionId.Inhumanus, Archetype.Gunner, 0, 2);
            int full = b2.Add(1, FactionId.Meeples, Archetype.Commander, 0, 0);
            var s2 = b2.Build();
            var o2 = Combat.Resolve(s2, s2.Unit(b2.Id(lancer)), new Hex(0, 0));
            Assert.That(o2.Breakdown.Any(x => x.Contains("Hostile Takeover")));
            Assert.AreEqual(3, o2.FinalDamage);
        }

        [Test]
        public void WeaponCooldown_PreventsBackToBackFiring()
        {
            var b = new Scenario();
            int lance = b.Add(0, FactionId.Craigon, Archetype.Gunner, 0, 2, "cra.w.neural_lance");
            b.Add(1, FactionId.Meeples, Archetype.Guardian, 0, 0);
            var s = b.Build();
            Scenario.Ok(s, Command.Attack(0, b.Id(lance), new Hex(0, 0)));
            Scenario.PassTurn(s);
            StringAssert.Contains("recharging", Scenario.Rejected(s, Command.Attack(0, b.Id(lance), new Hex(0, 0))));
            Scenario.PassTurn(s); Scenario.PassTurn(s);
            Assert.IsTrue(Scenario.Do(s, Command.Attack(0, b.Id(lance), new Hex(0, 0))).Accepted);
        }
    }
}
