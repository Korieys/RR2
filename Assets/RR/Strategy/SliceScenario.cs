using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    /// <summary>
    /// Builds the handcrafted slice scenario "The Eclipse Frontier" reproducibly. Positions come from the approved
    /// "Rule and Ruin - Faction Map" (1448 x 1086 px; 1,000 light years = 175 px on its scale bar); every name not on
    /// that map is provisional (flagged in data). This is fixture content for the vertical slice, not a campaign.
    /// </summary>
    public static class SliceScenario
    {
        public const string PlayerPolity = "silex";
        public const string EnemyPolity = "craigon";
        public const string HeroSystem = "aurelith";
        public const string HeroPlanet = "aurelith_prime";
        public const string PlayerFleet = "silex_first_crown";
        public const string RaidFleet = "craigon_brood";
        const double LyPerPx = 1000.0 / 175.0;

        static double X(double px) => (px - 724) * LyPerPx;
        static double Y(double py) => (543 - py) * LyPerPx;

        public static GalaxyState Create(int seed = 3104)
        {
            var g = new GalaxyState { Seed = seed, Rng = (uint)(seed * 2654435761u) | 1u };

            // ------------------------------------------------------------------ polities (map palette)
            g.Polities.Add(new Polity { Id = "silex", Name = "Silex Expanse", Color = 0x4AA8FF, PlayerControlled = true, Playable = true,
                CommandfallFaction = "Silex", Stock = new Resources(120, 180, 140, 30) });
            g.Polities.Add(new Polity { Id = "craigon", Name = "Cr\u00e4igon", Color = 0xC452FF, Playable = true, CommandfallFaction = "Craigon",
                Stock = new Resources(400, 400, 400, 40) });
            g.Polities.Add(new Polity { Id = "meeples", Name = "Meeples", Color = 0x3CC4FF });
            g.Polities.Add(new Polity { Id = "pygmaei", Name = "Pygmaei Sector", Color = 0xA45CFF });
            g.Polities.Add(new Polity { Id = "pirates", Name = "Pirate Reach", Color = 0xE8B23A });
            g.Polities.Add(new Polity { Id = "inhumanus", Name = "Inhumanus Dominion", Color = 0xE8443C });
            g.Polities.Add(new Polity { Id = "elven", Name = "Elven Front", Color = 0xFF7A48 });

            // ------------------------------------------------------------------ sectors
            AddSector("silex_expanse", "Silex Expanse", "Knowledge. Structure. Asylum.", "silex", 260, 275, 190);
            AddSector("pygmaei_sector", "Pygmaei Sector", "A People Reborn.", "pygmaei", 535, 275, 110);
            AddSector("pirate_reach", "Pirate Reach", "Lawless. Profitable. Familiar.", "pirates", 800, 245, 125);
            AddSector("inhumanus_dominion", "Inhumanus Dominion", "Consumption Without End.", "inhumanus", 1185, 300, 200);
            AddSector("elven_front", "Elven Front", "Beauty. Power. Proximity.", "elven", 1060, 480, 120);
            AddSector("meeples", "Meeples", "Core Worlds. Allied Races. A Larger Tomorrow.", "meeples", 760, 580, 230);
            AddSector("craigon", "Cr\u00e4igon", "Everywhere. Always.", "craigon", 290, 680, 210);
            // The Eclipse Sector replaces The Shattered Veil (latest creator decision). Placed in the dark south-central
            // gap below the Meeple core: a provisional reading of "the central region" (see Docs/DECISIONS.md).
            var ecl = AddSector("eclipse_sector", "The Eclipse Sector", "Uncharted. Unclaimed. Watched.", null, 700, 880, 120);
            ecl.Eclipse = true;

            // ------------------------------------------------------------------ systems
            // Silex Expanse
            AddSys("lithar", "Lithar", "silex_expanse", 235, 255, StarClass.BlueGiant, "silex", SystemDetail.Local, prov: true);
            AddSys("quartzhold", "Quartzhold", "silex_expanse", 330, 205, StarClass.White, "silex");
            AddSys("obsidia", "Obsidia", "silex_expanse", 150, 200, StarClass.Yellow, "silex");
            AddSys("geodane", "Geodane", "silex_expanse", 395, 300, StarClass.Orange, "silex");
            AddSys("prismvale", "Prismvale", "silex_expanse", 165, 325, StarClass.White, "silex");
            AddSys("facet_reach", "Facet Reach", "silex_expanse", 300, 335, StarClass.Yellow, "silex");
            AddSys("shalecrest", "Shalecrest", "silex_expanse", 425, 385, StarClass.Orange, "silex");
            AddSys(HeroSystem, "Aurelith", "silex_expanse", 285, 410, StarClass.White, "silex", SystemDetail.Hero, prov: true, station: true);
            // Unclaimed frontier between the Expanse and the Craigon incursion
            AddSys("cinder_verge", "Cinder Verge", null, 270, 488, StarClass.Orange, null, map: "orbital_wreckfield");
            // Craigon
            AddSys("origin_brood", "Origin Brood", "craigon", 100, 560, StarClass.Violet, "craigon");
            AddSys("carapace_deep", "Carapace Deep", "craigon", 215, 640, StarClass.RedDwarf, "craigon");
            AddSys("spore_drift", "Spore Drift", "craigon", 300, 565, StarClass.RedDwarf, "craigon", SystemDetail.Local, map: "orbital_wreckfield");
            AddSys("chitin_reach", "Chitin Reach", "craigon", 175, 760, StarClass.RedDwarf, "craigon");
            AddSys("brood_hollow", "Brood Hollow", "craigon", 330, 770, StarClass.Orange, "craigon");
            AddSys("synapse_gate", "Synapse Gate", "craigon", 440, 690, StarClass.Violet, "craigon");
            AddSys("the_moult", "The Moult", "craigon", 490, 830, StarClass.Orange, "craigon");
            // Meeples (names on the approved map are not provisional)
            AddSys("meeple_core", "Meeple Core", "meeples", 757, 625, StarClass.Yellow, "meeples", prov: false);
            AddSys("unity", "Unity", "meeples", 690, 578, StarClass.White, "meeples", prov: false);
            AddSys("hope", "Hope", "meeples", 815, 580, StarClass.Yellow, "meeples", prov: false);
            AddSys("commonweal", "Commonweal", "meeples", 848, 638, StarClass.White, "meeples", prov: false);
            AddSys("hearth", "Hearth", "meeples", 772, 708, StarClass.Orange, "meeples", prov: false);
            AddSys("concord", "Concord", "meeples", 615, 470, StarClass.Yellow, "meeples");
            // Pygmaei, Pirates, Inhumanus, Elven Front
            AddSys("forgefall", "Forgefall", "pygmaei_sector", 525, 240, StarClass.Orange, "pygmaei");
            AddSys("ironmouth", "Ironmouth", "pygmaei_sector", 592, 312, StarClass.RedDwarf, "pygmaei");
            AddSys("cinderhold", "Cinderhold", "pygmaei_sector", 470, 328, StarClass.Orange, "pygmaei");
            AddSys("blackwake", "Blackwake", "pirate_reach", 792, 232, StarClass.Yellow, "pirates");
            AddSys("shoal_haven", "Shoal Haven", "pirate_reach", 872, 292, StarClass.Orange, "pirates");
            AddSys("salvage_row", "Salvage Row", "pirate_reach", 700, 200, StarClass.RedDwarf, "pirates");
            AddSys("ledgerhold", "Ledgerhold", "inhumanus_dominion", 1190, 280, StarClass.White, "inhumanus");
            AddSys("pale_exchange", "Pale Exchange", "inhumanus_dominion", 1292, 385, StarClass.Yellow, "inhumanus");
            AddSys("dividend", "Dividend", "inhumanus_dominion", 1080, 200, StarClass.Orange, "inhumanus");
            AddSys("aethel_spire", "Aethel Spire", "elven_front", 1060, 452, StarClass.BlueGiant, "elven");
            AddSys("sylvara", "Sylvara", "elven_front", 1132, 522, StarClass.White, "elven");
            AddSys("thornmere", "Thornmere", "elven_front", 990, 512, StarClass.Yellow, "elven");
            // The Eclipse Sector: unclaimed, dim and unusual stars
            AddSys("umbra", "Umbra", "eclipse_sector", 692, 862, StarClass.Dim, null, SystemDetail.Local);
            AddSys("nocturne", "Nocturne", "eclipse_sector", 612, 902, StarClass.Violet, null);
            AddSys("hollow_star", "The Hollow Star", "eclipse_sector", 782, 905, StarClass.Dim, null);

            // ------------------------------------------------------------------ lanes
            string[] lanes =
            {
                "lithar-quartzhold", "lithar-obsidia", "lithar-prismvale", "lithar-facet_reach", "quartzhold-geodane", "geodane-shalecrest",
                "facet_reach-aurelith", "facet_reach-shalecrest", "prismvale-aurelith", "obsidia-prismvale", "facet_reach-geodane",
                "*geodane-cinderhold", "quartzhold-forgefall", "forgefall-cinderhold", "forgefall-ironmouth", "cinderhold-ironmouth",
                "*forgefall-salvage_row", "ironmouth-blackwake", "salvage_row-blackwake", "blackwake-shoal_haven", "*shoal_haven-dividend",
                "dividend-ledgerhold", "ledgerhold-pale_exchange", "*ledgerhold-aethel_spire", "pale_exchange-sylvara",
                "aethel_spire-sylvara", "aethel_spire-thornmere", "*thornmere-commonweal", "thornmere-hope", "concord-unity",
                "unity-meeple_core", "hope-meeple_core", "commonweal-meeple_core", "hearth-meeple_core", "unity-hope", "commonweal-hearth",
                "*concord-cinderhold", "*concord-shalecrest", "shoal_haven-hope", "origin_brood-carapace_deep", "carapace_deep-spore_drift",
                "carapace_deep-chitin_reach", "chitin_reach-brood_hollow", "brood_hollow-synapse_gate", "synapse_gate-spore_drift",
                "brood_hollow-the_moult", "*synapse_gate-unity", "spore_drift-cinder_verge", "cinder_verge-aurelith",
                "the_moult-nocturne", "nocturne-umbra", "umbra-hollow_star", "*umbra-hearth", "hollow_star-commonweal",
            };
            foreach (var l in lanes)
            {
                bool major = l.StartsWith("*");
                var parts = l.TrimStart('*').Split('-');
                var a = g.GetSystem(parts[0]); var b = g.GetSystem(parts[1]);
                if (a == null || b == null) throw new InvalidOperationException("Unknown lane endpoint in " + l);
                double px = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) / LyPerPx;
                g.Lanes.Add(new Lane { A = a.Id, B = b.Id, Major = major, Days = Math.Max(4, Math.Min(14, (int)Math.Round(px / 11.5))) });
            }

            // ------------------------------------------------------------------ planets
            HeroPlanets(g.GetSystem(HeroSystem));
            Planets(g.GetSystem("lithar"), ("Lithar Prime", PlanetKind.Crystal, 1.6, 1.05, "silex", 9_800_000_000L, 5, new Resources(8, 10, 6)),
                ("Lithar II", PlanetKind.Barren, 2.5, 0.6, null, 0, 0, default), ("Lithar III", PlanetKind.GasGiant, 3.8, 2.6, null, 0, 0, default),
                ("Lithar IV", PlanetKind.Ice, 5.0, 0.75, null, 0, 0, default));
            Planets(g.GetSystem("spore_drift"), ("Spore Drift I", PlanetKind.Toxic, 1.3, 0.9, "craigon", 1_200_000_000L, 2, new Resources(3, 3, 1)),
                ("Spore Drift II", PlanetKind.Barren, 2.6, 0.55, null, 0, 0, default), ("Spore Drift III", PlanetKind.GasGiant, 3.9, 1.9, null, 0, 0, default));
            Planets(g.GetSystem("umbra"), ("Umbra I", PlanetKind.Barren, 1.2, 0.6, null, 0, 0, default), ("Umbra II", PlanetKind.Ice, 2.8, 0.8, null, 0, 0, default),
                ("Umbra III", PlanetKind.GasGiant, 4.2, 1.6, null, 0, 0, default));
            Planets(g.GetSystem("cinder_verge"), ("Cinder Verge I", PlanetKind.Volcanic, 1.4, 0.7, null, 0, 0, default),
                ("Cinder Verge II", PlanetKind.Barren, 2.9, 0.65, null, 0, 0, default));
            foreach (var s in g.Systems.Where(s => s.Planets.Count == 0)) GeneratedPlanets(g, s);

            // ------------------------------------------------------------------ fleets
            var crown = NewFleet(g, PlayerFleet, "First Crown Fleet", "silex", HeroSystem, "lithar",
                "silex_throne_ark", "silex_obelisk_cruiser", "silex_pillar_destroyer", "silex_tessera_frigate", "silex_lattice_corvette",
                "silex_prism_escort", "silex_facet_surveyor");
            NewFleet(g, "silex_shalecrest_picket", "Shalecrest Picket", "silex", "shalecrest", "lithar", "silex_prism_escort", "silex_facet_surveyor");
            var brood = NewFleet(g, RaidFleet, "Brood of the Gift", "craigon", "spore_drift", "spore_drift",
                "craigon_prime_synapse_flagship", "craigon_synaptic_cruiser", "craigon_communion_destroyer", "craigon_pincer_frigate",
                "craigon_scuttler_corvette", "craigon_scuttler_interceptor", "craigon_sporewing_fighter", "craigon_sporewing_fighter");
            brood.AIControlled = true; brood.AITarget = HeroSystem; brood.AIDepartDay = 3;
            var guard = NewFleet(g, "craigon_origin_guard", "Origin Guard", "craigon", "origin_brood", "origin_brood",
                "craigon_ascendant_ark", "craigon_evolutionary_bastion", "craigon_chrysalis_carrier");
            guard.AIControlled = true;

            g.Note("info", "The Cr\u00e4igon brood at Spore Drift is gathering. Scouts expect it to move on Aurelith within days.");
            g.Note("info", "Select the First Crown Fleet to review its ships, or open Aurelith Prime to commission reinforcements.");
            return g;

            // ---------------------------------------------------------------- local helpers
            Sector AddSector(string id, string name, string sub, string owner, double px, double py, double rpx)
            {
                var sec = new Sector { Id = id, Name = name, Subtitle = sub, OwnerId = owner, X = X(px), Y = Y(py), Radius = rpx * LyPerPx,
                    ProvisionalName = false };
                g.Sectors.Add(sec);
                return sec;
            }
            void AddSys(string id, string name, string sector, double px, double py, StarClass star, string owner,
                     SystemDetail detail = SystemDetail.Distant, bool prov = true, bool station = false, string map = "orbital_gate")
            {
                int sd = Hash(id);
                g.Systems.Add(new StarSystem
                {
                    Id = id, Name = name, SectorId = sector, X = X(px), Y = Y(py), Z = ((sd & 0xFF) / 255.0 - 0.5) * 60.0, Star = star,
                    Detail = detail, OwnerId = owner, OrbitalControlId = owner, HasStation = station, BattlefieldMapId = map,
                    ProvisionalName = prov, Seed = sd,
                });
            }
        }

        static void HeroPlanets(StarSystem s)
        {
            s.Planets.Add(new Planet { Id = "aurelith_i", Name = "Aurelith I", Kind = PlanetKind.Volcanic, OrbitRadius = 1.0, Size = 0.5, OrbitPhase = 2.2,
                ProvisionalName = true, Seed = Hash("aurelith_i") });
            s.Planets.Add(new Planet { Id = HeroPlanet, Name = "Aurelith Prime", Kind = PlanetKind.Crystal, OrbitRadius = 1.8, Size = 1.0, OrbitPhase = 0.6,
                OwnerId = "silex", Population = 2_400_000_000L, Development = 3, DailyYield = new Resources(4, 6, 3), HasShipyard = true, Moons = 1,
                ProvisionalName = true, Seed = Hash(HeroPlanet) });
            s.Planets.Add(new Planet { Id = "aurelith_iii", Name = "Aurelith III", Kind = PlanetKind.GasGiant, OrbitRadius = 3.2, Size = 2.4, OrbitPhase = 4.0,
                Rings = true, Moons = 2, ProvisionalName = true, Seed = Hash("aurelith_iii") });
            s.Planets.Add(new Planet { Id = "aurelith_iv", Name = "Aurelith IV", Kind = PlanetKind.Ice, OrbitRadius = 4.6, Size = 0.7, OrbitPhase = 5.3,
                ProvisionalName = true, Seed = Hash("aurelith_iv") });
        }

        static void Planets(StarSystem s, params (string name, PlanetKind kind, double orbit, double size, string owner, long pop, int dev, Resources yield)[] list)
        {
            int i = 0;
            foreach (var p in list)
            {
                var id = s.Id + "_" + (++i);
                s.Planets.Add(new Planet { Id = id, Name = p.name, Kind = p.kind, OrbitRadius = p.orbit, Size = p.size, OwnerId = p.owner, Population = p.pop,
                    Development = p.dev, DailyYield = p.yield, OrbitPhase = (Hash(id) % 628) / 100.0, Rings = p.kind == PlanetKind.GasGiant && Hash(id) % 3 == 0,
                    Moons = p.kind == PlanetKind.GasGiant ? 1 + Hash(id) % 3 : 0, ProvisionalName = true, Seed = Hash(id) });
            }
        }

        static void GeneratedPlanets(GalaxyState g, StarSystem s)
        {
            var rnd = new Random(s.Seed);
            int n = 2 + rnd.Next(3);
            double orbit = 1.1;
            var kinds = new[] { PlanetKind.Barren, PlanetKind.Terran, PlanetKind.Ocean, PlanetKind.Ice, PlanetKind.GasGiant, PlanetKind.Volcanic, PlanetKind.Toxic };
            for (int i = 0; i < n; i++)
            {
                var kind = i == 0 && s.OwnerId != null ? (s.OwnerId == "silex" ? PlanetKind.Crystal : s.OwnerId == "craigon" ? PlanetKind.Toxic : PlanetKind.Terran)
                                                       : kinds[rnd.Next(kinds.Length)];
                var id = s.Id + "_" + (i + 1);
                bool inhabited = i == 0 && s.OwnerId != null;
                s.Planets.Add(new Planet
                {
                    Id = id, Name = s.Name + " " + Roman(i + 1), Kind = kind, OrbitRadius = orbit, Size = kind == PlanetKind.GasGiant ? 1.6 + rnd.NextDouble() : 0.5 + rnd.NextDouble() * 0.5,
                    OrbitPhase = rnd.NextDouble() * Math.PI * 2, OwnerId = inhabited ? s.OwnerId : null, Population = inhabited ? 200_000_000L + rnd.Next(1, 40) * 50_000_000L : 0,
                    Development = inhabited ? 1 + rnd.Next(3) : 0, DailyYield = inhabited ? new Resources(2 + rnd.Next(3), 2 + rnd.Next(4), 1 + rnd.Next(3)) : default,
                    Rings = kind == PlanetKind.GasGiant && rnd.Next(2) == 0, Moons = kind == PlanetKind.GasGiant ? rnd.Next(3) : 0, ProvisionalName = true, Seed = Hash(id),
                });
                orbit += 0.9 + rnd.NextDouble() * 0.9;
            }
        }

        static Fleet NewFleet(GalaxyState g, string id, string name, string owner, string at, string home, params string[] ships)
        {
            var f = new Fleet { Id = id, Name = name, OwnerId = owner, SystemId = at, HomeSystemId = home, LastSystemId = home };
            foreach (var defId in ships) f.Ships.Add(new ShipRecord { Uid = g.NextUid++, DefId = defId, Hp = ShipCatalog.MaxHp(defId) });
            g.Fleets.Add(f);
            return f;
        }

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => n.ToString() };

        /// <summary>Stable FNV-1a hash (string.GetHashCode is randomised per process).</summary>
        public static int Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}