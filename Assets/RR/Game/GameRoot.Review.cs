using Commandfall.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RR.Game
{
    // The decisive-action camera (Prompt 11): when a battle has ended by a flagship's destruction or capture, the camera moves
    // to where it happened for about two seconds before the result screen. It depicts the already resolved action: the
    // rules state is final before it starts, nothing is hidden, and a click, Space or Escape skips it. Reduced motion skips it.
    public sealed partial class GameRoot
    {
        int _reviewState;   // 0 not started, 1 showing, 2 done
        float _reviewTime;
        const float ReviewSeconds = 2.2f;

        void ResetReview() { _reviewState = 0; _reviewTime = 0; }

        /// <summary>The hex of the decisive action, or null when the battle ended any other way.</summary>
        static Hex? DecisiveHex(MatchState s)
        {
            if (!s.IsOver || s.Winner < 0) return null;
            if (s.VictoryReason != VictoryReason.CommanderEliminated && s.VictoryReason != VictoryReason.LeaderCaptured) return null;
            var leader = s.Commander(1 - s.Winner);
            return leader != null ? leader.Position : (Hex?)null;
        }

        /// <summary>True when the battle is ready for its result screen (the review shown or skipped).</summary>
        bool ReviewDone()
        {
            if (_reviewState == 2) return true;
            var b = Battle;
            if (_reviewState == 0)
            {
                PlaytestMetrics.BattleEnd(b.S.VictoryReason.ToString(), b.S.Winner == b.PlayerSeat, b.Session.Decisive);
                var hex = DecisiveHex(b.S);
                if (hex == null || Settings.ReducedMotion) { _reviewState = 2; return true; }
                _reviewState = 1; _reviewTime = 0;
                var to = BattlePose();
                to.Focus = b.transform.TransformPoint(BattlePresentation.World(hex.Value));
                to.Distance = 12.5f; to.Pitch = 38; to.Yaw = Rig.Current.Yaw + 14; to.Fov = 38;
                Rig.TransitionTo(to, 0.9f);
                b.Announce("Decisive action: " + (b.Session.Decisive ?? "the flagship falls") + " Click or press Space to continue.", "ok");
                return false;
            }
            _reviewTime += Time.unscaledDeltaTime;
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool skip = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                        (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
            if (_reviewTime < ReviewSeconds && !skip) return false;
            if (Rig.Transitioning) Rig.FinishTransition();
            _reviewState = 2;
            return true;
        }
    }
}
