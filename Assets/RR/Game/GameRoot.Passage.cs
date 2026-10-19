using System;
using System.Collections;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // The vertical slice's entry points (Docs/vertical-slice): the scenario menu, the trade-passage encounter as a
    // standalone battle (challenge, training, either faction, two players at one machine), and its result screen with
    // rematch, restart and the decisive sequence. Campaign battles keep their own flow in GameRoot.Flow.
    public sealed partial class GameRoot
    {
        SliceBattlePanel _slicePanel;
        int _passageRun;
        bool _startMenuShown;

        /// <summary>The galaxy system a battle is fought in: the skirmish's own, or the engagement's.</summary>
        string BattleSystemId => _setup == null ? null : _setup.SystemId ?? G.GetEngagement(_setup.EngagementId)?.SystemId;

        public void MaybeShowStartMenu()
        {
            if (_startMenuShown || Args.Has("-rrTour") || Args.Has("-rrSoak") || Args.Has("-rrShipSheet") || Args.Has("-rrCapture") || Args.Has("-rrPassageTour")) return;
            _startMenuShown = true;
            ShowPassageMenu();
        }

        static Label Para(string text, int size = 14, Color? col = null)
        {
            var l = new Label(text);
            l.style.whiteSpace = WhiteSpace.Normal; l.style.fontSize = size; l.style.marginBottom = 6;
            l.style.color = col ?? new Color(0.92f, 0.9f, 0.84f);
            return l;
        }

        public void ShowPassageMenu()
        {
            if (Scale == ViewScale.Battle || Busy) { UI.Strategy.Toast("Finish the current battle or action first.", "bad"); return; }
            var m = UI.OpenModal(980);
            var title = UIX.Text("THE TRADE PASSAGE", "h-display"); UIRoot.Display(title); m.Add(title);
            m.Add(Para("A Pygmaei raiding force and an Inhumanus convoy escort contest the passage at Umbra, on the border of The Eclipse Sector. " +
                       "Destroying the enemy flagship ends a battle at once; numbers protect nobody. Generals don't roll dice: every result is shown before you commit it."));
            var row = UIX.Div("row"); row.style.flexWrap = Wrap.Wrap; m.Add(row);
            void Option(string label, Action go) { var b = UIX.Btn(label, go); b.style.marginRight = 8; b.style.marginBottom = 8; row.Add(b); }
            Option("Battle as the Pygmaei raiders", () => StartPassage(PassageVariant.Challenge, FactionId.Pygmaei, false));
            Option("Battle as the Inhumanus escort", () => StartPassage(PassageVariant.Challenge, FactionId.Inhumanus, false));
            Option("Two players at this machine", () => StartPassage(PassageVariant.Challenge, FactionId.Pygmaei, true));
            Option("Training: three lessons", () => StartPassage(PassageVariant.Training, FactionId.Pygmaei, false));
            Option("Continue the Aurelith campaign", () => UI.CloseModal());
            m.Add(Para(PassageScenario.Briefing(PassageVariant.Challenge, FactionId.Pygmaei), 13, new Color(0.75f, 0.77f, 0.8f)));
            var fleets = UIX.Div("row"); fleets.style.alignItems = Align.FlexStart; m.Add(fleets);
            foreach (var f in new[] { FactionId.Pygmaei, FactionId.Inhumanus })
            {
                var col = UIX.Div(); col.style.width = Length.Percent(50); col.style.paddingRight = 12; fleets.Add(col);
                col.Add(UIX.Text(f == FactionId.Pygmaei ? "PYGMAEI FLEET" : "INHUMANUS FLEET", "h-section"));
                col.Add(Para(SliceRoster.DoctrineCard(f), 12, new Color(0.91f, 0.78f, 0.47f)));
                foreach (var id in SliceRoster.Classes(f))
                {
                    var p = SliceRoster.Profile(id);
                    col.Add(Para($"{p.Name} ({p.Cost} pts){(p.Leader ? ", flagship" : "")}. {p.Role} {p.Movement}; {p.Attack}; {p.Defences}. Signature: {p.Signature}. Counter: {p.Counter}", 12));
                }
            }
            m.Add(Para("Ship statistics are codex 0.1 draft values and the doctrines are experimental. Ship models for both factions are first-pass generated art pending approval. F6 reopens this menu.", 11, new Color(0.6f, 0.62f, 0.66f)));
        }

        public void StartPassage(PassageVariant v, FactionId player, bool hotseat)
        {
            if (Busy || Scale == ViewScale.Battle) return;
            var setup = PassageScenario.Create(v, player, ++_passageRun);
            setup.Hotseat = hotseat;
            if (hotseat) setup.Config.Players[1].IsAI = false;
            StartCoroutine(StartPassageRoutine(setup));
        }

        IEnumerator StartPassageRoutine(BattleSetup setup)
        {
            string err;
            var session = BattleSession.Begin(setup, out err);
            if (session == null) { UI.Strategy.Toast(err, "bad"); yield break; }
            UI.CloseModal();
            Clock.Paused = true;
            if (Scale == ViewScale.Planet) ExitPlanet();
            if (LocalSystem == null || LocalSystem.Data.Id != setup.SystemId || Scale == ViewScale.Galaxy)
            {
                if (Scale == ViewScale.System) yield return ExitToGalaxyRoutine();
                yield return EnterSystemRoutine(setup.SystemId);
            }
            Audio.Play("battle_commit");
            yield return EnterBattleRoutine(session);
            Battle.Announce(PassageScenario.Briefing(setup.Scenario == "passage_training" ? PassageVariant.Training : PassageVariant.Challenge,
                setup.Config.Players[setup.PlayerSeat].Army.Faction), "ok");
        }

        void FinishSkirmish()
        {
            _resultShown = true;
            var s = Battle.S;
            var setup = _setup;
            int me = setup.PlayerSeat;
            bool won = s.Winner == me;
            var m = UI.OpenModal(900);
            string head = setup.Hotseat ? s.Config.Players[s.Winner].DisplayName.ToUpperInvariant() + " WIN" : won ? "VICTORY" : "DEFEAT";
            var t = UIX.Text(head, "h-display"); UIRoot.Display(t); m.Add(t);
            m.Add(Para("Decisive action: " + (Battle.Session.Decisive ?? BattleNarrator.Ending(s)), 15));
            m.Add(Para($"Round {s.Round}, {Battle.History.Count} orders. Losses: {s.Config.Players[0].DisplayName} {s.Units.Count(u => u.Owner == 0 && !u.Alive && !u.R.IsToken)}, " +
                       $"{s.Config.Players[1].DisplayName} {s.Units.Count(u => u.Owner == 1 && !u.Alive && !u.R.IsToken)}.", 13, new Color(0.75f, 0.77f, 0.8f)));
            var seq = UIX.Div(); seq.style.display = DisplayStyle.None; m.Add(seq);
            var row = UIX.Div("row"); row.style.flexWrap = Wrap.Wrap; m.Add(row);
            void Btn(string label, Action go) { var b = UIX.Btn(label, go); b.style.marginRight = 8; b.style.marginTop = 8; row.Add(b); }
            var faction = setup.Config.Players[me].Army.Faction;
            var variant = setup.Scenario == "passage_training" ? PassageVariant.Training : PassageVariant.Challenge;
            Btn("Inspect the decisive sequence", () =>
            {
                seq.Clear();
                var n = Battle.Session.Narration;
                for (int i = Math.Max(0, n.Count - 12); i < n.Count; i++) seq.Add(Para($"{i + 1}. {n[i]}", 12));
                seq.style.display = seq.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            });
            Btn("Rematch", () => Replay(variant, faction, setup.Hotseat));
            Btn("Swap sides", () => Replay(variant, faction == FactionId.Pygmaei ? FactionId.Inhumanus : FactionId.Pygmaei, setup.Hotseat));
            Btn("Scenario menu", () => { UI.CloseModal(); _resultDone = () => { _resultDone = null; StartCoroutine(EndBattleThen(ShowPassageMenu)); }; _resultDone(); });
            Btn("Return to the galaxy", () => { UI.CloseModal(); _resultDone = () => { _resultDone = null; StartCoroutine(EndBattleRoutine()); }; _resultDone(); });
            _resultDone = () => { _resultDone = null; StartCoroutine(EndBattleRoutine()); };
            Automation.Note("skirmish over: winner seat " + s.Winner + " (" + s.VictoryReason + ") after round " + s.Round + ", " + Battle.History.Count + " orders; decisive: " + Battle.Session.Decisive);
        }

        void Replay(PassageVariant v, FactionId f, bool hotseat)
        {
            UI.CloseModal();
            _resultDone = null;
            StartCoroutine(EndBattleThen(() => StartPassage(v, f, hotseat)));
        }

        IEnumerator EndBattleThen(Action next)
        {
            yield return EndBattleRoutine();
            while (Busy) yield return null;
            next();
        }

        /// <summary>Restart the current skirmish from its start (same scenario and sides), discarding this attempt.</summary>
        public void RestartSkirmish()
        {
            if (_setup == null || _setup.Scenario == null || Battle == null || Battle.Animating) return;
            var variant = _setup.Scenario == "passage_training" ? PassageVariant.Training : PassageVariant.Challenge;
            var f = _setup.Config.Players[_setup.PlayerSeat].Army.Faction;
            bool hotseat = _setup.Hotseat;
            _resultShown = true;
            StartCoroutine(EndBattleThen(() => StartPassage(variant, f, hotseat)));
        }
    }
}
