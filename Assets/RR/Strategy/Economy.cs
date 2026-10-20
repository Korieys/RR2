using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    /// <summary>A local priority a world can work on (a project in the existing project system). Experimental values.</summary>
    public sealed class PriorityDef
    {
        public string Id, Polity, SystemId, Name, Effect, Enables, Forecloses;
        public Resources Cost;
        public int Days;
    }

    /// <summary>One affordable choice for the budget comparison, with what remains after it.</summary>
    public sealed class BudgetOption
    {
        public string Label, Detail;
        public Resources Cost;
        public bool Affordable;
        public string Blocked;
    }

    /// <summary>
    /// Campaign alpha Prompt 15: the bounded campaign's economy. It extends the existing one (the four resources, daily world
    /// yields, the project system, passage tolls) with what the scenario needs: fleet upkeep paid at each settlement, arrears
    /// instead of negative balances, a few local priorities with real effects, finite stores at a forward dock, and a
    /// settlement preview. Only the Passage War scenario uses it; the freeform campaigns are unchanged. No new currency.
    /// </summary>
    public static class Economy
    {
        public const int UpkeepPerPoint = 2;           // credits per fleet cost point, at each settlement (experimental)
        public const int DockCapacity = 60, DockRestock = 30;
        public const int OreYield = 4, PygmaeiConvoyYield = 3, InhumanusConvoyYield = 4;
        public const int UnderwritingDiscountPercent = 25;
        public const string FreePort = "shoal_haven";

        public static readonly PriorityDef[] Priorities =
        {
            new PriorityDef { Id = "forward_dock", Polity = "pygmaei", SystemId = "cinderhold", Name = "Forward repair dock", Cost = new Resources(0, 40, 40, 0), Days = 12,
                Effect = $"Capital ships can be repaired at Cinderhold. Materials come from the dock's own stores (up to {DockCapacity}, restocked with {DockRestock} from your stock at each settlement while Cinderhold is supplied); energy and credits from your stock.",
                Enables = "Repair at the front: Cinderhold is 20 days from Umbra, Forgefall 29.",
                Forecloses = "Cinderhold's project slot for 12 days, and the materials its stores hold." },
            new PriorityDef { Id = "ore_drive", Polity = "pygmaei", SystemId = "ironmouth", Name = "Ironmouth ore drive", Cost = new Resources(20, 0, 30, 0), Days = 8,
                Effect = $"+{OreYield} materials a day while Ironmouth is not blockaded.",
                Enables = "Repairs, refits and commissions sooner.",
                Forecloses = "30 credits now: about two settlements of upkeep, or most of an escort's credits." },
            new PriorityDef { Id = "convoy_office", Polity = "pygmaei", SystemId = "forgefall", Name = "Convoy office", Cost = new Resources(0, 30, 30, 0), Days = 10,
                Effect = $"+{PygmaeiConvoyYield} credits a day while your supply reaches the free port at Shoal Haven (by the northern route or through Umbra).",
                Enables = "Upkeep paid from trade; more credits for refits.",
                Forecloses = "The Forgefall shipyard for 10 days: no commissions there meanwhile." },
            new PriorityDef { Id = "convoy_office", Polity = "inhumanus", SystemId = "dividend", Name = "Exchange convoys", Cost = new Resources(0, 30, 30, 0), Days = 10,
                Effect = $"+{InhumanusConvoyYield} credits a day while your supply reaches the free port at Shoal Haven.",
                Enables = "Upkeep paid from trade; leverage for treaties.",
                Forecloses = "The Dividend shipyard for 10 days: no commissions there meanwhile." },
            new PriorityDef { Id = "ore_contract", Polity = "inhumanus", SystemId = "ledgerhold", Name = "Ledgerhold ore contract", Cost = new Resources(20, 0, 30, 0), Days = 8,
                Effect = $"+{OreYield} materials a day while Ledgerhold is not blockaded.",
                Enables = "Repairs, refits and commissions sooner.",
                Forecloses = "30 credits now." },
            new PriorityDef { Id = "underwriting", Polity = "inhumanus", SystemId = "ledgerhold", Name = "Escort underwriting", Cost = new Resources(0, 20, 40, 0), Days = 10,
                Effect = $"Escorts (not flagships) commissioned at Dividend cost {UnderwritingDiscountPercent}% fewer credits.",
                Enables = "More hulls for the same credits: a wider screen or a cheaper replacement escort.",
                Forecloses = "Ledgerhold's project slot for 10 days (it cannot also run the ore contract meanwhile)." },
        };

        static ScenarioState Sc(GalaxyState g) => PassageWar.Of(g);
        public static bool Active(GalaxyState g) => PassageWar.Running(g);

        public static PriorityDef Priority(string polity, string id) => Priorities.FirstOrDefault(p => p.Polity == polity && p.Id == id);
        public static bool Built(GalaxyState g, string polity, string id) => Sc(g)?.Built.Contains(polity + ":" + id) ?? false;
        public static bool HasDock(GalaxyState g, string polity, string systemId) =>
            Built(g, polity, "forward_dock") && Priority(polity, "forward_dock")?.SystemId == systemId && g.GetSystem(systemId).Planets.Any(p => p.OwnerId == polity);
        public static int Stores(GalaxyState g, string systemId) => Sc(g) != null && Sc(g).Stores.TryGetValue(systemId, out var v) ? v : 0;
        public static int Arrears(GalaxyState g, string polity) => Sc(g) != null && Sc(g).Arrears.TryGetValue(polity, out var v) ? v : 0;
        public static bool InArrears(GalaxyState g, string polity) => Arrears(g, polity) > 0;
        public static string ArrearsProblem(GalaxyState g, string polity) =>
            InArrears(g, polity) ? $"The treasury is {Arrears(g, polity)} credits in arrears on fleet upkeep: no repairs, refits or new projects until it is paid (income pays it first)." : null;

        /// <summary>Priority options a world offers (as projects), with every reason one is unavailable.</summary>
        public static IEnumerable<PriorityDef> PrioritiesAt(GalaxyState g, Planet p)
        {
            if (Sc(g) == null || p.OwnerId == null) return Enumerable.Empty<PriorityDef>();
            var sys = g.SystemOf(p);
            var first = sys.Planets.Where(x => x.OwnerId == p.OwnerId).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (first != p) return Enumerable.Empty<PriorityDef>();
            return Priorities.Where(x => x.Polity == p.OwnerId && x.SystemId == sys.Id && !Built(g, p.OwnerId, x.Id));
        }

        public static int Upkeep(GalaxyState g, string polity) =>
            g.Fleets.Where(f => f.OwnerId == polity).SelectMany(f => f.Ships).Sum(s => ShipCatalog.Cost(s.DefId)) * UpkeepPerPoint;

        /// <summary>Daily yields that priorities add (zero when their condition fails).</summary>
        public static Resources PriorityYield(GalaxyState g, string polity, SupplyNetwork net = null)
        {
            var r = default(Resources);
            if (Sc(g) == null) return r;
            foreach (var id in new[] { "ore_drive", "ore_contract" })
                if (Built(g, polity, id) && !g.GetSystem(Priority(polity, id).SystemId).Blockaded) r = r + new Resources(0, OreYield, 0, 0);
            if (Built(g, polity, "convoy_office"))
            {
                net = net ?? Supply.Of(g, polity);
                if (net.Has(FreePort)) r = r + new Resources(0, 0, polity == "inhumanus" ? InhumanusConvoyYield : PygmaeiConvoyYield, 0);
            }
            return r;
        }

        public static string ConvoyProblem(GalaxyState g, string polity)
        {
            if (!Built(g, polity, "convoy_office")) return null;
            var net = Supply.Of(g, polity);
            return net.Has(FreePort) ? null : $"Your convoys earn nothing while supply cannot reach Shoal Haven ({net.Node(FreePort)?.Reason ?? "no route"}).";
        }

        /// <summary>Credits a commission costs after any discount (escort underwriting).</summary>
        public static Resources CommissionCost(GalaxyState g, string polity, string defId, Resources baseCost)
        {
            if (!Built(g, polity, "underwriting") || ShipCatalog.IsLeader(defId)) return baseCost;
            return new Resources(baseCost.Energy, baseCost.Materials, baseCost.Credits * (100 - UnderwritingDiscountPercent) / 100, baseCost.Influence);
        }

        /// <summary>A priority project finished (called once by the project system when it completes).</summary>
        public static void Complete(GalaxyState g, Planet p, string priorityId)
        {
            var sc = Sc(g); var pr = Priority(p.OwnerId, priorityId);
            if (sc == null || pr == null || !sc.Built.Add(p.OwnerId + ":" + priorityId)) return;
            var sys = g.SystemOf(p);
            SliceCampaign.RecordEvent(g, "priority", sys.Id, null, $"{g.GetPolity(p.OwnerId).Name} completed {pr.Name} at {sys.Name}.");
            PassageWar.ForPolity(g, p.OwnerId, "priority", $"Completed {pr.Name} at {sys.Name}", default);
            g.Note(p.OwnerId == g.Player.Id ? "build" : "info", $"{pr.Name} is complete at {sys.Name}. {pr.Effect}");
        }

        // ------------------------------------------------------------------ the day and the settlement

        /// <summary>Daily, after world income and tolls: priority yields, arrears paid down first, and supply announcements.</summary>
        public static void Daily(GalaxyState g)
        {
            var sc = Sc(g);
            if (sc == null || !Active(g)) return;
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                var p = g.GetPolity(pol);
                p.Stock = p.Stock + PriorityYield(g, pol);
                int owed = Arrears(g, pol);
                if (owed > 0)
                {
                    int pay = Math.Min(owed, p.Stock.Credits);
                    p.Stock = p.Stock - new Resources(0, 0, pay, 0);
                    sc.Arrears[pol] = owed - pay;
                    if (sc.Arrears[pol] == 0) { sc.Arrears.Remove(pol); if (pol == g.Player.Id) g.Note("result", "Upkeep arrears are paid. Repairs, refits and projects may resume."); }
                }
            }
            Announce(g);
        }

        /// <summary>Tells the player when supply to a system is cut, restored or newly threatened (each change once).</summary>
        static void Announce(GalaxyState g)
        {
            var sc = Sc(g);
            string me = g.Player.Id;
            var net = Supply.Of(g, me);
            var now = new HashSet<string>(sc.ActiveSystems.Where(net.Has), StringComparer.Ordinal);
            var threats = new HashSet<string>(sc.ActiveSystems.Where(id => net.Node(id)?.Threat != null).Select(id => id + "|" + net.Node(id).Threat), StringComparer.Ordinal);
            if (sc.SupplyKnown != null)
            {
                var before = new HashSet<string>(sc.SupplyKnown, StringComparer.Ordinal);
                foreach (var id in before.Except(now).OrderBy(x => x, StringComparer.Ordinal))
                    g.Note("danger", $"Supply to {g.GetSystem(id).Name} is cut: {net.Node(id)?.Reason}. Effects begin at the settlement on day {PassageWar.NextSettlementDay(g)}.");
                foreach (var id in now.Except(before).OrderBy(x => x, StringComparer.Ordinal))
                    g.Note("result", $"Supply reaches {g.GetSystem(id).Name} again ({net.Node(id).Reason}).");
                var oldThreats = new HashSet<string>(sc.ThreatsKnown ?? new List<string>(), StringComparer.Ordinal);
                foreach (var t in threats.Except(oldThreats).OrderBy(x => x, StringComparer.Ordinal))
                    g.Note("danger", "Supply threatened: " + t.Split('|')[1] + ". Supply through it would be cut if it holds there.");
            }
            sc.SupplyKnown = now.OrderBy(x => x, StringComparer.Ordinal).ToList();
            sc.ThreatsKnown = threats.OrderBy(x => x, StringComparer.Ordinal).ToList();
        }

        /// <summary>At each settlement (keyed by PassageWar.OnDay): upkeep for both powers, arrears for what cannot be paid,
        /// and the dock restocked if it is supplied. Returns report lines for the player.</summary>
        public static List<string> Settle(GalaxyState g)
        {
            var lines = new List<string>();
            var sc = Sc(g);
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                var p = g.GetPolity(pol);
                int upkeep = Upkeep(g, pol);
                int pay = Math.Min(upkeep, p.Stock.Credits);
                p.Stock = p.Stock - new Resources(0, 0, pay, 0);
                if (upkeep > pay) sc.Arrears[pol] = Arrears(g, pol) + (upkeep - pay);
                if (upkeep > 0) PassageWar.ForPolity(g, pol, "upkeep", "Fleet upkeep" + (upkeep > pay ? $" ({upkeep - pay} credits unpaid, now arrears)" : ""), new Resources(0, 0, pay, 0));
                if (pol == g.Player.Id)
                    lines.Add($"Upkeep: {upkeep} credits for {upkeep / Math.Max(1, UpkeepPerPoint)} fleet points" + (upkeep > pay ? $"; {upkeep - pay} could not be paid and became arrears." : " (paid)."));
                var dock = Priority(pol, "forward_dock");
                if (dock != null && HasDock(g, pol, dock.SystemId))
                {
                    var net = Supply.Of(g, pol);
                    int have = Stores(g, dock.SystemId);
                    if (!net.Has(dock.SystemId)) { if (pol == g.Player.Id) lines.Add($"The {g.GetSystem(dock.SystemId).Name} dock was not restocked: {net.Node(dock.SystemId)?.Reason}. Its stores hold {have} materials."); }
                    else
                    {
                        int add = Math.Min(DockRestock, Math.Min(DockCapacity - have, p.Stock.Materials));
                        if (add > 0)
                        {
                            p.Stock = p.Stock - new Resources(0, add, 0, 0);
                            sc.Stores[dock.SystemId] = have + add;
                            PassageWar.ForPolity(g, pol, "stores", $"Restocked the {g.GetSystem(dock.SystemId).Name} dock with {add} materials", new Resources(0, add, 0, 0));
                        }
                        if (pol == g.Player.Id) lines.Add($"The {g.GetSystem(dock.SystemId).Name} dock holds {Stores(g, dock.SystemId)} of {DockCapacity} materials" + (add > 0 ? $" (+{add})." : "."));
                    }
                }
            }
            return lines;
        }

        // ------------------------------------------------------------------ forward repairs

        /// <summary>Whether <paramref name="f"/> can be repaired at a forward dock where it holds, and what stands in the way.</summary>
        public static bool AtDock(GalaxyState g, Fleet f) => f != null && !f.Moving && f.EngagementId == null && HasDock(g, f.OwnerId, f.SystemId);

        public static string DockProblem(GalaxyState g, Fleet f, Resources cost)
        {
            int have = Stores(g, f.SystemId);
            if (have < cost.Materials)
                return $"The {g.GetSystem(f.SystemId).Name} dock's stores hold {have} materials; this repair needs {cost.Materials}. " +
                       (Supply.Of(g, f.OwnerId).Has(f.SystemId) ? $"It is restocked with {DockRestock} at each settlement." : "It is cut off from supply and cannot be restocked.");
            return null;
        }

        public static void DrawStores(GalaxyState g, string systemId, int materials)
        {
            var sc = Sc(g);
            sc.Stores[systemId] = Math.Max(0, Stores(g, systemId) - materials);
        }

        // ------------------------------------------------------------------ previews

        /// <summary>The next normal settlement for <paramref name="polity"/>: inflows, upkeep, scheduled completions, known
        /// supply changes, and the projected stock (with the insolvency consequence if it falls short).</summary>
        public static List<string> Preview(GalaxyState g, string polity)
        {
            var lines = new List<string>();
            var sc = Sc(g);
            if (sc == null) return lines;
            int next = PassageWar.NextSettlementDay(g), days = next - g.Day;
            var pol = g.GetPolity(polity);
            var net = Supply.Of(g, polity);
            var worlds = StrategyRules.DailyIncome(g, polity);
            var prio = PriorityYield(g, polity, net);
            var passage = g.Slice.Passage(SliceCampaign.PassageSystem);
            int toll = passage.AccessHolderId == polity ? passage.TollPerDay : 0;
            lines.Add($"Next settlement: day {next} (in {days} day{(days == 1 ? "" : "s")}).");
            lines.Add($"Worlds: +{worlds.Energy} energy, +{worlds.Materials} materials, +{worlds.Credits} credits a day" +
                      (g.Systems.Any(s => s.Blockaded && s.OwnerId == polity) ? $" (blockaded worlds pay nothing: {string.Join(", ", g.Systems.Where(s => s.Blockaded && s.OwnerId == polity).Select(s => s.Name))})." : "."));
            if (prio.Materials + prio.Credits > 0) lines.Add($"Priorities: +{prio.Materials} materials, +{prio.Credits} credits a day.");
            var convoy = ConvoyProblem(g, polity);
            if (convoy != null) lines.Add(convoy);
            if (toll > 0) lines.Add($"Passage toll: +{toll} credits a day.");
            int upkeep = Upkeep(g, polity);
            lines.Add($"Fleet upkeep at the settlement: −{upkeep} credits ({upkeep / Math.Max(1, UpkeepPerPoint)} fleet points × {UpkeepPerPoint}).");
            if (InArrears(g, polity)) lines.Add($"Arrears: {Arrears(g, polity)} credits outstanding, paid first from income.");
            foreach (var p in g.Systems.SelectMany(s => s.Planets).Where(p => p.OwnerId == polity && p.Project != null).OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                int left = p.Project.DaysTotal - p.Project.DaysDone;
                string what = p.Project.Kind == ProjectKind.CommissionShip ? ShipCatalog.Name(p.Project.ShipDefId) : p.Project.Kind == ProjectKind.Priority ? Priority(polity, p.Project.PriorityId)?.Name : "Crystal Foundry";
                lines.Add(left <= days ? $"Completes before the settlement: {what} at {p.Name} (day {g.Day + left})." : $"In production: {what} at {p.Name}, {left} days left.");
                if (InArrears(g, polity)) lines.Add($"  (paused while in arrears)");
            }
            var dock = Priority(polity, "forward_dock");
            if (dock != null && HasDock(g, polity, dock.SystemId))
                lines.Add(net.Has(dock.SystemId) ? $"Dock restock: up to {DockRestock} materials into {g.GetSystem(dock.SystemId).Name}'s stores (now {Stores(g, dock.SystemId)})."
                                                 : $"{g.GetSystem(dock.SystemId).Name} dock: cut off, no restock ({net.Node(dock.SystemId)?.Reason}); stores {Stores(g, dock.SystemId)}.");
            foreach (var n in net.Nodes.Values.Where(n => n.Threat != null).OrderBy(n => n.SystemId, StringComparer.Ordinal)) lines.Add("Known threat: " + n.Threat + ".");
            int credits = pol.Stock.Credits - Arrears(g, polity) + days * (worlds.Credits + prio.Credits + toll) - upkeep;
            int materials = pol.Stock.Materials + days * (worlds.Materials + prio.Materials);
            lines.Add($"Projected after the settlement: about {Math.Max(0, credits)} credits and {materials} materials (before any orders you give).");
            if (credits < 0)
                lines.Add($"Insolvent by about {-credits} credits: unpaid upkeep becomes arrears. While in arrears projects pause and no repairs, refits or new projects are allowed; " +
                          "income pays the arrears first. Nothing is bought on credit and no balance goes below zero.");
            return lines;
        }

        /// <summary>The bounded choice the scenario asks about: repair the veteran flagship, fund an escort, or a local
        /// priority, each with its cost and what the stock would be after it.</summary>
        public static List<BudgetOption> Budget(GalaxyState g, string polity)
        {
            var list = new List<BudgetOption>();
            var pol = g.GetPolity(polity);
            var arrears = ArrearsProblem(g, polity);
            var flagRec = g.Fleets.Where(f => f.OwnerId == polity).SelectMany(f => f.Ships).FirstOrDefault(s => ShipCatalog.IsLeader(s.DefId));
            if (flagRec != null)
            {
                var cost = SliceCampaign.RepairCost(g, flagRec.Uid);
                var f = SliceCampaign.FleetOf(g, flagRec.Uid);
                var where = SliceCampaign.RefitLocationProblem(g, f);
                bool dock = where != null && AtDock(g, f);
                var pay = dock ? new Resources(cost.Energy, 0, cost.Credits, cost.Influence) : cost;
                string blocked = flagRec.Hp >= SliceCampaign.MaxHull(g, flagRec) ? "The hull is whole." : arrears ?? (where != null && !dock ? where : dock ? DockProblem(g, f, cost) : null) ??
                                 (pol.Stock.Covers(pay) ? null : $"Needs {pay}; you have {pol.Stock}.");
                list.Add(new BudgetOption { Label = $"Repair {SliceCampaign.ShipName(g, flagRec)}", Cost = pay, Affordable = blocked == null, Blocked = blocked,
                    Detail = $"Hull {flagRec.Hp} to {SliceCampaign.MaxHull(g, flagRec)}" + (dock ? ", materials from the dock's stores." : ".") });
            }
            var yard = g.GetSystem(SliceCampaign.HomeOf(polity)).Planets.FirstOrDefault(p => p.HasShipyard && p.OwnerId == polity);
            if (yard != null)
            {
                var escort = SliceCampaign.Commissionable(g, polity)?.Where(o => !ShipCatalog.IsLeader(o.defId)).OrderBy(o => o.cost.Credits).FirstOrDefault();
                if (escort != null && escort.Value.defId != null)
                {
                    var c = escort.Value.cost;
                    string blocked = arrears ?? (yard.Project != null ? $"{yard.Name} is busy ({ProjectName(g, yard)})." : pol.Stock.Covers(c) ? null : $"Needs {c}; you have {pol.Stock}.");
                    list.Add(new BudgetOption { Label = $"Commission a {ShipCatalog.Name(escort.Value.defId)}", Cost = c, Affordable = blocked == null, Blocked = blocked,
                        Detail = $"{escort.Value.days} days at {yard.Name}; +{ShipCatalog.Cost(escort.Value.defId) * UpkeepPerPoint} upkeep a settlement." });
                }
            }
            foreach (var pr in Priorities.Where(x => x.Polity == polity && !Built(g, polity, x.Id)))
            {
                var planet = g.GetSystem(pr.SystemId).Planets.Where(p => p.OwnerId == polity).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                string blocked = planet == null ? $"{g.GetSystem(pr.SystemId).Name} is not yours." : arrears ?? (planet.Project != null ? $"{planet.Name} is busy ({ProjectName(g, planet)})." :
                                 g.GetSystem(pr.SystemId).Blockaded ? $"{g.GetSystem(pr.SystemId).Name} is blockaded." : pol.Stock.Covers(pr.Cost) ? null : $"Needs {pr.Cost}; you have {pol.Stock}.");
                list.Add(new BudgetOption { Label = pr.Name, Cost = pr.Cost, Affordable = blocked == null, Blocked = blocked, Detail = $"{pr.Days} days at {g.GetSystem(pr.SystemId).Name}. {pr.Effect}" });
            }
            return list;
        }

        public static string ProjectName(GalaxyState g, Planet p) => p.Project == null ? "idle" :
            p.Project.Kind == ProjectKind.CommissionShip ? ShipCatalog.Name(p.Project.ShipDefId) : p.Project.Kind == ProjectKind.Priority ? Priority(p.OwnerId, p.Project.PriorityId)?.Name ?? p.Project.PriorityId : "Crystal Foundry";
    }
}
