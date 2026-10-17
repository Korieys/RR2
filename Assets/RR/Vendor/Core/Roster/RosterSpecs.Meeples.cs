namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static void Meeples()
        {
            // ---------------------------------------------------------------- planetary
            Def("meeples_council_infantry",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(Suppress(1)), Engine = "Target: a visible enemy within 2. Its movement is reduced by 1 (minimum 1) during its next activation." },
                Inert("Council Standard needs a non-Meeple coalition infantry ally (a campaign roster tag); single-faction battles have none."));
            Def("meeples_pact_scout",
                new ActiveSpec { Target = TargetKind.Self, Special = "RevealArea", Engine = "Enemy cloaked and hidden units and mines within 4 are REVEALED until the end of the round." },
                P(PassiveKind.PassThroughAlly, "Its movement may pass through one hex occupied by an ally (it cannot end there)."));
            Def("meeples_accord_ranger",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(MarkG(1, 1, 1)), Engine = "Target: a visible enemy within 3. MARKED: the next friendly hit on it this round deals +1 raw damage." },
                P(PassiveKind.IgnoreCoverFromCover, "While it stands on a cover hex, its ranged attacks ignore 1 point of cover reduction."));
            Def("meeples_accord_marksman",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 4, Pierce = 1, Engine = "A visible enemy within 5 takes 4 damage ignoring 1 ARM." },
                P(PassiveKind.DamageIfNotMoved, "+1 raw damage on its attacks if it has not moved this activation."));
            Def("meeples_council_guard",
                new ActiveSpec { Target = TargetKind.Ally, Grants = G(ProtectBy(1)), Engine = "Target: an adjacent ally. Until this unit's next activation, the first direct attack on it is redirected to this unit." },
                P(PassiveKind.GuardReductionForLeader, "When it intercepts an attack aimed at your leader, it takes 1 less damage from it."));
            Def("meeples_concord_sentinel",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, AllowSelf = true, Filter = Infantry, FilterText = "friendly infantry", Grants = G(ShieldUntilSource(2)), Engine = "Up to two friendly infantry within 2 (itself included) gain 2 temporary shield HP until this unit's next activation." },
                P(PassiveKind.AuraSuppressWard, "Once per round, an adjacent ally ignores a SUPPRESS movement reduction."));
            Def("meeples_honor_guard",
                new ActiveSpec { Target = TargetKind.Ally, Filter = LeaderOnly, FilterText = "your leader", Grants = G(ProtectBy(2, 2)), Engine = "Target: your leader within 2. Until this unit's next activation it intercepts the first direct attack on the leader, and that hit deals 2 less raw damage." },
                P(PassiveKind.NoDamagingActiveOnCaptured, "Oathkeeper: it never targets captured units with damaging abilities (it has none, so this is a restriction only)."));
            Def("meeples_coalition_marshal",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Grants = G(NextAttack(1), ArmorUntilSource(1)), Engine = "Up to two allies within 3 gain +1 raw damage on their next attack and +1 ARM until this unit's next activation." },
                Inert("Common Cause lets allies of different coalition races share GUARD; single-faction battles have only one race."));
            Def("meeples_mediation_envoy",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(Silence), Engine = "Target: an enemy within 2. During its next activation it cannot attack or use its active (it may still move)." },
                P(PassiveKind.ShieldAtStartIfNotAttacked, "If no enemy attacked it this round, it gains 2 temporary shield HP at the start of its activation.", amount: 2));
            Def("meeples_barrier_medic",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Filter = Infantry, FilterText = "friendly infantry", Heal = 3, Options = new[] { "Heal 3 HP", "Clear ROOTED" }, Special = "OptionHealOrUnroot",
                    Engine = "Target: friendly infantry within 2 (itself included). Restore 3 HP, or remove ROOTED instead." },
                P(PassiveKind.MarkAttackerIfGuardNear, "An enemy that attacks it while an allied Council Guard is within 2 becomes MARKED (+1 on the next friendly attack against it)."));
            Def("meeples_assembly_engineer",
                new ActiveSpec { Target = TargetKind.Hex, BarrierHp = 3, BarrierRounds = 2, BarrierBlocking = false, Engine = "Raise a 3-HP cover field on an adjacent empty hex for 2 rounds (low cover for the unit in it)." },
                P(PassiveKind.RepairBonusAura, "Adjacent allied vehicles repaired by any allied engineer receive +1 HP more (once per round each)."));
            Def("meeples_peacekeeper_captain",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, AllowSelf = false, Grants = G(GuardianRole()), Engine = "Up to two friendly units within 3 gain GUARD until the end of their next activation: each may intercept one direct attack on an adjacent ally." },
                P(PassiveKind.ArmorIfNoAlliedKill, "If your side has destroyed no enemy this round when it ends its activation, it gains +1 ARM until its next activation."));
            Def("meeples_peacekeeper_carrier",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Infantry, FilterText = "adjacent allied infantry", Range = 1, Special = "Evacuate", Engine = "Pick an adjacent allied infantry unit, then any empty hex within 2 of this vehicle: move it there." },
                P(PassiveKind.HealAllyEndingAdjacent, "An allied infantry unit that ends a move adjacent to it heals 1 HP (once per round each)."));
            Def("meeples_accord_strider",
                new ActiveSpec { Target = TargetKind.Hex, Special = "RevealCorridor", Engine = "Pick an adjacent hex to set a direction: enemy hidden units and mines on the 4 hexes in that straight line are REVEALED until the end of the round." },
                P(PassiveKind.IgnoreRough, "Rough hexes cost 1 movement instead of 2."));
            Def("meeples_citadel_shield_walker",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Grants = G(ShieldUntilSource(3)), Engine = "Target: a friendly unit within 2 (itself included). 3 temporary shield HP until this vehicle's next activation." },
                P(PassiveKind.AuraCover, "Adjacent allied infantry count as in low cover against ranged attacks.", role: UnitRole.Line));
            Def("meeples_unity_command_platform",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Grants = G(AttackThisRound(1)), Options = new[] { "+1 damage this round", "2 shield HP each" }, Special = "OptionAttackOrShield",
                    Engine = "Up to three allies within 4 each gain either +1 raw damage on every attack this round, or 2 temporary shield HP until this unit's next activation." },
                P(PassiveKind.Spotter, "Allied units within 3 may target cloaked enemies this unit can target (shared spotting).", range: 3));

            // ---------------------------------------------------------------- orbital
            Def("meeples_council_scoutship",
                new ActiveSpec { Target = TargetKind.Self, Special = "RevealArea", Engine = "Enemy cloaked ships and mines within 4 are REVEALED until the end of the round." },
                P(PassiveKind.AuraNebulaImmune, "Adjacent allied ships ignore the nebula range penalty."));
            Def("meeples_covenant_interceptor",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "allied ship", Grants = G(InterceptBy(1)), Special = "JumpAdjacent", Engine = "Pick an allied ship within 2: jump to an empty hex adjacent to it; until this interceptor's next activation it takes the first fighter-class attack on that ship." },
                P(PassiveKind.DamageVsLeaderThreat, "+1 basic attack damage against enemies whose basic attack can reach your leader."));
            Def("meeples_council_corvette",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = Ship, FilterText = "enemy ship", Grants = G(Suppress(1)), Engine = "Target: a visible enemy ship within 3. Its movement is reduced by 1 (minimum 1) during its next activation." },
                P(PassiveKind.AuraReduceForCarrier, "Attacks on an adjacent allied carrier deal 1 less raw damage."));
            Def("meeples_accord_frigate",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "allied ship", Grants = G(ProtectBy(2)), Special = "RangedOnly", Engine = "Target: an allied ship within 2. Until this frigate's next activation, the first direct ranged attack on it is redirected to this frigate." },
                P(PassiveKind.DebuffWardOnce, "The first non-damaging debuff applied to it in the battle has no effect."));
            Def("meeples_concord_destroyer",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(MarkG(2)), Engine = "Target: a visible enemy within 4. MARKED: the next allied attack on it deals +2 raw damage." },
                Inert("Disciplined Fire's optional mercy shot gives no in-battle advantage, so the engine does not offer it."));
            Def("meeples_assembly_cruiser",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, MinTargets = 1, Grants = G(ShieldUntilSource(2)), Engine = "Up to two different allies within 3 gain 2 temporary shield HP until this cruiser's next activation." },
                P(PassiveKind.AuraHealAtStart, "At the start of its activation, adjacent allied corvettes heal 1 HP.", role: UnitRole.Corvette));
            Def("meeples_unity_carrier",
                new ActiveSpec { Target = TargetKind.Hex, TokenName = "token_meeples_fighter", TokenCap = 2, Engine = "Deploy a 2-HP Council fighter token on an adjacent empty hex; at most two living tokens from this carrier." },
                P(PassiveKind.AuraMoveSmallCraft, "Allied fighters and tokens within 2 gain +1 movement.", range: 2));
            Def("meeples_coalition_battleship",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 7, Engine = "A visible (or Revealed) enemy within 5 takes a 7-damage attack." },
                P(PassiveKind.DamageIfAdjacentFrigate, "+1 raw damage while adjacent to an allied frigate."));
            Def("meeples_sanctuary_hospital_ship",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Filter = Ship, FilterText = "allied ship", Heal = 4, Special = "ClearDisabled", Engine = "Target: an allied ship within 3 (itself included). Restore 4 HP and clear DISABLED." },
                P(PassiveKind.NeutralEmblem, "It cannot gain attack buffs; if it did not move during its most recent activation it gains 2 temporary shield HP at the start of its activation.", amount: 2));
            Def("meeples_council_command_ark",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Grants = G(ShieldUntilSource(2), MoveNext(1)), Engine = "Up to three allies within 4 gain 2 temporary shield HP (until this ship's next activation) and +1 movement during their next activation." },
                P(PassiveKind.LeaderGuardAura, "While it is your leader, allied units within 2 that can GUARD may intercept a direct attack on it (each once per round).", range: 2));
        }
    }
}
