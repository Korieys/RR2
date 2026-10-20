using System;
using System.Collections.Generic;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Campaign alpha Prompt 15: the treasury window (settlement preview, the bounded budget choice, local priorities) and the
    // supply overlay on the galaxy. Both read the strategy layer (Economy, Supply); orders go through StrategyRules and
    // SliceCampaign like every other order.
    public sealed partial class GameRoot
    {
        public bool SupplyOverlay { get; private set; }
        public SupplyNetwork SupplyNet { get; private set; }
        string _supplySig;

        public void ToggleSupply()
        {
            SupplyOverlay = !SupplyOverlay;
            _supplySig = null;
            if (!SupplyOverlay) { Galaxy?.ShowSupply(null); SupplyNet = null; }
        }

        /// <summary>Rebuilds the overlay when anything it depends on changes (the day, fleets, orbits, access).</summary>
        void SupplyUpdate()
        {
            if (!SupplyOverlay || Galaxy == null || PassageWar.Of(G) == null) { if (SupplyOverlay && PassageWar.Of(G) == null) ToggleSupply(); return; }
            var p = G.Slice.Passage(SliceCampaign.PassageSystem);
            string sig = $"{G.Day}|{p.AccessHolderId}|{string.Join(",", G.Fleets.Select(f => f.Id + f.SystemId + (f.Moving ? "m" : "")))}|{string.Join(",", G.Systems.Select(s => s.OrbitalControlId))}";
            if (sig == _supplySig) return;
            _supplySig = sig;
            SupplyNet = Supply.Of(G, G.Player.Id);
            var segs = new List<(string, string, Color)>();
            foreach (var n in SupplyNet.Nodes.Values.Where(n => n.Supplied && n.Via != null))
                segs.Add((n.Via, n.SystemId, n.Threat != null ? new Color(1f, 0.72f, 0.3f, 1.2f) : new Color(0.45f, 0.95f, 0.78f, 1.1f)));
            // Cut segments: from a supplied system to a neighbour that supply cannot enter, so the break is visible where it is.
            foreach (var n in SupplyNet.Nodes.Values.Where(n => !n.Supplied))
                foreach (var l in G.LanesOf(n.SystemId).Where(l => SupplyNet.Has(l.Other(n.SystemId))))
                    segs.Add((l.Other(n.SystemId), n.SystemId, new Color(1f, 0.4f, 0.35f, 1.2f)));
            Galaxy.ShowSupply(segs);
        }

        public void OpenTreasury()
        {
            if (PassageWar.Of(G) == null) return;
            string me = G.Player.Id;
            var m = UI.OpenModal(1040);
            var t = UIX.Text("TREASURY AND SUPPLY", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P($"Stock: {G.Player.Stock.Energy} energy, {G.Player.Stock.Materials} materials, {G.Player.Stock.Credits} credits, {G.Player.Stock.Influence} influence. " +
                    "Values are provisional design numbers.", 13));
            var arrears = Economy.ArrearsProblem(G, me);
            if (arrears != null) m.Add(P(arrears, 13, new Color(1f, 0.55f, 0.5f)));
            m.Add(UIX.Text("THE NEXT SETTLEMENT", "h-section"));
            foreach (var l in Economy.Preview(G, me)) m.Add(P(l, l.StartsWith("Insolvent") ? 13 : 12, l.StartsWith("Insolvent") ? new Color(1f, 0.55f, 0.5f) : (Color?)null));
            m.Add(UIX.Text("CHOICES: WHAT EACH COSTS AND WHAT IS LEFT", "h-section"));
            foreach (var o in Economy.Budget(G, me))
            {
                var r = Row(); m.Add(r);
                var after = G.Player.Stock - o.Cost;
                var l = P($"{o.Label}: {(PassageWar.CostText(o.Cost) == "" ? "no cost" : PassageWar.CostText(o.Cost))}. {o.Detail}" + (o.Affordable ? $" Leaves {after.Energy} energy, {after.Materials} materials, {after.Credits} credits." : " " + o.Blocked), 12);
                l.style.width = 820; r.Add(l);
                if (o.Affordable) B(r, "Do it", () => BudgetAction(o.Label));
            }
            m.Add(UIX.Text("SUPPLY", "h-section"));
            m.Add(P("Supply runs from your shipyard (and a forward dock, if built) along lanes through your own systems, unclaimed systems and neutral ports. " +
                    "Enemy fleets holding a system, enemy-held orbits and passage access you do not hold stop it. It decides whether your hold at Umbra counts, " +
                    "whether a forward dock is restocked and whether convoys earn. It never changes combat.", 12));
            foreach (var l in Supply.Describe(G, me)) m.Add(P(l, 12, l.Contains(": cut") ? new Color(1f, 0.6f, 0.55f) : (Color?)null));
            var row = Row(); m.Add(row);
            B(row, SupplyOverlay ? "Hide the supply overlay" : "Show the supply overlay", () => { ToggleSupply(); _campaign?.Invalidate(); OpenTreasury(); });
            B(row, "The mandate", OpenMandate);
            B(row, "Close", () => UI.CloseModal());
        }

        /// <summary>Carries out a budget choice through the same rules the inspector and hangar use.</summary>
        void BudgetAction(string label)
        {
            string me = G.Player.Id;
            var flag = G.Fleets.Where(f => f.OwnerId == me).SelectMany(f => f.Ships).FirstOrDefault(s => ShipCatalog.IsLeader(s.DefId));
            if (flag != null && label == $"Repair {SliceCampaign.ShipName(G, flag)}") { Report(SliceCampaign.Repair(G, flag.Uid), "Repaired."); OpenTreasury(); return; }
            var yard = G.GetSystem(SliceCampaign.HomeOf(me)).Planets.FirstOrDefault(p => p.HasShipyard && p.OwnerId == me);
            var escort = SliceCampaign.Commissionable(G, me)?.Where(o => !ShipCatalog.IsLeader(o.defId)).OrderBy(o => o.cost.Credits).FirstOrDefault();
            if (yard != null && escort != null && label == $"Commission a {ShipCatalog.Name(escort.Value.defId)}") { StartProject(yard.Id, ProjectKind.CommissionShip, escort.Value.defId); OpenTreasury(); return; }
            var pr = Economy.Priorities.FirstOrDefault(x => x.Polity == me && x.Name == label);
            if (pr != null)
            {
                var planet = G.GetSystem(pr.SystemId).Planets.Where(p => p.OwnerId == me).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                if (planet != null) StartProject(planet.Id, ProjectKind.Priority, pr.Id);
                OpenTreasury();
            }
        }
    }
}
