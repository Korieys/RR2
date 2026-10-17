namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static void Pirates()
        {
            UnitFilter cmdOrSupply = (s, src, t) => IsLeader(s, t) || t.Def.Roster.Role == UnitRole.Support || t.Def.Roster.Role == UnitRole.Engineer;

            // ---------------------------------------------------------------- planetary
            Def("pirates_deckhand",
                new ActiveSpec { Target = TargetKind.Enemy, Range = 1, Grants = G(MarkG(1)), Engine = "Target: an adjacent enemy (the prose says adjacent; it wins over the listed range 2). MARKED: +1 raw damage on the next allied attack against it." },
                Inert("Improvised Gear refers to transport entry hexes, which these battlefields do not have."));
            Def("pirates_boarding_corsair",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = NonHeavy, FilterText = "non-heavy enemy", Engine = "Target: a visible non-heavy enemy within 2. Pull it 1 hex toward the Corsair if that hex is empty." },
                Inert("Zero-G Veteran restates what its KNIGHT movement already does: it jumps over low obstacles and units."));
            Def("pirates_salvage_runner",
                new ActiveSpec { Target = TargetKind.Ally, Heal = 1, HealVehicle = 2, Engine = "Target: an adjacent ally. Repair 2 HP on a vehicle or 1 HP on infantry." },
                P(PassiveKind.MoveIfFreshWreck, "If it starts its activation adjacent to a wreck created this round or last round, it gains +1 movement."));
            Def("pirates_marauder",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 3, Special = "PillageRush", Engine = "Rush up to 3 hexes in a straight line, then optionally make its basic attack; +1 raw damage if the target is EXPOSED." },
                P(PassiveKind.ShieldOnObjectiveMove, "After it moves onto an objective hex it gains 1 temporary shield HP until its next activation."));
            Def("pirates_broadside_gunner",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 3, Splash = 1, Engine = "Target: a hex within 4 in line of sight. 3 damage there and 1 to every unit on the adjacent hexes (allies included)." },
                Inert("Powder Load: attacks on barriers are never reduced by cover in these rules, so there is nothing extra to ignore."));
            Def("pirates_grenadier",
                new ActiveSpec { Target = TargetKind.Hex, Damage = 3, Special = "Mine", Engine = "Place a hidden 3-damage trap on an empty hex within 2. The first enemy to enter it stops there and takes 3 damage (ARM applies)." },
                Inert("Loose Fuse: your own mines never trigger on your units in these rules."));
            Def("pirates_cutlass_vanguard",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 3, StrikeDamage = 4, Engine = "Leap up to 3 to an empty hex, then deal 4 raw damage to one adjacent enemy." },
                P(PassiveKind.GangUp, "+1 raw damage against a target already adjacent to another allied pirate."));
            Def("pirates_void_stalker",
                new ActiveSpec { Target = TargetKind.Self, SelfGrants = G(new Grant(RStatusType.Cloaked, 0, RExpire.AfterOwnActivation, 1)), Engine = "CLOAKED until the end of its next activation (enemies more than 2 hexes away cannot target it); attacking or ending adjacent to an enemy breaks it." },
                P(PassiveKind.PierceFromCloak, "Its first attack while CLOAKED ignores 1 ARM (the attack then breaks the cloak)."));
            Def("pirates_quartermaster_guard",
                new ActiveSpec { Target = TargetKind.Ally, Filter = cmdOrSupply, FilterText = "leader or support unit", Grants = G(ProtectBy(1)), Engine = "Target: an adjacent leader or support unit. Until this unit's next activation, the first direct attack on it is redirected to this unit." },
                P(PassiveKind.AuraArmorOnObjective, "Adjacent allies standing on an objective your side controls gain +1 ARM."));
            Def("pirates_wreckwright",
                new ActiveSpec { Target = TargetKind.Ally, Heal = 2, HealVehicle = 3, Engine = "Target: an ally within 2. Repair 3 HP on a vehicle or 2 HP on infantry." },
                P(PassiveKind.AuraDisableWardVehiclesBattle, "Allied vehicles within 1 ignore the first DISABLE applied to them in the battle."));
            Def("pirates_plunder_captain",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 2, Grants = G(NextAttack(1), MoveNext(1)), Engine = "Up to two allies within 3 gain +1 raw damage on their next attack and +1 movement during their next activation." },
                P(PassiveKind.LeaderGuardAura, "While it is your leader, allied units within 2 that can GUARD may intercept a direct attack on it (each once per round).", range: 2));
            Def("pirates_freebooter_commander",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Special = "AllHands", Engine = "Up to three allies within 4 each immediately move 1 hex to an empty adjacent hex (pick an ally, then its destination). They cannot attack as part of it." },
                P(PassiveKind.AuraDamageVsObjectiveHolder, "Allied units within 3 gain +1 basic attack damage against enemies standing on objective hexes.", range: 3));
            Def("pirates_wreckrunner_buggy",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 4, Engine = "Jump to an empty hex within 4, ignoring units, rough and low cover in transit." },
                P(PassiveKind.ShieldOnScoutKill, "After it destroys an enemy scout, it gains 1 temporary shield HP."));
            Def("pirates_corsair_breacher",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 3, StrikeDamage = 3, Engine = "Rush up to 3 hexes straight, then deal 3 damage to one adjacent enemy at the endpoint." },
                P(PassiveKind.AuraMoveForCorsairs, "Adjacent allied Boarding Corsairs gain +1 movement."));
            Def("pirates_scrapjaw_tank",
                new ActiveSpec { Target = TargetKind.Self, SelfGrants = G(ShieldUntilSource(4), new Grant(RStatusType.Rooted, 0, RExpire.AfterOwnActivation, 1)), Engine = "Gain 4 temporary shield HP until its next activation begins; it cannot move during its next activation." },
                P(PassiveKind.ReduceFirstBasicPerRound, "The first basic attack against it each round deals 1 less damage."));
            Def("pirates_ironwake_artillery_rig",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 5, Splash = 2, IgnoresCover = true, Engine = "BOMBARD a hex within 6 in line of sight: 5 damage there and 2 to the adjacent hexes (allies included), ignoring cover." },
                P(PassiveKind.DamageIfNotMoved, "+1 basic attack damage if it has not moved this activation.", basicOnly: true));

            // ---------------------------------------------------------------- orbital
            Def("pirates_scrapwing_fighter",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Path, DashRange = 2, StrikeDamage = 1, Engine = "Move up to 2 hexes along an empty path, then strike one adjacent enemy for 1 raw damage (optional)." },
                P(PassiveKind.HazardImmune, "Ignores the first hazard hex damage it would take each activation."));
            Def("pirates_cutlass_interceptor",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Ship, FilterText = "allied ship", Grants = G(InterceptBy(2)), Engine = "Target: an allied ship within 2. Until this interceptor's next activation it takes the first fighter-class attack on that ship." },
                P(PassiveKind.DamageAdjacent, "+1 raw damage when adjacent to its target."));
            Def("pirates_reaver_corvette",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(MarkG(0, 1, 1)), Special = "WreckMarker", Engine = "Target: a visible enemy within 3. MARKED this round as a salvage designation (no damage bonus; if destroyed this round it is SALVAGEABLE after battle)." },
                P(PassiveKind.MoveIfAllyBoarded, "+1 movement during an activation after an allied pirate boarded a ship this round."));
            Def("pirates_gallow_frigate",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Filter = (s, src, t) => t.Def.Roster.Role == UnitRole.Corvette || t.Def.Roster.IsSmallCraft, FilterText = "corvette or fighter at 2 HP or less",
                    HpThreshold = 2, Special = "Grapple", Engine = "Target: a visible enemy corvette or fighter within 2 at 2 HP or less. BOARDED: it cannot activate until an ally rescues it or this frigate is destroyed." },
                P(PassiveKind.PierceVsBoarded, "Its basic attacks ignore 1 ARM against BOARDED targets."));
            Def("pirates_broadside_destroyer",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, MaxTargets = 2, MinTargets = 2, Damage = 3, Engine = "Strike two different visible enemies within 4 for 3 damage each." },
                P(PassiveKind.LosThroughAdjacentAlly, "Its line of sight passes through one adjacent allied ship."));
            Def("pirates_marauder_cruiser",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 3, Splash = 1, Engine = "Target: a hex within 4 in line of sight. 3 damage there and 1 to each unit on the adjacent hexes (allies included)." },
                Inert("Loot Holds increases after-battle salvage only."));
            Def("pirates_galleon_carrier",
                new ActiveSpec { Target = TargetKind.Hex, TokenName = "token_pirates_skiff", TokenCap = 2, Engine = "Deploy a 2-HP boarding skiff token on an adjacent empty hex; at most two active tokens from this carrier." },
                P(PassiveKind.AuraMoveSmallCraft, "Adjacent allied fighters and skiffs gain +1 movement.", range: 1));
            Def("pirates_ironwake_battleship",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, MaxTargets = 2, MinTargets = 2, Damage = 5, Engine = "Attack two different visible enemies within 5 for 5 damage each." },
                P(PassiveKind.AnchorGunners, "+1 attack damage if it has not moved this activation; after it fires, it has −1 movement during its next activation."));
            Def("pirates_blackflag_dreadnought",
                new ActiveSpec { Target = TargetKind.Hex, NeedsLos = true, Damage = 6, Splash = 3, Engine = "Target: a hex within 6 in line of sight. 6 damage there and 3 to every unit on the adjacent hexes (allies included)." },
                Inert("Warship Wreck adds an after-battle salvage marker only."));
            Def("pirates_freeport_flagship",
                new ActiveSpec { Target = TargetKind.Ally, MaxTargets = 3, Grants = G(NextAttack(2)), Options = new[] { "+2 next shot each", "2 shield HP each" }, Special = "OptionAttackOrShield",
                    Engine = "Up to three allies within 4 each gain either +2 raw damage on their next shot, or 2 temporary shield HP until this ship's next activation." },
                P(PassiveKind.AuraDamageVsBoarded, "Allied units within 3 gain +1 attack damage against BOARDED ships.", range: 3));
        }
    }
}
