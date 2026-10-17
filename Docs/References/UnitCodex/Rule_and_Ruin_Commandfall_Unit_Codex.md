# RULE AND RUIN | COMMANDFALL UNIT CODEX
**0.1 - implementation design draft | 2026-10-14**

**Status:** 156 prior roster names across 6 playable core factions. A 26-name Cyborg Elf proposal is marked optional. Iamni, Kallamori, Brutum excluded. Combat rules and abilities are design proposals derived from, not described in, the supplied lore document.

## Design Contract
- **Coordinates:** Axial (q,r). Six primary neighbors: (1,0),(1,-1),(0,-1),(-1,0),(-1,1),(0,1). Hex distance = (abs(dq)+abs(dr)+abs(dq+dr))/2.
- **Turn Structure:** Alternating single-unit activation. Each surviving unit activates once per round. On activation it may move according to its pattern, then choose one basic attack OR one active ability, or wait. An active that moves/attacks uses that action; no bonus attack unless expressly provided.
- **Damage:** No random hit, critical, dodge or damage rolls. A direct hit needs a legal target, range and line of sight. Compute max(0, raw damage - defender ARM - cover reduction); subtract that effective damage from temporary shields first, then any remainder from HP. Piercing abilities ignore named ARM, not shields. Area attacks hit all eligible occupants, friendly or enemy if specified.
- **Vision:** Sight blocked by tall obstacles and walls, not low cover. Reveal and shared vision are deterministic; range and LOS still apply unless skill states otherwise. No fog randomization.
- **Battle End:** Leader objective: killing the opposing nominated Captain/Commander on ground or Flagship in orbit ends the battle immediately, regardless of remaining forces. The hit must legally deal lethal damage after ARM, cover, shields and a declared GUARD intercept. Ordinary captures never substitute for destroying the protected enemy leader.
- **Guard:** A defender can be protected only by an actual guard ability/passive, shield, terrain or formation. Guards intercept at most one eligible direct attack per round, never chain and cannot block board-wide blasts unless explicitly specified.
- **Cooldowns:** Cooldown X means the active is unusable for the next X of that unit's own activations after use. CD 0 may be used on every activation. Every activation may use at most one special or one basic attack.
- **Duration:** Unless explicitly otherwise, next activation statuses end after the affected unit completes its next activation. Round statuses end after both sides have exhausted available unit activations.
- **Stacking:** Same-type buffs/debuffs take the strongest active instance; they do not stack. Shield points retain only the larger value. Armor has hard cap 4 after buffs, minimum 0.
- **Collision:** Forced move fails if target destination is occupied, blocking or outside the map, unless ability explicitly deals collision damage. Cannot displace enemy LEADER off board for victory.
- **Training Tokens:** SPAWN tokens have 2 HP, 0 ARM, basic attack 2 at range 1-2, STEP 2 unless the unit explicitly describes otherwise; they do not use active abilities, cannot be captured for economy, and despawn after battle.
- **Equipment Upgrades:** Unlock tier 1-5 is when the unit becomes available; weapon/armor research can upgrade existing unit without changing its movement identity. Proposed: weapon tier 3 adds +1 base damage, tier 5 adds a further +1. Armor tier 2/3/4/5 each add +1 printed HP; tier 3 adds +1 ARM and tier 5 another +1 (subject to cap 4). Applies equally to equivalent fleets as a starting balance model.
- **Faction Ability Note:** Faction rules are separate from unit active and passive abilities. Inhumanus charter is a strategic pre-battle selection, not an option to switch freely inside a battle.
### Movement patterns
- **STEP**: Move along an unoccupied, connected hex path with up to N steps. Can change direction freely.
- **RAY**: Move straight along one of the six hex axes up to N hexes; may not turn mid-move.
- **ZIGZAG**: Move along an unoccupied hex path of up to N steps and make at most one 60-degree turn.
- **DIAG**: Move along a straight corner-to-corner diagonal, using the six axial offset directions (1,1), (2,-1), (1,-2), (-1,-1), (-2,1), (-1,2). Each diagonal hop has hex distance 2; N counts diagonal hops, not ordinary steps.
- **KNIGHT**: Jump to a hex at 2*a+b where a is a primary hex direction and b is either immediately adjacent direction. Twelve possible offsets, skipping blockers and landing only on legal empty hexes; N is nominal range 3.
- **LEAP**: Jump to any empty hex within N hexes, skipping intervening units and low obstacles; cannot cross an explicitly anti-jump barrier or leave board.
- **PHASE**: Follow a STEP-style route of up to N, passing through units and cover; cannot end on an occupied or impassable destination.
### Status effects
- **ARM**: Flat armor reduction from each damage instance, before temporary shields are applied; base cap 4. Minimum direct-attack damage is 0.
- **HP**: Printed health. A unit is defeated at 0 HP. Temporary shield points absorb damage before health; each new shield source refreshes to the greater amount rather than stacking.
- **COVER**: A blocking terrain hex ends movement and line of sight; low-cover terrain reduces ranged damage by 1. A unit can attack out of its own low-cover hex.
- **ROOTED**: Cannot move during its next activation; can attack or use a non-movement ability unless DISABLED.
- **DISABLED**: Cannot use its active ability on its next activation. It may still move or use a basic attack unless the particular active explicitly forbids attacks too.
- **EXPOSED**: Armor reduced by 1 (minimum 0) until the end of the target's next activation, unless an ability specifies a shorter period.
- **MARKED**: The next qualifying friendly attack against the target gets the listed raw damage bonus. It is then consumed. Same-kind marks never stack; the strongest applies.
- **CLOAKED**: Invisible and untargetable by enemies farther than 2 hexes unless exposed by REVEAL. Breaks if the unit attacks, uses an offensive active, or ends adjacent to an enemy.
- **GUARD**: One unit is allowed to redirect one eligible direct attack away from a specified friendly target. Guardian must be within its protection range, present, alive, and not DISABLED. A redirected attack hits the guardian; no reaction chain is permitted.
- **SHIELD**: Temporary shield HP is consumed before normal HP. A shield expires at its specified duration. Multiple shields keep only the greatest current shield value.
- **CAPTURED**: Unit is immobilized and cannot act or exert zone control. It can be freed by adjacent allied rescue action unless affected by an explicit specialist rule; it has no command function.
- **BOARDED**: Ship is grappled and cannot activate until an ally removes boarding via an adjacent rescue action, or the boarding unit is defeated. Flagships still require a legal lethal attack for victory.
- **SALVAGEABLE**: A destroyed hull may be reclaimed in the post-battle campaign layer if a qualified salvage unit survives and the faction controls the battle site. Not a mid-fight resurrection.
- **SUPPRESSED**: Movement maximum is reduced by the specified amount for the next activation, to a minimum of 1 unless ROOTED.
- **BARRIER**: Temporary occupiable or blocking map structure, with its own HP and expiry. Blocking barriers obstruct movement and line of sight unless ability says otherwise.
### Effect codes
- **AOE**: Strike a selected center hex and adjacent hexes as stated by that specific unit. Unless stated otherwise, blast damage can hit allies.
- **ARMOR_BREAK**: Reduce target armor by the specified amount for one activation, usually via EXPOSED.
- **BARRAGE**: Resolve a defined sequence of separate damage hits, each with its own legal target. Can never hit one target twice unless text expressly permits it.
- **BARRIER**: Create or restore a battlefield cover or wall piece with stated HP and duration.
- **BOARD**: Apply BOARDED status to a qualifying weakened target. It is not automatic permanent ownership in battle.
- **BOMBARD**: Deal area damage centered on a visible hex, including over low cover when text allows. Resolve each hex once.
- **CAPTURE**: Apply CAPTURED to an adjacent non-leader below the stated HP threshold; captured targets remain on board but cannot act.
- **CHARGE**: Move then strike as one special action according to the specified line, distance and damage. No extra basic attack.
- **CLOAK**: Apply CLOAKED status for described duration.
- **CONVERT**: Transform a previously CAPTURED non-leader into a temporary 1-HP Cräigon auxiliary; requires uninterrupted action.
- **COUNTER**: Return damage in response to a triggering attack one time. It never chains into another counter.
- **DAMAGE**: Single-target active damage, substituting for a basic attack on that activation.
- **DASH**: Reposition as part of a special action; only deals damage when the unit text says it does.
- **DEBUFF**: Apply the exact listed weakening status to eligible units.
- **DISABLE**: Apply DISABLED unless text explicitly also prevents basic attacks.
- **GUARD**: Grant direct-attack interception, max once per guardian per round.
- **HEAL**: Restore printed HP to a friendly eligible unit, never beyond max; may cleanse a status if explicitly stated.
- **INTERCEPT**: Make a declared protective reaction on the next eligible hostile attack, per the unit text.
- **LEAP**: Special movement jump to indicated legal destination and any stated related attack.
- **MARK**: Apply a one-use damage amplification or tactical designation.
- **MINE**: Place a visible-to-owner, hidden-to-opponent mine with a deterministic trigger and damage amount.
- **PIERCE**: Deal the listed damage, ignoring the stated number of armor points, not shields unless specified.
- **PIN**: Apply ROOTED for the specified time.
- **PULL**: Displace a target by the stated number of hexes toward source, if the landing hex is empty and not blocked.
- **RALLY**: Provide the specified attack, movement, or defensive buff to named allies; no stacking of identical buffs.
- **REPAIR**: Restore HP to vehicles or constructs, not above their printed maximum.
- **REPOSITION**: Relocate eligible ally or self as the ability describes; cannot place units onto enemies or blocked hexes.
- **REVEAL**: Reveal stealth, mines, and hidden targets within the listed area for a defined duration.
- **SHIELD**: Grant temporary shield HP for the listed duration.
- **SPAWN**: Place a weak deployable token in an empty adjacent hex, respecting per-carrier cap; tokens never count as LEADERS.
- **SUPPRESS**: Reduce a target's next-activation movement by an explicitly stated amount.
- **TELEPORT**: Relocate unit to a legal empty target hex, ignoring intervening terrain but never the endpoint restrictions.

## Cräigon
**Combat identity:** Telepathic bio-engineers who capture enemies and impose The Gift. Durable infantry, precise disabling attacks, weaker mass destruction.
**Faction rule:** Collective Link: adjacent Cräigon infantry can use each other for friendly support ranges; Gift conversions create Cräigon bio-forms rather than using captured foreign weapons.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Carapace Drone | T1 Line
**Stats:** HP 6 · ARM 1 · Move STEP 2 · Pincer Cut: 2 damage, R1-1.
**Active — Clamp Relay** [PIN, CD 2, R1]: An adjacent enemy becomes ROOTED through its next activation.
**Passive — Collective Plates:** Gain +1 ARM when adjacent to another Cräigon infantry unit.

#### 2. Shellbound | T1 Defender
**Stats:** HP 8 · ARM 2 · Move RAY 2 · Shell Strike: 2 damage, R1-1.
**Active — Living Barricade** [GUARD, CD 2, R1]: Guard one adjacent ally until your next activation and intercept its next direct attack.
**Passive — Interlocking Shells:** Gain +1 ARM while not moving during your activation.

#### 3. Tide Skimmer | T1 Scout
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Skimmer Talon: 2 damage, R1-1.
**Active — Surf Lunge** [DASH, CD 2, R2]: Jump to an empty hex within 2 and make a 1-damage strike against one adjacent enemy.
**Passive — Shore Runner:** Ignores the first difficult-terrain movement surcharge per activation.

#### 4. Synapse Stalker | T2 Saboteur
**Stats:** HP 5 · ARM 0 · Move PHASE 3 · Neural Blade: 2 damage, R1-1.
**Active — Thought Sever** [DISABLE, CD 2, R1]: Disable an adjacent enemy's active ability during its next activation.
**Passive — Hushed Synapse:** Cannot be targeted from more than 2 hexes away until it attacks or is revealed.

#### 5. Pincer Guard | T2 Heavy
**Stats:** HP 8 · ARM 1 · Move RAY 2 · Pincer Cleave: 3 damage, R1-1.
**Active — Lockjaw** [PIN, CD 2, R1]: Deal 1 damage and ROOT the adjacent target for its next activation.
**Passive — Guarding Instinct:** May intercept one direct attack against an adjacent allied commander per round.

#### 6. Carapace Custodian | T2 Defender
**Stats:** HP 8 · ARM 2 · Move STEP 2 · Shield Claw: 2 damage, R1-1.
**Active — Carapace Wall** [BARRIER, CD 3, R1]: Create 1 temporary cover hex adjacent to this unit, lasting 2 rounds.
**Passive — Custodian Aegis:** Adjacent allies gain +1 cover against ranged attacks, not stackable.

#### 7. Neural Spiker | T3 Marksman
**Stats:** HP 5 · ARM 0 · Move DIAG 2 · Neural Spike: 3 damage, R2-4.
**Active — Cortex Needle** [DISABLE, CD 3, R4]: Deal 1 penetrating damage and DISABLE a visible enemy's next active ability.
**Passive — Long Synapse:** Can attack targets revealed by a linked Cräigon scout even if outside its own sight, with normal range limits.

#### 8. Harmonic Disruptor | T3 Support
**Stats:** HP 6 · ARM 0 · Move STEP 2 · Resonance Pulse: 2 damage, R1-3.
**Active — Feedback Field** [DEBUFF, CD 3, R2]: Enemies within 2 lose 1 attack damage on their next attack; minimum raw damage is 1.
**Passive — Synchronized Echo:** Adjacent allied ranged attacks ignore 1 point of cover reduction.

#### 9. Assimilator | T4 Capture
**Stats:** HP 6 · ARM 1 · Move STEP 2 · Subduing Grip: 3 damage, R1-1.
**Active — The Gift** [CAPTURE, CD 3, R1]: An adjacent non-leader target at 2 HP or below is CAPTURED instead of killed; it cannot act and requires escort after battle.
**Passive — Merciful Procedure:** Its attacks that would kill a non-leader may leave the target at 1 HP and ROOTED instead.

#### 10. Giftbearer | T4 Support
**Stats:** HP 6 · ARM 0 · Move RAY 2 · Gift Staff: 2 damage, R1-2.
**Active — Conversion Rite** [CONVERT, CD 4, R1]: Convert an adjacent CAPTURED unit into a 1-HP Cräigon auxiliary after a full uninterrupted round; no original alien equipment is retained.
**Passive — Communal Purpose:** Captured units adjacent to this model cannot be freed by non-specialist enemy interactions.

#### 11. Fleshweaver | T4 Engineer
**Stats:** HP 6 · ARM 1 · Move STEP 2 · Flesh Needle: 2 damage, R1-2.
**Active — Adaptive Tissue** [HEAL, CD 2, R2]: Restore 3 HP to an allied biological unit within 2, up to its printed maximum.
**Passive — Genetic Triage:** Adjacent allied BIO units recover 1 HP after its activation if they did not attack this round, max 1 per round.

#### 12. Evolved Praetor | T5 Commander
**Stats:** HP 10 · ARM 1 · Move DIAG 2 · Praetor Talon: 5 damage, R1-2.
**Active — Dominion Command** [RALLY, CD 3, R3]: Two allied BIO units within 3 gain +1 raw attack damage on their next attack this round.
**Passive — Synaptic Crown:** Linked Cräigon allies within 2 may transfer one unused GUARD interception to protect this commander.

#### 13. Specimen Crawler | T2 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move STEP 2 · Capture Harpoon: 2 damage, R1-2.
**Active — Specimen Winch** [PULL, CD 2, R2]: Pull a visible non-heavy enemy 1 hex toward this vehicle if the destination is unoccupied.
**Passive — Specimen Hold:** Captured non-leader enemies adjacent to this vehicle cannot be freed except by a rescue ability.

#### 14. Chitin Walker | T3 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move RAY 2 · Chitin Cannon: 4 damage, R1-3.
**Active — Piercing Leg** [CHARGE, CD 3, R2]: Move up to 2 in a straight line; deal 2 damage to the first adjacent enemy at the endpoint.
**Passive — Jointed Frame:** Traverses low cover without movement penalty.

#### 15. Convergence Hauler | T4 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move STEP 2 · Restraint Cannon: 2 damage, R1-3.
**Active — Mass Extraction** [REPOSITION, CD 3, R2]: Move an allied infantry or CAPTURED unit within 2 to an empty hex adjacent to the Hauler.
**Passive — Bio-Laboratory:** A friendly Assimilator within 2 gains +1 target range on The Gift.

#### 16. Ascension Engine | T5 Vehicle Siege
**Stats:** HP 12 · ARM 2 · Move RAY 1 · Ascension Lance: 5 damage, R2-5.
**Active — Elevating Pulse** [AOE, CD 4, R4]: Deal 3 damage to one target and 1 to all units in its neighboring hexes; line of sight required to center.
**Passive — Perfected Engine:** Ignores the first point of cover when firing its basic attack.

### Orbital Commandfall: space units
#### 1. Sporewing Fighter | T1 Fighter
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Spore Bolts: 2 damage, R1-2.
**Active — Spore Screen** [DEBUFF, CD 2, R2]: Target enemy loses 1 basic-attack range on its next activation, minimum range 1.
**Passive — Living Wings:** Can end movement in nebula hexes without penalty.

#### 2. Scuttler Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 3 · Needle Beams: 2 damage, R1-2.
**Active — Interception Snap** [INTERCEPT, CD 2, R2]: Intercept a fighter or missile attack targeting an ally within 2, becoming its target instead.
**Passive — Swarm Chase:** Gain +1 raw attack damage when striking a Fighter or Interceptor.

#### 3. Scuttler Corvette | T2 Corvette
**Stats:** HP 7 · ARM 1 · Move RAY 3 · Claw Cannons: 3 damage, R1-3.
**Active — Fleet Pin** [PIN, CD 2, R3]: ROOT a visible enemy smaller than Cruiser for its next activation.
**Passive — Pack Link:** Gain +1 attack damage while adjacent to another Cräigon corvette, capped at +1.

#### 4. Pincer Frigate | T2 Frigate
**Stats:** HP 9 · ARM 1 · Move ZIGZAG 3 · Twin Pincer Batteries: 3 damage, R2-3.
**Active — Grapple Beam** [PULL, CD 3, R3]: Pull a visible enemy Corvette or smaller 1 hex toward this ship.
**Passive — Bilateral Targeting:** May attack through one friendly fighter tile when checking line of sight.

#### 5. Communion Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 1 · Move RAY 2 · Communion Lances: 4 damage, R2-4.
**Active — Shared Fire** [RALLY, CD 3, R3]: One allied smaller spacecraft within 3 gains +2 damage on its next basic attack this round.
**Passive — Cortex Broadcast:** Allied Cräigon ships within 2 may share revealed-target information.

#### 6. Synaptic Cruiser | T3 Cruiser
**Stats:** HP 13 · ARM 2 · Move DIAG 2 · Neural Batteries: 4 damage, R2-4.
**Active — Command Silence** [DISABLE, CD 3, R4]: DISABLE the active ability of one visible enemy ship next activation.
**Passive — Relay Node:** Linked friendly ships within 2 ignore the first 1 range penalty applied to them.

#### 7. Chrysalis Carrier | T4 Carrier
**Stats:** HP 14 · ARM 1 · Move RAY 2 · Brood Cannon: 3 damage, R2-4.
**Active — Launch Sporewing** [SPAWN, CD 3, R1]: Deploy one 2-HP Sporewing token in an empty adjacent hex; maximum two living tokens per carrier.
**Passive — Brood Shelter:** Adjacent allied Fighters recover 1 HP after this carrier activates, max once per round.

#### 8. Evolutionary Bastion | T4 Space Bastion
**Stats:** HP 19 · ARM 3 · Move RAY 1 · Bastion Lance: 5 damage, R2-5.
**Active — Living Bulwark** [SHIELD, CD 3, R2]: Grant 3 temporary shield HP to one allied spacecraft within 2 until your next activation.
**Passive — Evolutionary Plates:** When stationary, this ship gains +1 ARM to direct attacks, capped at 4 total ARM.

#### 9. Ascendant Ark | T5 Ark
**Stats:** HP 18 · ARM 2 · Move RAY 1 · Ascendant Pulse: 5 damage, R2-5.
**Active — Mass Communion** [RALLY, CD 4, R3]: Up to three allied Cräigon spacecraft within 3 gain +1 raw attack damage this round.
**Passive — Resilient Genome:** The first destroyed allied non-leader fighter within 2 leaves a salvageable biomass marker for after-battle recovery.

#### 10. Prime Synapse Flagship | T5 Flagship
**Stats:** HP 20 · ARM 2 · Move RAY 1 · Prime Synapse Beam: 7 damage, R2-6.
**Active — Absolute Concord** [DISABLE, CD 4, R5]: Choose two visible enemy ships within 5; each loses its active ability for its next activation.
**Passive — Singular Thought:** Allied Cräigon ships within 3 may share line-of-sight spotting; flagship remains a killable LEADER.

## Silex
**Combat identity:** Silicon-crystal monarchy: formidable armor, living fortifications, slow advance, defensive capital ships.
**Faction rule:** Living Stone: while a Silex unit ends its activation without moving, it gets +1 temporary guard armor until its next activation (non-stacking).

### Planetary Commandfall: infantry and ground vehicles
#### 1. Shard Retainer | T1 Line
**Stats:** HP 7 · ARM 2 · Move STEP 2 · Shard Spear: 2 damage, R1-1.
**Active — Royal Formation** [GUARD, CD 2, R1]: Grant GUARD to one adjacent Silex ally, intercepting the next direct strike.
**Passive — Disciplined Stone:** If adjacent to another Silex, gain +1 ARM until it moves.

#### 2. Shard Sentinel | T1 Defender
**Stats:** HP 9 · ARM 3 · Move RAY 1 · Crystal Pike: 2 damage, R1-2.
**Active — Prism Guard** [SHIELD, CD 2, R1]: Give an adjacent ally 2 temporary shield HP until its next activation.
**Passive — Dense Lattice:** Immune to forced movement of 1 hex unless caused by siege-class units.

#### 3. Lithic Strider | T2 Scout
**Stats:** HP 6 · ARM 1 · Move DIAG 2 · Quartz Blade: 2 damage, R1-1.
**Active — Faultstep** [LEAP, CD 2, R2]: Leap to an empty hex within 2 ignoring units and low cover.
**Passive — Thin Vein:** May traverse rocky tiles at no additional movement cost.

#### 4. Obelisk Guard | T2 Heavy
**Stats:** HP 9 · ARM 2 · Move RAY 2 · Obelisk Slam: 3 damage, R1-2.
**Active — Stone Column** [BARRIER, CD 3, R1]: Raise one adjacent blocking crystal hex with 3 HP lasting 2 rounds.
**Passive — Ancient Weight:** Cannot be pushed by non-siege infantry attacks.

#### 5. Bastion Keeper | T2 Defender
**Stats:** HP 9 · ARM 3 · Move STEP 1 · Bastion Mace: 3 damage, R1-1.
**Active — Hold the Gate** [GUARD, CD 2, R2]: Give GUARD to up to two allied infantry within 2; each may intercept one attack this round.
**Passive — Fortress Blood:** Gets +1 ARM while defending a cover or objective hex.

#### 6. Resonance Adept | T3 Support
**Stats:** HP 7 · ARM 1 · Move STEP 2 · Resonance Beam: 2 damage, R1-3.
**Active — Crystal Mend** [HEAL, CD 2, R2]: Restore 3 HP to a Silex ally within 2; cannot exceed printed max.
**Passive — Harmonic Field:** Allied Silex within 1 ignore the first enemy DISABLE once per round.

#### 7. Prism Herald | T3 Support
**Stats:** HP 7 · ARM 1 · Move RAY 2 · Refraction Shot: 2 damage, R2-3.
**Active — Prism Mirror** [COUNTER, CD 3, R2]: One ally within 2 reflects 2 damage back on the next ranged basic attack that damages it.
**Passive — Court Beacon:** Friendly Silex within 2 gain +1 visibility against cloaked enemies.

#### 8. Faultline Breaker | T3 Assault
**Stats:** HP 7 · ARM 1 · Move RAY 2 · Seismic Fist: 4 damage, R1-1.
**Active — Faultline** [AOE, CD 3, R2]: Deal 2 damage to all units on two consecutive hexes in a chosen straight line, excluding self.
**Passive — Shattering Weight:** Ignores 1 ARM on attacks against BARRIER and fortified units.

#### 9. Geode Colossus | T5 Elite
**Stats:** HP 10 · ARM 2 · Move RAY 1 · Geode Crush: 6 damage, R1-1.
**Active — Unbroken Wall** [GUARD, CD 3, R2]: Intercept one attack against any chosen ally within 2; prevent the first 2 damage dealt to this unit by that hit.
**Passive — Living Mountain:** Cannot be pushed, pulled or rooted by units below Elite or Vehicle Siege class.

#### 10. Crown Warden | T4 Elite
**Stats:** HP 10 · ARM 2 · Move STEP 2 · Crown Hammer: 4 damage, R1-2.
**Active — Royal Sanction** [MARK, CD 2, R3]: Mark one enemy within 3 so the next allied attack deals +2 damage, then consume mark.
**Passive — Sovereign Presence:** Adjacent friendly Defenders gain +1 raw attack damage.

#### 11. Facet Mason | T2 Engineer
**Stats:** HP 7 · ARM 2 · Move STEP 2 · Mason Drill: 2 damage, R1-1.
**Active — Rebuild Facet** [BARRIER, CD 2, R1]: Restore 3 HP to a friendly crystal barrier or build 1 new adjacent 2-HP barrier.
**Passive — Architect's Eye:** May identify and reveal enemy hidden traps within 2 at start of activation.

#### 12. Royal Castellan | T5 Commander
**Stats:** HP 11 · ARM 2 · Move RAY 1 · Castellan Scepter: 4 damage, R1-3.
**Active — Castle Doctrine** [GUARD, CD 3, R3]: Two friendly Silex within 3 gain GUARD until their next activation.
**Passive — Throne Shield:** A friendly Guard within 1 may intercept one incoming leader-targeted direct attack per round.

#### 13. Bastion Sled | T2 Vehicle Support
**Stats:** HP 12 · ARM 2 · Move RAY 2 · Crystal Driver: 2 damage, R1-2.
**Active — Deploy Rampart** [BARRIER, CD 3, R1]: Create an adjacent 3-HP crystal cover tile lasting 3 rounds.
**Passive — Fortress Platform:** Allied infantry adjacent to the sled have +1 cover against ranged damage.

#### 14. Facet Ram | T3 Vehicle Assault
**Stats:** HP 11 · ARM 2 · Move RAY 3 · Impact Ram: 4 damage, R1-1.
**Active — Facet Breach** [CHARGE, CD 3, R3]: Move in a straight line up to 3; deal 3 to the first enemy or barrier at destination.
**Passive — Weighted Prow:** Ignores the first 1 damage from counterattacks on its own activation.

#### 15. Geode Walker | T4 Vehicle Siege
**Stats:** HP 14 · ARM 3 · Move STEP 2 · Prismatic Mortar: 4 damage, R2-4.
**Active — Geo-Anchors** [SHIELD, CD 3, R0]: Gain 4 temporary shield HP until the start of your next activation; cannot move next activation.
**Passive — Living Struts:** Stationary Geode Walker gains +1 attack range, maximum 5.

#### 16. Throne Engine | T5 Vehicle Siege
**Stats:** HP 14 · ARM 3 · Move RAY 1 · Crown Breaker: 6 damage, R2-5.
**Active — Royal Edict** [GUARD, CD 4, R3]: Grant GUARD and 2 temporary shield HP to one allied commander or fortification within 3.
**Passive — Monarch's Engine:** Nearby allied infantry within 2 get +1 ARM if they have not moved this round.

### Orbital Commandfall: space units
#### 1. Facet Surveyor | T1 Space Scout
**Stats:** HP 7 · ARM 1 · Move DIAG 3 · Survey Ray: 1 damage, R1-2.
**Active — Facet Scan** [REVEAL, CD 2, R4]: Reveal enemy cloaked ships and mines within 4 until the end of the round.
**Passive — Charted Approach:** Ignores nebula vision penalties when scouting.

#### 2. Prism Escort | T1 Interceptor
**Stats:** HP 7 · ARM 1 · Move LEAP 3 · Prism Needles: 2 damage, R1-2.
**Active — Escort Screen** [INTERCEPT, CD 2, R2]: Redirect one basic attack from a protected adjacent friendly ship to this escort.
**Passive — Reflective Hull:** Takes 1 less damage from a Fighter's first strike each round.

#### 3. Lattice Corvette | T2 Corvette
**Stats:** HP 9 · ARM 2 · Move RAY 2 · Lattice Shot: 3 damage, R1-3.
**Active — Linked Grid** [SHIELD, CD 3, R2]: Give 2 shield HP to a smaller ally within 2 until the next round.
**Passive — Segmented Hull:** Gain +1 ARM while adjacent to a Silex ship with 10 or more max HP.

#### 4. Tessera Frigate | T2 Frigate
**Stats:** HP 11 · ARM 2 · Move DIAG 2 · Tessera Cannons: 3 damage, R2-4.
**Active — Angle Reflect** [COUNTER, CD 3, R2]: Redirect the next 2 damage received by an ally within 2 back to the attacker as return damage.
**Passive — Tiled Armor:** Armor reduction against an unmarked ranged basic hit increases by 1 when stationary.

#### 5. Pillar Destroyer | T3 Destroyer
**Stats:** HP 13 · ARM 2 · Move RAY 2 · Pillar Lance: 4 damage, R2-4.
**Active — Anchored Salvo** [PIERCE, CD 3, R4]: Make a 5-damage attack ignoring 2 ARM at a visible target in range 2 to 4; cannot move this activation.
**Passive — Keel Foundation:** Cannot be moved by ship abilities from ships below Cruiser class.

#### 6. Obelisk Cruiser | T3 Cruiser
**Stats:** HP 15 · ARM 3 · Move RAY 2 · Obelisk Array: 4 damage, R2-5.
**Active — Obelisk Screen** [BARRIER, CD 4, R2]: Place two adjoining 3-HP crystal barriers within 2, blocking movement and line of sight for 2 rounds.
**Passive — Silicate Core:** Restore 1 HP if it did not move or attack on its activation, max once per round.

#### 7. Crown Bastion | T4 Space Bastion
**Stats:** HP 21 · ARM 4 · Move RAY 1 · Royal Lance: 5 damage, R2-5.
**Active — Citadel Field** [SHIELD, CD 4, R3]: Three allied Silex spacecraft within 3 each gain 2 temporary shield HP until your next activation.
**Passive — Crown Formation:** Adjacent ships gain +1 ARM as long as Bastion is stationary.

#### 8. Monolith Battleship | T4 Battleship
**Stats:** HP 19 · ARM 3 · Move RAY 1 · Monolith Broadside: 6 damage, R2-5.
**Active — Stone Judgment** [AOE, CD 4, R5]: Fire a 4-damage area strike at one hex and 2 damage on all adjacent hexes.
**Passive — Inertial Mass:** Unaffected by forced movement and by Fighter-class pin effects.

#### 9. Citadel Dreadnought | T5 Dreadnought
**Stats:** HP 22 · ARM 4 · Move RAY 1 · Citadel Cannon: 7 damage, R2-6.
**Active — Impenetrable Front** [GUARD, CD 4, R2]: Intercept one attack aimed at a friendly LEADER within 2 and reduce its damage by 3 before armor.
**Passive — Royal Ramparts:** Gain +1 ARM while stationary; cannot exceed 4 ARM total.

#### 10. Throne Ark | T5 Flagship
**Stats:** HP 22 · ARM 3 · Move RAY 1 · Throne Beam: 6 damage, R2-6.
**Active — King's Decree** [RALLY, CD 4, R4]: Up to three friendly Silex units within 4 may each gain 2 temporary shields or +1 next-attack damage.
**Passive — Seat of Empire:** Friendly Silex ships within 3 count as stationary for their armor passives only if they themselves did not move this round; Ark is a killable LEADER.

## Pygmaei
**Combat identity:** Gnome-dwarf war foundries; swift infantry and light ships, ruthless aggression, destructive weaponry, fragile defenses.
**Faction rule:** War Momentum: first Pygmaei unit to defeat an enemy in a round may immediately step 1 free hex if legal; this is not another attack.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Rush Trooper | T1 Line
**Stats:** HP 5 · ARM 0 · Move ZIGZAG 3 · Rapid Carbine: 3 damage, R1-2.
**Active — Rolling Barrage** [DAMAGE, CD 2, R2]: Attack a visible target for 3 damage, then step 1 hex away from it if a hex is open.
**Passive — Breakthrough Pace:** After eliminating an enemy, may advance 1 empty hex once per round.

#### 2. Ashrunner | T1 Scout
**Stats:** HP 4 · ARM 0 · Move KNIGHT 3 · Soot Rifle: 2 damage, R1-2.
**Active — Ash Dash** [DASH, CD 2, R3]: Leap to any empty hex within 3 and attack an adjacent enemy for 1 raw damage.
**Passive — Smoke Legs:** Does not pay extra movement cost when leaving cover or rubble.

#### 3. Volt Striker | T2 Assault
**Stats:** HP 5 · ARM 0 · Move LEAP 3 · Arc Hammer: 4 damage, R1-1.
**Active — Shock Crack** [DISABLE, CD 3, R1]: Deal 2 armor-piercing damage to an adjacent target and DISABLE its next active ability.
**Passive — Capacitor Drive:** First attack made immediately after a LEAP gains +1 raw damage.

#### 4. Ember Vanguard | T2 Heavy
**Stats:** HP 7 · ARM 0 · Move RAY 3 · Flame Barrage: 3 damage, R1-2.
**Active — Fireline Sweep** [AOE, CD 3, R2]: Deal 2 damage to enemies on a chosen line of two hexes; also ignite those hexes for 1 round.
**Passive — Heat Hardened:** Ignores environmental burn hazard damage, not enemy weapon damage.

#### 5. Breach Commando | T2 Saboteur
**Stats:** HP 4 · ARM 0 · Move KNIGHT 3 · Breach Shotgun: 4 damage, R1-1.
**Active — Door Cracker** [ARMOR_BREAK, CD 2, R1]: Deal 2 damage and apply EXPOSED (-1 ARM) to adjacent enemy for its next activation.
**Passive — Demolition Kit:** Deals +2 damage to barriers and fortifications with basic attacks.

#### 6. Starbreaker Gunner | T4 Marksman
**Stats:** HP 4 · ARM 0 · Move RAY 2 · Starbreaker Rail: 5 damage, R2-5.
**Active — Overpenetration** [PIERCE, CD 3, R5]: Fire a 6-damage attack ignoring 2 ARM down a clear line; first target absorbs the strike.
**Passive — Recoil Braced:** Basic attack gets +1 raw damage if it did not move this activation.

#### 7. Minejack Saboteur | T3 Saboteur
**Stats:** HP 4 · ARM 0 · Move ZIGZAG 3 · Blast Pistol: 2 damage, R1-2.
**Active — Buried Charge** [MINE, CD 2, R1]: Place an adjacent 3-damage mine that detonates the first enemy to enter its hex.
**Passive — Quick Fuse:** Can disarm an adjacent hostile mine at activation start without consuming its action.

#### 8. Forge Warden | T3 Defender
**Stats:** HP 7 · ARM 2 · Move STEP 2 · Heavy Stubber: 3 damage, R1-3.
**Active — Crossfire Lock** [SUPPRESS, CD 3, R3]: Choose a visible enemy within 3; its movement maximum is reduced by 2 next activation.
**Passive — Welding Plates:** When stationary, reduce the first ranged hit each round by 1 damage.

#### 9. Combat Engineer | T2 Engineer
**Stats:** HP 5 · ARM 0 · Move STEP 3 · Rivet Driver: 2 damage, R1-2.
**Active — Field Rebuild** [REPAIR, CD 2, R2]: Repair 3 HP on an allied vehicle within 2 or 2 HP on an allied infantry unit.
**Passive — Portable Foundry:** Can traverse friendly mine hexes without triggering them.

#### 10. Shock Medic | T3 Support
**Stats:** HP 5 · ARM 0 · Move ZIGZAG 3 · Stun Dart: 2 damage, R1-2.
**Active — Combat Stim** [HEAL, CD 2, R2]: Restore 2 HP to an allied infantry unit within 2 and add +1 move next activation.
**Passive — Adrenaline Reserves:** Once per round, allies within 1 ignore a single point of hazard damage.

#### 11. Borglow Vanguard | T4 Elite
**Stats:** HP 8 · ARM 0 · Move LEAP 3 · Vanguard Blaster: 5 damage, R1-3.
**Active — Bloodrush** [CHARGE, CD 3, R3]: Move up to 3 toward an enemy in a straight line, then strike it for 5 raw damage if adjacent.
**Passive — No Chains:** Cannot be ROOTED by enemy abilities below tier 4; lore/name spelling flagged for review.

#### 12. Iron Marshal | T5 Commander
**Stats:** HP 9 · ARM 0 · Move RAY 3 · Marshal Cannon: 5 damage, R1-3.
**Active — War Orders** [RALLY, CD 3, R3]: Two allied Pygmaei within 3 gain +1 attack damage and +1 movement next activation.
**Passive — The Advance:** When a friendly Pygmaei defeats an enemy within 3, adjacent allies gain +1 move next activation, non-stacking.

#### 13. Razorbug Speeder | T2 Vehicle Scout
**Stats:** HP 7 · ARM 0 · Move LEAP 4 · Rotary Blaster: 3 damage, R1-2.
**Active — Boost Pass** [DASH, CD 2, R3]: Move to any empty hex within 3 ignoring intervening units; may not attack during this special action.
**Passive — Low Profile:** Ranged attacks from 4 or more hexes away deal 1 less damage to the Speeder.

#### 14. Blast Furnace Tank | T3 Vehicle Assault
**Stats:** HP 9 · ARM 0 · Move RAY 3 · Furnace Cannon: 5 damage, R1-3.
**Active — Meltdown Shot** [AOE, CD 3, R3]: Deal 4 damage to center and 2 to units on adjacent hexes; self takes 1 damage.
**Passive — Smelter Skin:** Immune to burn terrain damage; does not block other explosions.

#### 15. Burrowdrill | T3 Vehicle Support
**Stats:** HP 10 · ARM 0 · Move ZIGZAG 2 · Hydraulic Ram: 4 damage, R1-1.
**Active — Tunnel Break** [REPOSITION, CD 3, R3]: Teleport this vehicle to an empty rubble or ground hex within 3; cannot pass a fortified wall.
**Passive — Subsurface Engine:** Ignores ground rough-terrain cost, but not blocking terrain.

#### 16. Voltfang Gun Walker | T5 Vehicle Siege
**Stats:** HP 12 · ARM 2 · Move RAY 2 · Voltfang Battery: 6 damage, R2-4.
**Active — Capacitor Volley** [PIERCE, CD 3, R4]: Fire one armor-piercing 7-damage shot ignoring 2 ARM; become EXPOSED for next enemy activation.
**Passive — Overkill Circuit:** If basic attack destroys cover, carry 1 extra raw damage into a unit directly behind it on the same line.

### Orbital Commandfall: space units
#### 1. Razor Fighter | T1 Fighter
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Dual Cutters: 3 damage, R1-2.
**Active — Full Throttle** [DASH, CD 2, R3]: Jump to an empty hex within 3, ignoring intervening units, and gain +1 raw attack damage on next activation.
**Passive — Redline Engine:** Gain +1 raw damage against targets that moved this round.

#### 2. Rift Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 4 · Rift Cannons: 3 damage, R1-2.
**Active — Snap Intercept** [INTERCEPT, CD 2, R2]: Jump adjacent to one allied ship within 2 and intercept the next fighter attack against it.
**Passive — Turnfighter:** After intercepting, may rotate or step 1 free hex if still alive.

#### 3. Needle Gunship | T2 Fighter
**Stats:** HP 5 · ARM 0 · Move DIAG 3 · Needle Volley: 3 damage, R1-3.
**Active — Piercing Burst** [PIERCE, CD 3, R3]: Deal 4 damage ignoring 1 ARM to one visible ship within 3.
**Passive — Low Silhouette:** Cannot be targeted by basic capital cannons beyond 4 hexes until revealed.

#### 4. Blastwake Corvette | T2 Corvette
**Stats:** HP 7 · ARM 0 · Move ZIGZAG 3 · Rotary Battery: 4 damage, R1-3.
**Active — Drive-By** [BARRAGE, CD 3, R3]: Move up to 2 then attack two different enemies for 2 damage each within 3.
**Passive — Aggressive Thrusters:** Gain +1 move after an allied Pygmaei kill this round.

#### 5. Skirmish Frigate | T2 Frigate
**Stats:** HP 9 · ARM 0 · Move RAY 3 · Skirmish Guns: 4 damage, R2-3.
**Active — Flanking Run** [REPOSITION, CD 2, R2]: Shift this ship to an empty hex within 2 and give its next shot +1 damage.
**Passive — Wolfpack:** Attack gains +1 raw damage if another ally threatens the same target within 2.

#### 6. Annihilator Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 0 · Move RAY 3 · Annihilator Array: 5 damage, R2-4.
**Active — Saturation Strike** [AOE, CD 3, R4]: Deal 4 to the selected hex and 2 to each adjacent hex, including allies.
**Passive — Ordnance Factory:** Attacks against capital ships ignore 1 armor if stationary.

#### 7. Furnace Cruiser | T3 Cruiser
**Stats:** HP 13 · ARM 1 · Move ZIGZAG 2 · Furnace Broadside: 5 damage, R2-4.
**Active — Heat Flood** [SUPPRESS, CD 3, R4]: Deal 3 damage and reduce a visible target's movement maximum by 2 next activation.
**Passive — Open Kilns:** Does not take environmental heat hazards; attacks ignite destroyed terrain cover.

#### 8. Thunderhead Artillery Barge | T4 Space Support
**Stats:** HP 12 · ARM 0 · Move RAY 1 · Thunderhead Shell: 6 damage, R3-6.
**Active — Meteor Salvo** [BOMBARD, CD 4, R6]: Deal 4 blast damage to a chosen visible hex within 6 and 2 to adjacent hexes, including allies.
**Passive — Fixed Battery:** Cannot make its basic attack in the same activation after moving.

#### 9. Starbreaker Siege Ship | T5 Battleship
**Stats:** HP 17 · ARM 1 · Move RAY 1 · Starbreaker Lance: 8 damage, R3-6.
**Active — Pierce the Horizon** [PIERCE, CD 4, R6]: One 9-damage attack ignoring 3 ARM along a straight line to a visible target.
**Passive — Volatile Reactor:** If destroyed, deal 2 damage to ships immediately adjacent, allies included.

#### 10. Revenant Command Ship | T5 Flagship
**Stats:** HP 20 · ARM 1 · Move RAY 2 · Revenant Salvo: 7 damage, R2-5.
**Active — Total Offensive** [RALLY, CD 4, R4]: Up to three allied ships within 4 gain +2 attack damage for the current round but become EXPOSED next activation.
**Passive — Victory at Any Cost:** Friendly Pygmaei within 3 gain +1 movement while this exposed, killable LEADER is alive.

## Meeples / Universal Council
**Combat identity:** Multispecies coalition, defensive diplomacy, adaptable formations, nonlethal options and field support.
**Faction rule:** Coalition Cover: allied units occupying adjacent hexes may donate one GUARD intercept each round when their own ability or passive explicitly permits it.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Council Infantry | T1 Line
**Stats:** HP 5 · ARM 1 · Move STEP 2 · Council Carbine: 2 damage, R1-2.
**Active — Pact Suppression** [SUPPRESS, CD 2, R2]: Visible enemy within 2 loses 1 movement next activation; does not stack.
**Passive — Council Standard:** Gain +1 ARM while adjacent to a non-Meeple coalition infantry unit (campaign roster tag).

#### 2. Pact Scout | T1 Scout
**Stats:** HP 4 · ARM 0 · Move KNIGHT 3 · Scout Pistol: 2 damage, R1-2.
**Active — Signal Beacon** [REVEAL, CD 2, R4]: Reveal hidden units and traps within 4 until end of round.
**Passive — Forward Liaison:** May traverse one ally's occupied hex during movement but cannot end there.

#### 3. Accord Ranger | T2 Assault
**Stats:** HP 5 · ARM 0 · Move ZIGZAG 3 · Ranger Rifle: 3 damage, R1-3.
**Active — Measured Volley** [MARK, CD 2, R3]: Mark visible enemy for +1 damage on the next allied hit this round.
**Passive — Ranger Training:** Ignores 1 point of cover reduction when shooting from a cover hex.

#### 4. Accord Marksman | T2 Marksman
**Stats:** HP 4 · ARM 0 · Move DIAG 2 · Precision Rifle: 3 damage, R2-5.
**Active — Priority Target** [PIERCE, CD 3, R5]: Make a 4-damage precision shot ignoring 1 ARM at a visible target.
**Passive — Disciplined Aim:** Gain +1 raw attack damage while stationary.

#### 5. Council Guard | T2 Defender
**Stats:** HP 7 · ARM 2 · Move RAY 2 · Guard Pike: 2 damage, R1-2.
**Active — Council Intercept** [GUARD, CD 2, R1]: Intercept one direct attack against an adjacent ally until next activation.
**Passive — Shield Discipline:** When protecting a commander, take 1 less damage from the intercepted attack.

#### 6. Concord Sentinel | T3 Heavy
**Stats:** HP 7 · ARM 1 · Move STEP 2 · Concord Cannon: 3 damage, R1-3.
**Active — Stabilize Lines** [SHIELD, CD 3, R2]: Give two friendly infantry units within 2 each 2 temporary shield HP.
**Passive — Standfast:** A friendly ally adjacent to Sentinel does not lose movement to SUPPRESS effects, once per round.

#### 7. Honor Guard | T4 Elite
**Stats:** HP 8 · ARM 1 · Move RAY 2 · Honor Halberd: 4 damage, R1-2.
**Active — Last Stand** [GUARD, CD 3, R2]: Intercept one leader-directed hit within 2 and reduce its raw damage by 2 before ARM.
**Passive — Oathkeeper:** Cannot voluntarily target CAPTURED or noncombatant units with damaging active effects.

#### 8. Coalition Marshal | T5 Commander
**Stats:** HP 9 · ARM 1 · Move STEP 2 · Marshal Pistol: 4 damage, R1-3.
**Active — Unified Assault** [RALLY, CD 3, R3]: Two allies of any coalition species within 3 get +1 attack and +1 armor until next activation.
**Passive — Common Cause:** Adjacent allied units may use GUARD even if they are of different races when coalition permissions allow.

#### 9. Mediation Envoy | T3 Support
**Stats:** HP 5 · ARM 0 · Move STEP 2 · Stun Baton: 1 damage, R1-1.
**Active — Ceasefire Order** [DISABLE, CD 3, R2]: One enemy within 2 cannot attack or use an active ability during its next activation, but may move.
**Passive — Protected Diplomat:** If not attacked this round, gain 2 temporary shield HP at next activation start.

#### 10. Barrier Medic | T2 Support
**Stats:** HP 5 · ARM 0 · Move STEP 2 · Medic Sidearm: 1 damage, R1-2.
**Active — Triage Field** [HEAL, CD 2, R2]: Heal a friendly infantry for 3 HP within 2, or clear ROOTED instead.
**Passive — Noncombatant Mark:** Attackers receive MARKED if they strike a medic while a Council Guard is within 2.

#### 11. Assembly Engineer | T3 Engineer
**Stats:** HP 5 · ARM 1 · Move STEP 2 · Utility Bolt: 2 damage, R1-2.
**Active — Rapid Emplacement** [BARRIER, CD 2, R1]: Create a 3-HP cover field on one adjacent empty hex lasting 2 rounds.
**Passive — Field Logistics:** Adjacent vehicles receive +1 HP when repaired by any allied engineer, max once per round.

#### 12. Peacekeeper Captain | T4 Commander
**Stats:** HP 9 · ARM 1 · Move RAY 2 · Captain Blaster: 4 damage, R1-3.
**Active — Protect Civilians** [GUARD, CD 3, R3]: Grant GUARD to up to two friendly units within 3, each limited to one intercept.
**Passive — Rules of Engagement:** If no enemy unit is killed by allies this round, gain +1 ARM until next activation.

#### 13. Peacekeeper Carrier | T2 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move STEP 2 · Defense Turret: 2 damage, R1-3.
**Active — Evacuate** [REPOSITION, CD 3, R2]: Move one adjacent allied infantry to any empty hex within 2 of this vehicle.
**Passive — Medical Hold:** An infantry ally ending a move adjacent heals 1 HP, max once per round.

#### 14. Accord Strider | T3 Vehicle Scout
**Stats:** HP 7 · ARM 1 · Move LEAP 3 · Accord Beam: 3 damage, R1-3.
**Active — Watch Corridor** [REVEAL, CD 2, R4]: Reveal hidden enemies and traps along a straight 4-hex corridor.
**Passive — Flexible Suspension:** Ignores movement cost from low terrain.

#### 15. Citadel Shield Walker | T4 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move STEP 2 · Shield Lance: 4 damage, R1-3.
**Active — Mobile Bastion** [SHIELD, CD 3, R2]: Give 3 temporary shields to one friendly unit within 2.
**Passive — Pact Barrier:** Adjacent allied infantry gain +1 cover versus ranged attacks.

#### 16. Unity Command Platform | T5 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move RAY 1 · Command Arc: 4 damage, R2-4.
**Active — Unified Signal** [RALLY, CD 4, R4]: Up to three allied units within 4 gain +1 raw attack damage this round or 2 shields each.
**Passive — Relay Uplink:** Allied Council units within 3 may share revealed vision and targeting.

### Orbital Commandfall: space units
#### 1. Council Scoutship | T1 Space Scout
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Scout Lasers: 1 damage, R1-2.
**Active — Beacon Sweep** [REVEAL, CD 2, R4]: Reveal cloaked ships, concealed debris hazards and mines within 4.
**Passive — Council Transponder:** Adjacent friendly ships ignore one nebula vision penalty.

#### 2. Covenant Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 3 · Covenant Pulse: 2 damage, R1-2.
**Active — Guardian Vector** [INTERCEPT, CD 2, R2]: Jump adjacent to a friend within 2 and redirect the next incoming fighter attack to this vessel.
**Passive — Security Patrol:** +1 basic attack damage against ships threatening an allied Ark or flagship.

#### 3. Council Corvette | T2 Corvette
**Stats:** HP 7 · ARM 1 · Move RAY 3 · Standard Guns: 3 damage, R1-3.
**Active — Covering Volley** [SUPPRESS, CD 2, R3]: Target visible ship within 3 loses 1 movement next activation.
**Passive — Escort Protocol:** Attacks against an allied Carrier adjacent to this ship deal 1 less raw damage.

#### 4. Accord Frigate | T2 Frigate
**Stats:** HP 9 · ARM 1 · Move ZIGZAG 2 · Accord Batteries: 3 damage, R2-4.
**Active — Defensive Exchange** [GUARD, CD 3, R2]: Guard an allied ship within 2 and redirect the next direct ranged attack onto this frigate.
**Passive — Diverse Crew:** Unaffected by first non-damaging DEBUFF in a battle.

#### 5. Concord Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 1 · Move RAY 2 · Concord Cannon: 4 damage, R2-4.
**Active — Stern Warning** [MARK, CD 3, R4]: Mark one visible opponent; next allied ship to hit it deals +2 raw damage.
**Passive — Disciplined Fire:** May choose to reduce own damage to 1 to leave a target ship at 1 HP instead of killing it.

#### 6. Assembly Cruiser | T3 Cruiser
**Stats:** HP 13 · ARM 2 · Move STEP 2 · Assembly Array: 4 damage, R2-4.
**Active — Mutual Shield** [SHIELD, CD 3, R3]: Choose two different allies within 3; each gains 2 temporary shields.
**Passive — Shipyard Reserves:** Adjacent allied corvettes heal 1 HP at beginning of Assembly Cruiser's activation.

#### 7. Unity Carrier | T4 Carrier
**Stats:** HP 14 · ARM 1 · Move RAY 2 · Carrier Battery: 3 damage, R2-4.
**Active — Rapid Launch** [SPAWN, CD 3, R1]: Deploy a 2-HP Council fighter token into an empty adjacent hex, max two tokens on map from this carrier.
**Passive — Alliance Hangar:** Allied fighters within 2 get +1 movement maximum on next activation.

#### 8. Coalition Battleship | T4 Battleship
**Stats:** HP 17 · ARM 2 · Move RAY 1 · Coalition Broadside: 6 damage, R2-5.
**Active — United Salvo** [DAMAGE, CD 4, R5]: Make a 7-damage single-target attack; target must be revealed by a coalition scout or within normal line of sight.
**Passive — Shared Fire Control:** Gain +1 raw attack damage when adjacent to any allied frigate.

#### 9. Sanctuary Hospital Ship | T3 Space Support
**Stats:** HP 12 · ARM 1 · Move STEP 2 · Defense Emitter: 1 damage, R1-2.
**Active — Emergency Evacuation** [HEAL, CD 3, R3]: Restore 4 HP to any allied ship within 3 and clear its DISABLED status.
**Passive — Neutral Emblem:** It cannot gain attack buffs; when stationary, gains 2 temporary shield HP at activation start.

#### 10. Council Command Ark | T5 Flagship
**Stats:** HP 20 · ARM 2 · Move RAY 1 · Ark Cannon: 5 damage, R2-5.
**Active — Council Mandate** [RALLY, CD 4, R4]: Up to three allied units within 4 gain 2 temporary shield HP and +1 movement on their next activation.
**Passive — Last Institution:** Allies within 2 may intercept leader-directed attacks via explicit GUARD abilities; Ark is a killable LEADER.

## Inhumanus
**Combat identity:** Hyper-capitalist low-gravity technologists relying on gravity manipulation, contracted assets, drones and locked military specialization.
**Faction rule:** Corporate Charter: one strategic specialization (Assault, Bulwark, Naval or Commerce) is locked before deployment; the battle roster reflects that choice.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Contract Trooper | T1 Line
**Stats:** HP 4 · ARM 1 · Move STEP 2 · Service Rifle: 2 damage, R1-2.
**Active — Paid Overtime** [RALLY, CD 3, R2]: One friendly infantry within 2 gains +1 raw attack damage on its next attack.
**Passive — Cost Per Kill:** If this unit survives its first attack each battle, it gains 1 temporary shield HP.

#### 2. Pale Operative | T1 Scout
**Stats:** HP 3 · ARM 0 · Move KNIGHT 3 · Covert Dart: 2 damage, R1-2.
**Active — Silent Takeover** [CLOAK, CD 3, R0]: Become CLOAKED until after next activation; attacking or ending adjacent to enemy reveals it.
**Passive — Low-Gravity Frame:** Movement ignores one hex of difficult terrain each activation.

#### 3. Nullstride Agent | T2 Saboteur
**Stats:** HP 3 · ARM 0 · Move PHASE 3 · Null Blade: 3 damage, R1-1.
**Active — Null Passage** [TELEPORT, CD 3, R3]: Teleport to any visible unoccupied hex within 3, ignoring units and barriers.
**Passive — Shadow Contract:** If it begins an activation unmarked, its first attack ignores 1 ARM.

#### 4. Vector Sniper | T2 Marksman
**Stats:** HP 3 · ARM 0 · Move DIAG 2 · Vector Rail: 3 damage, R2-5.
**Active — Trajectory Audit** [PIERCE, CD 3, R5]: Attack for 4 damage ignoring 2 ARM when a straight sight line exists.
**Passive — Distance Dividend:** Gain +1 raw attack damage against enemies at 4 or more range.

#### 5. Gravity Weaver | T3 Support
**Stats:** HP 4 · ARM 0 · Move STEP 2 · Gravity Pulse: 2 damage, R1-3.
**Active — Local Singularity** [PULL, CD 3, R3]: Pull visible enemy within 3 exactly 1 hex toward a designated empty adjacent hex; no wall traversal.
**Passive — Field Equation:** Adjacent enemies lose 1 maximum movement unless they are a Vehicle Siege or Elite.

#### 6. Asset Recovery Agent | T3 Capture
**Stats:** HP 5 · ARM 1 · Move ZIGZAG 2 · Repossession Baton: 2 damage, R1-1.
**Active — Collateral Seizure** [CAPTURE, CD 3, R1]: CAPTURE an adjacent non-leader enemy at 2 HP or below, preventing its action until freed.
**Passive — Enforceable Clause:** Targets currently MARKED by Inhumanus suffer -1 ARM from this unit.

#### 7. Phase Auditor | T3 Support
**Stats:** HP 4 · ARM 0 · Move PHASE 2 · Audit Beam: 2 damage, R1-3.
**Active — Phase Freeze** [DISABLE, CD 3, R3]: Enemy within 3 loses active ability on its next activation and cannot gain temporary shield this round.
**Passive — Clause Inspector:** Can reveal enemy buffs and traps within 2 at activation start.

#### 8. Spectrum Analyst | T2 Support
**Stats:** HP 4 · ARM 0 · Move STEP 2 · Spectral Probe: 1 damage, R1-3.
**Active — Data Exposure** [MARK, CD 2, R4]: MARK one revealed enemy within 4 so next allied hit receives +2 damage.
**Passive — Market Intelligence:** Friendly Inhumanus within 2 ignore 1 point of cover reduction when targeting MARKED enemies.

#### 9. Security Drone | T2 Scout
**Stats:** HP 3 · ARM 0 · Move LEAP 3 · Pulse Carbine: 2 damage, R1-2.
**Active — Perimeter Sweep** [REVEAL, CD 2, R3]: Reveal cloaked enemies and mines within 3; MARK the nearest revealed hostile until end of round.
**Passive — Automated Protocol:** Unaffected by fear, morale, and charisma tactical modifiers.

#### 10. Corporate Warden | T4 Defender
**Stats:** HP 7 · ARM 2 · Move RAY 2 · Ward Baton: 3 damage, R1-2.
**Active — Asset Safeguard** [GUARD, CD 2, R2]: Intercept one direct attack against allied support, commander, or vehicle within 2.
**Passive — Paid Protection:** When adjacent to corporate LEADER, gain +1 ARM against the first hit each round.

#### 11. Cybernetic Executor | T4 Elite
**Stats:** HP 8 · ARM 1 · Move LEAP 3 · Executor Talon: 5 damage, R1-2.
**Active — Hostile Liquidation** [CHARGE, CD 3, R3]: Dash 2 hexes on a clear line and attack adjacent enemy for 5 damage ignoring 1 ARM.
**Passive — Reinforced Skeleton:** Ignore the first 1 damage from a direct attack each round.

#### 12. Executive Adjudicator | T5 Commander
**Stats:** HP 9 · ARM 1 · Move STEP 2 · Adjudicator Arc: 4 damage, R1-4.
**Active — Binding Contract** [DISABLE, CD 4, R4]: A visible enemy within 4 cannot attack or use active ability during next activation, but may move.
**Passive — Executive Immunity:** The first enemy MARK applied to this commander each battle is ignored.

#### 13. Vector Skimmer | T2 Vehicle Scout
**Stats:** HP 7 · ARM 1 · Move LEAP 4 · Vector Repeater: 3 damage, R1-2.
**Active — Gravity Slingshot** [DASH, CD 2, R4]: Jump to any visible unoccupied hex within 4, ignoring ground cover.
**Passive — Low Gravity:** Ignore normal movement costs from slopes and rubble.

#### 14. Asset Recovery Walker | T3 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move RAY 2 · Recovery Cannon: 4 damage, R1-3.
**Active — Foreclosure Claw** [PULL, CD 3, R2]: Pull an enemy non-siege unit within 2 one hex closer, then deal 1 unavoidable damage.
**Passive — Seizure Rig:** Enemies CAPTURED beside this walker require two rescue actions to free.

#### 15. Graviton Suppressor | T4 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move STEP 2 · Suppressor Arc: 3 damage, R2-4.
**Active — Anchor Well** [PIN, CD 3, R4]: ROOT two enemy units that occupy adjacent hexes within a 4-hex targeting range for their next activations.
**Passive — Mass Control:** Immune to hostile PUSH or PULL while this unit did not move this round.

#### 16. Parallax Siege Platform | T5 Vehicle Siege
**Stats:** HP 12 · ARM 2 · Move RAY 1 · Parallax Driver: 6 damage, R3-6.
**Active — Market Correction** [PIERCE, CD 4, R6]: Fire one 8-damage shot ignoring 2 ARM to a visible target in range 3-6.
**Passive — Premium Ammunition:** Basic attacks ignore 1 armor against shields and fortifications.

### Orbital Commandfall: space units
#### 1. Prospect Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 4 · Prospect Pulses: 2 damage, R1-2.
**Active — First Claim** [MARK, CD 2, R3]: MARK an enemy within 3 for +1 damage on next friendly attack.
**Passive — Capital Scout:** First to enter a previously unoccupied objective hex earns 1 temporary shield HP.

#### 2. Mercator Corvette | T2 Corvette
**Stats:** HP 7 · ARM 1 · Move ZIGZAG 3 · Mercator Guns: 3 damage, R1-3.
**Active — Arbitrage Run** [DASH, CD 2, R3]: Move up to 3 hexes through empty path and gain +1 raw damage on next basic attack.
**Passive — Market Flex:** Repositioned this round: takes 1 less damage from next ranged shot.

#### 3. Proxy Frigate | T2 Frigate
**Stats:** HP 9 · ARM 1 · Move RAY 3 · Proxy Battery: 3 damage, R2-4.
**Active — Subcontract** [SPAWN, CD 4, R1]: Deploy one 2-HP mercenary escort token into an adjacent free hex, max one at a time.
**Passive — Proxy Personnel:** First enemy attack against this vessel each battle reduces damage by 1.

#### 4. Arbitration Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 1 · Move RAY 2 · Arbitration Beam: 4 damage, R2-4.
**Active — Injunction** [DISABLE, CD 3, R4]: Prevent an enemy ship within 4 from using its active ability on its next activation.
**Passive — Contract Enforcement:** Basic attacks deal +1 damage against MARKED enemy ships.

#### 5. Parallax Cruiser | T3 Cruiser
**Stats:** HP 13 · ARM 2 · Move PHASE 2 · Parallax Beam: 4 damage, R2-4.
**Active — Gravity Fold** [TELEPORT, CD 3, R2]: Teleport this cruiser or a friendly corvette within 2 to a different free hex within 2 of its start, LOS to destination.
**Passive — Dimensional Hedge:** Ignore one enemy ROOT effect per battle.

#### 6. Dividend Carrier | T4 Carrier
**Stats:** HP 14 · ARM 1 · Move RAY 2 · Dividend Turret: 3 damage, R2-4.
**Active — Preferred Launch** [SPAWN, CD 3, R1]: Deploy one 2-HP autonomous fighter adjacent, max two tokens per carrier.
**Passive — Shareholder Defense:** Fighters within 2 receive +1 temporary shield HP when they activate.

#### 7. Hegemony Battleship | T4 Battleship
**Stats:** HP 17 · ARM 2 · Move RAY 1 · Hegemony Lance: 6 damage, R2-5.
**Active — Controlling Stake** [MARK, CD 3, R5]: Mark an enemy ship within 5 so next three allied direct attacks each gain +1 damage, then clear.
**Passive — Monopoly Gunline:** Gain +1 attack damage when attacking ships without adjacent allies.

#### 8. Acquisition Vessel | T3 Space Support
**Stats:** HP 12 · ARM 1 · Move STEP 2 · Capture Laser: 2 damage, R1-3.
**Active — Asset Tow** [BOARD, CD 3, R2]: Claim a disabled enemy Corvette or Frigate within 2 at 2 HP or less if no allied enemy unit adjacent to it; remains inert this battle.
**Passive — Legal Salvage:** Claimed hulls can be refit after battle if the Acquisition Vessel survives.

#### 9. Sovereign Ledger | T5 Space Bastion
**Stats:** HP 19 · ARM 3 · Move RAY 1 · Ledger Battery: 5 damage, R2-5.
**Active — Debt Collection** [DEBUFF, CD 4, R4]: Choose two enemy ships within 4: each loses 1 ARM until the end of its next activation.
**Passive — Asset Fortress:** Adjacent allied ships gain 1 shield HP at the start of their activations, once per round.

#### 10. Executive Flagship | T5 Flagship
**Stats:** HP 20 · ARM 2 · Move RAY 1 · Executive Prism: 6 damage, R2-5.
**Active — Hostile Merger** [DISABLE, CD 4, R5]: DISABLE enemy active ability within 5 and give +2 shield HP to one visible ally within 3.
**Passive — Profit Directive:** Nearby allied ships within 3 gain +1 attack damage against MARKED targets; flagship is a killable LEADER.

## Space Pirates / Pirate's Reach
**Combat identity:** Mixed-species privateers with heavy broadsides, boarding, scavenging, raiding and volatile crews.
**Faction rule:** Claim the Wreck: ships marked SALVAGEABLE can be reclaimed after battle by surviving salvage-capable pirate units, never resurrected mid-battle.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Deckhand | T1 Line
**Stats:** HP 5 · ARM 1 · Move STEP 2 · Scrap Pistol: 2 damage, R1-2.
**Active — Dirty Throw** [MARK, CD 2, R2]: Mark adjacent enemy for +1 damage on the next allied attack.
**Passive — Improvised Gear:** May begin each battle on a friendly transport entry hex.

#### 2. Boarding Corsair | T1 Assault
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Boarding Cutlass: 3 damage, R1-1.
**Active — Hook and Haul** [PULL, CD 2, R2]: Pull visible non-heavy enemy within 2 one hex toward Corsair into an empty hex.
**Passive — Zero-G Veteran:** Can leap over low obstacles and units using KNIGHT pattern.

#### 3. Salvage Runner | T1 Scout
**Stats:** HP 4 · ARM 0 · Move ZIGZAG 3 · Sawed Rifle: 2 damage, R1-2.
**Active — Scrounge** [REPAIR, CD 2, R1]: Restore 2 HP to adjacent pirate vehicle or 1 HP to pirate infantry; consumes salvage supply if present.
**Passive — Pocket Salvage:** When adjacent to a fresh wreck, gain +1 move next activation.

#### 4. Marauder | T2 Assault
**Stats:** HP 5 · ARM 0 · Move RAY 3 · Marauder SMG: 3 damage, R1-2.
**Active — Pillage Rush** [DASH, CD 2, R3]: Move up to 3 along a clear line, then gain +1 raw damage if attacking an exposed target.
**Passive — Raid Instinct:** After moving onto an objective, gain 1 temporary shield HP until next activation.

#### 5. Broadside Gunner | T3 Heavy
**Stats:** HP 7 · ARM 1 · Move RAY 2 · Deck Cannon: 4 damage, R2-4.
**Active — Cannon Sweep** [AOE, CD 3, R4]: Deal 3 damage to a target hex and 1 to neighbors; may strike friendly units.
**Passive — Powder Load:** Basic attack can damage cover structures without reduction from cover.

#### 6. Grenadier | T2 Saboteur
**Stats:** HP 4 · ARM 0 · Move ZIGZAG 2 · Fragment Shot: 3 damage, R1-2.
**Active — Powder Keg** [MINE, CD 2, R2]: Place a 3-damage trap within 2 that explodes on the next enemy entry.
**Passive — Loose Fuse:** Cannot suffer terrain hazard damage from its own deployed mines.

#### 7. Cutlass Vanguard | T4 Elite
**Stats:** HP 8 · ARM 1 · Move LEAP 3 · Cutlass Arc: 5 damage, R1-1.
**Active — Captain's Charge** [CHARGE, CD 3, R3]: Leap up to 3 to an empty hex and deal 4 damage to one adjacent enemy.
**Passive — Brutal Boarding:** Gain +1 raw damage against a target already adjacent to another Pirate.

#### 8. Void Stalker | T3 Saboteur
**Stats:** HP 4 · ARM 0 · Move PHASE 3 · Stalker Blade: 3 damage, R1-1.
**Active — Ghost Walk** [CLOAK, CD 3, R0]: Become CLOAKED until end of next activation, unless it attacks or ends adjacent to enemy.
**Passive — Shadow Rig:** Its first hit out of CLOAK ignores 1 ARM.

#### 9. Quartermaster Guard | T3 Defender
**Stats:** HP 7 · ARM 2 · Move STEP 2 · Quartermaster Gun: 3 damage, R1-2.
**Active — Protect the Loot** [GUARD, CD 2, R1]: Guard adjacent pirate commander, supply unit, or captured objective until next activation.
**Passive — Tight Inventory:** Adjacent pirate units gain +1 ARM when defending a controlled objective.

#### 10. Wreckwright | T3 Engineer
**Stats:** HP 5 · ARM 1 · Move STEP 2 · Welder Arc: 2 damage, R1-2.
**Active — Field Patch** [REPAIR, CD 2, R2]: Restore 3 HP to an allied vehicle or 2 to infantry within 2.
**Passive — Jury-Rig:** Friendly vehicles within 1 ignore the first DISABLE applied per battle.

#### 11. Plunder Captain | T4 Commander
**Stats:** HP 9 · ARM 1 · Move ZIGZAG 2 · Captain's Blaster: 4 damage, R1-3.
**Active — Prize Order** [RALLY, CD 3, R3]: Two nearby pirate allies gain +1 raw damage and +1 movement next activation.
**Passive — Crew Loyalty:** Allied pirates within 2 may GUARD captain only if equipped with GUARD skill or passive.

#### 12. Freebooter Commander | T5 Commander
**Stats:** HP 9 · ARM 1 · Move RAY 2 · Command Revolver: 5 damage, R1-3.
**Active — All Hands** [RALLY, CD 4, R4]: Up to three pirates within 4 may move 1 free hex immediately but cannot attack in this free action.
**Passive — Black Flag:** Friendly pirates within 3 gain +1 basic attack damage against enemy objective-holders.

#### 13. Wreckrunner Buggy | T2 Vehicle Scout
**Stats:** HP 7 · ARM 1 · Move LEAP 4 · Scrap Repeater: 3 damage, R1-2.
**Active — Scavenge Route** [DASH, CD 2, R4]: Jump to an open hex within 4; can ignore rubble and low cover in transit.
**Passive — Stolen Engine:** After defeating an enemy Scout, gain 1 temporary shield HP.

#### 14. Corsair Breacher | T3 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move RAY 3 · Breacher Cannon: 4 damage, R1-3.
**Active — Ram and Board** [CHARGE, CD 3, R3]: Rush 3 hexes straight and deal 3 damage to the first adjacent target at endpoint.
**Passive — Boarding Ramp:** Adjacent friendly Boarding Corsairs gain +1 movement next activation.

#### 15. Scrapjaw Tank | T4 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move STEP 2 · Scrapjaw Mortar: 5 damage, R1-3.
**Active — Salvage Plating** [SHIELD, CD 3, R0]: Gain 4 temporary shield HP until next activation, but cannot move next activation.
**Passive — Patchwork Armor:** First basic attack against this unit each round deals 1 less damage.

#### 16. Ironwake Artillery Rig | T5 Vehicle Siege
**Stats:** HP 12 · ARM 2 · Move RAY 1 · Ironwake Cannon: 6 damage, R3-6.
**Active — Broadside Cluster** [BOMBARD, CD 4, R6]: Blast a visible target hex for 5 damage and neighboring hexes for 2; damages friendlies.
**Passive — Heavy Powder:** Gets +1 basic attack damage while stationary.

### Orbital Commandfall: space units
#### 1. Scrapwing Fighter | T1 Fighter
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Scrap Bolts: 2 damage, R1-2.
**Active — Feint Strike** [DASH, CD 2, R2]: Move 2 and strike an adjacent target for 1 damage.
**Passive — Junkyard Jets:** Ignores the first hazard tile it crosses in an activation.

#### 2. Cutlass Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 4 · Cutlass Lasers: 2 damage, R1-2.
**Active — Boarding Screen** [INTERCEPT, CD 2, R2]: Interpose against the next fighter attack on a friendly ship within 2.
**Passive — Knife Fighter:** Gain +1 raw damage when adjacent to the target.

#### 3. Reaver Corvette | T2 Corvette
**Stats:** HP 7 · ARM 1 · Move ZIGZAG 3 · Reaver Guns: 3 damage, R1-3.
**Active — Wreck Marker** [MARK, CD 2, R3]: MARK a visible target; if destroyed this round it becomes SALVAGEABLE after battle.
**Passive — Prize Hunter:** Gain +1 move after an allied Pirate boards a ship this round.

#### 4. Gallow Frigate | T2 Frigate
**Stats:** HP 9 · ARM 1 · Move RAY 3 · Gallow Batteries: 3 damage, R2-4.
**Active — Grapple Cables** [BOARD, CD 3, R2]: A visible enemy Corvette or Fighter within 2 at 2 HP or less becomes BOARDED, unable to act until freed.
**Passive — Harpoon Teams:** Basic attacks on BOARDED targets ignore 1 ARM.

#### 5. Broadside Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 1 · Move RAY 2 · Double Broadside: 5 damage, R2-4.
**Active — Crossfire Burst** [BARRAGE, CD 3, R4]: Strike two different enemies within 4 for 3 damage each; both need line of sight.
**Passive — Side Cannons:** Can attack through one adjacent allied ship without blocking line of sight.

#### 6. Marauder Cruiser | T3 Cruiser
**Stats:** HP 14 · ARM 2 · Move ZIGZAG 2 · Marauder Salvo: 5 damage, R2-4.
**Active — Plunder Sweep** [AOE, CD 3, R4]: Deal 3 damage to a center hex and 1 to each neighboring occupied hex, allies included.
**Passive — Loot Holds:** If this vessel survives battle, salvage reward from its defeated MARKED targets increases.

#### 7. Galleon Carrier | T4 Carrier
**Stats:** HP 14 · ARM 1 · Move RAY 2 · Galleon Guns: 4 damage, R2-4.
**Active — Launch Raiders** [SPAWN, CD 3, R1]: Spawn 1 boarding skiff token with 2 HP in a free adjacent hex, max two active tokens.
**Passive — Open Hangars:** Adjacent pirate fighters or skiffs gain +1 movement on their next activation.

#### 8. Ironwake Battleship | T4 Battleship
**Stats:** HP 18 · ARM 2 · Move RAY 1 · Ironwake Broadsides: 7 damage, R2-5.
**Active — Full Broadside** [BARRAGE, CD 4, R5]: Attack two different targets for 5 damage each if both fall within range and line of sight.
**Passive — Anchor Gunners:** Gain +1 attack damage when stationary, but lose 1 move next activation after firing.

#### 9. Blackflag Dreadnought | T5 Dreadnought
**Stats:** HP 21 · ARM 3 · Move RAY 1 · Blackflag Cannon: 8 damage, R2-6.
**Active — Terror Battery** [AOE, CD 4, R6]: Deal 6 to a visible target hex and 3 to its neighbors, allied units included.
**Passive — Warship Wreck:** When destroyed, generates one additional SALVAGEABLE wreck marker after battle.

#### 10. Freeport Flagship | T5 Flagship
**Stats:** HP 20 · ARM 2 · Move RAY 1 · Freeport Broadside: 6 damage, R2-5.
**Active — Pirate Council** [RALLY, CD 4, R4]: Up to three allies within 4 may each gain +2 raw damage on their next shot or 2 temporary shields.
**Passive — Loose Alliance:** Friendly pirates within 3 gain +1 attack against BOARDED ships; Freeport is a killable LEADER.

## Cyborg Elves (provisional roster proposal)
**Combat identity:** Former telepathic imperial rulers who rebuilt themselves into powerful cybernetic expansionists. They prize technological augmentation and high-output firepower.
**Faction rule:** Augmented Supremacy: cybernetic infantry ignore the first DISABLE applied to them per battle if they have an intact synaptic relay; this rule is provisional.

### Planetary Commandfall: infantry and ground vehicles
#### 1. Chrome Thrall | T1 Line
**Stats:** HP 5 · ARM 1 · Move STEP 2 · Servo Blade: 2 damage, R1-1.
**Active — Machine Reflex** [COUNTER, CD 2, R1]: Counter next adjacent basic hit for 2 damage after armor.
**Passive — Metal Fidelity:** Ignore first ROOTED from a non-elite enemy each battle.

#### 2. Augment Skirmisher | T1 Scout
**Stats:** HP 4 · ARM 0 · Move KNIGHT 3 · Pulse Needle: 2 damage, R1-2.
**Active — Afterimage** [DASH, CD 2, R3]: Jump within 3 to a free hex and gain 1 temporary shield HP.
**Passive — Synthetic Muscles:** Traverses rubble and vertical terrain at standard movement cost.

#### 3. Synaptic Lancer | T2 Assault
**Stats:** HP 5 · ARM 0 · Move RAY 3 · Synapse Lance: 3 damage, R1-2.
**Active — Circuit Spear** [PIERCE, CD 3, R2]: Make a 4-damage attack ignoring 1 ARM within 2.
**Passive — Neural Focus:** +1 attack damage if attacking in a straight line after RAY move.

#### 4. Ironblood Knight | T2 Defender
**Stats:** HP 7 · ARM 2 · Move STEP 2 · Ironblood Sword: 3 damage, R1-1.
**Active — Imperial Bodyguard** [GUARD, CD 2, R1]: Intercept one direct attack against adjacent allied leader until next activation.
**Passive — Steel Oath:** +1 ARM when guarding a LEADER.

#### 5. Null Veil Sniper | T3 Marksman
**Stats:** HP 4 · ARM 0 · Move DIAG 2 · Null Rifle: 3 damage, R2-5.
**Active — Veil Pierce** [PIERCE, CD 3, R5]: Make a 5-damage precision attack ignoring 2 ARM to visible unit.
**Passive — Longshot Array:** CLOAKED enemy units within 2 of an allied scout count as revealed to this sniper.

#### 6. Memory Leech | T3 Saboteur
**Stats:** HP 4 · ARM 0 · Move PHASE 3 · Leech Injector: 2 damage, R1-2.
**Active — Memory Extract** [DISABLE, CD 3, R2]: DISABLE enemy active next activation and reveal its next intended attack target to opposing player.
**Passive — Residual Echo:** First enemy that attacks it each battle becomes MARKED until end of round.

#### 7. Coil Warden | T3 Heavy
**Stats:** HP 8 · ARM 1 · Move RAY 2 · Coil Glaive: 4 damage, R1-2.
**Active — Grounding Net** [PIN, CD 2, R2]: ROOT a visible enemy within 2 for its next activation.
**Passive — Interlocked Servos:** Cannot be pushed by infantry below Elite.

#### 8. Fleshless Surgeon | T3 Support
**Stats:** HP 5 · ARM 0 · Move STEP 2 · Arc Scalpel: 2 damage, R1-1.
**Active — Augment Repair** [HEAL, CD 2, R2]: Heal an allied cybernetic infantry 3 HP within 2 and clear its DISABLED status.
**Passive — Redundant Organs:** Ignores first 1 hazard damage each round.

#### 9. Neural Archon | T4 Support
**Stats:** HP 5 · ARM 0 · Move PHASE 2 · Neural Pulse: 3 damage, R1-3.
**Active — Dominion Echo** [DEBUFF, CD 3, R3]: Two enemy infantry within 3 suffer -1 attack damage on their next attacks.
**Passive — Signal Emperor:** Friendly CYBERNETIC infantry within 2 get +1 against targets already MARKED.

#### 10. Titanbound | T4 Elite
**Stats:** HP 9 · ARM 1 · Move RAY 2 · Titan Fist: 5 damage, R1-2.
**Active — Heavy Frame** [SHIELD, CD 3, R0]: Gain 4 temporary shields; movement maximum becomes 1 next activation.
**Passive — Titan Skeleton:** Immune to non-siege push or pull.

#### 11. Sovereign Blade | T5 Elite
**Stats:** HP 9 · ARM 1 · Move LEAP 3 · Sovereign Edge: 5 damage, R1-1.
**Active — Decapitating Arc** [PIERCE, CD 3, R1]: Attack adjacent target for 7 raw damage ignoring 2 ARM; remains subject to GUARD interception.
**Passive — Machine Perfection:** Basic attack gains +1 damage against targets with no adjacent ally.

#### 12. Ascendant Regent | T5 Commander
**Stats:** HP 10 · ARM 1 · Move STEP 2 · Regent Staff: 5 damage, R1-3.
**Active — Imperial Override** [RALLY, CD 4, R3]: Two allied cybernetic units within 3 gain +1 move and +2 damage on next direct attack.
**Passive — Inherited Dominion:** Cybernetic allies within 2 may intercept direct attack on this LEADER if they have GUARD.

#### 13. Spinal Strider | T2 Vehicle Scout
**Stats:** HP 7 · ARM 1 · Move LEAP 3 · Spinal Needles: 3 damage, R1-3.
**Active — Wall Traverse** [DASH, CD 2, R3]: Leap to unoccupied hex within 3 ignoring cover height.
**Passive — Spinal Actuators:** Does not incur rough terrain cost.

#### 14. Reaver Skiff | T3 Vehicle Assault
**Stats:** HP 9 · ARM 1 · Move ZIGZAG 3 · Reaver Projector: 4 damage, R1-3.
**Active — Shock Landing** [AOE, CD 3, R2]: Move up to 2 then deal 2 damage to each adjacent enemy at destination.
**Passive — Shock Absorber:** Takes 1 less damage from the first counterattack each round.

#### 15. Manifold Walker | T4 Vehicle Support
**Stats:** HP 10 · ARM 1 · Move RAY 2 · Manifold Guns: 3 damage, R1-4.
**Active — Synthetic Guard** [GUARD, CD 3, R2]: Guard up to two nearby allied infantry within 2, intercepting one direct hit each.
**Passive — Repair Clamps:** Adjacent friendly cybernetic infantry heal 1 HP at activation start, max once per round.

#### 16. Synod Siege Engine | T5 Vehicle Siege
**Stats:** HP 12 · ARM 2 · Move RAY 1 · Synod Cannon: 7 damage, R2-6.
**Active — Imperial Sundering** [PIERCE, CD 4, R6]: Make a 9-damage line shot ignoring 2 ARM against visible target.
**Passive — Ruin Blueprint:** Damage against barriers and structures +2.

### Orbital Commandfall: space units
#### 1. Etherglass Scout | T1 Space Scout
**Stats:** HP 5 · ARM 0 · Move KNIGHT 3 · Ether Dart: 1 damage, R1-2.
**Active — Memory Scan** [REVEAL, CD 2, R4]: Reveal invisible ships and traps within 4 until end of round.
**Passive — Antique Optics:** Ignore one nebula vision penalty.

#### 2. Razorwing Interceptor | T1 Interceptor
**Stats:** HP 5 · ARM 0 · Move LEAP 4 · Razor Pulse: 2 damage, R1-2.
**Active — Augmented Reflex** [INTERCEPT, CD 2, R2]: Interpose against next basic fighter attack on allied ship within 2.
**Passive — Neural Reflex:** +1 damage against Fighters and Interceptors.

#### 3. Cerebral Corvette | T2 Corvette
**Stats:** HP 7 · ARM 1 · Move ZIGZAG 3 · Cerebral Repeater: 3 damage, R1-3.
**Active — Signal Sting** [DISABLE, CD 2, R3]: One visible enemy within 3 cannot use active on its next activation.
**Passive — Dream Relay:** Shares visible enemy positions with friendly Elven ships within 2.

#### 4. Chrome Frigate | T2 Frigate
**Stats:** HP 9 · ARM 1 · Move DIAG 2 · Chrome Batteries: 4 damage, R2-4.
**Active — Blade Screen** [SHIELD, CD 3, R2]: Add 3 temporary shields to allied ship within 2.
**Passive — Chrome Girders:** While stationary, gain +1 ARM against next direct hit.

#### 5. Dominion Destroyer | T3 Destroyer
**Stats:** HP 11 · ARM 1 · Move RAY 2 · Dominion Lance: 5 damage, R2-4.
**Active — Submission Pulse** [SUPPRESS, CD 3, R4]: Deal 2 damage and reduce target movement by 2 next activation.
**Passive — Imperial Doctrine:** Gain +1 raw damage against enemy ships on objective hexes.

#### 6. Sovereign Cruiser | T3 Cruiser
**Stats:** HP 13 · ARM 2 · Move ZIGZAG 2 · Sovereign Array: 5 damage, R2-5.
**Active — Fractal Lance** [PIERCE, CD 3, R5]: Attack one visible ship for 6 damage ignoring 1 ARM.
**Passive — Ancient Hull:** Ignore the first non-damaging DEBUFF applied per battle.

#### 7. Soulforge Carrier | T4 Carrier
**Stats:** HP 14 · ARM 1 · Move RAY 2 · Soulforge Beam: 4 damage, R2-4.
**Active — Deploy Augments** [SPAWN, CD 3, R1]: Deploy one 2-HP drone fighter into an adjacent open hex; max two per carrier.
**Passive — Foundry Heart:** Adjacent fighter tokens restore 1 HP after carrier activation.

#### 8. Ascendant Battleship | T4 Battleship
**Stats:** HP 17 · ARM 2 · Move RAY 1 · Ascendant Battery: 7 damage, R2-5.
**Active — Imperial Volley** [BARRAGE, CD 4, R5]: Strike two visible enemy ships within 5 for 5 damage each.
**Passive — Perfect Aim:** Ignore 1 point of cover reduction for all basic attacks.

#### 9. Nullstar Dreadnought | T5 Dreadnought
**Stats:** HP 20 · ARM 3 · Move RAY 1 · Nullstar Cannon: 8 damage, R2-6.
**Active — Silence Array** [AOE, CD 4, R6]: Deal 6 damage to center and 2 to adjacent hexes; affected ships also become MARKED.
**Passive — Unyielding Core:** Immune to forced move from ships smaller than Cruiser.

#### 10. Imperial Synod Flagship | T5 Flagship
**Stats:** HP 20 · ARM 2 · Move RAY 1 · Synod Beam: 7 damage, R2-6.
**Active — Imperial Mandate** [RALLY, CD 4, R4]: Three friendly Elven ships within 4 gain +2 raw damage on their next attacks, but then lose 1 movement next activation.
**Passive — Reclaimed Throne:** Cybernetic ships within 3 may GUARD this exposed LEADER only with a specific ability or passive.

## Naming and content review
- Earlier unit lists used **Borglow Vanguard**; recent character art uses **Barlow the Vicious**, while the uploaded lore sheet contains **Borglow**. Keep the previous unit identifier intact until the project chooses a canonical spelling.
- The new Cyborg Elf roster is an optional proposal because the updated lore documents their faction but does not supply individual unit names.
- These are draft base numbers, not playtest-certified balance settings. No abilities call for random hit, crit or damage rolls.
