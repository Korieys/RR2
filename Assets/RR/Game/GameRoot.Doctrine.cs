using System;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Campaign alpha Prompt 18: the doctrine window. Two strategic paths per faction; each project shows its prerequisite, its
    // real price and time, what starting it commits, delays or forecloses, and a line in the faction's voice (provisional
    // writing pending approval). Orders go through Doctrines.Start and Doctrines.Cancel.
    public sealed partial class GameRoot
    {
        public void OpenDoctrine()
        {
            if (PassageWar.Of(G) == null) return;
            string me = G.Player.Id;
            var m = UI.OpenModal(1120);
            var t = UIX.Text("DOCTRINE", "modal-title"); UIRoot.Display(t); m.Add(t);
            var path = Doctrines.PathOf(G, me);
            m.Add(P(path == null ? "No path chosen yet. The first project you start commits you to its path: the other path's first two projects then take twice the time and cost, and its last is foreclosed."
                                 : $"Committed to {Doctrines.PathNames[path]}. Adopted doctrines are never refunded; cancelling a project under way returns half its cost.", 13));
            var cur = Doctrines.Current(G, me);
            if (cur != null)
            {
                var row0 = Row(); m.Add(row0);
                var l0 = P($"Under way: {Doctrines.Get(cur.ProjectId).Name}, {cur.DaysDone} of {cur.DaysTotal} days" + (Economy.InArrears(G, me) ? " (paused: arrears)." : "."), 13); l0.style.width = 800; row0.Add(l0);
                B(row0, "Cancel (half back)", () => { Report(Doctrines.Cancel(G, me), "Cancelled."); OpenDoctrine(); });
            }
            var cols = Row(); cols.style.alignItems = Align.FlexStart; m.Add(cols);
            foreach (var p in Doctrines.All.Where(x => x.Polity == me).Select(x => x.Path).Distinct())
            {
                var col = new VisualElement(); col.style.width = 530; col.style.marginRight = 16; cols.Add(col);
                col.Add(UIX.Text(Doctrines.PathNames[p].ToUpperInvariant() + (path == p ? "  (COMMITTED)" : ""), "h-section"));
                foreach (var d in Doctrines.All.Where(x => x.Polity == me && x.Path == p).OrderBy(x => x.Tier))
                {
                    var (cost, days) = Doctrines.Price(G, me, d);
                    bool has = Doctrines.Has(G, me, d.Id);
                    var why = has ? null : Doctrines.Problem(G, me, d);
                    col.Add(UIX.Text($"{d.Tier}. {d.Name}" + (has ? " (adopted)" : cur?.ProjectId == d.Id ? " (under way)" : ""), "item-title"));
                    col.Add(P(d.Voice, 12, new Color(0.91f, 0.78f, 0.47f)));
                    col.Add(P(d.Effect, 12));
                    if (!has)
                    {
                        col.Add(P($"{PassageWar.CostText(cost)}; {days} days" + (d.Requires != null ? $"; requires {Doctrines.Get(d.Requires).Name}" : "") + ". " + Doctrines.Consequence(G, me, d), 11));
                        var r = Row(); col.Add(r);
                        B(r, why == null ? "Begin" : "Unavailable", () => { Report(Doctrines.Start(G, me, d.Id), "Begun."); PlaytestMetrics.Decision("doctrine " + d.Id); OpenDoctrine(); }, why == null);
                        if (why != null) col.Add(P(why, 11, new Color(1f, 0.6f, 0.55f)));
                    }
                }
            }
            string them = PassageWar.Other(me);
            var theirs = Doctrines.Disclosure(G, them);
            if (theirs.Count > 0)
            {
                m.Add(UIX.Text($"{G.GetPolity(them).Name.ToUpperInvariant()} DOCTRINES (PUBLIC)", "h-section"));
                foreach (var l in theirs) m.Add(P(l, 12));
            }
            m.Add(P("Broader politics (satisfaction, legitimacy, unrest) are not part of this project; these doctrines change the economy, routes and battle preparation only. The writing is provisional and pending approval.", 11));
            var end = Row(); m.Add(end);
            B(end, "Close", () => UI.CloseModal());
        }
    }
}
