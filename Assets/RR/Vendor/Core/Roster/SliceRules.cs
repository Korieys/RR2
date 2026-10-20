using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Rule and Ruin vertical-slice rules layered on the Full Roster orbital ruleset (Docs/vertical-slice/DECISIONS.md, VS-10
    /// to VS-12; Docs/vertical-slice/VENDOR_PATCHES.md). Every number here is an experimental slice default, not codex canon.
    /// All state lives in roster statuses on <see cref="UnitState"/> and in roster equipment tokens, so it is serialized,
    /// hashed, replayed and previewed by the existing machinery.
    /// </summary>
    /// <summary>A capital-ship module (see <see cref="SliceRules.Modules"/>).</summary>
    public sealed class ModuleDef
    {
        public string Id, Name, Slot, Summary;
        public int Move, Armor, Hull, Damage, Range;
        /// <summary>Campaign alpha Prompt 19 (vs-6): content data. The module mesh to show, and the refit price.</summary>
        public string Visual;
        public int CostEnergy, CostMaterials, CostCredits;
    }

    /// <summary>A scenario loadout of existing hulls (content data, Prompt 19).</summary>
    public sealed class LoadoutDef
    {
        public string Id, Name, Note;
        public FactionId Faction;
        public string[] Units;
    }

    public static class SliceRules
    {
        // ================================================================== capital firing arcs (Prompt 2)

        /// <summary>Move option for a turn order: the ship spends its move to face a new direction without moving.</summary>
        public const int TurnOption = 2;

        /// <summary>Capital gunlines (flagships, battleships, bastions) carry their main batteries forward: their basic
        /// weapon cannot fire into the 120° arc directly astern.</summary>
        public static bool HasArc(UnitState u) => u != null && u.R.IsShip && !u.R.IsToken && HasArcRole(u.R.Role);
        public static bool HasArcRole(UnitRole r) => r == UnitRole.Flagship || r == UnitRole.Battleship || r == UnitRole.SpaceBastion;

        /// <summary>Twelve facings, clockwise from East: even = a hex side, odd = a hex corner between two sides.</summary>
        public static Hex FacingVector(int f)
        {
            f = ((f % 12) + 12) % 12;
            return f % 2 == 0 ? Hex.Directions[f / 2] : Hex.Directions[f / 2] + Hex.Directions[(f / 2 + 1) % 6];
        }

        public static readonly string[] FacingNames = { "east", "south-south-east", "south-east", "south", "south-west", "west-south-west", "west", "west-north-west", "north-west", "north", "north-east", "east-north-east" };

        /// <summary>At deployment a capital ship faces the enemy: North for the southern side (seat 0), South for seat 1.</summary>
        public static int DefaultFacing(int seat) => seat == 0 ? 9 : 3;

        public static int Facing(UnitState u)
        {
            var st = u.GetR(RStatusType.Facing);
            return st.HasValue ? st.Value.Amount : DefaultFacing(u.Owner);
        }

        public static void SetFacing(UnitState u, int f)
        {
            u.RStatuses.RemoveAll(x => x.Type == RStatusType.Facing);
            u.RStatuses.Add(new RStatus { Type = RStatusType.Facing, Amount = ((f % 12) + 12) % 12, Expire = RExpire.Battle });
        }

        // Exact integer geometry in the board's pointy-top layout: x = (√3/2)(2q + r), z = −(3/2) r.
        static long Dot4(Hex a, Hex b) => 3L * a.Lateral * b.Lateral + 9L * a.R * b.R;

        /// <summary>The facing closest in angle to a direction (ties: the lower index).</summary>
        public static int NearestFacing(Hex delta)
        {
            int best = 0; double bestCos = double.MinValue;
            double dl = Math.Sqrt(Dot4(delta, delta));
            for (int f = 0; f < 12; f++)
            {
                var v = FacingVector(f);
                double c = Dot4(v, delta) / (Math.Sqrt(Dot4(v, v)) * dl);
                if (c > bestCos + 1e-9) { bestCos = c; best = f; }
            }
            return best;
        }

        /// <summary>True when <paramref name="target"/> is outside the rear 120° of a ship at <paramref name="from"/>
        /// facing <paramref name="facing"/> (angle to the facing at most 120°, boundary included).</summary>
        public static bool InArc(int facing, Hex from, Hex target)
        {
            var t = target - from;
            if (t.Q == 0 && t.R == 0) return true;
            var f = FacingVector(facing);
            long d = Dot4(f, t);
            if (d >= 0) return true;
            return 4 * d * d <= Dot4(f, f) * Dot4(t, t);   // cos ≥ −1/2
        }

        public static string ArcBlock(UnitState u, Hex from, Hex target) =>
            HasArc(u) && !InArc(Facing(u), from, target)
                ? $"{u.Def.Name} faces {FacingNames[Facing(u)]}: its main battery cannot fire into the arc astern. Turn it (spending its move) or attack with another ship."
                : null;

        /// <summary>After a move: a capital ship ends facing the direction of its last step.</summary>
        public static void FaceAlong(UnitState u, List<Hex> path)
        {
            if (!HasArc(u) || path == null || path.Count < 2) return;
            SetFacing(u, NearestFacing(path[path.Count - 1] - path[path.Count - 2]));
        }

        public static string ValidateTurn(MatchState s, UnitState u, Hex target)
        {
            if (!HasArc(u)) return $"{u.Def.Name} has no fixed firing arc, so it never needs to turn.";
            if (u.MovedThisActivation || u.ActedThisActivation) return $"{u.Def.Name} has already used its move this activation; turning costs the move.";
            if (Budget0(s, u)) return $"{u.Def.Name} cannot move this activation, so it cannot turn.";
            int f = TurnFacing(u, target);
            if (f < 0) return "Choose one of the twelve facings around the ship.";
            if (f == Facing(u)) return $"{u.Def.Name} already faces {FacingNames[f]}.";
            return null;
        }

        static bool Budget0(MatchState s, UnitState u) => RosterMovement.Budget(s, u) <= 0;

        public static int TurnFacing(UnitState u, Hex target)
        {
            for (int f = 0; f < 12; f++) if (u.Position + FacingVector(f) == target) return f;
            return -1;
        }

        /// <summary>Slice orders for the legal-action list (AI, threat analysis, tests): overclocked moves and attacks, and turns.</summary>
        public static void ExtraOrders(MatchState s, MatchState view, UnitState u, List<Command> list)
        {
            int seat = u.Owner;
            var vu = view.Unit(u.Id);
            bool oc = CanOverclock(s, u) == null;
            if (oc && !u.MovedThisActivation && !u.ActedThisActivation && vu != null)
            {
                var normal = RosterMovement.Reachable(view, vu);
                foreach (var kv in RosterMovement.Reachable(view, vu, OverclockMoveOf(u)).OrderBy(k => k.Key))
                    if (!normal.ContainsKey(kv.Key)) list.Add(Order(CommandType.Move, seat, u.Id, kv.Key, OverclockOption));
            }
            if (HasArc(u))
                for (int f = 0; f < 12; f++)
                {
                    var t = u.Position + FacingVector(f);
                    if (ValidateTurn(s, u, t) == null) list.Add(Order(CommandType.Move, seat, u.Id, t, TurnOption));
                }
            var enemyLeader = s.Commander(1 - seat);
            if (enemyLeader != null && enemyLeader.OnBoard && ValidateBoarding(s, u, enemyLeader.Position) == null)
                list.Add(Order(CommandType.Attack, seat, u.Id, enemyLeader.Position, BoardOption));
            if (oc && RosterRules.CanAct(s, u, attack: true) == null)
                foreach (var e in s.Units.Where(x => x.OnBoard && x.Owner != seat).OrderBy(x => x.Id))
                    if (RosterCombat.ResolveBasic(s, u, e.Position, overclock: true).Legal)
                        list.Add(Order(CommandType.Attack, seat, u.Id, e.Position, OverclockOption));
        }

        public static Command Order(CommandType type, int seat, int unitId, Hex target, int option) =>
            new Command { Type = type, Seat = seat, UnitId = unitId, Target = target, Option = option };

        // ================================================================== Pygmaei industrial overclock (Prompt 4)

        /// <summary>Move or attack option: overclocked.</summary>
        public const int OverclockOption = 1;
        public const int OverclockMove = 2, OverclockDamage = 2, OverclockHeat = 2, OverheatAt = 4, CoolPerActivation = 2, OverheatArmor = 2;

        // Campaign alpha Prompt 18 (vs-5): doctrine sidegrades a faction adopts in the campaign ride on its ships as army-entry
        // tokens, so previews, the AI, validation and results all read the same values. Each is disclosed before deployment.
        public const string DoctrineToken = "doctrine:";
        public const string CoolantDoctrine = "doctrine:coolant", FurnaceDoctrine = "doctrine:furnace", LeveragedDoctrine = "doctrine:leveraged", GuaranteedDoctrine = "doctrine:guaranteed";
        static bool Doc(UnitState u, string t) => u != null && u.Equipment.Contains(t);
        /// <summary>Coolant discipline: +1 instead of +2; overheated at 6 instead of 4. Furnace doctrine: +3 damage for 3 heat.</summary>
        public static int OverclockMoveOf(UnitState u) => Doc(u, CoolantDoctrine) ? 1 : OverclockMove;
        public static int OverclockDamageOf(UnitState u) => Doc(u, CoolantDoctrine) ? 1 : Doc(u, FurnaceDoctrine) ? 3 : OverclockDamage;
        public static int OverclockHeatOf(UnitState u, bool attack) => attack && Doc(u, FurnaceDoctrine) ? 3 : OverclockHeat;
        public static int OverheatAtOf(UnitState u) => Doc(u, CoolantDoctrine) ? 6 : OverheatAt;
        /// <summary>Leveraged contracts: 2 points cheaper, arriving in round 4. Guaranteed arrival: full price, arriving in round 2.</summary>
        public static int ArrivalRoundOf(UnitState u) => Doc(u, LeveragedDoctrine) ? 4 : Doc(u, GuaranteedDoctrine) ? 2 : ContractArrivalRound;
        public static int ContractDiscountOf(RosterEntry e) => e.Equipment.Contains(LeveragedDoctrine) ? 2 : e.Equipment.Contains(GuaranteedDoctrine) ? 0 : ContractDiscount;

        public static bool OverclockCapable(UnitState u) => u != null && u.Def.Faction == FactionId.Pygmaei && u.R.IsShip && !u.R.IsToken;

        public static int Heat(UnitState u) => u.RAmount(RStatusType.Heat);
        public static bool Overheated(UnitState u) => Heat(u) >= OverheatAtOf(u);

        public static string CanOverclock(MatchState s, UnitState u)
        {
            if (!OverclockCapable(u)) return $"{u.Def.Name} has no overclock (Pygmaei ships only).";
            if (Overheated(u)) return $"{u.Def.Name} is OVERHEATED (heat {Heat(u)}): it cannot overclock until it cools below {OverheatAtOf(u)}.";
            return null;
        }

        /// <summary>Adds heat after an overclocked order and marks the activation as overclocked (no cooling at its end).</summary>
        public static void AddHeat(MatchState s, UnitState u, int amount, List<GameEvent> ev)
        {
            int before = Heat(u);
            u.RStatuses.RemoveAll(x => x.Type == RStatusType.Heat);
            u.RStatuses.Add(new RStatus { Type = RStatusType.Heat, Amount = before + amount, Expire = RExpire.Battle, Mode = 1 });
            ev.Add(new GameEvent(GameEventType.HeatChanged) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, Amount = before + amount,
                Text = before + amount >= OverheatAtOf(u) ? $"OVERHEATED (heat {before + amount})" : $"heat {before + amount}" });
        }

        /// <summary>End of the ship's own activation: it sheds heat unless it overclocked during that activation.</summary>
        public static void Cool(UnitState u, List<GameEvent> ev)
        {
            int i = u.RIndex(RStatusType.Heat);
            if (i < 0) return;
            var st = u.RStatuses[i];
            if (st.Mode == 1) { st.Mode = 0; u.RStatuses[i] = st; return; }
            int after = Math.Max(0, st.Amount - CoolPerActivation);
            if (after == 0) u.RStatuses.RemoveAt(i); else { st.Amount = after; u.RStatuses[i] = st; }
            ev.Add(new GameEvent(GameEventType.HeatChanged) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = u.Position, Amount = after, Text = $"cools to heat {after}" });
        }

        /// <summary>Armour lost while overheated (shown in every attack preview).</summary>
        public static int HeatArmorPenalty(UnitState t) => Overheated(t) ? OverheatArmor : 0;

        // ================================================================== capital-ship modules (Prompt 7)

        /// <summary>Army-entry token for a fitted module ("module:afterburners").</summary>
        public const string ModuleToken = "module:";

        /// <summary>Three slot-compatible sidegrades for capital ships: each gains one thing and pays for it with another.
        /// Experimental values. They are applied by <see cref="ApplyModules"/> whenever a unit's stats are computed, so
        /// previews, the AI and replays all see them.</summary>
        /// (vs-6) The modules are content: authored in Assets/RR/Content/slice_content.json and generated into
        /// SliceContent.g.cs by Tools/Content/rr_content.py. Adding a sidegrade needs no rules code.
        public static ModuleDef[] Modules => SliceContent.Modules;

        public static ModuleDef Module(string id) => Modules.FirstOrDefault(m => m.Id == id);
        public static IEnumerable<string> ModulesOf(IEnumerable<string> equipment) =>
            equipment.Where(x => x.StartsWith(ModuleToken, StringComparison.Ordinal)).Select(x => x.Substring(ModuleToken.Length));

        /// <summary>Applies fitted modules to derived stats (called at the end of <see cref="RosterArmy.Recompute"/>).</summary>
        public static void ApplyModules(UnitState u)
        {
            foreach (var id in ModulesOf(u.Equipment))
            {
                var m = Module(id);
                if (m == null) continue;
                u.MaxHp = Math.Max(1, u.MaxHp + m.Hull);
                u.BaseArmor = Math.Max(0, Math.Min(RosterCombat.ArmorCap, u.BaseArmor + m.Armor));
                if (m.Damage != 0) u.Weapon.Damage = Math.Max(1, u.Weapon.Damage + m.Damage);
                if (m.Range != 0) u.Weapon.MaxRange = Math.Max(u.Weapon.MinRange, u.Weapon.MaxRange + m.Range);
            }
        }

        /// <summary>Movement change from modules (added in <see cref="RosterMovement.Budget"/>, never below 1 in total).</summary>
        public static int ModuleMove(UnitState u) => ModulesOf(u.Equipment).Sum(id => Module(id)?.Move ?? 0);

        // ================================================================== capture by boarding (Prompt 8)

        /// <summary>Scenario policy token, carried on each side's flagship entry: capture is allowed in this battle.</summary>
        public const string CapturePolicyToken = "policy:capture";
        /// <summary>Campaign alpha (Prompt 16, vs-4): marks the army's acting commander after succession. The unit leads the
        /// army (its loss ends the battle like a flagship's or commander's) and counts as the army's one leader.</summary>
        public const string ActingCommandToken = "command:acting";
        public static bool IsActingCommander(RosterEntry e) => e.Equipment.Contains(ActingCommandToken);
        /// <summary>Attack option: a boarding action against the enemy flagship.</summary>
        public const int BoardOption = 3;
        /// <summary>A flagship can be boarded only when DISABLED and at or below this share of its hull (in thirds).</summary>
        public const int BoardHullThirds = 1;
        public const int BoarderExposed = 2;

        public static bool CaptureEnabled(MatchState s) => s.Units.Any(u => u.Equipment.Contains(CapturePolicyToken));

        /// <summary>Why <paramref name="u"/> cannot board the ship on <paramref name="target"/> now (null = it can). Every
        /// condition is stated; low hull or a disabled engine alone never counts as capture.</summary>
        public static string ValidateBoarding(MatchState s, UnitState u, Hex target)
        {
            if (!CaptureEnabled(s)) return "Capture is not allowed in this battle (no capture policy).";
            var t = s.UnitAt(target);
            if (t == null || t.Owner == u.Owner) return "Board an enemy ship.";
            if (s.Players[t.Owner].CommanderId != t.Id) return "Only the enemy flagship can be boarded.";
            if (u.R.IsToken) return "Tokens cannot board.";
            var why = RosterRules.CanAct(s, u, attack: true);
            if (why != null) return why;
            if (Hex.Distance(u.Position, target) != 1) return "The boarding ship must be adjacent to the flagship.";
            if (!t.HasR(RStatusType.Disabled)) return $"{t.Def.Name} must be DISABLED first (an Injunction, a Hostile Merger or similar).";
            if (t.Hp * 3 > t.MaxHp * BoardHullThirds) return $"{t.Def.Name} must be at or below a third of its hull ({t.Hp} of {t.MaxHp}).";
            return null;
        }

        public static string BoardingPreview(MatchState s, UnitState u, Hex target)
        {
            var why = ValidateBoarding(s, u, target);
            var t = s.UnitAt(target);
            if (why != null) return "Boarding: " + why;
            return $"Board {t.Def.Name}: it is captured with its crew and modules intact, and the battle ends at once. {u.Def.Name} spends its action and is EXPOSED (armour −{BoarderExposed}) until its next activation. Destroying it instead would be certain but leave only a wreck.";
        }

        public static void ApplyBoarding(MatchState s, UnitState u, Hex target, List<GameEvent> ev)
        {
            var t = s.UnitAt(target);
            u.ActedThisActivation = true; u.AttackedThisActivation = true; u.AttackedRound = s.Round;
            u.RStatuses.Add(new RStatus { Type = RStatusType.Exposed, Amount = BoarderExposed, Expire = RExpire.AfterOwnActivation, ExpireAt = u.Activations + 2, SourceId = u.Id });
            t.RStatuses.Add(new RStatus { Type = RStatusType.Captured, SourceId = u.Id, Expire = RExpire.Battle });
            ev.Add(new GameEvent(GameEventType.UnitCaptured) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = t.Id, From = u.Position, To = t.Position, Text = "boarded" });
            RulesEngine.EndMatchInternal(s, u.Owner, VictoryReason.LeaderCaptured, ev);
        }

        // ================================================================== Inhumanus fleet contracts (Prompt 4)

        public const string ContractToken = "contract@";
        public const int ContractArrivalRound = 3, MaxContracts = 2, ContractDiscount = 1;
        public static readonly string[] ContractLanes = { "west", "centre", "east" };

        public static string ContractLane(RosterEntry e) => e.Equipment.Where(x => x.StartsWith(ContractToken, StringComparison.Ordinal)).Select(x => x.Substring(ContractToken.Length)).FirstOrDefault();
        public static string ContractLane(UnitState u) => u.Equipment.Where(x => x.StartsWith(ContractToken, StringComparison.Ordinal)).Select(x => x.Substring(ContractToken.Length)).FirstOrDefault();
        public static bool IsContracted(UnitState u) => ContractLane(u) != null;
        public static bool InReserve(UnitState u) => u.Alive && !u.Deployed && IsContracted(u);

        /// <summary>Contracted escorts are paid one point less (minimum 1); the price of arriving late and in the open.</summary>
        public static int EntryCost(RosterEntry e, int printed) => ContractLane(e) != null ? Math.Max(1, printed - ContractDiscountOf(e)) : printed;

        /// <summary>Errors for slice tokens in an army entry (contracts and modules); null when there are none or all are valid.</summary>
        public static string ValidateContract(ArmyRoster army, RosterEntry e, BattleDomain domain)
        {
            var def = RosterContent.Unit(e.UnitId);
            var extra = e.Equipment.Where(x => !x.StartsWith(ContractToken, StringComparison.Ordinal) && !x.StartsWith(ModuleToken, StringComparison.Ordinal) && x != CapturePolicyToken && x != ActingCommandToken && !x.StartsWith(DoctrineToken, StringComparison.Ordinal)).ToList();
            foreach (var d in e.Equipment.Where(x => x.StartsWith(DoctrineToken, StringComparison.Ordinal)))
            {
                bool pyg = d == CoolantDoctrine || d == FurnaceDoctrine, inh = d == LeveragedDoctrine || d == GuaranteedDoctrine;
                if (!pyg && !inh) return $"Unknown doctrine '{d}'.";
                if (domain != BattleDomain.Orbital || (pyg && army.Faction != FactionId.Pygmaei) || (inh && army.Faction != FactionId.Inhumanus)) return $"{d} is not a doctrine of this army.";
            }
            if (e.Equipment.Contains(CoolantDoctrine) && e.Equipment.Contains(FurnaceDoctrine)) return "Coolant discipline and furnace doctrine are exclusive.";
            if (e.Equipment.Contains(LeveragedDoctrine) && e.Equipment.Contains(GuaranteedDoctrine)) return "Leveraged contracts and guaranteed arrival are exclusive.";
            if (e.Equipment.Contains(ActingCommandToken) && def != null && def.Roster.IsLeaderRole) return $"{def.Name} already leads by role; it cannot also be an acting commander.";
            if (e.Equipment.Contains(ActingCommandToken) && ContractLane(e) != null) return "An acting commander must deploy at the start; it cannot be contracted.";
            if (extra.Count > 0) return $"{def?.Name ?? e.UnitId}: Full Roster units carry no equipment (research upgrades apply automatically).";
            var mods = ModulesOf(e.Equipment).ToList();
            if (mods.Count > 0)
            {
                if (domain != BattleDomain.Orbital || def == null || !def.Roster.IsLeaderRole) return "Modules fit only capital ships (the flagship).";
                if (mods.Any(m => Module(m) == null)) return $"Unknown module on {def.Name}.";
                if (mods.Select(m => Module(m).Slot).Distinct().Count() != mods.Count) return $"{def.Name} has two modules in one slot.";
            }
            var lane = ContractLane(e);
            if (lane == null) return null;
            if (army.Faction != FactionId.Inhumanus || domain != BattleDomain.Orbital) return "Fleet contracts are an Inhumanus orbital doctrine.";
            if (def.Roster.IsLeaderRole) return "The flagship cannot be contracted; it must deploy at the start.";
            if (Array.IndexOf(ContractLanes, lane) < 0) return $"Unknown contract entry lane '{lane}' (west, centre or east).";
            if (e.Equipment.Count(x => x.StartsWith(ContractToken, StringComparison.Ordinal)) > 1) return $"{def.Name} has two contracts.";
            return null;
        }

        /// <summary>The disclosed entry hex of a lane for a seat: its back row, west, centre or east.</summary>
        public static Hex EntryHex(MapDefinition map, int seat, string lane)
        {
            int idx = Array.IndexOf(ContractLanes, lane);
            int lateral = (idx - 1) * 8;
            int r = map.Radius;
            // Seat 0 holds the southern rows (R = +Radius); seat 1 is the 180° rotation.
            var candidates = map.AllHexes.Where(h => h.R == r && map.IsDeploymentHex(0, h)).OrderBy(h => Math.Abs(h.Lateral - lateral)).ThenBy(h => h).ToList();
            var h0 = candidates.Count > 0 ? candidates[0] : new Hex((lateral - r) / 2, r);
            return seat == 0 ? h0 : h0.Rotate180();
        }

        /// <summary>True when <paramref name="h"/> is the declared entry hex of one of the seat's own contracted escorts.</summary>
        public static bool ReservedEntry(MatchState s, int seat, Hex h) =>
            s.Units.Any(u => u.Owner == seat && InReserve(u) && EntryHex(s.Map, seat, ContractLane(u)) == h);

        /// <summary>Start of a round: contracted escorts due by now enter on their declared hex if it is free; otherwise they
        /// wait for the next round. Deterministic by unit id.</summary>
        public static void Arrivals(MatchState s, List<GameEvent> ev)
        {
            foreach (var u in s.Units.Where(InReserve).OrderBy(x => x.Id).ToList())
            {
                if (s.Round < ArrivalRoundOf(u)) continue;   // each escort's contract (vs-5: a doctrine may move it)
                var h = EntryHex(s.Map, u.Owner, ContractLane(u));
                if (RosterTerrain.Blocks(s, h) || s.UnitAt(h) != null)
                {
                    ev.Add(new GameEvent(GameEventType.ArrivalDelayed) { Seat = u.Owner, UnitId = u.Id, TargetUnitId = u.Id, To = h, Text = $"{u.Def.Name}'s contracted arrival at {h} is blocked; it will try again next round." });
                    continue;
                }
                u.Position = h; u.Deployed = true; u.ActivatedRound = s.Round - 1;
                if (HasArc(u)) SetFacing(u, DefaultFacing(u.Owner));
                ev.Add(new GameEvent(GameEventType.UnitArrived) { Seat = u.Owner, UnitId = u.Id, To = h, Text = $"{u.Def.Name} arrives under contract" });
            }
        }
    }
}
