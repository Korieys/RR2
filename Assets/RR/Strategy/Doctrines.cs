using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>One doctrine project: a step on one of a faction's two strategic paths (campaign alpha Prompt 18).</summary>
    public sealed class DoctrineProject
    {
        public string Id, Polity, Path, Name, Requires, Kind, Effect, Voice;
        public int Tier;
        public Resources Cost;
        public int Days;
        /// <summary>The army-entry token a tactical sidegrade adds to the faction's ships in battle, or null.</summary>
        public string Token;
    }

    public sealed class DoctrineResearch
    {
        public string ProjectId;
        public int DaysDone, DaysTotal;
        public Resources Paid;
    }

    /// <summary>
    /// A small progression layer that connects civilization management to fleet doctrine. It extends what exists: research
    /// becomes a scenario project paid in the existing resources (influence included) and built over days; effects reuse the
    /// supply network, lane travel, tolls, treaties, repairs, the forward dock, the operation reserves, Pygmaei overclock heat
    /// and Inhumanus fleet contracts. No satisfaction or legitimacy system exists in this project, so policy effects stay
    /// economic; broader politics are deferred. Each faction has two paths; the first project started commits the faction to
    /// its path: the other path's first two projects take twice the time and cost, and its last project is foreclosed.
    /// Completed projects are never refunded; a cancelled one returns half its cost. Values are provisional.
    /// </summary>
    public static class Doctrines
    {
        public const int OffPathMultiplier = 2;
        public const string CoolantToken = SliceRules.CoolantDoctrine, FurnaceToken = SliceRules.FurnaceDoctrine, LeveragedToken = SliceRules.LeveragedDoctrine, GuaranteedToken = SliceRules.GuaranteedDoctrine;

        public static readonly DoctrineProject[] All =
        {
            // Pygmaei: rapid expedition support
            new DoctrineProject { Id = "pyg_forward_logistics", Polity = "pygmaei", Path = "expedition", Tier = 1, Name = "Forward logistics", Kind = "route",
                Cost = new Resources(20, 20, 40, 10), Days = 15,
                Effect = "Your fleets cross lanes inside your own supply network a quarter faster (a lane of 4 days or more saves at least a day).",
                Voice = "A people reborn does not wait at home for the war to come to it. The forges send their fire forward." },
            new DoctrineProject { Id = "pyg_field_refits", Polity = "pygmaei", Path = "expedition", Tier = 2, Requires = "pyg_forward_logistics", Name = "Field refits", Kind = "preparation",
                Cost = new Resources(0, 40, 40, 10), Days = 15,
                Effect = "Your forward dock can fit modules as well as repair hulls, and modules fitted there cost a quarter fewer credits.",
                Voice = "A ship that must sail home to change its guns is a ship out of the fight." },
            new DoctrineProject { Id = "pyg_coolant_discipline", Polity = "pygmaei", Path = "expedition", Tier = 3, Requires = "pyg_field_refits", Name = "Coolant discipline", Kind = "tactical",
                Cost = new Resources(20, 30, 30, 10), Days = 20, Token = CoolantToken,
                Effect = "Tactical sidegrade: your ships overheat at 6 heat instead of 4, but an overclock adds +1 movement or +1 damage instead of +2. Disclosed to both sides before deployment.",
                Voice = "Burn steadily and you burn longer. The furnace is a tool, not a temper." },
            // Pygmaei: sustained industrial campaigning
            new DoctrineProject { Id = "pyg_foundry_quotas", Polity = "pygmaei", Path = "industry", Tier = 1, Name = "Foundry quotas", Kind = "economy",
                Cost = new Resources(0, 0, 40, 10), Days = 12,
                Effect = "+2 materials a day at each of your worlds in the scenario that is not blockaded.",
                Voice = "Every forge on every world, lit and counted. The war is won in the quota books first." },
            new DoctrineProject { Id = "pyg_yard_expansion", Polity = "pygmaei", Path = "industry", Tier = 2, Requires = "pyg_foundry_quotas", Name = "Yard expansion", Kind = "economy",
                Cost = new Resources(30, 60, 40, 10), Days = 20,
                Effect = "Ships commissioned at your home shipyard take a third less time.",
                Voice = "More slips, more hulls, fewer excuses." },
            new DoctrineProject { Id = "pyg_furnace_doctrine", Polity = "pygmaei", Path = "industry", Tier = 3, Requires = "pyg_yard_expansion", Name = "Furnace doctrine", Kind = "tactical",
                Cost = new Resources(20, 40, 30, 10), Days = 20, Token = FurnaceToken,
                Effect = "Tactical sidegrade: an overclocked attack adds +3 damage instead of +2 but costs 3 heat instead of 2 (still overheated at 4). Disclosed to both sides before deployment.",
                Voice = "Hit once, hit hard, and let the hull sing with it." },
            // Inhumanus: commercial-access leverage
            new DoctrineProject { Id = "inh_toll_brokerage", Polity = "inhumanus", Path = "commerce", Tier = 1, Name = "Toll brokerage", Kind = "economy",
                Cost = new Resources(0, 0, 40, 10), Days = 12,
                Effect = "While you hold the passage its toll pays you 2 more credits a day, and in treaty proposals your payments weigh a third more (payment ÷ 3 instead of ÷ 4).",
                Voice = "Every passage is a ledger line. We would rather own the line than fight over the ships." },
            new DoctrineProject { Id = "inh_free_port_charter", Polity = "inhumanus", Path = "commerce", Tier = 2, Requires = "inh_toll_brokerage", Name = "Free-port charter", Kind = "route",
                Cost = new Resources(0, 20, 50, 10), Days = 15,
                Effect = "Your fleets cross lanes that touch Shoal Haven a quarter faster, and your convoys earn 2 more credits a day.",
                Voice = "The free port belongs to no one. Its schedules belong to us." },
            new DoctrineProject { Id = "inh_leveraged_contracts", Polity = "inhumanus", Path = "commerce", Tier = 3, Requires = "inh_free_port_charter", Name = "Leveraged contracts", Kind = "tactical",
                Cost = new Resources(0, 20, 40, 10), Days = 20, Token = LeveragedToken,
                Effect = "Tactical sidegrade: contracted escorts cost 2 points less (minimum 1) but arrive at the start of round 4 instead of 3. Disclosed to both sides before deployment.",
                Voice = "Deferred delivery, discounted terms. The counterparty always pays for impatience." },
            // Inhumanus: underwritten military protection
            new DoctrineProject { Id = "inh_underwritten_escorts", Polity = "inhumanus", Path = "protection", Tier = 1, Name = "Underwritten repairs", Kind = "economy",
                Cost = new Resources(0, 30, 40, 10), Days = 12,
                Effect = "Repairs to your ships cost half the credits.",
                Voice = "An asset under warranty is an asset that returns to service." },
            new DoctrineProject { Id = "inh_bonded_garrisons", Polity = "inhumanus", Path = "protection", Tier = 2, Requires = "inh_underwritten_escorts", Name = "Bonded garrisons", Kind = "preparation",
                Cost = new Resources(20, 40, 30, 10), Days = 15,
                Effect = "In an operation against one of your worlds, your garrison's reserves stay available even if its supplies are seized.",
                Voice = "A bonded garrison is paid in advance. It does not starve when the convoys stop." },
            new DoctrineProject { Id = "inh_guaranteed_arrival", Polity = "inhumanus", Path = "protection", Tier = 3, Requires = "inh_bonded_garrisons", Name = "Guaranteed arrival", Kind = "tactical",
                Cost = new Resources(20, 30, 40, 10), Days = 20, Token = GuaranteedToken,
                Effect = "Tactical sidegrade: contracted escorts arrive at the start of round 2 instead of 3, at full price (no discount). Disclosed to both sides before deployment.",
                Voice = "Protection that arrives late is a refund. We do not issue refunds." },
        };

        public static readonly Dictionary<string, string> PathNames = new Dictionary<string, string>
        {
            ["expedition"] = "Rapid expedition support", ["industry"] = "Sustained industrial campaigning",
            ["commerce"] = "Commercial-access leverage", ["protection"] = "Underwritten military protection",
        };

        static ScenarioState Sc(GalaxyState g) => PassageWar.Of(g);
        public static DoctrineProject Get(string id) => All.FirstOrDefault(p => p.Id == id);
        public static bool Has(GalaxyState g, string polity, string id) => Sc(g)?.Researched.Contains(polity + ":" + id) ?? false;
        public static string PathOf(GalaxyState g, string polity) => Sc(g) != null && Sc(g).DoctrinePath.TryGetValue(polity, out var p) ? p : null;
        public static DoctrineResearch Current(GalaxyState g, string polity) => Sc(g) != null && Sc(g).Researching.TryGetValue(polity, out var r) ? r : null;

        /// <summary>The cost and time of a project for this polity now (doubled off its committed path).</summary>
        public static (Resources cost, int days) Price(GalaxyState g, string polity, DoctrineProject p)
        {
            var path = PathOf(g, polity);
            bool off = path != null && path != p.Path;
            return off ? (new Resources(p.Cost.Energy * OffPathMultiplier, p.Cost.Materials * OffPathMultiplier, p.Cost.Credits * OffPathMultiplier, p.Cost.Influence * OffPathMultiplier), p.Days * OffPathMultiplier)
                       : (p.Cost, p.Days);
        }

        /// <summary>Why a project cannot be started now, or null.</summary>
        public static string Problem(GalaxyState g, string polity, DoctrineProject p)
        {
            if (Sc(g) == null || !PassageWar.Running(g)) return "Doctrine projects belong to the Passage War scenario.";
            if (p.Polity != polity) return "That is not your faction's doctrine.";
            if (Has(g, polity, p.Id)) return "Already adopted.";
            if (Current(g, polity) != null) return $"{Get(Current(g, polity).ProjectId).Name} is under way; one doctrine at a time.";
            var path = PathOf(g, polity);
            if (path != null && path != p.Path && p.Tier >= 3) return $"Foreclosed: you committed to {PathNames[path]}.";
            if (p.Requires != null && !Has(g, polity, p.Requires)) return $"Requires {Get(p.Requires).Name}.";
            if (Economy.InArrears(g, polity)) return Economy.ArrearsProblem(g, polity);
            var (cost, _) = Price(g, polity, p);
            if (!g.GetPolity(polity).Stock.Covers(cost)) return $"Needs {PassageWar.CostText(cost)}; you have {PassageWar.CostText(g.GetPolity(polity).Stock)}.";
            return null;
        }

        /// <summary>What starting this project would commit, delay or foreclose (shown before the choice).</summary>
        public static string Consequence(GalaxyState g, string polity, DoctrineProject p)
        {
            var path = PathOf(g, polity);
            if (path == null)
            {
                var other = PathNames.Keys.First(k => k != p.Path && All.Any(x => x.Polity == polity && x.Path == k));
                return $"Commits you to {PathNames[p.Path]}: {PathNames[other]} projects then take twice the time and cost, and its last project ({All.First(x => x.Polity == polity && x.Path == other && x.Tier == 3).Name}) is foreclosed.";
            }
            return path == p.Path ? $"On your committed path ({PathNames[path]})." : $"Off your committed path: twice the time and cost.";
        }

        public static StratResult Start(GalaxyState g, string polity, string id)
        {
            var p = Get(id);
            if (p == null) return StratResult.Reject("No such doctrine.");
            var why = Problem(g, polity, p);
            if (why != null) return StratResult.Reject(why);
            var sc = Sc(g);
            var (cost, days) = Price(g, polity, p);
            var pol = g.GetPolity(polity);
            pol.Stock = pol.Stock - cost;
            if (PathOf(g, polity) == null) sc.DoctrinePath[polity] = p.Path;
            sc.Researching[polity] = new DoctrineResearch { ProjectId = id, DaysTotal = days, Paid = cost };
            PassageWar.ForPolity(g, polity, "doctrine", $"Began {p.Name} ({days} days)", cost);
            if (polity == g.Player.Id) g.Note("build", $"Doctrine: {p.Name} begun ({days} days).");
            return StratResult.Accept();
        }

        /// <summary>Cancels the project under way: half its cost back. If it was the faction's only step on its path, the
        /// commitment is released (so a change of mind costs half the first project: no free respecification).</summary>
        public static StratResult Cancel(GalaxyState g, string polity)
        {
            var sc = Sc(g);
            var r = Current(g, polity);
            if (r == null) return StratResult.Reject("No doctrine is under way.");
            sc.Researching.Remove(polity);
            var back = r.Paid.Half();
            g.GetPolity(polity).Stock = g.GetPolity(polity).Stock + back;
            var p = Get(r.ProjectId);
            if (!sc.Researched.Any(x => x.StartsWith(polity + ":", StringComparison.Ordinal))) sc.DoctrinePath.Remove(polity);
            PassageWar.ForPolity(g, polity, "doctrine", $"Cancelled {p.Name} (half refunded)", default(Resources) - back);
            return StratResult.Accept();
        }

        /// <summary>Daily progress (paused while in arrears), completed exactly once.</summary>
        public static void Daily(GalaxyState g)
        {
            var sc = Sc(g);
            if (sc == null || !PassageWar.Running(g)) return;
            foreach (var pol in sc.Researching.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList())
            {
                if (Economy.InArrears(g, pol)) continue;
                var r = sc.Researching[pol];
                r.DaysDone++;
                if (r.DaysDone < r.DaysTotal) continue;
                sc.Researching.Remove(pol);
                if (!sc.Researched.Add(pol + ":" + r.ProjectId)) continue;
                var p = Get(r.ProjectId);
                SliceCampaign.RecordEvent(g, "doctrine", SliceCampaign.HomeOf(pol), null, $"{g.GetPolity(pol).Name} adopted {p.Name}. {p.Effect}");
                PassageWar.ForPolity(g, pol, "doctrine", $"Adopted {p.Name}", default);
                g.Note(pol == g.Player.Id ? "result" : "info", $"{g.GetPolity(pol).Name} adopted {p.Name}.");
            }
        }

        // ------------------------------------------------------------------ effects, read by the rules that already exist

        /// <summary>Lane days for a fleet: a quarter fewer on lanes inside its supply network (Forward logistics) or touching
        /// Shoal Haven (Free-port charter); otherwise the lane's days.</summary>
        public static int LaneDays(GalaxyState g, string polity, Lane lane, SupplyNetwork net = null)
        {
            if (polity == null || Sc(g) == null) return lane.Days;
            bool faster = false;
            if (Has(g, polity, "pyg_forward_logistics")) { net = net ?? Supply.Of(g, polity); faster = net.Has(lane.A) && net.Has(lane.B); }
            if (Has(g, polity, "inh_free_port_charter") && (lane.A == Economy.FreePort || lane.B == Economy.FreePort)) faster = true;
            if (!faster) return lane.Days;
            return Math.Max(1, lane.Days - Math.Max(lane.Days >= 4 ? 1 : 0, lane.Days / 4));
        }

        /// <summary>Tactical tokens a polity's ships carry into battle (disclosed in battle preparation).</summary>
        public static IEnumerable<string> Tokens(GalaxyState g, string polity) =>
            All.Where(p => p.Token != null && p.Polity == polity && Has(g, polity, p.Id)).Select(p => p.Token);

        /// <summary>One line per adopted doctrine, for battle preparation and the inspector (both sides' are public).</summary>
        public static List<string> Disclosure(GalaxyState g, string polity) =>
            All.Where(p => p.Polity == polity && Has(g, polity, p.Id)).Select(p => $"{p.Name}: {p.Effect}").ToList();
    }
}
