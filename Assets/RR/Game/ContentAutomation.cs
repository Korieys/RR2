using System;
using System.Collections;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Content preview path (campaign alpha Prompt 19; -rrContentPreview <absolute dir>, with -rrSavePath <disposable>): every
    // module in the content file fitted to both flagships in the hangar (the live 3D hull with the module's mesh) and the stat
    // changes the rules will apply, plus the content validation report. For authors checking a new sidegrade.
    public static class ContentAutomation
    {
        static void Note(string s) => Automation.Note(s);

        public static IEnumerator Preview(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("content preview; rules " + RulesVersion.Id + "; content " + RulesVersion.ContentFingerprint + "; " + SliceContent.Version);
            var problems = SliceContentCheck.Validate();
            Note(problems.Count == 0 ? "content validation: ok" : "content validation: " + string.Join("; ", problems));
            yield return SliceAutomation.Wait(2f);
            foreach (var f in new[] { FactionId.Pygmaei, FactionId.Inhumanus })
            {
                root.StartPassageWar(f);
                yield return SliceAutomation.Until(() => !root.Busy && PassageWar.Of(root.G) != null && root.UI.ModalOpen, 60);
                root.UI.CloseModal();
                var g = root.G;
                g.Player.Stock = new RR.Strategy.Resources(999, 999, 999, 99);   // a preview, not play: the save is disposable
                int flag = g.Fleets.First(x => x.OwnerId == g.Player.Id).Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Uid;
                foreach (var m in SliceRules.Modules)
                {
                    var r = SliceCampaign.ApplyRefit(g, flag, m.Id);
                    var def = ShipCatalog.Def(g.Fleets.SelectMany(x => x.Ships).First(s => s.Uid == flag).DefId);
                    Note($"{f} {m.Id} ({m.Slot}, visual {m.Visual}): {(r.Ok ? "fitted" : r.Reason)}; {m.Summary} Base: move {def.Roster.MoveHexes}, hull {def.MaxHp}, armour {def.Armor}, " +
                         $"damage {def.Weapon.Damage}, range {def.Weapon.MinRange}-{def.Weapon.MaxRange}; fitted now: {string.Join(", ", g.Slice.Profile(flag).Modules)} (hull {SliceCampaign.MaxHull(g, SliceCampaign.Ship(g, flag))}).");
                    root.OpenHangar(flag);
                    yield return SliceAutomation.Wait(1.0f);
                    yield return Automation.Shot(dir, $"cp_{f.ToString().ToLowerInvariant()}_{m.Id}");
                    root.UI.CloseModal();
                }
            }
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            Application.Quit();
        }
    }
}
