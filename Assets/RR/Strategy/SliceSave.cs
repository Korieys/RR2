using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    /// <summary>Save data for <see cref="SliceCampaignState"/> (under the "slice" key from save version 2).</summary>
    public static class SliceSave
    {
        static Dictionary<string, object> Res(Resources r) => new Dictionary<string, object> { ["e"] = r.Energy, ["m"] = r.Materials, ["c"] = r.Credits, ["i"] = r.Influence };
        static Resources Res(IDictionary<string, object> d) => d == null ? default : new Resources(d.I("e"), d.I("m"), d.I("c"), d.I("i"));
        static T En<T>(string s) where T : struct => Enum.TryParse<T>(s, out var v) ? v : default;
        static List<object> L<T>(IEnumerable<T> xs, Func<T, object> f) => xs.Select(f).ToList();
        static IEnumerable<IDictionary<string, object>> Os(IDictionary<string, object> d, string k) => d.A(k).OfType<IDictionary<string, object>>();

        public static Dictionary<string, object> ToData(SliceCampaignState s)
        {
            if (s == null) return null;
            return new Dictionary<string, object>
            {
                ["player"] = s.PlayerPolityId,
                ["objectives"] = L(s.Objectives, o => new Dictionary<string, object> { ["id"] = o.Id, ["kind"] = o.Kind, ["polity"] = o.PolityId, ["target"] = o.TargetPolityId,
                    ["system"] = o.SystemId, ["status"] = o.Status.ToString(), ["day"] = o.DeclaredDay, ["attempts"] = o.Attempts, ["note"] = o.Note }),
                ["passages"] = L(s.Passages, p => new Dictionary<string, object> { ["system"] = p.SystemId, ["name"] = p.Name, ["holder"] = p.AccessHolderId, ["toll"] = p.TollPerDay, ["since"] = p.SinceDay, ["basis"] = p.Basis }),
                ["relations"] = L(s.Relations, r => new Dictionary<string, object> { ["a"] = r.A, ["b"] = r.B, ["status"] = r.Status,
                    ["mods"] = L(r.Modifiers, m => new Dictionary<string, object> { ["key"] = m.Key, ["cause"] = m.Cause, ["amount"] = m.Amount, ["until"] = m.ExpiresDay }) }),
                ["ships"] = L(s.Ships.Values.OrderBy(p => p.Uid), p => new Dictionary<string, object> { ["uid"] = p.Uid, ["def"] = p.DefId, ["owner"] = p.OwnerId, ["name"] = p.Name,
                    ["modules"] = L(p.Modules, m => m), ["scars"] = p.Scars, ["destroyed"] = p.Destroyed, ["captured"] = p.Captured, ["fate"] = p.Fate,
                    ["service"] = L(p.Service, e => new Dictionary<string, object> { ["day"] = e.Day, ["kind"] = e.Kind, ["text"] = e.Text, ["battle"] = e.BattleId }) }),
                ["history"] = L(s.History, e => new Dictionary<string, object> { ["id"] = e.Id, ["kind"] = e.Kind, ["system"] = e.SystemId, ["battle"] = e.BattleId, ["text"] = e.Text,
                    ["day"] = e.Day, ["ships"] = L(e.Ships, u => u) }),
                ["wrecks"] = L(s.Wrecks, w => new Dictionary<string, object> { ["id"] = w.Id, ["system"] = w.SystemId, ["def"] = w.DefId, ["name"] = w.Name, ["owner"] = w.OwnerId,
                    ["battle"] = w.BattleId, ["uid"] = w.Uid, ["day"] = w.Day, ["salvaged"] = w.Salvaged, ["by"] = w.SalvagedBy, ["module"] = w.Module, ["yield"] = Res(w.Materials) }),
                ["settlements"] = L(s.Settlements, x => new Dictionary<string, object> { ["id"] = x.Id, ["offer"] = x.OfferId, ["battle"] = x.BattleId, ["system"] = x.SystemId,
                    ["conceder"] = x.ConcederPolity, ["recipient"] = x.RecipientPolity, ["reparations"] = x.Reparations, ["day"] = x.Day, ["surrendered"] = x.Surrendered,
                    ["surrenderedUid"] = x.SurrenderedUid, ["text"] = x.Text }),
                ["expeditions"] = L(s.Expeditions, e => new Dictionary<string, object> { ["id"] = e.Id, ["system"] = e.SystemId, ["title"] = e.Title, ["state"] = e.State.ToString(),
                    ["scouted"] = e.Scouted, ["map"] = e.MapId, ["rival"] = e.RivalComposition, ["asset"] = e.Asset, ["day"] = e.CreatedDay, ["outcome"] = e.Outcome, ["battle"] = e.BattleId }),
                ["applied"] = s.Applied.OrderBy(x => x, StringComparer.Ordinal).Cast<object>().ToList(),
                ["scenario"] = Scenario(s.Scenario),
                ["ground"] = L(s.Ground, f => new Dictionary<string, object> { ["id"] = f.Id, ["owner"] = f.OwnerId, ["planet"] = f.PlanetId, ["fleet"] = f.FleetId,
                    ["units"] = L(f.Units, u => new Dictionary<string, object> { ["uid"] = u.Uid, ["def"] = u.DefId, ["hp"] = u.Hp }) }),
                ["operations"] = L(s.Operations, Operation),
            };
        }

        static Dictionary<string, object> Operation(Operation o) => new Dictionary<string, object>
        {
            ["id"] = o.Id, ["attacker"] = o.AttackerPolity, ["defender"] = o.DefenderPolity, ["system"] = o.SystemId, ["planet"] = o.PlanetId,
            ["attackerFleet"] = o.AttackerFleetId, ["defenderFleet"] = o.DefenderFleetId, ["phase"] = o.Phase.ToString(), ["started"] = o.StartedDay, ["ended"] = o.EndedDay,
            ["orbitalFought"] = o.OrbitalFought, ["orbitalA"] = o.OrbitalAttackerWins, ["orbitalD"] = o.OrbitalDefenderWins, ["protected"] = o.SuppliesProtected,
            ["seized"] = o.SuppliesSeized, ["followUp"] = o.FollowUp, ["followUpResolved"] = o.FollowUpResolved, ["landed"] = L(o.Landed, x => x), ["hasLanded"] = o.HasLanded,
            ["winsA"] = o.AttackerWins, ["winsD"] = o.DefenderWins,
            ["battles"] = L(o.Battles, b => new Dictionary<string, object> { ["id"] = b.BattleId, ["kind"] = b.Kind, ["winner"] = b.WinnerPolity, ["reason"] = b.Reason, ["note"] = b.Note,
                ["map"] = b.MapId, ["index"] = b.Index, ["conceded"] = b.Conceded, ["deployed"] = L(b.Deployed, x => x) }),
            ["acting"] = o.Acting.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            ["prisoners"] = L(o.Prisoners, x => x), ["outcome"] = o.Outcome, ["winner"] = o.WinnerPolity, ["pending"] = o.PendingBattleId,
        };

        static List<int> IntList(IDictionary<string, object> d, string k) => d.A(k).OfType<double>().Select(x => (int)x).ToList();

        static Operation Operation(IDictionary<string, object> d)
        {
            var o = new Operation { Id = d.S("id"), AttackerPolity = d.S("attacker"), DefenderPolity = d.S("defender"), SystemId = d.S("system"), PlanetId = d.S("planet"),
                AttackerFleetId = d.S("attackerFleet"), DefenderFleetId = d.S("defenderFleet"), Phase = En<OperationPhase>(d.S("phase")), StartedDay = d.I("started"), EndedDay = d.I("ended"),
                OrbitalFought = d.I("orbitalFought"), OrbitalAttackerWins = d.I("orbitalA"), OrbitalDefenderWins = d.I("orbitalD"), SuppliesProtected = d.B("protected"),
                SuppliesSeized = d.B("seized"), FollowUp = d.B("followUp"), FollowUpResolved = d.B("followUpResolved"), HasLanded = d.B("hasLanded"),
                AttackerWins = d.I("winsA"), DefenderWins = d.I("winsD"), Outcome = d.S("outcome"), WinnerPolity = d.S("winner"), PendingBattleId = d.S("pending") };
            o.Landed.AddRange(IntList(d, "landed"));
            o.Prisoners.AddRange(IntList(d, "prisoners"));
            foreach (var b in Os(d, "battles"))
            {
                var ob = new OperationBattle { BattleId = b.S("id"), Kind = b.S("kind"), WinnerPolity = b.S("winner"), Reason = b.S("reason"), Note = b.S("note"), MapId = b.S("map"),
                    Index = b.I("index"), Conceded = b.B("conceded") };
                ob.Deployed.AddRange(IntList(b, "deployed"));
                o.Battles.Add(ob);
            }
            var acting = d.O("acting");
            if (acting != null) foreach (var kv in acting) if (kv.Value is double x) o.Acting[kv.Key] = (int)x;
            return o;
        }

        /// <summary>The bounded scenario (PassageWar): configuration, hold progress, outcome, ledger and settlement reports.</summary>
        static Dictionary<string, object> Scenario(ScenarioState sc)
        {
            if (sc == null) return null;
            return new Dictionary<string, object>
            {
                ["id"] = sc.Id, ["title"] = sc.Title, ["version"] = sc.Version, ["active"] = L(sc.ActiveSystems, x => x),
                ["designated"] = sc.Designated.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)L(kv.Value, x => x)),
                ["interval"] = sc.Interval, ["hold"] = sc.HoldRequired, ["treatyHold"] = sc.TreatyHoldRequired, ["mandate"] = sc.MandateDay,
                ["holdCount"] = sc.HoldCount, ["holdNote"] = sc.HoldNote, ["outcome"] = sc.Outcome.ToString(), ["ended"] = sc.EndedDay, ["endText"] = sc.EndText,
                ["freeform"] = sc.Freeform,
                ["proposals"] = sc.LastProposal.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["ledger"] = L(sc.Ledger, l => new Dictionary<string, object> { ["day"] = l.Day, ["polity"] = l.Polity, ["kind"] = l.Kind, ["text"] = l.Text, ["cost"] = Res(l.Cost) }),
                ["reports"] = L(sc.Reports, r => new Dictionary<string, object> { ["day"] = r.Day, ["lines"] = L(r.Lines, x => x) }),
                ["built"] = sc.Built.OrderBy(x => x, StringComparer.Ordinal).Cast<object>().ToList(),
                ["stores"] = sc.Stores.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["arrears"] = sc.Arrears.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["supplyKnown"] = sc.SupplyKnown == null ? null : L(sc.SupplyKnown, x => x),
                ["threatsKnown"] = sc.ThreatsKnown == null ? null : L(sc.ThreatsKnown, x => x),
                ["aiPlan"] = sc.AiPlan.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["aiLog"] = L(sc.AiLog, x => x), ["autoPlayer"] = sc.AutoPlayer, ["variant"] = sc.Variant, ["setupSeed"] = sc.SetupSeed,
                ["researching"] = sc.Researching.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)new Dictionary<string, object>
                    { ["project"] = kv.Value.ProjectId, ["done"] = kv.Value.DaysDone, ["total"] = kv.Value.DaysTotal, ["paid"] = Res(kv.Value.Paid) }),
                ["researched"] = sc.Researched.OrderBy(x => x, StringComparer.Ordinal).Cast<object>().ToList(),
                ["doctrinePath"] = sc.DoctrinePath.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["pending"] = sc.Pending == null ? null : new Dictionary<string, object> { ["from"] = sc.Pending.From, ["kind"] = sc.Pending.Kind, ["payment"] = sc.Pending.Payment,
                    ["day"] = sc.Pending.Day, ["expires"] = sc.Pending.Expires, ["reason"] = sc.Pending.Reason },
            };
        }

        static ScenarioState Scenario(IDictionary<string, object> d)
        {
            if (d == null) return null;
            var sc = new ScenarioState { Id = d.S("id"), Title = d.S("title"), Version = d.I("version"), Interval = d.I("interval"), HoldRequired = d.I("hold"),
                TreatyHoldRequired = d.I("treatyHold"), MandateDay = d.I("mandate"), HoldCount = d.I("holdCount"), HoldNote = d.S("holdNote"),
                Outcome = En<CampaignOutcome>(d.S("outcome")), EndedDay = d.I("ended"), EndText = d.S("endText"), Freeform = d.B("freeform") };
            sc.ActiveSystems.AddRange(d.A("active").OfType<string>());
            var des = d.O("designated");
            if (des != null) foreach (var kv in des) sc.Designated[kv.Key] = (kv.Value as List<object> ?? new List<object>()).OfType<string>().ToList();
            var prop = d.O("proposals");
            if (prop != null) foreach (var kv in prop) if (kv.Value is double x) sc.LastProposal[kv.Key] = (int)x;
            foreach (var l in Os(d, "ledger")) sc.Ledger.Add(new LedgerEntry { Day = l.I("day"), Polity = l.S("polity"), Kind = l.S("kind"), Text = l.S("text"), Cost = Res(l.O("cost")) });
            foreach (var r in Os(d, "reports")) { var rep = new SettlementReport { Day = r.I("day") }; rep.Lines.AddRange(r.A("lines").OfType<string>()); sc.Reports.Add(rep); }
            foreach (var b in d.A("built").OfType<string>()) sc.Built.Add(b);
            Ints(d.O("stores"), sc.Stores);
            Ints(d.O("arrears"), sc.Arrears);
            if (d.TryGetValue("supplyKnown", out var sk) && sk is List<object> skl) sc.SupplyKnown = skl.OfType<string>().ToList();
            if (d.TryGetValue("threatsKnown", out var tk) && tk is List<object> tkl) sc.ThreatsKnown = tkl.OfType<string>().ToList();
            var plan = d.O("aiPlan");
            if (plan != null) foreach (var kv in plan) if (kv.Value is string sv) sc.AiPlan[kv.Key] = sv;
            sc.AiLog.AddRange(d.A("aiLog").OfType<string>());
            sc.AutoPlayer = d.B("autoPlayer");
            sc.Variant = d.S("variant") ?? Variants.Standard;
            sc.SetupSeed = d.I("setupSeed");
            var res = d.O("researching");
            if (res != null) foreach (var kv in res) if (kv.Value is IDictionary<string, object> r)
                sc.Researching[kv.Key] = new DoctrineResearch { ProjectId = r.S("project"), DaysDone = r.I("done"), DaysTotal = r.I("total"), Paid = Res(r.O("paid")) };
            foreach (var x in d.A("researched").OfType<string>()) sc.Researched.Add(x);
            var dp = d.O("doctrinePath");
            if (dp != null) foreach (var kv in dp) if (kv.Value is string v) sc.DoctrinePath[kv.Key] = v;
            var pend = d.O("pending");
            if (pend != null) sc.Pending = new Proposal { From = pend.S("from"), Kind = pend.S("kind"), Payment = pend.I("payment"), Day = pend.I("day"), Expires = pend.I("expires"), Reason = pend.S("reason") };
            return sc;
        }

        static void Ints(IDictionary<string, object> d, Dictionary<string, int> into)
        {
            if (d == null) return;
            foreach (var kv in d) if (kv.Value is double x) into[kv.Key] = (int)x;
        }

        public static SliceCampaignState FromData(IDictionary<string, object> d)
        {
            var s = new SliceCampaignState();
            if (d == null) return s;
            s.PlayerPolityId = d.S("player");
            foreach (var o in Os(d, "objectives"))
                s.Objectives.Add(new WarObjective { Id = o.S("id"), Kind = o.S("kind"), PolityId = o.S("polity"), TargetPolityId = o.S("target"), SystemId = o.S("system"),
                    Status = En<ObjectiveStatus>(o.S("status")), DeclaredDay = o.I("day"), Attempts = o.I("attempts"), Note = o.S("note") });
            foreach (var p in Os(d, "passages"))
                s.Passages.Add(new PassageState { SystemId = p.S("system"), Name = p.S("name"), AccessHolderId = p.S("holder"), TollPerDay = p.I("toll"), SinceDay = p.I("since"), Basis = p.S("basis") });
            foreach (var r in Os(d, "relations"))
            {
                var rel = new Relation { A = r.S("a"), B = r.S("b"), Status = r.S("status") ?? "tension" };
                foreach (var m in Os(r, "mods")) rel.Modifiers.Add(new RelationModifier { Key = m.S("key"), Cause = m.S("cause"), Amount = m.I("amount"), ExpiresDay = m.I("until") });
                s.Relations.Add(rel);
            }
            foreach (var p in Os(d, "ships"))
            {
                var prof = new ShipProfile { Uid = p.I("uid"), DefId = p.S("def"), OwnerId = p.S("owner"), Name = p.S("name"), Scars = p.I("scars"), Destroyed = p.B("destroyed"),
                    Captured = p.B("captured"), Fate = p.S("fate") };
                prof.Modules.AddRange(p.A("modules").OfType<string>());
                foreach (var e in Os(p, "service")) prof.Service.Add(new ServiceEntry { Day = e.I("day"), Kind = e.S("kind"), Text = e.S("text"), BattleId = e.S("battle") });
                s.Ships[prof.Uid] = prof;
            }
            foreach (var e in Os(d, "history"))
            {
                var h = new HistoryEvent { Id = e.S("id"), Kind = e.S("kind"), SystemId = e.S("system"), BattleId = e.S("battle"), Text = e.S("text"), Day = e.I("day") };
                h.Ships.AddRange(e.A("ships").OfType<double>().Select(x => (int)x));
                s.History.Add(h);
            }
            foreach (var w in Os(d, "wrecks"))
                s.Wrecks.Add(new WreckRecord { Id = w.S("id"), SystemId = w.S("system"), DefId = w.S("def"), Name = w.S("name"), OwnerId = w.S("owner"), BattleId = w.S("battle"),
                    Uid = w.I("uid"), Day = w.I("day"), Salvaged = w.B("salvaged"), SalvagedBy = w.S("by"), Module = w.S("module"), Materials = Res(w.O("yield")) });
            foreach (var x in Os(d, "settlements"))
                s.Settlements.Add(new Settlement { Id = x.S("id"), OfferId = x.S("offer"), BattleId = x.S("battle"), SystemId = x.S("system"), ConcederPolity = x.S("conceder"),
                    RecipientPolity = x.S("recipient"), Reparations = x.I("reparations"), Day = x.I("day"), Surrendered = x.B("surrendered"), SurrenderedUid = x.I("surrenderedUid"), Text = x.S("text") });
            foreach (var e in Os(d, "expeditions"))
                s.Expeditions.Add(new Expedition { Id = e.S("id"), SystemId = e.S("system"), Title = e.S("title"), State = En<ExpeditionState>(e.S("state")), Scouted = e.B("scouted"),
                    MapId = e.S("map"), RivalComposition = e.S("rival"), Asset = e.S("asset"), CreatedDay = e.I("day"), Outcome = e.S("outcome"), BattleId = e.S("battle") });
            foreach (var k in d.A("applied").OfType<string>()) s.Applied.Add(k);
            s.Scenario = Scenario(d.O("scenario"));
            foreach (var f in Os(d, "ground"))
            {
                var force = new GroundForce { Id = f.S("id"), OwnerId = f.S("owner"), PlanetId = f.S("planet"), FleetId = f.S("fleet") };
                foreach (var u in Os(f, "units")) force.Units.Add(new GroundUnit { Uid = u.I("uid"), DefId = u.S("def"), Hp = u.I("hp") });
                s.Ground.Add(force);
            }
            foreach (var o in Os(d, "operations")) s.Operations.Add(Operation(o));
            return s;
        }
    }
}
