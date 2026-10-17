using System.Linq;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    public class FactionAbilityTests
    {
        // ------------------------------------------------------------------ Craigon: The Gift
        [Test]
        public void Craigon_TheGift_ConvertsAOneHpEnemy()
        {
            var b = new Scenario();
            int hiero = b.Add(0, FactionId.Craigon, Archetype.Commander, 0, 2);
            int prey = b.Add(1, FactionId.Pygmaei, Archetype.Gunner, 0, 0);
            b.WithHp(prey, 1);
            var s = b.Build();
            var r = Scenario.Do(s, Command.UseAbility(0, b.Id(hiero), AbilityId.TheGift, new Hex(0, 0)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsFalse(s.Unit(b.Id(prey)).Alive);
            var convert = s.UnitAt(new Hex(0, 0));
            Assert.IsNotNull(convert);
            Assert.AreEqual(0, convert.Owner);
            Assert.AreEqual(FactionId.Craigon, convert.Def.Faction);
            Assert.AreEqual(Archetype.Gunner, convert.Def.Archetype);
            Assert.AreEqual(1, convert.Hp);
            Assert.IsTrue(convert.Converted);
            Assert.IsTrue(convert.HasStatus(StatusType.Dazed));
            Assert.AreEqual(convert.Def.Weapon.Name, convert.Weapon.Name, "uses the Craigon equipment template");
            Assert.AreEqual(-1, s.WreckIndex(new Hex(0, 0)), "conversion leaves no wreck");
            Assert.AreEqual(1, RulesEngine.ChargesLeft(s.Unit(b.Id(hiero)), AbilityId.TheGift));
            Assert.IsTrue(r.Events.Any(e => e.Type == GameEventType.UnitConverted));
            // Dazed for its controller's next activation.
            Scenario.PassTurn(s);
            Assert.IsFalse(RulesEngine.CanActivate(s, convert, out _));
            Scenario.PassTurn(s); Scenario.PassTurn(s);
            Assert.IsTrue(RulesEngine.CanActivate(s, convert, out var why), why);
        }

        [Test]
        public void Craigon_TheGift_Restrictions()
        {
            var b = new Scenario();
            int hiero = b.Add(0, FactionId.Craigon, Archetype.Commander, 0, 2);
            int healthy = b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            int cmd = b.Add(1, FactionId.Meeples, Archetype.Commander, 1, 0);
            int far = b.Add(1, FactionId.Meeples, Archetype.Raider, 0, -2);
            b.WithHp(cmd, 1).WithHp(far, 1);
            var s = b.Build();
            int h = b.Id(hiero);
            StringAssert.Contains("exactly 1 HP", Scenario.Rejected(s, Command.UseAbility(0, h, AbilityId.TheGift, new Hex(0, 0))));
            StringAssert.Contains("Commanders are immune", Scenario.Rejected(s, Command.UseAbility(0, h, AbilityId.TheGift, new Hex(1, 0))));
            StringAssert.Contains("Out of range", Scenario.Rejected(s, Command.UseAbility(0, h, AbilityId.TheGift, new Hex(0, -2))));
            s.Unit(b.Id(healthy)).Hp = 1;
            s.Unit(b.Id(healthy)).Guarded = true;
            StringAssert.Contains("Guarded mind", Scenario.Rejected(s, Command.UseAbility(0, h, AbilityId.TheGift, new Hex(0, 0))));
        }

        [Test]
        public void Craigon_TheGift_RespectsUnitCapAndCharges()
        {
            var b = new Scenario { MaxUnits = 2 };
            int hiero = b.Add(0, FactionId.Craigon, Archetype.Commander, 0, 2);
            b.Add(0, FactionId.Craigon, Archetype.Infantry, 1, 2);
            int prey = b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            b.WithHp(prey, 1);
            var s = b.Build();
            StringAssert.Contains("unit cap", Scenario.Rejected(s, Command.UseAbility(0, b.Id(hiero), AbilityId.TheGift, new Hex(0, 0))));

            var b2 = new Scenario();
            int h2 = b2.Add(0, FactionId.Craigon, Archetype.Commander, 0, 2);
            int p1 = b2.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            int p2 = b2.Add(1, FactionId.Meeples, Archetype.Infantry, 1, 0);
            int p3 = b2.Add(1, FactionId.Meeples, Archetype.Infantry, -1, 1);
            b2.WithHp(p1, 1).WithHp(p2, 1).WithHp(p3, 1);
            var s2 = b2.Build();
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(h2), AbilityId.TheGift, new Hex(0, 0)));
            Scenario.PassTurn(s2);
            StringAssert.Contains("cooldown", Scenario.Rejected(s2, Command.UseAbility(0, b2.Id(h2), AbilityId.TheGift, new Hex(1, 0))));
            Scenario.PassTurn(s2); Scenario.PassTurn(s2);
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(h2), AbilityId.TheGift, new Hex(1, 0)));
            Scenario.PassTurn(s2); Scenario.PassTurn(s2); Scenario.PassTurn(s2);
            StringAssert.Contains("no charges", Scenario.Rejected(s2, Command.UseAbility(0, b2.Id(h2), AbilityId.TheGift, new Hex(-1, 1))));
        }

        [Test]
        public void Craigon_HiveLink_AddsArmorWhenAdjacent()
        {
            var b = new Scenario();
            int a = b.Add(0, FactionId.Craigon, Archetype.Infantry, 0, 0);
            b.Add(0, FactionId.Craigon, Archetype.Infantry, 1, 0);
            int lone = b.Add(0, FactionId.Craigon, Archetype.Infantry, -1, 3);
            var s = b.Build();
            Assert.AreEqual(1, BoardQueries.EffectiveArmor(s, s.Unit(b.Id(a))));
            Assert.AreEqual(0, BoardQueries.EffectiveArmor(s, s.Unit(b.Id(lone))));
        }

        // ------------------------------------------------------------------ Silex: Resonant Bulwark
        [Test]
        public void Silex_Bulwark_BlocksMovementAndSight_AndCanBeDestroyed()
        {
            var b = new Scenario();
            int lord = b.Add(0, FactionId.Silex, Archetype.Guardian, 0, 1);
            int gun = b.Add(1, FactionId.Meeples, Archetype.Gunner, 0, -2);
            int runner = b.Add(1, FactionId.Meeples, Archetype.Raider, 1, -1);
            var s = b.Build();
            Assert.IsTrue(Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, 1)).Legal, "before the wall");
            Scenario.Ok(s, Command.UseAbility(0, b.Id(lord), AbilityId.ResonantBulwark, new Hex(0, 0)));
            var wall = s.BulwarkAt(new Hex(0, 0));
            Assert.IsNotNull(wall);
            Assert.AreEqual(2, wall.Hp);
            Assert.AreEqual(LosState.BlockedByTerrain, Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, 1)).Los);
            Assert.IsNotNull(BoardQueries.CoverSource(s, s.Unit(b.Id(lord))), "adjacent ally of a friendly bulwark is in cover");
            StringAssert.Contains("bulwark", Scenario.Rejected(s, Command.Move(1, b.Id(runner), new Hex(0, 0))));
            Scenario.Ok(s, Command.Attack(1, b.Id(runner), new Hex(0, 0)));
            Assert.AreEqual(1, s.BulwarkAt(new Hex(0, 0)).Hp);
        }

        [Test]
        public void Silex_Bulwark_OnePerUnit_AndExpires()
        {
            var b = new Scenario();
            int lord = b.Add(0, FactionId.Silex, Archetype.Commander, 0, 1);
            var s = b.Build();
            Scenario.Ok(s, Command.UseAbility(0, b.Id(lord), AbilityId.ResonantBulwark, new Hex(0, 0)));
            for (int i = 0; i < 2; i++) Scenario.PassTurn(s);
            Assert.AreEqual(1, s.Bulwarks.Count);
            Scenario.PassTurn(s);
            Assert.AreEqual(0, s.Bulwarks.Count, "bulwarks expire after 4 activations");

            // With a Resonance Core the wall outlives the cooldown, so the one-per-unit rule matters.
            var b2 = new Scenario();
            int core = b2.Add(0, FactionId.Silex, Archetype.Guardian, 0, 1, "sil.u.resonance_core");
            var s2 = b2.Build();
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(core), AbilityId.ResonantBulwark, new Hex(0, 0)));
            Assert.AreEqual(3, s2.BulwarkAt(new Hex(0, 0)).Hp);
            for (int i = 0; i < 5; i++) Scenario.PassTurn(s2);
            Assert.IsNotNull(s2.BulwarkAt(new Hex(0, 0)));
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(core), AbilityId.ResonantBulwark, new Hex(1, 0)));
            Assert.AreEqual(1, s2.Bulwarks.Count, "raising a new bulwark shatters the old one");
            Assert.IsNotNull(s2.BulwarkAt(new Hex(1, 0)));
        }

        [Test]
        public void Silex_Bulwark_TargetValidation()
        {
            var b = new Scenario(Scenario.LosMap);
            int lord = b.Add(0, FactionId.Silex, Archetype.Commander, 1, 0);
            var s = b.Build();
            StringAssert.Contains("obstacle", Scenario.Rejected(s, Command.UseAbility(0, b.Id(lord), AbilityId.ResonantBulwark, new Hex(2, 0))));
            StringAssert.Contains("adjacent", Scenario.Rejected(s, Command.UseAbility(0, b.Id(lord), AbilityId.ResonantBulwark, new Hex(-1, 0))));
        }

        // ------------------------------------------------------------------ Pygmaei: Overclock Barrage
        [Test]
        public void Pygmaei_Overclock_MovesThenFires_ThenOverheats()
        {
            var b = new Scenario();
            int crew = b.Add(0, FactionId.Pygmaei, Archetype.Gunner, 0, 3);
            int tgt = b.Add(1, FactionId.Meeples, Archetype.Guardian, 0, -1);
            var s = b.Build();
            var gunner = s.Unit(b.Id(crew));
            Assert.IsFalse(Combat.Resolve(s, gunner, new Hex(0, -1)).Legal, "out of range from the start");
            var preview = RulesEngine.PreviewOverclock(s, gunner, new Hex(0, 2), new Hex(0, -1));
            Assert.IsTrue(preview.Legal, preview.Reason);
            Scenario.Ok(s, Command.Overclock(0, gunner.Id, new Hex(0, 2), new Hex(0, -1)));
            Assert.AreEqual(new Hex(0, 2), gunner.Position);
            Assert.AreEqual(4 - preview.FinalDamage, s.Unit(b.Id(tgt)).Hp);
            Assert.IsTrue(gunner.HasStatus(StatusType.Overheated));
            // Overheated: +1 damage taken and cannot Guard.
            var o = Combat.Resolve(s, s.Unit(b.Id(tgt)), gunner.Position);
            Assert.IsFalse(o.Legal); // guardian melee cannot reach — check vulnerability via a fresh attacker below
            Scenario.PassTurn(s);
            StringAssert.Contains("Overheated", Scenario.Rejected(s, Command.Guard(0, gunner.Id)));
            Assert.AreEqual(1, RulesEngine.ChargesLeft(gunner, AbilityId.OverclockBarrage));
        }

        [Test]
        public void Pygmaei_Overheated_TakesExtraDamage_AndExpires()
        {
            var b = new Scenario();
            int crew = b.Add(0, FactionId.Pygmaei, Archetype.Raider, 0, 1);
            int hitter = b.Add(1, FactionId.Meeples, Archetype.Gunner, 1, -2);
            b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, -1);
            var s = b.Build();
            var raider = s.Unit(b.Id(crew));
            // Raider overclocks 1 hex forward to (0,0) and hammers the infantry at (0,-1).
            Scenario.Ok(s, Command.Overclock(0, raider.Id, new Hex(0, 0), new Hex(0, -1)));
            var o = Combat.Resolve(s, s.Unit(b.Id(hitter)), raider.Position);
            Assert.That(o.Breakdown.Any(x => x.Contains("Overheated")));
            Scenario.PassTurn(s); Scenario.PassTurn(s); Scenario.PassTurn(s);
            Assert.IsFalse(raider.HasStatus(StatusType.Overheated), "Overheated lasts until the end of the owner's next activation");
        }

        // ------------------------------------------------------------------ Meeples: Council Formation
        [Test]
        public void Meeples_CouncilFormation_GuardsSelfAndAdjacentAllies()
        {
            var b = new Scenario();
            int cmd = b.Add(0, FactionId.Meeples, Archetype.Commander, 0, 2);
            int a1 = b.Add(0, FactionId.Meeples, Archetype.Infantry, 1, 2);
            int a2 = b.Add(0, FactionId.Meeples, Archetype.Gunner, -1, 2);
            int far = b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var s = b.Build();
            Assert.AreEqual(1, BoardQueries.EffectiveArmor(s, s.Unit(b.Id(cmd))), "Shared Command: +1 armor beside an ally");
            Scenario.Ok(s, Command.UseAbility(0, b.Id(cmd), AbilityId.CouncilFormation, new Hex(0, 2)));
            Assert.IsTrue(s.Unit(b.Id(cmd)).Guarded);
            Assert.IsTrue(s.Unit(b.Id(a1)).Guarded);
            Assert.IsTrue(s.Unit(b.Id(a2)).Guarded);
            Assert.IsFalse(s.Unit(b.Id(far)).Guarded);
            Assert.AreEqual(1, RulesEngine.ChargesLeft(s.Unit(b.Id(cmd)), AbilityId.CouncilFormation));
        }

        // ------------------------------------------------------------------ Inhumanus: Lien + doctrine
        [Test]
        public void Inhumanus_Lien_BlocksAbilitiesAndGuard_WithCounterplay()
        {
            var b = new Scenario();
            int archon = b.Add(0, FactionId.Inhumanus, Archetype.Commander, 0, 2);
            int warden = b.Add(1, FactionId.Craigon, Archetype.Guardian, 0, 0);
            b.Add(1, FactionId.Craigon, Archetype.Infantry, 1, -1);
            var s = b.Build();
            var target = s.Unit(b.Id(warden));
            target.Guarded = true;
            Scenario.Ok(s, Command.UseAbility(0, b.Id(archon), AbilityId.Lien, new Hex(0, 0)));
            Assert.IsTrue(target.HasStatus(StatusType.Lien));
            Assert.IsFalse(target.Guarded, "Lien strips the Guard token");
            StringAssert.Contains("Lien", Scenario.Rejected(s, Command.Guard(1, target.Id)));
            StringAssert.Contains("Lien", Scenario.Rejected(s, Command.UseAbility(1, target.Id, AbilityId.MindShield, new Hex(1, -1))));
            Scenario.Ok(s, Command.UseAbility(1, target.Id, AbilityId.SettleDebt, target.Position));
            Assert.IsFalse(target.HasStatus(StatusType.Lien));
        }

        [Test]
        public void Inhumanus_Lien_VoidedWhenHolderDies_AndExpires()
        {
            var b = new Scenario();
            int lancer = b.Add(0, FactionId.Inhumanus, Archetype.Gunner, 0, 2);
            int victim = b.Add(1, FactionId.Meeples, Archetype.Gunner, 0, -1);
            var s = b.Build();
            Scenario.Ok(s, Command.UseAbility(0, b.Id(lancer), AbilityId.Lien, new Hex(0, -1)));
            Scenario.Ok(s, Command.Attack(1, b.Id(victim), new Hex(0, 2))); // 2 dmg kills the 2 HP lancer
            Assert.IsFalse(s.Unit(b.Id(lancer)).Alive);
            Assert.IsFalse(s.Unit(b.Id(victim)).HasStatus(StatusType.Lien), "destroying the lien holder voids the lien");

            var b2 = new Scenario();
            int archon = b2.Add(0, FactionId.Inhumanus, Archetype.Commander, 0, 2);
            int v2 = b2.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var s2 = b2.Build();
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(archon), AbilityId.Lien, new Hex(0, 0)));
            for (int i = 0; i < 3; i++) Scenario.PassTurn(s2);
            Assert.IsTrue(s2.Unit(b2.Id(v2)).HasStatus(StatusType.Lien));
            Scenario.PassTurn(s2);
            Assert.IsFalse(s2.Unit(b2.Id(v2)).HasStatus(StatusType.Lien), "Lien lasts 5 activations");
        }

        [Test]
        public void Inhumanus_Doctrines_ChangeTheArmy()
        {
            var b = new Scenario { Doctrines = new[] { Doctrine.Defense, Doctrine.Assault } };
            int cmd = b.Add(0, FactionId.Inhumanus, Archetype.Commander, 0, 2);
            var cfgState = b.Build();
            Assert.AreEqual(6, cfgState.Unit(b.Id(cmd)).MaxHp, "Defense doctrine: Commander +1 HP");

            var s2 = RulesEngine.CreateMatch(new MatchConfig
            {
                Players = new[]
                {
                    new PlayerSetup { Army = ArmyPresets.For(FactionId.Inhumanus).First(a => a.Doctrine == Doctrine.Defense).Clone(), Caps = new TechCaps(3) },
                    new PlayerSetup { Army = ArmyPresets.Default(FactionId.Meeples).Clone(), Caps = new TechCaps(3) },
                },
            });
            Scenario.Ok(s2, Command.AutoDeploy(0));
            Scenario.Ok(s2, Command.AutoDeploy(1));
            Assert.IsTrue(s2.LiveUnits(0).All(u => u.Guarded), "Defense doctrine starts the battle Guarded");

            var b3 = new Scenario { Doctrines = new[] { Doctrine.Control, Doctrine.Assault } };
            int ctl = b3.Add(0, FactionId.Inhumanus, Archetype.Commander, 0, 3);
            int runner = b3.Add(1, FactionId.Meeples, Archetype.Raider, 0, -1);
            var s3 = b3.Build();
            Assert.AreEqual(3, RulesEngine.ChargesLeft(s3.Unit(b3.Id(ctl)), AbilityId.Lien), "Control: +1 Lien charge");
            Assert.AreEqual(4, RulesEngine.AbilityRange(s3, s3.Unit(b3.Id(ctl)), AbilityId.Lien), "Control: +1 Lien range");
            Scenario.Ok(s3, Command.UseAbility(0, b3.Id(ctl), AbilityId.Lien, new Hex(0, -1)));
            Assert.AreEqual(1, BoardQueries.EffectiveMove(s3, s3.Unit(b3.Id(runner))), "Control: liened units move at most 1");
        }

        // ------------------------------------------------------------------ Space Pirates: Salvage
        [Test]
        public void Pirates_Salvage_RepairOrRig_ConsumesWreck()
        {
            var b = new Scenario();
            int deck = b.Add(0, FactionId.SpacePirates, Archetype.Guardian, 0, 1);
            int gun = b.Add(0, FactionId.SpacePirates, Archetype.Gunner, -1, 2);
            int victim = b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            b.WithHp(deck, 2);
            var s = b.Build();
            StringAssert.Contains("no wreck", Scenario.Rejected(s, Command.UseAbility(0, b.Id(deck), AbilityId.Salvage, new Hex(0, 0))));
            Scenario.Ok(s, Command.Attack(0, b.Id(gun), new Hex(0, 0))); // kills the infantry → wreck
            Assert.AreEqual(0, s.WreckIndex(new Hex(0, 0)));
            Scenario.PassTurn(s);
            Scenario.Ok(s, Command.UseAbility(0, b.Id(deck), AbilityId.Salvage, new Hex(0, 0), (int)SalvageChoice.Repair));
            Assert.AreEqual(4, s.Unit(b.Id(deck)).Hp);
            Assert.AreEqual(-1, s.WreckIndex(new Hex(0, 0)));
            Assert.IsFalse(s.Unit(b.Id(victim)).Alive);
        }

        [Test]
        public void Pirates_Salvage_RigWeapon_AddsDamageOnce()
        {
            var b = new Scenario();
            int axe = b.Add(0, FactionId.SpacePirates, Archetype.Infantry, 0, 1);
            int tgt = b.Add(1, FactionId.Silex, Archetype.Guardian, 0, 0);
            var s = b.Build();
            s.Wrecks.Add(new Wreck { Hex = new Hex(1, 1), Faction = FactionId.Meeples, Archetype = Archetype.Infantry });
            Scenario.Ok(s, Command.UseAbility(0, b.Id(axe), AbilityId.Salvage, new Hex(1, 1), (int)SalvageChoice.RigWeapon));
            Assert.IsTrue(s.Unit(b.Id(axe)).HasStatus(StatusType.JuryRigged));
            Scenario.PassTurn(s);
            var o = Combat.Resolve(s, s.Unit(b.Id(axe)), new Hex(0, 0));
            Assert.IsTrue(o.JuryRigConsumed);
            Assert.AreEqual(1, o.FinalDamage, "1 + 1 rig - 1 armor");
            Scenario.Ok(s, Command.Attack(0, b.Id(axe), new Hex(0, 0)));
            Assert.IsFalse(s.Unit(b.Id(axe)).HasStatus(StatusType.JuryRigged));
        }

        // ------------------------------------------------------------------ supporting abilities
        [Test]
        public void Support_MindShieldSmokeRelocationPatchUp()
        {
            var b = new Scenario();
            int warden = b.Add(0, FactionId.Craigon, Archetype.Guardian, 0, 1);
            int ally = b.Add(0, FactionId.Craigon, Archetype.Infantry, 1, 1);
            var s = b.Build();
            Scenario.Ok(s, Command.UseAbility(0, b.Id(warden), AbilityId.MindShield, new Hex(1, 1)));
            Assert.IsTrue(s.Unit(b.Id(ally)).HasStatus(StatusType.Ward));

            var b2 = new Scenario();
            int engine = b2.Add(0, FactionId.Pygmaei, Archetype.Guardian, 0, 2);
            int gun = b2.Add(1, FactionId.Meeples, Archetype.Gunner, 0, -1);
            b2.Add(0, FactionId.Pygmaei, Archetype.Infantry, 0, 1);
            var s2 = b2.Build();
            Scenario.Ok(s2, Command.UseAbility(0, b2.Id(engine), AbilityId.Smoke, new Hex(0, 0)));
            Assert.AreEqual(LosState.BlockedBySmoke, Combat.Resolve(s2, s2.Unit(b2.Id(gun)), new Hex(0, 1)).Los);

            var b3 = new Scenario();
            int liab = b3.Add(0, FactionId.Inhumanus, Archetype.Guardian, 0, 1);
            int cmd = b3.Add(0, FactionId.Inhumanus, Archetype.Commander, 0, 0);
            var s3 = b3.Build();
            Scenario.Ok(s3, Command.UseAbility(0, b3.Id(liab), AbilityId.AssetRelocation, new Hex(0, 0)));
            Assert.AreEqual(new Hex(0, 1), s3.Unit(b3.Id(cmd)).Position);
            Assert.AreEqual(new Hex(0, 0), s3.Unit(b3.Id(liab)).Position);

            var b4 = new Scenario();
            int medic = b4.Add(0, FactionId.Meeples, Archetype.Raider, 0, 1, "gen.u.medkit");
            b4.WithHp(medic, 1);
            var s4 = b4.Build();
            Scenario.Ok(s4, Command.UseAbility(0, b4.Id(medic), AbilityId.PatchUp, new Hex(0, 1)));
            Assert.AreEqual(2, s4.Unit(b4.Id(medic)).Hp);
            Scenario.PassTurn(s4);
            StringAssert.Contains("no charges", Scenario.Rejected(s4, Command.UseAbility(0, b4.Id(medic), AbilityId.PatchUp, new Hex(0, 1))));
        }

        [Test]
        public void Unbreakable_SurvivesFirstLethalHit()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int heart = b.Add(1, FactionId.Silex, Archetype.Guardian, 0, 0, "sil.d.monolith_heart");
            b.WithHp(heart, 1);
            var s = b.Build();
            Scenario.Ok(s, Command.Attack(0, b.Id(gun), new Hex(0, 0)));
            Assert.IsTrue(s.Unit(b.Id(heart)).Alive);
            Assert.AreEqual(1, s.Unit(b.Id(heart)).Hp);
            Assert.IsFalse(s.Unit(b.Id(heart)).HasStatus(StatusType.Unbreakable));
        }
    }
}
