namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static void Pygmaei()
        {
            // ---------------------------------------------------------------- planetary
            Def("pygmaei_rush_trooper",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 3, Special = "StepAway", Engine = "A visible enemy within 2 takes a 3-damage attack; then this unit steps 1 hex directly away from it if a hex is open." },
                P(PassiveKind.FreeStepOnKill, "Once per round, after it destroys an enemy it may step 1 free hex before ending its activation."));
            Def("pygmaei_ashrunner",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 3, StrikeDamage = 1, Engine = "Leap to an empty hex within 3, then strike one adjacent enemy for 1 raw damage (optional)." },
                P(PassiveKind.IgnoreRough, "Rough hexes cost 1 movement instead of 2."));
            Def("pygmaei_volt_striker",
                new ActiveSpec { Target = TargetKind.Enemy, Damage = 2, Pierce = 99, Grants = G(Disable), Engine = "Target: an adjacent enemy. 2 damage ignoring all ARM, and DISABLED during its next activation." },
                P(PassiveKind.DamageAfterLeap, "Its first attack in an activation in which it moved (LEAP) gains +1 raw damage."));
            Def("pygmaei_ember_vanguard",
                new ActiveSpec { Target = TargetKind.Hex, Damage = 2, EnemiesOnly = true, Special = "Line2Ignite", Engine = "Pick an adjacent hex to set a direction: 2 damage to enemies on that hex and the next in line, and both hexes burn until the end of the next round (1 damage to a unit ending a move there)." },
                P(PassiveKind.HazardImmune, "Takes no damage from hazard or burning hexes."));
            Def("pygmaei_breach_commando",
                new ActiveSpec { Target = TargetKind.Enemy, Damage = 2, Grants = G(Expose(1)), Engine = "Target: an adjacent enemy. 2 damage, then EXPOSED (−1 ARM) through its next activation." },
                P(PassiveKind.DamageVsBarrier, "Its basic attack deals +2 damage to barriers.", amount: 2));
            Def("pygmaei_starbreaker_gunner",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 6, Pierce = 2, Engine = "A visible enemy within 5 takes 6 damage ignoring 2 ARM (the first unit on the line absorbs it: line of sight applies)." },
                P(PassiveKind.DamageIfNotMoved, "+1 raw damage on its basic attack if it has not moved this activation.", basicOnly: true));
            Def("pygmaei_minejack_saboteur",
                new ActiveSpec { Target = TargetKind.Hex, Damage = 3, Special = "Mine", Engine = "Place a hidden 3-damage mine on an adjacent empty hex. The first enemy to enter it stops there and takes 3 damage (ARM applies)." },
                P(PassiveKind.DisarmMinesAtStart, "At the start of its activation it disarms enemy mines on adjacent hexes."));
            Def("pygmaei_forge_warden",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(Suppress(2)), Engine = "Target: a visible enemy within 3. Its movement is reduced by 2 (minimum 1) during its next activation." },
                P(PassiveKind.ReduceFirstRangedIfStationary, "If it did not move during its most recent activation, the first ranged hit on it each round deals 1 less damage."));
            Def("pygmaei_combat_engineer",
                new ActiveSpec { Target = TargetKind.Ally, Heal = 2, HealVehicle = 3, Engine = "Target: an ally within 2. Repair 3 HP on a vehicle or 2 HP on infantry, never above maximum." },
                Inert("Portable Foundry: your own mines never trigger on your units in these rules, so there is nothing extra to ignore."));
            Def("pygmaei_shock_medic",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Filter = Infantry, FilterText = "allied infantry", Heal = 2, Grants = G(MoveNext(1)), Engine = "Target: allied infantry within 2 (itself included). Restore 2 HP and +1 movement during its next activation." },
                P(PassiveKind.AuraHazardWard, "Allies within 1 ignore 1 point of hazard damage, once per round each."));
            Def("pygmaei_borglow_vanguard",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 3, StrikeDamage = 5, Engine = "Charge up to 3 hexes in a straight line, then deal 5 raw damage to one adjacent enemy at the endpoint." },
                P(PassiveKind.RootImmuneBelowTier4, "Cannot be ROOTED by enemy abilities from units below tier 4."));
            Def("pygmaei_iron_marshal",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Grants = G(NextAttack(1), MoveNext(1)), Engine = "Up to two allies within 3 gain +1 raw damage on their next attack and +1 movement during their next activation." },
                P(PassiveKind.AllyKillMoveAura, "When an allied unit within 3 of it destroys an enemy, allies adjacent to the killer gain +1 movement during their next activation.", range: 3));
            Def("pygmaei_razorbug_speeder",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 3, Engine = "Move to any empty hex within 3, over units. No attack." },
                P(PassiveKind.ReduceFromDistance, "Ranged attacks from 4 or more hexes away deal 1 less damage to it.", range: 4));
            Def("pygmaei_blast_furnace_tank",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 4, Splash = 2, SelfDamage = 1, Engine = "Target: a hex within 3 in line of sight. 4 damage there, 2 to every unit on the adjacent hexes (allies included); the tank takes 1 damage." },
                P(PassiveKind.HazardImmune, "Takes no damage from hazard or burning hexes."));
            Def("pygmaei_burrowdrill",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Teleport, DashRange = 3, Special = "Tunnel", Engine = "Tunnel to any empty open, rough or cover hex within 3 (no line of sight needed; walls and barriers cannot be the destination)." },
                P(PassiveKind.IgnoreRough, "Rough hexes cost 1 movement instead of 2."));
            Def("pygmaei_voltfang_gun_walker",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 7, Pierce = 2, SelfGrants = G(new Grant(RStatusType.Exposed, 1, RExpire.NextEnemyActivation)), Engine = "A visible enemy within 4 takes 7 damage ignoring 2 ARM; the walker is EXPOSED (−1 ARM) until the next enemy activation ends." },
                P(PassiveKind.OverkillThroughCover, "If its basic attack destroys a barrier, the unit directly behind it on the same line takes 1 damage."));

            // ---------------------------------------------------------------- orbital
            Def("pygmaei_razor_fighter",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 3, SelfGrants = G(new Grant(RStatusType.AttackBuff, 1, RExpire.AfterOwnActivation, 1, uses: 1)), Engine = "Jump to an empty hex within 3, over units; +1 raw damage on its next attack (next activation)." },
                P(PassiveKind.DamageVsMovedTarget, "+1 raw damage against targets that moved this round."));
            Def("pygmaei_rift_interceptor",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "allied ship", Grants = G(InterceptBy(1)), Special = "JumpAdjacent", Engine = "Pick an allied ship within 2: jump to an empty hex adjacent to it; until this interceptor's next activation it takes the first fighter-class attack on that ship." },
                P(PassiveKind.MoveAfterIntercept, "After it intercepts an attack and survives, it gains +1 movement during its next activation (the codex's free step)."));
            Def("pygmaei_needle_gunship",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = Ship, FilterText = "enemy ship", Damage = 4, Pierce = 1, Engine = "A visible enemy ship within 3 takes 4 damage ignoring 1 ARM." },
                P(PassiveKind.UntargetableByCapitalBeyond, "Capital ships (cruiser and larger) cannot target it with basic attacks from more than 4 hexes unless it is Revealed.", range: 4));
            Def("pygmaei_blastwake_corvette",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Path, DashRange = 2, Damage = 2, MaxTargets = 2, MinTargets = 1, Special = "DriveBy",
                    Engine = "Move up to 2 hexes along an open path (or stay), then attack up to two different enemies within 3 for 2 damage each (line of sight for each)." },
                P(PassiveKind.MoveIfAllyKill, "+1 movement during an activation after an allied unit destroyed an enemy this round."));
            Def("pygmaei_skirmish_frigate",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 2, SelfGrants = G(new Grant(RStatusType.AttackBuff, 1, RExpire.AfterOwnActivation, 1, uses: 1)), Engine = "Shift to an empty hex within 2; its next shot gains +1 raw damage." },
                P(PassiveKind.DamageIfAllyNearTarget, "+1 raw damage if another ally is within 2 of the target.", range: 2));
            Def("pygmaei_annihilator_destroyer",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 4, Splash = 2, Engine = "Target: a hex within 4 in line of sight. 4 damage there and 2 to every unit on the adjacent hexes (allies included)." },
                P(PassiveKind.PierceVsCapitalIfStationary, "If it did not move this activation, its attacks on capital ships ignore 1 ARM."));
            Def("pygmaei_furnace_cruiser",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 3, Grants = G(Suppress(2)), Engine = "A visible enemy within 4 takes 3 damage and its movement is reduced by 2 (minimum 1) during its next activation." },
                P(PassiveKind.HazardImmune, "Takes no damage from hazard or burning hexes."));
            Def("pygmaei_thunderhead_artillery_barge",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 4, Splash = 2, IgnoresCover = true, Engine = "BOMBARD a hex within 6 in line of sight: 4 damage there and 2 on the adjacent hexes (allies included), ignoring cover." },
                P(PassiveKind.NoAttackAfterMove, "It cannot make its basic attack in an activation in which it moved."));
            Def("pygmaei_starbreaker_siege_ship",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 9, Pierce = 3, Engine = "A visible enemy within 6 takes 9 damage ignoring 3 ARM." },
                P(PassiveKind.DeathBlast, "When destroyed it deals 2 damage to every adjacent unit (allies included).", amount: 2));
            Def("pygmaei_revenant_command_ship",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Filter = Ship, FilterText = "allied ship", Grants = G(AttackThisRound(2), new Grant(RStatusType.Exposed, 1, RExpire.AfterOwnActivation, 1)),
                    Engine = "Up to three allied ships within 4 gain +2 raw damage on every attack this round, and are EXPOSED (−1 ARM) through their next activation." },
                P(PassiveKind.AuraMove, "Allied units within 3 gain +1 movement while this leader lives.", range: 3));
        }
    }
}
