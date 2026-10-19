using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Vertical-slice battle features (Docs/vertical-slice): overclock and turn orders, firing arcs and facing, contract
    // entry markers, the command-threat analysis, and the slice AI on a worker thread. Every order still goes through
    // BattleSession.Submit; this file only builds orders, shows rule state and schedules the AI.
    public sealed partial class BattlePresentation
    {
        public bool OverclockMode { get; private set; }
        public bool TurnMode { get; private set; }
        /// <summary>Lethal lines the enemy has on the player's flagship (as if the enemy acted next).</summary>
        public ThreatReport MyThreat { get; private set; }
        /// <summary>Lethal lines the player has on the enemy flagship this activation.</summary>
        public ThreatReport Opportunity { get; private set; }
        public Exposure MyExposure { get; private set; }
        public string AiReason { get; private set; } = "";
        public bool Training => Setup.Scenario == "passage_training";
        /// <summary>Automation only: a hex the hover is pinned to (null = follow the pointer).</summary>
        public Commandfall.Core.Hex? ForcedHover;

        readonly SliceAI _sliceAi = new SliceAI();
        Task<Command> _aiTask;
        int _aiRevision = -1, _analysisRevision = -1;
        GameObject _arcFan;
        readonly List<GameObject> _entryMarkers = new List<GameObject>();

        /// <summary>The seat whose orders the interface issues: fixed, or the side to act in a hotseat battle.</summary>
        int CommandSeat => Setup.Hotseat && !S.IsOver ? S.ActiveSeat : Setup.PlayerSeat;

        bool AiControls(int seat) => !Setup.Hotseat && seat != Setup.PlayerSeat;

        public void ToggleOverclock()
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (!OverclockMode)
            {
                if (u == null) { Say("Select a ship first.", "bad"); return; }
                var why = SliceRules.CanOverclock(S, u);
                if (why != null) { Say(why, "bad"); _root.Audio.Play("ui_invalid"); return; }
            }
            OverclockMode = !OverclockMode; TurnMode = false;
            RefreshCells();
        }

        public void ToggleTurn()
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (!TurnMode)
            {
                if (u == null) { Say("Select a ship first.", "bad"); return; }
                if (!SliceRules.HasArc(u)) { Say(u.Def.Name + " has no fixed firing arc, so it never needs to turn.", "bad"); return; }
                var probe = u.Position + SliceRules.FacingVector((SliceRules.Facing(u) + 6) % 12);
                var why = SliceRules.ValidateTurn(S, u, probe);
                if (why != null) { Say(why, "bad"); _root.Audio.Play("ui_invalid"); return; }
            }
            TurnMode = !TurnMode; OverclockMode = false;
            RefreshCells();
        }

        void ClearModes() { OverclockMode = false; TurnMode = false; }

        /// <summary>The order a click on <paramref name="h"/> issues for <paramref name="u"/> in the current mode.</summary>
        Command OrderFor(UnitState u, Commandfall.Core.Hex h, bool attack)
        {
            int seat = CommandSeat;
            if (TurnMode)
            {
                int f = SliceRules.NearestFacing(h - u.Position);
                return SliceRules.Order(CommandType.Move, seat, u.Id, u.Position + SliceRules.FacingVector(f), SliceRules.TurnOption);
            }
            int opt = OverclockMode ? SliceRules.OverclockOption : 0;
            return SliceRules.Order(attack ? CommandType.Attack : CommandType.Move, seat, u.Id, h, opt);
        }

        /// <summary>Reach shown in gold: the overclocked reach in overclock mode.</summary>
        Dictionary<Commandfall.Core.Hex, MoveOption> ShownReach(UnitState u) =>
            RosterMovement.Reachable(S, u, OverclockMode && SliceRules.CanOverclock(S, u) == null && !u.MovedThisActivation ? SliceRules.OverclockMove : 0);

        IEnumerable<Commandfall.Core.Hex> ShownTargets(UnitState u)
        {
            foreach (var e in S.Units.Where(x => x.OnBoard && x.Owner != u.Owner))
                if (RosterCombat.ResolveBasic(S, u, e.Position, overclock: OverclockMode).Legal) yield return e.Position;
        }

        /// <summary>Preview lines for the slice modes (null = use the ordinary preview).</summary>
        string SlicePreview(UnitState u, Commandfall.Core.Hex h)
        {
            if (TurnMode)
            {
                int f = SliceRules.NearestFacing(h - u.Position);
                var why = SliceRules.ValidateTurn(S, u, u.Position + SliceRules.FacingVector(f));
                return why ?? $"Turn to face {SliceRules.FacingNames[f]}. This uses the move; the ship can still fire, into its new arc.";
            }
            var at = S.UnitAt(h);
            if (at != null && at.Owner != u.Owner)
            {
                var lines = AttackExplainer.Explain(S, u, h, OverclockMode);
                if (OverclockMode) lines.Add(HeatLine(u));
                return string.Join(" ", lines);
            }
            if (OverclockMode && at == null)
            {
                MoveOption mo;
                if (ShownReach(u).TryGetValue(h, out mo)) return $"Overclocked move here ({mo.Cost} movement). {HeatLine(u)}";
            }
            return null;
        }

        string HeatLine(UnitState u)
        {
            int now = SliceRules.Heat(u), after = now + SliceRules.OverclockHeat;
            return after >= SliceRules.OverheatAt
                ? $"Heat {now} → {after}: OVERHEATED, armour −{SliceRules.OverheatArmor} and no overclocking until it cools below {SliceRules.OverheatAt}."
                : $"Heat {now} → {after} (overheats at {SliceRules.OverheatAt}; sheds {SliceRules.CoolPerActivation} after any activation without overclocking).";
        }

        // ------------------------------------------------------------------ analysis

        /// <summary>Recomputes the command-threat analysis when the state has changed (same rules as everything else).</summary>
        public void RefreshAnalysis()
        {
            if (S.Revision == _analysisRevision || Animating) return;
            _analysisRevision = S.Revision;
            int me = Setup.PlayerSeat;
            MyThreat = CommandThreat.Assess(S, me);
            MyExposure = CommandThreat.RoundExposure(S, me);
            Opportunity = S.ActiveSeat == me ? CommandThreat.Assess(S, 1 - me) : null;
        }

        // ------------------------------------------------------------------ AI on a worker thread

        /// <summary>Runs the opponent without blocking the frame: the slice AI chooses on a copy of the state, and the
        /// order is submitted through the session only if the state has not changed meanwhile. Training opponents wait.</summary>
        void StepOpponent()
        {
            int seat = S.ActiveSeat;
            if (Training)
            {
                var id = S.ActiveUnitId != 0 ? S.ActiveUnitId : S.Units.Where(x => x.Owner == seat && RosterRules.Eligible(S, x)).Select(x => x.Id).FirstOrDefault();
                var wait = id != 0 ? Command.Wait(seat, id) : Command.Pass(seat);
                var rw = Session.Submit(wait, seat);
                if (rw.Accepted) StartCoroutine(Play(rw.Events));
                return;
            }
            if (_aiTask == null)
            {
                var copy = S.Clone();
                var paths = S.Map.Paths;   // build shared caches on the main thread before the worker reads them
                _aiRevision = S.Revision;
                _aiTask = Task.Run(() => _sliceAi.Choose(copy));
                return;
            }
            if (!_aiTask.IsCompleted) return;
            Command c;
            try { c = _aiTask.Result; } catch (Exception ex) { Debug.LogError("AI failed: " + ex); c = Command.Pass(seat); }
            _aiTask = null;
            if (_aiRevision != S.Revision) return;
            AiReason = _sliceAi.LastReason;
            var res = Session.Submit(c, seat);
            if (!res.Accepted) { c = Command.Pass(seat); res = Session.Submit(c, seat); if (!res.Accepted) return; }
            StartCoroutine(Play(res.Events));
        }

        public bool AiThinking => _aiTask != null && !_aiTask.IsCompleted;
        /// <summary>The state revision the threat analysis was last computed for (the panel refreshes on it).</summary>
        public int AnalysisRevision => _analysisRevision;

        // ------------------------------------------------------------------ facing, arcs and markers

        static Vector3 FacingDir(int f)
        {
            var v = World(SliceRules.FacingVector(f)) - World(Commandfall.Core.Hex.Zero);
            return v.normalized;
        }

        /// <summary>Capital ships point where their main battery points (the rule, not the last animation).</summary>
        void ApplyFacings()
        {
            foreach (var u in S.Units)
            {
                UnitView v;
                if (!u.OnBoard || !SliceRules.HasArc(u) || !Units.TryGetValue(u.Id, out v)) continue;
                v.T.localRotation = Quaternion.LookRotation(FacingDir(SliceRules.Facing(u)), Vector3.up);
            }
            UpdateArcFan();
            UpdateEntryMarkers();
        }

        /// <summary>The selected (or hovered) capital ship's forward arc, drawn on the board: 240° out to its weapon range.</summary>
        void UpdateArcFan()
        {
            var u = Selected != 0 ? S.Unit(Selected) : HoverHex.HasValue ? S.UnitAt(HoverHex.Value) : null;
            if (u == null || !u.OnBoard || !SliceRules.HasArc(u)) { if (_arcFan != null) _arcFan.SetActive(false); return; }
            if (_arcFan == null)
            {
                _arcFan = new GameObject("Firing arc");
                _arcFan.transform.SetParent(transform, false);
                _arcFan.AddComponent<MeshFilter>();
                var mr = _arcFan.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Look.Unlit(ArtLibrary.I.fx, new Color(1f, 0.82f, 0.45f) * 0.22f, "Firing arc");
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _arcFan.SetActive(true);
            float r = (u.Weapon.MaxRange + 0.5f) * 1.7320508f * Hex;
            var mesh = new Mesh { name = "Arc fan" };
            const int seg = 40;
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-120f, 120f, i / (float)seg) * Mathf.Deg2Rad;
                verts.Add(new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r));
                if (i > 0) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            }
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, Enumerable.Repeat(new Vector2(0, 0.5f), verts.Count).ToList());   // RR_Fx: uv.y 0.5 = full strength
            var old = _arcFan.GetComponent<MeshFilter>().sharedMesh;
            _arcFan.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (old != null) Destroy(old);
            _arcFan.transform.localPosition = World(u.Position) + Vector3.up * 0.03f;
            _arcFan.transform.localRotation = Quaternion.LookRotation(FacingDir(SliceRules.Facing(u)), Vector3.up);
        }

        /// <summary>Disclosed contract entry hexes for escorts still under way (both sides see them).</summary>
        void UpdateEntryMarkers()
        {
            var due = S.Units.Where(SliceRules.InReserve).Select(u => SliceRules.EntryHex(S.Map, u.Owner, SliceRules.ContractLane(u))).Distinct().ToList();
            while (_entryMarkers.Count < due.Count)
            {
                var m = Mesh("Contract entry", MeshFactory.Circle(0.7f, 0.05f, 48), Look.Unlit(ArtLibrary.I.fx, Look.GoldBright * 1.2f, "Contract entry"));
                m.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Dash", 8);
                _entryMarkers.Add(m);
            }
            for (int i = 0; i < _entryMarkers.Count; i++)
            {
                bool on = i < due.Count;
                _entryMarkers[i].SetActive(on);
                if (on) _entryMarkers[i].transform.localPosition = World(due[i]) + Vector3.up * 0.05f;
            }
        }

        public IEnumerable<(Commandfall.Core.Hex hex, string text)> EntryLabels() =>
            S.Units.Where(SliceRules.InReserve).Select(u => (SliceRules.EntryHex(S.Map, u.Owner, SliceRules.ContractLane(u)),
                $"{u.Def.Name}: round {Math.Max(S.Round + 1, SliceRules.ContractArrivalRound)}"));

        /// <summary>World position of a unit's view (for HUD labels), or null.</summary>
        public Vector3? ViewPosition(int unitId)
        {
            UnitView v;
            return Units.TryGetValue(unitId, out v) && !v.Dead ? v.T.position : (Vector3?)null;
        }

        static Color FactionColor(GalaxyState g, FactionId f)
        {
            string id = f == FactionId.Pygmaei ? "pygmaei" : f == FactionId.Inhumanus ? "inhumanus" : f == FactionId.Silex ? "silex" : f == FactionId.Craigon ? "craigon" : null;
            return Look.PolityColor(g, id);
        }
    }
}
