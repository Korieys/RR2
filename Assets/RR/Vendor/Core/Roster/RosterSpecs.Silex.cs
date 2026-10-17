namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static void Silex()
        {
            UnitFilter silexAlly = (s, src, t) => t.Def.Faction == FactionId.Silex;
            UnitFilter cmdOrFort = (s, src, t) => IsLeader(s, t) || t.Def.Roster.Role == UnitRole.Defender || t.Def.Roster.Role == UnitRole.SpaceBastion
                                                  || t.Def.Roster.Role == UnitRole.VehicleSiege;

            // ---------------------------------------------------------------- planetary
            Def("silex_shard_retainer",
                new ActiveSpec { Target = TargetKind.Ally, Filter = silexAlly, FilterText = "Silex ally", Grants = G(ProtectBy(1)), Engine = "Target: an adjacent ally. Until this unit's next activation, the first direct attack on it is redirected to this unit." },
                P(PassiveKind.ArmorIfAdjacentAllyStationary, "+1 ARM while adjacent to another Silex unit, if it did not move during its most recent activation."));
            Def("silex_shard_sentinel",
                new ActiveSpec { Target = TargetKind.Ally, Grants = G(ShieldOwnNext(2)), Engine = "Target: an adjacent ally. 2 temporary shield HP until the end of that ally's next activation." },
                P(PassiveKind.ForcedMoveImmune, "Cannot be pushed or pulled except by siege vehicles."));
            Def("silex_lithic_strider",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 2, Engine = "Leap to an empty hex within 2, over units and low cover." },
                P(PassiveKind.IgnoreRough, "Rough hexes cost 1 movement instead of 2."));
            Def("silex_obelisk_guard",
                new ActiveSpec { Target = TargetKind.Hex, BarrierHp = 3, BarrierRounds = 2, BarrierBlocking = true, Engine = "Raise a 3-HP crystal wall on an adjacent empty hex for 2 rounds: it blocks movement and line of sight until destroyed." },
                P(PassiveKind.ForcedMoveImmuneVsInfantry, "Cannot be pushed or pulled by non-siege infantry."));
            Def("silex_bastion_keeper",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Filter = Infantry, FilterText = "allied infantry", Grants = G(ProtectBy(2, 0, RExpire.RoundEnd)), Special = "EachIntercepts",
                    Engine = "Up to two allied infantry within 2 are guarded until the end of the round: this unit intercepts one direct attack on each (two intercepts this round)." },
                P(PassiveKind.ArmorOnCoverOrObjective, "+1 ARM while standing on a cover, barrier or objective hex."));
            Def("silex_resonance_adept",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Heal = 3, Engine = "Target: a Silex ally within 2 (itself included). Restore 3 HP, never above maximum." },
                P(PassiveKind.AuraDisableWard, "Allied Silex within 1 ignore the first DISABLE applied to them each round."));
            Def("silex_prism_herald",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Grants = G(new Grant(RStatusType.Reflect, 2, RExpire.SourceNextActivation, 1, uses: 1)), Engine = "Target: an ally within 2. Until this unit's next activation, the next ranged basic attack that damages it takes 2 damage back (no chain)." },
                P(PassiveKind.AuraCloakSight, "Allied Silex within 2 can target cloaked enemies up to 3 hexes away (instead of 2).", range: 2));
            Def("silex_faultline_breaker",
                new ActiveSpec { Target = TargetKind.Hex, Damage = 2, Special = "Line2", Engine = "Pick an adjacent hex to set a direction: 2 damage to every unit on that hex and the next one in the same straight line (allies included, never itself)." },
                P(PassiveKind.PierceVsFortified, "Ignores 1 ARM against defenders, bastions and units standing on cover or barrier hexes; +1 damage against barriers."));
            Def("silex_geode_colossus",
                new ActiveSpec { Target = TargetKind.Ally, Grants = G(ProtectBy(2, 2)), Engine = "Target: an ally within 2. Until its next activation, this unit intercepts the first direct attack on it, and that hit deals 2 less raw damage to the Colossus." },
                P(PassiveKind.ForcedMoveAndRootImmuneBelowElite, "Cannot be pushed, pulled or ROOTED by units below elite class (elites, commanders, siege vehicles and capital ships can)."));
            Def("silex_crown_warden",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(MarkG(2)), Engine = "Target: an enemy within 3. MARKED: the next friendly attack on it deals +2 raw damage (lasts until used or the end of next round)." },
                P(PassiveKind.AuraDamageToRole, "Adjacent allied defenders gain +1 raw damage.", role: UnitRole.Defender));
            Def("silex_facet_mason",
                new ActiveSpec { Target = TargetKind.Hex, BarrierHp = 2, BarrierRounds = 2, BarrierBlocking = true, Special = "RebuildFacet", Engine = "Either restore 3 HP to a friendly barrier within 1, or raise a new 2-HP crystal wall on an adjacent empty hex for 2 rounds." },
                P(PassiveKind.RevealMinesAtStart, "At the start of its activation, enemy mines within 2 are revealed until the end of the round.", range: 2));
            Def("silex_royal_castellan",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Filter = silexAlly, FilterText = "Silex ally", Grants = G(GuardianRole()), Engine = "Up to two Silex allies within 3 gain GUARD until the end of their next activation: each may intercept one direct attack on an adjacent ally." },
                P(PassiveKind.LeaderGuardAura, "While it is your leader, allied units within 1 that can GUARD may intercept a direct attack on it (each once per round).", range: 1));
            Def("silex_bastion_sled",
                new ActiveSpec { Target = TargetKind.Hex, BarrierHp = 3, BarrierRounds = 3, BarrierBlocking = false, Engine = "Raise a 3-HP crystal cover field on an adjacent empty hex for 3 rounds (low cover for the unit in it)." },
                P(PassiveKind.AuraCover, "Adjacent allied infantry count as in low cover against ranged attacks.", role: UnitRole.Line));
            Def("silex_facet_ram",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 3, StrikeDamage = 3, Special = "StrikeBarrier", Engine = "Charge up to 3 hexes in a straight line, then deal 3 damage to one adjacent enemy or enemy barrier at the endpoint." },
                P(PassiveKind.CounterReduction, "Counter-damage it takes during its own activation is reduced by 1."));
            Def("silex_geode_walker",
                new ActiveSpec { Target = TargetKind.Self, SelfGrants = G(ShieldUntilSource(4), new Grant(RStatusType.Rooted, 0, RExpire.AfterOwnActivation, 1)), Engine = "Gain 4 temporary shield HP until its next activation begins; it cannot move during its next activation." },
                P(PassiveKind.RangeIfStationary, "+1 basic attack range (maximum 5) if it did not move during its most recent activation and has not moved this activation."));
            Def("silex_throne_engine",
                new ActiveSpec { Target = TargetKind.Ally, Filter = cmdOrFort, FilterText = "leader, defender, bastion or siege unit", Grants = G(ProtectBy(3), ShieldUntilSource(2)),
                    Engine = "Target: your leader or a defender/siege unit within 3. Until this engine's next activation it intercepts the first direct attack on it, and the target gains 2 temporary shield HP." },
                P(PassiveKind.AuraArmorInfantryUnmoved, "Allied infantry within 2 that have not moved this round gain +1 ARM.", range: 2));

            // ---------------------------------------------------------------- orbital
            Def("silex_facet_surveyor",
                new ActiveSpec { Target = TargetKind.Self, Special = "RevealArea", Engine = "Enemy cloaked ships and mines within 4 are REVEALED until the end of the round." },
                P(PassiveKind.NebulaImmune, "Ignores the nebula range penalty."));
            Def("silex_prism_escort",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "adjacent allied ship", Grants = G(ProtectBy(1)), Special = "BasicOnly", Range = 1,
                    Engine = "Target: an adjacent allied ship. Until this escort's next activation, the first basic attack on it is redirected to this escort." },
                P(PassiveKind.ReduceFirstFighterHit, "The first hit by a fighter-class attacker each round deals 1 less damage."));
            Def("silex_lattice_corvette",
                new ActiveSpec { Target = TargetKind.Ally, Filter = SmallerThanSelf, FilterText = "smaller allied ship", Grants = G(ShieldRound(2, 2)), Engine = "Target: an allied ship smaller than this corvette within 2. 2 temporary shield HP until the end of the next round." },
                P(PassiveKind.ArmorIfAdjacentBigShip, "+1 ARM while adjacent to an allied ship with 10 or more max HP."));
            Def("silex_tessera_frigate",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Grants = G(new Grant(RStatusType.Redirect, 2, RExpire.SourceNextActivation, 1, uses: 1)), Engine = "Target: an ally within 2. Until this frigate's next activation, the next 2 damage that ally would take is sent back to the attacker instead." },
                P(PassiveKind.ArmorStationaryVsUnmarkedRanged, "+1 ARM against ranged basic attacks while it is not MARKED, if it did not move during its most recent activation."));
            Def("silex_pillar_destroyer",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, MinRange = 2, Damage = 5, Pierce = 2, RequiresNoMove = true, Engine = "Only if it has not moved this activation. A visible enemy at range 2–4 takes 5 damage, ignoring 2 ARM." },
                P(PassiveKind.ForcedMoveImmuneSmallSource, "Cannot be moved by abilities of ships smaller than a cruiser."));
            Def("silex_obelisk_cruiser",
                new ActiveSpec { Target = TargetKind.Hex, MaxTargets = 2, MinTargets = 2, BarrierHp = 3, BarrierRounds = 2, BarrierBlocking = true, Special = "TwoAdjoining",
                    Engine = "Pick two adjoining empty hexes within 2: each becomes a 3-HP crystal wall blocking movement and sight for 2 rounds." },
                P(PassiveKind.SelfHealIfIdle, "Restores 1 HP at the end of an activation in which it neither moved nor attacked."));
            Def("silex_crown_bastion",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, AllowSelf = true, Filter = Ship, FilterText = "allied ship", Grants = G(ShieldUntilSource(2)), Engine = "Up to three allied ships within 3 (itself included) gain 2 temporary shield HP until this bastion's next activation." },
                P(PassiveKind.AuraArmorWhileStationary, "Adjacent allied ships gain +1 ARM while this bastion did not move during its most recent activation."));
            Def("silex_monolith_battleship",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 4, Splash = 2, Engine = "Target: a hex within 5 in line of sight. 4 damage to the unit there and 2 to every unit on the adjacent hexes (allies included)." },
                P(PassiveKind.InertialMass, "Immune to forced movement, and to ROOT from fighter-class ships."));
            Def("silex_citadel_dreadnought",
                new ActiveSpec { Target = TargetKind.Ally, Filter = LeaderOnly, FilterText = "your leader", Grants = G(ProtectBy(2, 3)), Engine = "Target: your leader within 2. Until this ship's next activation it intercepts the first direct attack on the leader, and that hit deals 3 less raw damage." },
                P(PassiveKind.ArmorIfStationary, "+1 ARM if it did not move during its most recent activation (ARM never exceeds 4)."));
            Def("silex_throne_ark",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Grants = G(ShieldUntilSource(2)), Options = new[] { "2 shield HP each", "+1 next-attack damage each" }, Special = "OptionShieldOrAttack",
                    Engine = "Up to three allies within 4 each gain either 2 temporary shield HP (until this ship's next activation) or +1 raw damage on their next attack — choose one option for all." },
                Inert("Seat of Empire only restates that armor passives check each ship's own movement and that the Ark is a killable leader; the engine already works that way."));
        }
    }
}
