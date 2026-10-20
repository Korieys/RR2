using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace RR.Game
{
    // Local playtest metrics (Prompt 12): one JSON object per line in playtest_metrics.jsonl next to the save file. Nothing
    // leaves the machine; -rrNoMetrics turns it off. Measured: time to the first meaningful decision, battle duration and
    // orders, invalid-action attempts, inspected previews, refits, and the result type. Automated runs are labelled as such,
    // so technical verification is never mixed up with human evidence.
    public static class PlaytestMetrics
    {
        static string _path;
        static float _sessionStart;
        static bool _decided;
        static string _source = "player";
        static string _battleId, _battleKind;
        static float _battleStart, _firstOrderAt;
        static int _orders, _invalid, _previews;

        public static string FilePath => _path;

        public static void Init(string savePath)
        {
            if (Args.Has("-rrNoMetrics")) return;
            try { _path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath)), "playtest_metrics.jsonl"); }
            catch { _path = null; return; }
            _sessionStart = Time.unscaledTime;
            if (Args.Has("-rrTour") || Args.Has("-rrPassageTour") || Args.Has("-rrCampaignTour") || Args.Has("-rrWarTour") || Args.Has("-rrSoak") || Args.Has("-rrCapture")) _source = "automation";
            Write("session_start", "build", Application.version, "unity", Application.unityVersion);
        }

        /// <summary>A meaningful decision: an accepted order, a declaration, a refit, a commitment to battle.</summary>
        public static void Decision(string what)
        {
            if (_decided || _path == null) return;
            _decided = true;
            Write("first_decision", "what", what, "seconds", Secs(Time.unscaledTime - _sessionStart));
        }

        public static void BattleStart(string battleId, string kind)
        {
            _battleId = battleId; _battleKind = kind ?? "";
            _battleStart = Time.unscaledTime; _firstOrderAt = -1;
            _orders = _invalid = _previews = 0;
        }

        public static void Order(bool accepted, string reason)
        {
            if (_battleId == null) return;
            if (accepted)
            {
                _orders++;
                if (_firstOrderAt < 0) _firstOrderAt = Time.unscaledTime - _battleStart;
                Decision("battle order");
            }
            else { _invalid++; Write("invalid_action", "battle", _battleId, "reason", reason ?? ""); }
        }

        /// <summary>An attempt the interface refused before it became an order (a mode or a pick that cannot apply).</summary>
        public static void Refused(string reason) { if (_battleId != null) { _invalid++; Write("invalid_action", "battle", _battleId, "reason", reason ?? ""); } }

        public static void Preview() { if (_battleId != null) _previews++; }

        public static void BattleEnd(string result, bool playerWon, string decisive)
        {
            if (_battleId == null) return;
            Write("battle_end", "battle", _battleId, "kind", _battleKind, "result", result, "player_won", playerWon ? "true" : "false",
                  "seconds", Secs(Time.unscaledTime - _battleStart), "first_order_seconds", _firstOrderAt < 0 ? "" : Secs(_firstOrderAt),
                  "orders", _orders.ToString(), "invalid_actions", _invalid.ToString(), "previews_inspected", _previews.ToString(), "decisive", decisive ?? "");
            _battleId = null;
        }

        public static void Refit(string ship, string module, bool ok, string reason)
        {
            if (ok) Decision("refit");
            Write("refit", "ship", ship, "module", module, "ok", ok ? "true" : "false", "reason", reason ?? "");
        }

        static string Secs(float s) => s.ToString("0.0", CultureInfo.InvariantCulture);

        static void Write(string kind, params string[] kv)
        {
            if (_path == null) return;
            var sb = new StringBuilder();
            sb.Append("{\"t\":\"").Append(DateTime.Now.ToString("s", CultureInfo.InvariantCulture)).Append("\",\"source\":\"").Append(_source).Append("\",\"event\":\"").Append(kind).Append('"');
            for (int i = 0; i + 1 < kv.Length; i += 2) sb.Append(",\"").Append(kv[i]).Append("\":\"").Append(Escape(kv[i + 1])).Append('"');
            sb.Append('}');
            try { File.AppendAllText(_path, sb.ToString() + "\n"); }
            catch (Exception ex) { Debug.LogWarning("Playtest metrics could not be written: " + ex.Message); _path = null; }
        }

        static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
