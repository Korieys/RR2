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
                ["passages"] = L(s.Passages, p => new Dictionary<string, object> { ["system"] = p.SystemId, ["name"] = p.Name, ["holder"] = p.AccessHolderId, ["toll"] = p.TollPerDay, ["since"] = p.SinceDay }),
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
            };
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
                s.Passages.Add(new PassageState { SystemId = p.S("system"), Name = p.S("name"), AccessHolderId = p.S("holder"), TollPerDay = p.I("toll"), SinceDay = p.I("since") });
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
            return s;
        }
    }
}
