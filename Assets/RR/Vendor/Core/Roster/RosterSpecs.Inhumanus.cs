namespace Commandfall.Core
{
    public static partial class RosterSpecs
    {
        static void Inhumanus()
        {
            UnitFilter supportCmdVehicle = (s, src, t) => IsLeader(s, t) || t.Def.Roster.IsVehicle
                                                         || t.Def.Roster.Role == UnitRole.Support || t.Def.Roster.Role == UnitRole.Engineer;

            // ---------------------------------------------------------------- planetary
            Def("inhumanus_contract_trooper",
                new ActiveSpec { Target = TargetKind.Ally, Filter = Infantry, FilterText = "friendly infantry", Grants = G(NextAttack(1)), Engine = "Target: friendly infantry within 2. +1 raw damage on its next attack." },
                P(PassiveKind.ShieldAfterFirstHitSurvived, "The first time it survives an attack in the battle, it gains 1 temporary shield HP (until the end of the next round)."));
            Def("inhumanus_pale_operative",
                new ActiveSpec { Target = TargetKind.Self, SelfGrants = G(new Grant(RStatusType.Cloaked, 0, RExpire.AfterOwnActivation, 1)), Engine = "CLOAKED until the end of its next activation: enemies more than 2 hexes away cannot target it. Attacking, using an offensive active or ending adjacent to an enemy breaks it." },
                P(PassiveKind.IgnoreRoughOnce, "The first rough hex it enters each activation costs 1 instead of 2."));
            Def("inhumanus_nullstride_agent",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Teleport, DashRange = 3, NeedsLos = true, Engine = "Teleport to any empty hex within 3 that it can see, ignoring units and barriers in between." },
                P(PassiveKind.PierceIfUnmarked, "If it is not MARKED when its activation begins, its first attack that activation ignores 1 ARM."));
            Def("inhumanus_vector_sniper",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Damage = 4, Pierce = 2, Engine = "A visible enemy within 5 takes 4 damage ignoring 2 ARM." },
                P(PassiveKind.DamageAtDistance, "+1 raw damage against enemies 4 or more hexes away.", range: 4));
            Def("inhumanus_gravity_weaver",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Special = "PullToHex", Engine = "Pick a visible enemy within 3, then an empty hex adjacent to it that is closer to this unit: pull it exactly 1 hex there." },
                P(PassiveKind.AuraEnemySlow, "Adjacent enemies have −1 movement unless they are siege or elite class."));
            Def("inhumanus_asset_recovery_agent",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = NonLeader, FilterText = "non-leader at 2 HP or less", HpThreshold = 2, Engine = "Target: an adjacent non-leader enemy at 2 HP or less. It becomes CAPTURED until rescued." },
                P(PassiveKind.PierceVsMarked, "Its attacks ignore 1 ARM against MARKED targets."));
            Def("inhumanus_phase_auditor",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(Disable, new Grant(RStatusType.NoShieldGain, 0, RExpire.RoundEnd, 1)), Engine = "Target: an enemy within 3. DISABLED during its next activation, and it cannot gain temporary shields this round." },
                P(PassiveKind.RevealMinesAtStart, "At the start of its activation, enemy mines within 2 are revealed until the end of the round.", range: 2));
            Def("inhumanus_spectrum_analyst",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(MarkG(2)), Engine = "Target: a visible (or Revealed) enemy within 4. MARKED: the next allied hit on it deals +2 raw damage." },
                P(PassiveKind.AuraIgnoreCoverVsMarked, "Allied units within 2 ignore 1 point of cover reduction against MARKED enemies.", range: 2));
            Def("inhumanus_security_drone",
                new ActiveSpec { Target = TargetKind.Self, Special = "RevealAreaMarkNearest", Engine = "Enemy cloaked units and mines within 3 are REVEALED until the end of the round, and the nearest revealed enemy is MARKED (+1) until the end of the round." },
                Inert("Automated Protocol: there are no fear, morale or charisma modifiers in these rules."));
            Def("inhumanus_corporate_warden",
                new ActiveSpec { Target = TargetKind.Ally, Filter = supportCmdVehicle, FilterText = "support unit, leader or vehicle", Grants = G(ProtectBy(2)), Engine = "Target: an allied support unit, leader or vehicle within 2. Until this unit's next activation, the first direct attack on it is redirected to this unit." },
                P(PassiveKind.ArmorFirstHitNearLeader, "While adjacent to your leader, it has +1 ARM against the first hit each round."));
            Def("inhumanus_cybernetic_executor",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Straight, DashRange = 2, StrikeDamage = 5, StrikePierce = 1, Engine = "Dash up to 2 hexes in a straight line, then deal 5 damage ignoring 1 ARM to one adjacent enemy." },
                P(PassiveKind.ReduceFirstDirectHit, "The first direct attack on it each round deals 1 less damage."));
            Def("inhumanus_executive_adjudicator",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, Grants = G(Silence), Engine = "Target: a visible enemy within 4. During its next activation it cannot attack or use its active (it may move)." },
                P(PassiveKind.MarkWardOnce, "The first enemy MARK applied to it in the battle is ignored."));
            Def("inhumanus_vector_skimmer",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Leap, DashRange = 4, NeedsLos = true, Engine = "Jump to any empty hex within 4 that it can see, over units and ground cover." },
                P(PassiveKind.IgnoreRough, "Rough hexes cost 1 movement instead of 2."));
            Def("inhumanus_asset_recovery_walker",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = NonSiege, FilterText = "non-siege enemy", Damage = 1, Unavoidable = true, Special = "PullThenDamage", Engine = "Target: a non-siege enemy within 2. Pull it 1 hex closer (if the hex is empty), then deal 1 unavoidable damage (ignores ARM, cover and shields)." },
                P(PassiveKind.RescueDouble, "Enemies captured next to it need two rescue actions to free."));
            Def("inhumanus_graviton_suppressor",
                new ActiveSpec { Target = TargetKind.Enemy, MaxTargets = 2, MinTargets = 2, NeedsLos = true, Grants = G(Root), Special = "AdjacentPair", Engine = "Pick two enemies within 4 that stand on adjacent hexes: both are ROOTED through their next activation." },
                P(PassiveKind.ForcedMoveImmuneIfStationary, "Immune to enemy push and pull while it has not moved this round."));
            Def("inhumanus_parallax_siege_platform",
                new ActiveSpec { Target = TargetKind.Enemy, NeedsLos = true, MinRange = 3, Damage = 8, Pierce = 2, Engine = "A visible enemy at range 3–6 takes 8 damage ignoring 2 ARM." },
                P(PassiveKind.PierceVsShieldedOrFortified, "Its basic attack ignores 1 ARM against shielded targets, defenders and bastions."));

            // ---------------------------------------------------------------- orbital
            Def("inhumanus_prospect_interceptor",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(MarkG(1)), Engine = "Target: an enemy within 3. MARKED: +1 raw damage on the next friendly attack against it." },
                P(PassiveKind.ShieldOnFirstObjective, "The first time it ends a move on an objective hex no unit has held, it gains 1 temporary shield HP."));
            Def("inhumanus_mercator_corvette",
                new ActiveSpec { Target = TargetKind.Hex, Dash = DashKind.Path, DashRange = 3, SelfGrants = G(new Grant(RStatusType.AttackBuff, 1, RExpire.AfterOwnActivation, 1, uses: 1)), Engine = "Move up to 3 hexes along an empty path; +1 raw damage on its next basic attack." },
                P(PassiveKind.ReduceNextRangedIfMoved, "If it moved this round, the next ranged hit on it deals 1 less damage."));
            Def("inhumanus_proxy_frigate",
                new ActiveSpec { Target = TargetKind.Hex, TokenName = "token_inhumanus_escort", TokenCap = 1, Engine = "Deploy a 2-HP mercenary escort token on an adjacent empty hex; at most one at a time." },
                P(PassiveKind.ReduceFirstAttackBattle, "The first enemy attack on it in the battle deals 1 less damage."));
            Def("inhumanus_arbitration_destroyer",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = Ship, FilterText = "enemy ship", Grants = G(Disable), Engine = "Target: an enemy ship within 4. DISABLED during its next activation." },
                P(PassiveKind.DamageVsMarked, "+1 basic attack damage against MARKED enemies."));
            Def("inhumanus_parallax_cruiser",
                new ActiveSpec { Target = TargetKind.Ally, AllowSelf = true, Filter = (s, src, t) => t.Id == src.Id || t.Def.Roster.Role == UnitRole.Corvette, FilterText = "itself or an allied corvette",
                    Special = "GravityFold", Engine = "Pick itself or an allied corvette within 2, then a different empty hex within 2 of that ship that this cruiser can see: teleport it there." },
                P(PassiveKind.RootWardOnce, "The first enemy ROOT applied to it in the battle is ignored."));
            Def("inhumanus_dividend_carrier",
                new ActiveSpec { Target = TargetKind.Hex, TokenName = "token_inhumanus_fighter", TokenCap = 2, Engine = "Deploy a 2-HP autonomous fighter token on an adjacent empty hex; at most two from this carrier." },
                P(PassiveKind.AuraShieldOnActivate, "Allied fighters and tokens within 2 gain 1 temporary shield HP when they begin an activation.", range: 2, role: UnitRole.Fighter));
            Def("inhumanus_hegemony_battleship",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = Ship, FilterText = "enemy ship", Grants = G(MarkG(1, 3)), Engine = "Target: an enemy ship within 5. MARKED for three uses: the next three allied direct attacks on it each deal +1 raw damage." },
                P(PassiveKind.DamageVsIsolated, "+1 damage against targets with no adjacent allies."));
            Def("inhumanus_acquisition_vessel",
                new ActiveSpec { Target = TargetKind.Enemy, Filter = Both(CorvetteOrFrigate, (s, src, t) => t.HasR(RStatusType.Disabled) || t.HasR(RStatusType.Silenced)), FilterText = "DISABLED enemy corvette or frigate at 2 HP or less",
                    HpThreshold = 2, Special = "AssetTow", Engine = "Target: a DISABLED enemy corvette or frigate within 2 at 2 HP or less with none of its allies adjacent. It is BOARDED for the rest of the battle (inert)." },
                Inert("Legal Salvage refits claimed hulls after the battle; a single battle has no campaign layer."));
            Def("inhumanus_sovereign_ledger",
                new ActiveSpec { Target = TargetKind.Enemy, MaxTargets = 2, Filter = Ship, FilterText = "enemy ship", Grants = G(Expose(1)), Engine = "Up to two enemy ships within 4 lose 1 ARM (EXPOSED) through their next activation." },
                P(PassiveKind.AuraShieldOnActivate, "Adjacent allied ships gain 1 temporary shield HP when they begin an activation (once per round each).", range: 1));
            Def("inhumanus_executive_flagship",
                new ActiveSpec { Target = TargetKind.Enemy, Grants = G(Disable), Special = "AlsoShieldAlly", Engine = "Target: an enemy within 5 (DISABLED during its next activation), then optionally an ally within 3 it can see: +2 temporary shield HP until this ship's next activation." },
                P(PassiveKind.AuraDamageVsMarked, "Allied units within 3 gain +1 damage against MARKED targets.", range: 3));
        }
    }
}
