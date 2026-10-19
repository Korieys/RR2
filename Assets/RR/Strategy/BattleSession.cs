using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>
    /// Identifies every input to combat resolution (Docs/vertical-slice/DECISIONS.md, VS-03). Snapshots and saves carry it, and
    /// a battle recorded under different rules is refused rather than replayed into a different result.
    /// </summary>
    public static class RulesVersion
    {
        public const string Commandfall = "0.15.0";
        /// <summary>Slice revision of the vendored rules. Increment it with every rules-affecting change recorded in
        /// Docs/vertical-slice/VENDOR_PATCHES.md.</summary>
        public const int SliceRevision = 1;
        public static string Id => $"commandfall-{Commandfall}+{GameContent.ContentVersion}+{RosterContent.ContentVersion}+vs-{SliceRevision}";

        static string _fingerprint;
        /// <summary>Hash of every unit, ability and battlefield definition: catches a content change the version id missed.</summary>
        public static string ContentFingerprint => _fingerprint ??= Serialization.ContentHash().ToString("x16");
    }

    /// <summary>Everything needed to reproduce a battle exactly: its setup, the accepted commands, and the state hash after
    /// each of them. Cosmetic data (camera, animation, effects, timestamps) is not part of it.</summary>
    public sealed class BattleSnapshot
    {
        public string RulesVersionId;
        public string ContentFingerprint;
        public BattleSetup Setup;
        public List<Command> Log = new List<Command>();
        public ulong InitialHash;
        /// <summary>State hash after each logged command, in order.</summary>
        public List<ulong> Hashes = new List<ulong>();
        public ulong FinalHash => Hashes.Count > 0 ? Hashes[Hashes.Count - 1] : InitialHash;
    }

    /// <summary>A consequence computed by applying the command to a copy of the authoritative state with the same rules
    /// function that commits it. The slice's orbital battles have fog of war off, so a copy reveals nothing hidden.</summary>
    public sealed class BattlePreview
    {
        public bool Accepted;
        public string Reason;
        public List<GameEvent> Events = new List<GameEvent>();
        public MatchState After;
        public ulong HashAfter;
    }

    /// <summary>Where a replay first departs from the record. <see cref="Index"/> is the command index, or −1 for the setup.</summary>
    public sealed class Divergence
    {
        public int Index;
        public string Kind;
        public string Detail;
        public ulong Expected;
        public ulong Actual;
        public override string ToString() =>
            Index < 0 ? $"{Kind}: {Detail}" : $"command {Index} ({Detail}): {Kind}, expected {Expected:x16}, got {Actual:x16}";
    }

    /// <summary>
    /// The authoritative orbital battle: setup, live state, accepted command log and per-step hashes. Players, the AI,
    /// automation and restores all go through <see cref="Submit"/>; previews go through <see cref="Preview"/>; both call the
    /// vendored <see cref="RulesEngine.Apply"/>, so nothing else decides a result.
    /// </summary>
    public sealed class BattleSession
    {
        /// <summary>Controller id for host or system commands (replay, automation); every other caller names its seat.</summary>
        public const int Host = -1;

        public BattleSetup Setup { get; private set; }
        public MatchState State { get; private set; }
        public string RulesVersionId { get; private set; }
        public ulong InitialHash { get; private set; }
        public IReadOnlyList<Command> Log => _log;
        public IReadOnlyList<ulong> Hashes => _hashes;
        public ulong Hash => _hashes.Count > 0 ? _hashes[_hashes.Count - 1] : InitialHash;

        readonly List<Command> _log = new List<Command>();
        readonly List<ulong> _hashes = new List<ulong>();

        BattleSession() { }

        public static BattleSession Begin(BattleSetup setup, out string error)
        {
            var s = BattleBridge.CreateState(setup, out error);
            if (s == null) return null;
            return new BattleSession { Setup = setup, State = s, RulesVersionId = RulesVersion.Id, InitialHash = Serialization.StateHash(s) };
        }

        /// <summary>Validates and applies one command for <paramref name="controller"/> (a seat, or <see cref="Host"/>).
        /// A rejected command leaves the state, log and hashes untouched and says why.</summary>
        public CommandResult Submit(Command c, int controller)
        {
            if (c == null) return CommandResult.Reject(null, "Empty command.");
            if (controller != Host)
            {
                if (c.Seat != controller) return CommandResult.Reject(c, "You do not command that side.");
                if (c.Type == CommandType.Forfeit || c.Type == CommandType.ClockExpire)
                    return CommandResult.Reject(c, "Only the host can issue that command.");
            }
            var copy = c.Clone();
            var r = RulesEngine.Apply(State, copy);
            if (!r.Accepted) return r;
            _log.Add(copy.Clone());
            _hashes.Add(Serialization.StateHash(State));
            return r;
        }

        /// <summary>The exact consequence of a command, computed on a copy by the function that would commit it.</summary>
        public BattlePreview Preview(Command c)
        {
            var copy = State.Clone();
            var r = RulesEngine.Apply(copy, c?.Clone());
            var p = new BattlePreview { Accepted = r.Accepted, Reason = r.Reason, Events = r.Events };
            if (r.Accepted) { p.After = copy; p.HashAfter = Serialization.StateHash(copy); }
            return p;
        }

        public BattleSnapshot Snapshot() => new BattleSnapshot
        {
            RulesVersionId = RulesVersionId, ContentFingerprint = RulesVersion.ContentFingerprint, Setup = Setup,
            Log = _log.Select(x => x.Clone()).ToList(), InitialHash = InitialHash, Hashes = _hashes.ToList(),
        };

        /// <summary>Rebuilds a battle from its snapshot by replaying it under the current rules. Refuses a snapshot recorded
        /// under other rules or content, and one whose replay departs from the recorded hashes, so a reload can never
        /// reroll or silently change a battle.</summary>
        public static BattleSession Restore(BattleSnapshot snap, out string error)
        {
            error = null;
            if (snap?.Setup?.Config == null) { error = "The saved battle is incomplete."; return null; }
            if (snap.RulesVersionId != RulesVersion.Id)
            { error = $"The saved battle was fought under rules {snap.RulesVersionId}; this build uses {RulesVersion.Id}."; return null; }
            if (snap.ContentFingerprint != RulesVersion.ContentFingerprint)
            { error = "The saved battle was fought with different unit or battlefield definitions."; return null; }
            var d = FindDivergence(snap, out var session);
            if (d != null) { error = "The saved battle does not replay identically: " + d; return null; }
            return session;
        }

        /// <summary>Replays a record step by step and reports the first departure (null if the replay is identical).</summary>
        public static Divergence FindDivergence(BattleSnapshot snap, out BattleSession replayed)
        {
            replayed = Begin(snap.Setup, out var err);
            if (replayed == null) return new Divergence { Index = -1, Kind = "setup failed", Detail = err };
            if (replayed.InitialHash != snap.InitialHash)
                return new Divergence { Index = -1, Kind = "different starting state", Detail = "the setup no longer produces the recorded start", Expected = snap.InitialHash, Actual = replayed.InitialHash };
            if (snap.Hashes.Count != snap.Log.Count)
                return new Divergence { Index = -1, Kind = "incomplete record", Detail = $"{snap.Log.Count} commands but {snap.Hashes.Count} hashes" };
            for (int i = 0; i < snap.Log.Count; i++)
            {
                var c = snap.Log[i];
                var r = replayed.Submit(c, Host);
                if (!r.Accepted) return new Divergence { Index = i, Kind = "rejected on replay", Detail = c + ": " + r.Reason, Expected = snap.Hashes[i] };
                if (replayed.Hash != snap.Hashes[i]) return new Divergence { Index = i, Kind = "different result", Detail = c.ToString(), Expected = snap.Hashes[i], Actual = replayed.Hash };
            }
            return null;
        }

        /// <summary>The action log for inspection: one line per accepted command with the state hash after it.</summary>
        public IEnumerable<string> DescribeLog()
        {
            yield return $"rules {RulesVersionId}, content {RulesVersion.ContentFingerprint}, battle {Setup.BattleId}, start {InitialHash:x16}";
            for (int i = 0; i < _log.Count; i++) yield return $"{i,4}  {_log[i]}  -> {_hashes[i]:x16}";
        }

        // ------------------------------------------------------------------ save data

        public static Dictionary<string, object> ToData(BattleSnapshot b)
        {
            var st = b.Setup;
            return new Dictionary<string, object>
            {
                ["rules"] = b.RulesVersionId, ["content"] = b.ContentFingerprint,
                ["id"] = st.BattleId, ["engagement"] = st.EngagementId, ["playerSeat"] = st.PlayerSeat,
                ["fleets"] = st.FleetIds.Cast<object>().ToList(),
                ["ships"] = st.ShipUids.Select(l => (object)l.Cast<object>().ToList()).ToList(),
                ["startHp"] = st.StartHp.Select(l => (object)l.Cast<object>().ToList()).ToList(),
                ["deployment"] = st.Deployment.Select(l => l == null ? null : (object)l.Select(h => (object)new List<object> { h.Q, h.R }).ToList()).ToList(),
                ["config"] = Convert.ToBase64String(Serialization.ConfigBytes(st.Config)),
                ["start"] = b.InitialHash.ToString("x16"),
                ["commands"] = b.Log.Select(c => (object)Convert.ToBase64String(Serialization.CommandBytes(c))).ToList(),
                ["hashes"] = b.Hashes.Select(h => (object)h.ToString("x16")).ToList(),
            };
        }

        /// <summary>Reads a snapshot from save data; null with a reason if it is malformed. Replay checks happen in <see cref="Restore"/>.</summary>
        public static BattleSnapshot FromData(IDictionary<string, object> d, out string error)
        {
            error = null;
            try
            {
                var st = new BattleSetup
                {
                    BattleId = d.S("id"), EngagementId = d.S("engagement"), PlayerSeat = d.I("playerSeat"),
                    Config = Serialization.FromBytes(Convert.FromBase64String(d.S("config")), Serialization.ReadConfig),
                };
                var fleets = d.A("fleets");
                var ships = d.A("ships");
                var hp = d.A("startHp");
                var dep = d.A("deployment");
                if (fleets.Count != 2 || ships.Count != 2 || hp.Count != 2) throw new FormatException("expected two sides");
                for (int seat = 0; seat < 2; seat++)
                {
                    st.FleetIds[seat] = fleets[seat] as string;
                    st.ShipUids[seat] = ((List<object>)ships[seat]).Select(Int).ToList();
                    st.StartHp[seat] = ((List<object>)hp[seat]).Select(Int).ToList();
                    if (seat < dep.Count && dep[seat] is List<object> hexes)
                        st.Deployment[seat] = hexes.Select(v => { var p = (List<object>)v; return new Hex(Int(p[0]), Int(p[1])); }).ToList();
                }
                var snap = new BattleSnapshot
                {
                    RulesVersionId = d.S("rules"), ContentFingerprint = d.S("content"), Setup = st, InitialHash = Hex64(d.S("start")),
                    Log = d.A("commands").Select(v => Serialization.FromBytes(Convert.FromBase64String((string)v), Serialization.ReadCommand)).ToList(),
                    Hashes = d.A("hashes").Select(v => Hex64((string)v)).ToList(),
                };
                if (string.IsNullOrEmpty(st.BattleId) || string.IsNullOrEmpty(snap.RulesVersionId)) throw new FormatException("missing identity");
                return snap;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is ArgumentException || ex is NullReferenceException
                                       || ex is System.IO.InvalidDataException || ex is System.IO.EndOfStreamException || ex is IndexOutOfRangeException)
            {
                error = "The saved battle is damaged: " + ex.Message;
                return null;
            }
        }

        static int Int(object v) => Convert.ToInt32(v, CultureInfo.InvariantCulture);
        static ulong Hex64(string s) => ulong.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
