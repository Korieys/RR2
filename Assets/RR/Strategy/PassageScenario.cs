using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    public enum PassageVariant : byte { Challenge = 0, Training = 1 }

    /// <summary>
    /// The curated flagship encounter at the trade passage (Prompt 5). A smaller Pygmaei raiding force meets an Inhumanus
    /// convoy escort whose Executive Flagship is already damaged from an earlier engagement. Both sides use the ordinary
    /// army rules (30 points, 8 ships, one flagship) and the ordinary battle rules; the setup (forces, carried damage,
    /// deployment) is data, saved with the battle, so a reload never rerolls it. Either faction can be the player's.
    /// </summary>
    public static class PassageScenario
    {
        public const string MapId = "trade_passage";
        /// <summary>The galaxy system that hosts the passage (an unclaimed Eclipse border system between the two powers).</summary>
        public const string SystemId = "umbra";

        /// <summary>Pygmaei raiders, seat 0 (south). 18 of 30 points: outnumbered on purpose.</summary>
        static readonly (string id, int q, int r)[] Raiders =
        {
            ("pygmaei_revenant_command_ship", -1, 4), ("pygmaei_annihilator_destroyer", 1, 3), ("pygmaei_skirmish_frigate", -5, 2),
            ("pygmaei_skirmish_frigate", 4, 2), ("pygmaei_rift_interceptor", 0, 3),
        };

        /// <summary>Inhumanus convoy escort, seat 1 (north). 28 of 30 points.</summary>
        static readonly (string id, int q, int r)[] Escort =
        {
            ("inhumanus_executive_flagship", 1, -4), ("inhumanus_parallax_cruiser", 0, -3), ("inhumanus_arbitration_destroyer", 3, -3),
            ("inhumanus_proxy_frigate", -1, -2), ("inhumanus_proxy_frigate", 3, -2), ("inhumanus_mercator_corvette", -4, -2),
            ("inhumanus_mercator_corvette", 6, -3),
        };

        /// <summary>The convoy flagship's carried damage: it limps in at this hull (of 20).</summary>
        public const int FlagshipCarriedHull = 17;
        /// <summary>The training target: two good hits, so one salvo cannot skip the flank lesson.</summary>
        public const int TrainingFlagshipHull = 9;

        public static string Title(PassageVariant v) => v == PassageVariant.Training ? "Trade Passage: training" : "Trade Passage: the convoy";

        public static string Briefing(PassageVariant v, FactionId player) => v == PassageVariant.Training
            ? "Training. Three lessons on the real rules: destroying a flagship ends the battle, protection is a declared rule you can read, and a flank opens a line a wreck blocks. The enemy does not act in this exercise."
            : player == FactionId.Pygmaei
                ? $"Your five raiders face a convoy escort of seven. Their Executive Flagship carries earlier damage ({FlagshipCarriedHull} of 20 hull). Destroying it ends the battle; so does losing your own Revenant. Find a legal line the escort leaves open."
                : $"Your convoy escort of seven faces five Pygmaei raiders. The Executive Flagship is damaged ({FlagshipCarriedHull} of 20 hull): keep it screened, out of range and out of their overclocked reach. Losing it ends the battle at once.";

        /// <summary>A battle setup for the encounter. <paramref name="playerFaction"/> chooses which side the player commands;
        /// the Pygmaei always hold the southern edge and move first (they are the attackers).</summary>
        public static BattleSetup Create(PassageVariant v, FactionId playerFaction, int rematch = 0, int? flagshipHull = null, Hex? flagshipAt = null)
        {
            var cfg = new MatchConfig { ClockSeconds = 0, Label = Title(v) }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = MapId;
            cfg.TechMode = TechMode.Balanced;
            cfg.FirstSeat = 0; cfg.DefenderSeat = 1;
            var setup = new BattleSetup
            {
                BattleId = $"passage_{(v == PassageVariant.Training ? "training" : "challenge")}_{(playerFaction == FactionId.Pygmaei ? "pyg" : "inh")}_{rematch}",
                EngagementId = null, Config = cfg, PlayerSeat = playerFaction == FactionId.Pygmaei ? 0 : 1,
                Scenario = v == PassageVariant.Training ? "passage_training" : "passage_challenge", SystemId = SystemId,
            };
            var forces = v == PassageVariant.Training ? TrainingForces() : (Raiders, Escort);
            var sides = new[] { forces.Item1, forces.Item2 };
            var factions = new[] { FactionId.Pygmaei, FactionId.Inhumanus };
            var names = new[] { "Pygmaei raiders", "Inhumanus convoy escort" };
            for (int seat = 0; seat < 2; seat++)
            {
                var army = new ArmyRoster { Name = names[seat], Faction = factions[seat] };
                foreach (var e in sides[seat]) army.Units.Add(new RosterEntry(e.id));
                cfg.Players[seat] = new PlayerSetup
                {
                    DisplayName = names[seat], Army = army, Caps = BattleBridge.Caps.Clone(), IsAI = seat != setup.PlayerSeat, AIDifficulty = AIDifficulty.Tactical,
                };
                setup.Deployment[seat] = sides[seat].Select(e => new Hex(e.q, e.r)).ToList();
                setup.StartHp[seat] = sides[seat].Select(e => RosterContent.Unit(e.id).MaxHp).ToList();
                setup.ShipUids[seat] = sides[seat].Select(_ => 0).ToList();
            }
            setup.StartHp[1][0] = flagshipHull ?? (v == PassageVariant.Training ? TrainingFlagshipHull : FlagshipCarriedHull);
            if (flagshipAt.HasValue) setup.Deployment[1][0] = flagshipAt.Value;
            return setup;
        }

        static ((string id, int q, int r)[], (string id, int q, int r)[]) TrainingForces() => (
            new[] { ("pygmaei_revenant_command_ship", -6, 4), ("pygmaei_rift_interceptor", -5, 3), ("pygmaei_skirmish_frigate", 2, 2), ("pygmaei_skirmish_frigate", -4, 2) },
            new[] { ("inhumanus_executive_flagship", 2, -2), ("inhumanus_proxy_frigate", 0, -3), ("inhumanus_mercator_corvette", -3, -2) });

        /// <summary>Training lessons, each satisfied by an ordinary legal command.</summary>
        public static readonly string[] TrainingSteps =
        {
            "Lesson 1 of 3: find the leader. The crowned ship with the gold ring is their Executive Flagship. Hover it: the inspector says that destroying it ends the battle, however many escorts remain.",
            "Lesson 2 of 3: protection is a rule, not a crowd. Select your Rift Interceptor and use Snap Intercept (3) on your Revenant. The inspector now lists 'Protected by': one intercept, against small craft, within 2 hexes. Ships merely standing nearby protect nothing.",
            "Lesson 3 of 3: take a flank. Their flagship sits behind the wreck at (2,0): your eastern frigate's attack preview says the line is blocked. Move around the wreck (press O to overclock for reach), check that the preview reaches their hull, and strike; the Revenant can finish it.",
        };
    }
}
