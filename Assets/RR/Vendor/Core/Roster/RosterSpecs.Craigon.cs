namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static readonly UnitFilter Bio = (s, src, t) => t.Def.Faction == FactionId.Craigon || t.Def.Roster.HasTag("BIO");

        static void Craigon()
        {
            // ---------------------------------------------------------------- planetary
            Def("craigon_carapace_drone",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(Root), Engine = "Target: an adjacent enemy. It is ROOTED (cannot move) through its next activation." },
                P(PassiveKind.ArmorIfAdjacentAlly, "+1 ARM while adjacent to another allied Cräigon infantry unit.", role: UnitRole.Line));
            Def("craigon_shellbound",
                new ActiveSpec { Target = TargetKind.Ally, Grants = G(ProtectBy(1)), Engine = "Target: an adjacent ally. Until this unit's next activation, the first direct attack on that ally is redirected to this unit (range 1)." },
                P(PassiveKind.ArmorIfStationary, "+1 ARM if it did not move during its most recent activation."));
            Def("craigon_tide_skimmer",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 2, StrikeDamage = 1, Engine = "Jump to an empty hex within 2 (over units), then strike one adjacent enemy for 1 raw damage (optional)." },
                P(PassiveKind.IgnoreRoughOnce, "The first rough hex it enters each activation costs 1 instead of 2."));
            Def("craigon_synapse_stalker",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(Disable), Engine = "Target: an adjacent enemy. It is DISABLED (cannot use its active) during its next activation." },
                P(PassiveKind.HiddenBeyond, "Enemies more than 2 hexes away cannot target it unless it is Revealed or has attacked this round.", range: 2));
            Def("craigon_pincer_guard",
                new ActiveSpec { Target = TargetKind.Enemy, Damage = 1, Grants = G(Root), Engine = "Target: an adjacent enemy. Deal 1 raw damage, then ROOT it through its next activation." },
                P(PassiveKind.GuardLeader, "Once per round, automatically intercepts a direct attack on your adjacent leader (the hit is resolved against this unit)."));
            Def("craigon_carapace_custodian",
                new ActiveSpec { Target = TargetKind.Hex, BarrierHp = 3, BarrierRounds = 2, BarrierBlocking = false, Engine = "Raise a 3-HP cover field on an adjacent empty hex for 2 rounds: a unit standing in it has low cover (−1 ranged damage). Enemies can shoot an empty field." },
                P(PassiveKind.AuraCover, "Adjacent allies count as in low cover against ranged attacks (one extra point at most, never stacking with another aura)."));
            Def("craigon_neural_spiker",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 1, Pierce = 99, Grants = G(Disable), Engine = "Target: a visible enemy within 4. Deal 1 damage ignoring all ARM, and DISABLE it for its next activation." },
                P(PassiveKind.AttackViaSpotter, "Its basic attack ignores line of sight to a target that an allied Scout can see (range still applies)."));
            Def("craigon_harmonic_disruptor",
                new ActiveSpec { Target = TargetKind.Self, Grants = G(new Grant(RStatusType.AttackDebuff, 1, RExpire.AfterOwnActivation, 1, uses: 1)), Special = "AllEnemiesInRange",
                    Engine = "Every enemy within 2 deals 1 less raw damage on its next attack (minimum raw 1)." },
                P(PassiveKind.AuraIgnoreCover, "Adjacent allies' ranged attacks ignore 1 point of cover reduction."));
            Def("craigon_assimilator",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = NonLeader, FilterText = "non-leader at 2 HP or less", HpThreshold = 2, Engine = "Target: an adjacent non-leader enemy at 2 HP or less. It becomes CAPTURED: it stays on the board but cannot act, move or protect until rescued." },
                P(PassiveKind.MercifulKill, "When its attack would destroy a non-leader, the target is left at 1 HP and ROOTED instead (ready for The Gift)."));
            Def("craigon_giftbearer",
                new ActiveSpec { Target = TargetKind.Enemy, Special = "ConversionRite", Engine = "Target: an adjacent CAPTURED enemy. If the Giftbearer is still alive and adjacent and the unit is still captured at the end of the next round, it becomes a 1-HP Cräigon auxiliary under your control." },
                P(PassiveKind.BlockRescue, "Captured enemies adjacent to it cannot be rescued."));
            Def("craigon_fleshweaver",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Filter = Bio, FilterText = "biological ally", Heal = 3, Engine = "Target: an allied biological unit within 2 (itself included). Restore 3 HP, never above maximum." },
                P(PassiveKind.AuraHealAfterActivation, "After its activation, adjacent allied biological units that have not attacked this round recover 1 HP (once per round each)."));
            Def("craigon_evolved_praetor",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Filter = Bio, FilterText = "biological ally", Grants = G(NextAttack(1, RExpire.RoundEnd)), Engine = "Up to two allied biological units within 3 gain +1 raw damage on their next attack this round." },
                P(PassiveKind.LeaderGuardAura, "While it is your leader, allied units within 2 that can GUARD may intercept a direct attack on it (each once per round).", range: 2));
            Def("craigon_specimen_crawler",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = NonHeavy, FilterText = "non-heavy enemy", Engine = "Target: a visible non-heavy enemy within 2. Pull it 1 hex toward this vehicle if that hex is empty." },
                P(PassiveKind.BlockRescue, "Captured enemies adjacent to it cannot be rescued."));
            Def("craigon_chitin_walker",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 2, StrikeDamage = 2, Engine = "Charge up to 2 hexes in a straight line, then deal 2 raw damage to one adjacent enemy at the endpoint." },
                Inert("Low cover never costs extra movement in these rules, so Jointed Frame has nothing to ignore."));
            Def("craigon_convergence_hauler",
                new ActiveSpec { Target = TargetKind.Ally, Special = "MassExtraction", Engine = "Pick an allied infantry unit or a CAPTURED unit within 2, then an empty hex adjacent to the Hauler: move it there." },
                P(PassiveKind.AuraAbilityRange, "A friendly Assimilator within 2 uses The Gift at range 2.", range: 2));
            Def("craigon_ascension_engine",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 3, Splash = 1, Engine = "Target: a visible enemy within 4. Deal 3 damage to it and 1 to every unit on the six adjacent hexes (allies included)." },
                P(PassiveKind.IgnoreCover, "Its basic attack ignores 1 point of cover reduction."));

            // ---------------------------------------------------------------- orbital
            Def("craigon_sporewing_fighter",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(new Grant(RStatusType.RangeDebuff, 1)), Engine = "Target: a visible enemy within 2. Its basic attack range is reduced by 1 (minimum 1) during its next activation." },
                P(PassiveKind.NebulaImmune, "Ignores the nebula range penalty."));
            Def("craigon_scuttler_interceptor",
                new ActiveSpec { Target = TargetKind.Ally, Grants = G(InterceptBy(2)), Engine = "Target: an ally within 2. Until this ship's next activation, the first attack on it by a fighter, interceptor or token is redirected to this ship." },
                P(PassiveKind.DamageVsSmallCraft, "+1 raw damage against fighters, interceptors, scouts and tokens."));
            Def("craigon_scuttler_corvette",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = SmallerThanCruiser, FilterText = "ship smaller than a cruiser", Grants = G(Root), Engine = "Target: a visible enemy ship smaller than a cruiser within 3. ROOTED through its next activation." },
                P(PassiveKind.DamageIfAdjacentRole, "+1 damage while adjacent to another allied corvette.", role: UnitRole.Corvette));
            Def("craigon_pincer_frigate",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = CorvetteOrSmaller, FilterText = "corvette or smaller", Engine = "Target: a visible enemy corvette or smaller within 3. Pull it 1 hex toward this ship if that hex is empty." },
                P(PassiveKind.LosThroughFriendlySmallCraft, "Its line of sight passes through one allied fighter, interceptor or token."));
            Def("craigon_communion_destroyer",
                new ActiveSpec { Target = TargetKind.Ally, Filter = SmallerThanSelf, FilterText = "smaller allied ship", Grants = G(NextAttack(2, RExpire.RoundEnd)), Engine = "Target: an allied ship smaller than this destroyer within 3. +2 raw damage on its next basic attack this round." },
                P(PassiveKind.Spotter, "Allied ships within 2 may target cloaked enemies this ship can target (shared spotting).", range: 2));
            Def("craigon_synaptic_cruiser",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = Ship, FilterText = "enemy ship", Grants = G(Disable), Engine = "Target: a visible enemy ship within 4. DISABLED during its next activation." },
                P(PassiveKind.AuraNebulaImmune, "Allied ships within 2 ignore the nebula range penalty.", range: 2));
            Def("craigon_chrysalis_carrier",
                new ActiveSpec { Target = TargetKind.Hex, TokenName = "token_craigon_sporewing", TokenCap = 2, Engine = "Deploy a 2-HP Sporewing token (basic 2 at range 1–2, STEP 2) on an adjacent empty hex; at most two living tokens from this carrier." },
                P(PassiveKind.AuraHealAfterActivation, "After it activates, adjacent allied fighters and tokens recover 1 HP (once per round each).", role: UnitRole.Fighter));
            Def("craigon_evolutionary_bastion",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "allied ship", Grants = G(ShieldUntilSource(3)), Engine = "Target: an allied ship within 2. 3 temporary shield HP until this ship's next activation." },
                P(PassiveKind.ArmorIfStationary, "+1 ARM if it did not move during its most recent activation (ARM never exceeds 4)."));
            Def("craigon_ascendant_ark",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Filter = Ship, FilterText = "allied ship", Grants = G(AttackThisRound(1)), Engine = "Up to three allied ships within 3 gain +1 raw damage on every attack for the rest of this round." },
                Inert("Resilient Genome leaves a biomass marker for after-battle recovery only; a single battle has no campaign layer."));
            Def("craigon_prime_synapse_flagship",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, MaxTargets = 2, Filter = Ship, FilterText = "enemy ship", Grants = G(Disable), Engine = "Up to two visible enemy ships within 5 are DISABLED during their next activation." },
                P(PassiveKind.Spotter, "Allied ships within 3 may target cloaked enemies this ship can target (shared spotting).", range: 3));
        }
    }
}
