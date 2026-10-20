using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>A hand-authored starting situation for the Passage War (campaign alpha Prompt 19).</summary>
    public sealed class VariantDef
    {
        public string Id, Name, Summary, Changes;
    }

    /// <summary>
    /// Bounded replay variation: the same campaign (factions, economy, war rules, saves, win conditions) from a few authored
    /// starting situations. They change route choice, initial knowledge, objective placement or affordable forces; nothing
    /// changes combat values and nothing is random. The chosen variant and the setup seed are stored in the save.
    /// </summary>
    public static class Variants
    {
        public const string Standard = "standard", OpenCorridor = "open_corridor", NarrowNetwork = "narrow_network", SalvageField = "salvage_field";

        public static readonly VariantDef[] All =
        {
            new VariantDef { Id = Standard, Name = "The Passage War", Summary = "Both powers at home; Umbra contested; two routes between the bases.",
                Changes = "The baseline configuration." },
            new VariantDef { Id = OpenCorridor, Name = "The open corridor", Summary = "The fleets start a jump forward and the passage pays more; the Eclipse derelict is already charted.",
                Changes = "Pygmaei fleet at Cinderhold and Inhumanus fleet at Shoal Haven (shorter, riskier approaches); passage toll 5 a day instead of 3; Gloam's drift and rival force known from the start." },
            new VariantDef { Id = NarrowNetwork, Name = "The narrow network", Summary = "The Pirate Reach is closed: every route between the powers runs through Umbra.",
                Changes = "Salvage Row and Blackwake leave the active scope (8 systems): no northern route, no bypass of the passage; both treasuries start with a third fewer credits, so fewer hulls are affordable." },
            new VariantDef { Id = SalvageField, Name = "The salvage field", Summary = "An earlier battle left Umbra full of wrecks; both flagships come home damaged and materials are scarce.",
                Changes = "Umbra is fought among the wreckage; two salvageable wrecks lie at Umbra and one at Gloam; both flagships start at 60% hull; materials start at 60; the fleets are the Breaker Crews and the Salvage Picket loadouts." },
        };

        public static VariantDef Get(string id) => All.FirstOrDefault(v => v.Id == id);

        /// <summary>Applies a variant to a freshly created scenario (called once by PassageWar.Create).</summary>
        public static void Apply(GalaxyState g, string id)
        {
            var sc = PassageWar.Of(g);
            sc.Variant = id ?? Standard;
            sc.SetupSeed = g.Seed;
            switch (sc.Variant)
            {
                case OpenCorridor:
                    Station(g, "pygmaei", "cinderhold");
                    Station(g, "inhumanus", "shoal_haven");
                    g.Slice.Passage(SliceCampaign.PassageSystem).TollPerDay = 5;
                    var ex = Expeditions.Of(g);
                    if (ex != null) { ex.Scouted = true; ex.State = ExpeditionState.Scouted; }
                    break;
                case NarrowNetwork:
                    sc.ActiveSystems.RemoveAll(x => x == "salvage_row" || x == "blackwake");
                    foreach (var p in new[] { "pygmaei", "inhumanus" })
                    {
                        var pol = g.GetPolity(p);
                        pol.Stock = new Resources(pol.Stock.Energy, pol.Stock.Materials, pol.Stock.Credits * 2 / 3, pol.Stock.Influence);
                    }
                    break;
                case SalvageField:
                    g.GetSystem(SliceCampaign.PassageSystem).BattlefieldMapId = SliceCampaign.WreckedPassageMap;
                    Refleet(g, "pygmaei", "breaker_crews");
                    Refleet(g, "inhumanus", "salvage_picket");
                    foreach (var f in g.Fleets.Where(f => f.OwnerId == "pygmaei" || f.OwnerId == "inhumanus"))
                        foreach (var s in f.Ships.Where(s => ShipCatalog.IsLeader(s.DefId))) s.Hp = Math.Max(1, ShipCatalog.MaxHp(s.DefId) * 6 / 10);
                    foreach (var p in new[] { "pygmaei", "inhumanus" })
                    {
                        var pol = g.GetPolity(p);
                        pol.Stock = new Resources(pol.Stock.Energy, 60, pol.Stock.Credits, pol.Stock.Influence);
                    }
                    Wreck(g, "umbra", "pygmaei_furnace_cruiser", "the cruiser Slag Anthem", "pygmaei", null, 50);
                    Wreck(g, "umbra", "inhumanus_parallax_cruiser", "the cruiser Margin Call", "inhumanus", "ablative_plating", 40);
                    Wreck(g, "gloam", "inhumanus_arbitration_destroyer", "the destroyer Escrow", "inhumanus", null, 30);
                    break;
            }
            g.Note("info", $"Starting situation: {Get(sc.Variant).Name}. {Get(sc.Variant).Changes}");
        }

        static void Station(GalaxyState g, string polity, string systemId)
        {
            var f = g.Fleets.First(x => x.Id == SliceCampaign.FleetIdOf(polity));
            f.SystemId = systemId; f.LastSystemId = SliceCampaign.HomeOf(polity);
        }

        /// <summary>Replaces a power's starting fleet with a content loadout (the same flagship identity is created anew).</summary>
        static void Refleet(GalaxyState g, string polity, string loadoutId)
        {
            var l = SliceContent.Loadouts.First(x => x.Id == loadoutId);
            var f = g.Fleets.First(x => x.Id == SliceCampaign.FleetIdOf(polity));
            var oldFlag = f.Ships.FirstOrDefault(s => ShipCatalog.IsLeader(s.DefId));
            var profile = oldFlag != null ? g.Slice.Profile(oldFlag.Uid) : null;
            f.Ships.Clear();
            foreach (var id in l.Units)
            {
                var ship = new ShipRecord { Uid = ShipCatalog.IsLeader(id) && oldFlag != null ? oldFlag.Uid : g.NextUid++, DefId = id, Hp = ShipCatalog.MaxHp(id) };
                f.Ships.Add(ship);
            }
            f.Name = l.Name;
            if (profile != null) profile.Service[0].Text = $"Commissioned as flagship of the {l.Name} at {g.GetSystem(SliceCampaign.HomeOf(polity)).Name}.";
        }

        static void Wreck(GalaxyState g, string systemId, string defId, string name, string owner, string module, int materials)
        {
            g.Slice.Wrecks.Add(new WreckRecord { Id = $"wreck_variant_{systemId}_{defId}", SystemId = systemId, DefId = defId, Name = name, OwnerId = owner, BattleId = null,
                Uid = 0, Day = 0, Module = module, Materials = new Resources(0, materials, 0, 0) });
        }

        // ------------------------------------------------------------------ validation

        /// <summary>The checks every variant must pass before it is offered: route reachability, viable deployment, supply
        /// access, command succession, economic recovery, objective attainability and tactical topology. Empty when all pass.</summary>
        public static List<string> Validate(GalaxyState g)
        {
            var problems = new List<string>();
            var sc = PassageWar.Of(g);
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                var name = g.GetPolity(pol).Name;
                var f = g.Fleets.FirstOrDefault(x => x.OwnerId == pol && StrategyRules.CanFight(x));
                if (f == null) { problems.Add($"{name}: no fleet led by a flagship"); continue; }
                // Route reachability: to the passage and back home, inside the scope.
                if (!StrategyRules.PlanRoute(g, f, SliceCampaign.PassageSystem).Legal && f.SystemId != SliceCampaign.PassageSystem) problems.Add($"{name}: cannot reach the passage");
                foreach (var d in sc.Designated[pol]) if (f.SystemId != d && !StrategyRules.PlanRoute(g, f, d).Legal) problems.Add($"{name}: cannot reach {g.GetSystem(d).Name}");
                if (!sc.ActiveSystems.All(id => g.GetSystem(id) != null)) problems.Add("unknown system in scope");
                // Viable deployment: the fleet forms a legal battle line.
                var line = BattleBridge.AutoSelect(g, f);
                if (!BattleBridge.Validate(g, f, line).Valid) problems.Add($"{name}: no legal battle line");
                // Command succession: at least one ship could act as flagship in an operation.
                if (f.Ships.Count(s => !ShipCatalog.IsLeader(s.DefId)) == 0) problems.Add($"{name}: no possible successor");
                // Supply access: the home yard supplies the designated worlds; holding the passage would supply it.
                var net = Supply.Of(g, pol);
                foreach (var d in sc.Designated[pol]) if (!net.Has(d)) problems.Add($"{name}: no supply to {g.GetSystem(d).Name}");
                var copy = SaveGame.FromJson(SaveGame.ToJson(g), out _);
                copy.Slice.Passage(SliceCampaign.PassageSystem).AccessHolderId = pol;
                if (!Supply.Of(copy, pol).Has(SliceCampaign.PassageSystem)) problems.Add($"{name}: holding the passage would not bring supply to it");
                // Economic recovery: a replacement flagship is affordable within 90 days of income at most.
                var inc = StrategyRules.DailyIncome(g, pol);
                var need = PassageWar.FlagshipCost(pol);
                var stock = g.GetPolity(pol).Stock;
                bool ok = true;
                foreach (var (have, per, want) in new[] { (stock.Energy, inc.Energy, need.Energy), (stock.Materials, inc.Materials, need.Materials), (stock.Credits, inc.Credits, need.Credits) })
                    if (have < want && (per <= 0 || (want - have + per - 1) / per > 90)) ok = false;
                if (!ok) problems.Add($"{name}: a replacement flagship would take more than 90 days of income");
            }
            // Objective attainability: the passage map is a validated battlefield; a treaty is possible in principle.
            var map = MapLibrary.Get(g.GetSystem(SliceCampaign.PassageSystem).BattlefieldMapId);
            if (map == null || map.Id != g.GetSystem(SliceCampaign.PassageSystem).BattlefieldMapId) problems.Add("the passage battlefield is not registered");
            else
            {
                var topo = TopologyValidator.Validate(map);
                if (!topo.Passed) problems.Add("the passage battlefield fails topology validation: " + string.Join("; ", topo.AllErrors()));
            }
            if (PassageWar.MaxPayment / 3 + PassageWar.LeaderlessPressure + PassageWar.BlockadePressure < PassageWar.HeldValue + PassageWar.UntestedValue)
                problems.Add("a treaty could never be accepted");
            return problems;
        }
    }
}
