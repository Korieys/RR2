using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    public sealed partial class BattlePresentation
    {
        // Reconcile every view with the authoritative state (after animation, load, or anything not animated).
        public void SyncAll(bool snap)
        {
            foreach (var u in S.Units)
            {
                UnitView v;
                if (!Units.TryGetValue(u.Id, out v)) { if (u.Alive && u.OnBoard) CreateUnitView(u); continue; }
                if (!u.Alive && !v.Dead) { v.Dead = true; v.T.gameObject.SetActive(false); continue; }
                if (u.Alive && u.OnBoard)
                {
                    v.T.gameObject.SetActive(true);
                    if (snap) v.T.localPosition = World(u.Position);
                    var block = new MaterialPropertyBlock();
                    float dmg = 1f - u.Hp / (float)Mathf.Max(1, u.MaxHp);
                    if (dmg > 0.05f) block.SetFloat("_Damage", dmg);
                    if (u.Id == Selected) block.SetColor("_RimTint", new Color(1f, 0.82f, 0.45f, 0.9f));
                    else if (RosterRules.Inert(u)) block.SetColor("_RimTint", new Color(0.8f, 0.4f, 1f, 0.7f));
                    else block.SetColor("_RimTint", u.Owner == PlayerSeat ? new Color(0.35f, 0.65f, 1f, 0.35f) : new Color(0.8f, 0.4f, 1f, 0.35f));   // ownership by rim as well as ring
                    foreach (var mr in v.Model.GetComponentsInChildren<MeshRenderer>()) mr.SetPropertyBlock(block);
                    v.RingMat.SetFloat("_Fade", RosterRules.Done(S, u) ? 0.35f : 1f);
                }
            }
            foreach (var b in S.Barriers.Where(b => !_barriers.ContainsKey(b.Hex)).ToList())
            {
                var lib = ArtLibrary.I;
                var go = new GameObject("Barrier");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = World(b.Hex);
                for (int i = 0; i < 5; i++)
                {
                    var crystal = Mesh("Crystal", MeshFactory.Sphere(8), Look.Unlit(lib.fx, new Color(0.35f, 0.65f, 1f) * 0.8f, "Barrier"));
                    crystal.transform.SetParent(go.transform, false);
                    float a = i * 1.26f;
                    crystal.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.4f, 0.4f, Mathf.Sin(a) * 0.4f);
                    crystal.transform.localScale = new Vector3(0.12f, 0.6f, 0.12f);
                }
                _barriers[b.Hex] = go;
            }
            foreach (var h in _barriers.Keys.ToList())
                if (S.BarrierAt(h) == null) { Destroy(_barriers[h]); _barriers.Remove(h); }
            for (int i = 0; i < S.Objectives.Count && i < _objectiveRings.Count; i++)
            {
                var o = S.Objectives[i];
                var mr = _objectiveRings[i].GetComponent<MeshRenderer>();
                Color c = o.Owner < 0 ? Look.Gold * 1.2f : o.Owner == PlayerSeat ? new Color(0.35f, 0.7f, 1f) * 1.6f : new Color(0.8f, 0.4f, 1f) * 1.6f;
                mr.sharedMaterial.SetColor("_Color", c);
            }
            RefreshCells();
        }

        public void RefreshCells()
        {
            var px = new Color32[32 * 16];
            var codes = new Dictionary<Commandfall.Core.Hex, int>();
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (u != null && u.Alive && S.ActiveSeat == PlayerSeat && !S.IsOver && !Animating)
            {
                if (AbilityMode)
                {
                    foreach (var c in RosterAbilities.Candidates(S, u, Picks, AbilityOption)) if (c.Valid) codes[c.Hex] = 3;
                    foreach (var p in Picks) codes[p] = 4;
                }
                else
                {
                    foreach (var kv in RosterMovement.Reachable(S, u)) if (kv.Key != u.Position) codes[kv.Key] = 1;
                    foreach (var o in RosterCombat.LegalTargets(S, u)) codes[o.TargetHex] = 2;
                }
                codes[u.Position] = 4;
            }
            if (ShowThreat) foreach (var h in ThreatHexes()) if (!codes.ContainsKey(h)) codes[h] = 5;
            foreach (var h in S.Map.AllHexes)
            {
                int x = h.Q + 16, y = h.R + 8;
                if (x < 0 || y < 0 || x >= 32 || y >= 16) continue;
                var t = S.Map.Terrain(h);
                int terr = t == TerrainType.Cover ? 1 : t == TerrainType.Elevated ? 2 : t == TerrainType.Obstacle ? 3 : t == TerrainType.Hazard ? 4 : 0;
                int code; codes.TryGetValue(h, out code);
                float zone = S.Objectives.Any(o => o.Hex == h) ? 1f : S.Map.ZoneSeat(h) == PlayerSeat ? 0.25f : S.Map.ZoneSeat(h) >= 0 ? 0.5f : 0f;
                px[y * 32 + x] = new Color(1, terr / 4f, code / 8f, zone);
            }
            _cells.SetPixels32(px);
            _cells.Apply(false);
        }

        // Hexes the opponent could attack on its next activation (from every legal destination of every enemy ship).
        public HashSet<Commandfall.Core.Hex> ThreatHexes()
        {
            var set = new HashSet<Commandfall.Core.Hex>();
            foreach (var e in S.Units.Where(x => x.Owner != PlayerSeat && x.OnBoard && !RosterRules.Inert(x)))
            {
                var from = RosterMovement.Reachable(S, e).Keys.ToList();
                from.Add(e.Position);
                int max = e.Weapon.MaxRange, min = e.Weapon.MinRange;
                foreach (var f in from)
                    foreach (var h in S.Map.AllHexes)
                    {
                        int d = Commandfall.Core.Hex.Distance(f, h);
                        if (d >= min && d <= max) set.Add(h);
                    }
            }
            return set;
        }

        public bool PlayersTurn { get { return !S.IsOver && S.Phase == MatchPhase.Battle && S.ActiveSeat == PlayerSeat && !Animating; } }

        public void Select(int unitId)
        {
            var u = S.Unit(unitId);
            if (u == null || u.Owner != PlayerSeat) return;
            if (S.ActiveUnitId != 0 && S.ActiveUnitId != unitId) { Say("Finish the current activation first: act or wait.", "bad"); return; }
            string why;
            if (!RulesEngine.CanActivate(S, u, out why)) { Say(why, "bad"); return; }
            Selected = unitId;
            AbilityMode = false; Picks.Clear(); AbilityOption = 0;
            _root.Audio.Play("ui_select");
            SyncAll(false);
        }

        void Say(string text, string kind) { if (Notice != null) Notice(text, kind); }
        public void Announce(string text, string kind) { Say(text, kind); }

        public void ToggleAbility()
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (u == null) { Say("Select one of your ships first.", "bad"); return; }
            if (!AbilityMode)
            {
                var v = RosterRules.CanAct(S, u, false);
                if (v != null) { Say(v, "bad"); return; }
                if (u.Cooldown > 0) { Say(u.R.ActiveName + " is recharging for " + u.Cooldown + " more activation" + (u.Cooldown == 1 ? "." : "s."), "bad"); return; }
            }
            AbilityMode = !AbilityMode; Picks.Clear();
            if (AbilityMode && RosterAbilities.Steps(u, AbilityOption).Count == 0) { ConfirmAbility(); return; }
            RefreshCells();
        }

        public void ConfirmAbility()
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (u == null) return;
            Submit(Command.Active(PlayerSeat, u.Id, Picks, AbilityOption));
        }

        public void UndoPick()
        {
            if (Picks.Count > 0) { Picks.RemoveAt(Picks.Count - 1); RefreshCells(); }
            else if (AbilityMode) { AbilityMode = false; RefreshCells(); }
            else if (S.ActiveUnitId == 0) { Selected = 0; SyncAll(false); }
        }

        public void Wait()
        {
            var u = Selected != 0 ? S.Unit(Selected) : (S.ActiveUnitId != 0 ? S.Unit(S.ActiveUnitId) : null);
            if (u == null) { Say("Select a ship to hold position with.", "bad"); return; }
            Submit(Command.Wait(PlayerSeat, u.Id));
        }

        public void ClickHex(Commandfall.Core.Hex h)
        {
            if (!PlayersTurn) return;
            var at = S.UnitAt(h);
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (AbilityMode && u != null)
            {
                var why = RosterAbilities.CheckPick(S, u, Picks, h, AbilityOption);
                if (why != null) { Say(why, "bad"); _root.Audio.Play("ui_invalid"); return; }
                Picks.Add(h);
                var steps = RosterAbilities.Steps(u, AbilityOption);
                if (Picks.Count >= steps.Count && RosterAbilities.Ready(u, Picks, AbilityOption)) ConfirmAbility();
                else RefreshCells();
                return;
            }
            if (at != null && at.Owner == PlayerSeat && at.Id != Selected) { Select(at.Id); return; }
            if (u == null)
            {
                if (at != null && at.Owner != PlayerSeat) Say(at.Def.Name + ": " + at.Hp + " of " + at.MaxHp + " hull, armor " + at.BaseArmor + ".", "ok");
                return;
            }
            if (at != null && at.Owner != PlayerSeat) { Submit(Command.Attack(PlayerSeat, u.Id, h)); return; }
            if (at == null && h != u.Position) Submit(Command.Move(PlayerSeat, u.Id, h));
        }

        public void Hover(Commandfall.Core.Hex? h)
        {
            HoverHex = h;
            _gridMat.SetVector("_Hover", h.HasValue ? new Vector4(h.Value.Q, h.Value.R, 1, 0) : Vector4.zero);
            PreviewText = h.HasValue && S.Map.Contains(h.Value) ? Preview(h.Value) : null;
        }

        /// <summary>The preview text the interface would show for a hex with the current selection (automation and checks).</summary>
        public string PreviewAt(Commandfall.Core.Hex h) { return Preview(h); }

        // Preview through the same rules functions that resolve the action.
        string Preview(Commandfall.Core.Hex h)
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            var at = S.UnitAt(h);
            if (u == null || !PlayersTurn)
                return at != null ? at.Def.Name + (S.Players[at.Owner].CommanderId == at.Id ? " (leader)" : "") + ": " + at.Hp + " of " + at.MaxHp + " hull, armor " + at.BaseArmor + "." : TerrainText(S.Map.Terrain(h));
            if (AbilityMode)
            {
                var why = RosterAbilities.CheckPick(S, u, Picks, h, AbilityOption);
                return why == null ? u.R.ActiveName + ": a legal target." : why;
            }
            if (at != null && at.Owner != PlayerSeat)
            {
                var o = RosterCombat.ResolveBasic(S, u, h);
                if (!o.Legal) return o.Reason;
                string s = u.Weapon.Name + " hits " + at.Def.Name + " for " + o.FinalDamage + (o.Breakdown.Count > 0 ? " (" + string.Join(", ", o.Breakdown) + ")" : "") + ". Hull " + o.TargetHpBefore + " to " + o.TargetHpAfter + ".";
                if (o.Roster != null && o.Roster.GuardianId != 0 && o.Roster.GuardianId != at.Id && S.Unit(o.Roster.GuardianId) != null) s += " " + S.Unit(o.Roster.GuardianId).Def.Name + " will intercept this attack.";
                if (o.TargetDestroyed && S.Players[at.Owner].CommanderId == at.Id) s += " This destroys their flagship and ends the battle.";
                else if (o.TargetDestroyed) s += " This destroys it.";
                return s;
            }
            if (at == null)
            {
                MoveOption mo;
                if (RosterMovement.Reachable(S, u).TryGetValue(h, out mo)) return "Move here (" + mo.Cost + " movement). " + TerrainText(S.Map.Terrain(h));
                return RosterMovement.Explain(S, u, h);
            }
            return null;
        }

        static string TerrainText(TerrainType t)
        {
            return t == TerrainType.Cover ? "Asteroid cover: ranged attacks against this hex deal 1 less damage."
                 : t == TerrainType.Elevated ? "Nebula: basic weapon range is 1 shorter inside."
                 : t == TerrainType.Hazard ? "Ion storm: 1 damage when a move ends here."
                 : t == TerrainType.Obstacle ? "Large wreck: blocks movement and line of sight." : "";
        }

        public void Submit(Command c)
        {
            if (Animating) return;
            var res = Session.Submit(c, PlayerSeat);
            if (!res.Accepted) { Say(res.Reason, "bad"); _root.Audio.Play("ui_invalid"); return; }
            if (c.Seat == PlayerSeat) _root.Audio.Play("order_accepted", 0.7f);
            AbilityMode = false; Picks.Clear();
            Selected = S.ActiveSeat == PlayerSeat && S.ActiveUnitId != 0 ? S.ActiveUnitId : 0;
            StartCoroutine(Play(res.Events));
        }

        void Update()
        {
            float t = Time.unscaledTime;
            if (!Settings.ReducedMotion)
                foreach (var v in Units.Values) if (!v.Dead) v.Model.localPosition = new Vector3(0, 0.32f + Mathf.Sin(t * 0.8f + v.Bob) * 0.03f, 0);
            if (S.IsOver || Animating) return;
            if (S.ActiveSeat != PlayerSeat)
            {
                _aiDelay += Time.unscaledDeltaTime * AnimSpeed;
                if (_aiDelay < 0.45f) return;
                _aiDelay = 0;
                var c = _ai.ChooseAction(S);
                int seat = S.ActiveSeat;
                var res = Session.Submit(c, seat);
                if (!res.Accepted) { c = Command.Pass(seat); res = Session.Submit(c, seat); if (!res.Accepted) return; }
                StartCoroutine(Play(res.Events));
            }
            else if (Selected == 0 && S.ActiveUnitId != 0) Selected = S.ActiveUnitId;
        }
        IEnumerator Play(List<GameEvent> events)
        {
            Animating = true;
            RefreshCells();
            foreach (var e in events)
            {
                float k = 1f / Mathf.Max(0.25f, AnimSpeed) * (Settings.ReducedMotion ? 0.6f : 1f);
                switch (e.Type)
                {
                    case GameEventType.UnitMoved:
                        yield return MoveAlong(e.UnitId, e.Path != null && e.Path.Count > 1 ? e.Path : new List<Commandfall.Core.Hex> { e.From, e.To }, k);
                        break;
                    case GameEventType.UnitJumped:
                    case GameEventType.UnitDisplaced:
                    case GameEventType.FreeMove:
                        if (e.From != e.To) yield return Jump(e.UnitId, e.From, e.To, k);
                        break;
                    case GameEventType.AttackResolved:
                        yield return Fire(e.UnitId, e.To, k);
                        break;
                    case GameEventType.Intercepted:
                        _root.Audio.Play("guard");
                        Float(World(e.To), "Intercepted", new Color(0.6f, 0.85f, 1f));
                        Flash(World(e.To) + Vector3.up * 0.5f, new Color(0.5f, 0.8f, 1f) * 3, 1.4f, 0.4f * k);
                        yield return new WaitForSecondsRealtime(0.25f * k);
                        break;
                    case GameEventType.ShieldAbsorbed:
                        var su = S.Unit(e.TargetUnitId);
                        Float(World(su != null ? su.Position : e.To), "Shield absorbs " + e.Amount, new Color(0.6f, 0.85f, 1f));
                        break;
                    case GameEventType.DamageApplied:
                        Float(World(e.To), "-" + e.Amount, e.Lethal ? Look.Danger : new Color(1f, 0.85f, 0.6f));
                        Flash(World(e.To) + Vector3.up * 0.4f, new Color(1f, 0.6f, 0.3f) * 3, 0.9f, 0.25f * k);
                        _root.Audio.Play("impact", 0.6f);
                        break;
                    case GameEventType.UnitDestroyed:
                        yield return Destroyed(e.UnitId, k);
                        break;
                    case GameEventType.UnitCaptured:
                        Float(World(e.To), "Captured", new Color(0.85f, 0.5f, 1f));
                        break;
                    case GameEventType.TokenSpawned:
                        SyncAll(false);
                        Flash(World(e.To) + Vector3.up * 0.4f, new Color(0.8f, 0.4f, 1f) * 2, 1f, 0.3f * k);
                        break;
                    case GameEventType.RStatusApplied:
                        var tu = e.TargetUnitId != 0 ? S.Unit(e.TargetUnitId) : null;
                        if (tu != null) Float(World(tu.Position) + Vector3.up * 0.4f, e.RStatus.ToString().ToUpperInvariant(), new Color(0.85f, 0.8f, 1f));
                        break;
                    case GameEventType.HazardDamage:
                        Float(World(e.To), "Ion storm", new Color(0.4f, 0.8f, 1f));
                        break;
                    case GameEventType.ObjectiveClaimed:
                        Float(World(e.To), "Objective held", Look.GoldBright);
                        _root.Audio.Play("objective", 0.6f);
                        break;
                    case GameEventType.RoundStarted:
                        Say("Round " + e.Amount + " begins.", "ok");
                        break;
                    case GameEventType.CommanderKilled:
                        Say("A flagship has fallen.", "danger");
                        break;
                }
            }
            SyncAll(true);
            Animating = false;
            RefreshCells();
            if (S.IsOver) _root.Audio.Play(S.Winner == PlayerSeat ? "victory" : "defeat");
            else if (S.ActiveSeat == PlayerSeat && S.ActiveUnitId == 0 && LeaderThreatened()) { Say("Your flagship is within reach of enemy guns.", "danger"); _root.Audio.Play("threat", 0.7f); }
        }

        bool LeaderThreatened()
        {
            var leader = S.Unit(S.Players[PlayerSeat].CommanderId);
            return leader != null && leader.Alive && ThreatHexes().Contains(leader.Position);
        }

        IEnumerator MoveAlong(int id, List<Commandfall.Core.Hex> path, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(id, out v)) yield break;
            _root.Audio.Play("engine", 0.45f);
            int n = path.Count - 1;
            float total = Mathf.Min(1.3f, 0.26f * n) * k;
            var pts = path.Select(World).ToList();
            float len = 0; for (int i = 0; i < n; i++) len += Vector3.Distance(pts[i], pts[i + 1]);
            float t = 0;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / total);
                float ease = u * u * (3 - 2 * u);
                float d = ease * len, acc = 0;
                for (int i = 0; i < n; i++)
                {
                    float seg = Vector3.Distance(pts[i], pts[i + 1]);
                    if (d <= acc + seg || i == n - 1)
                    {
                        float f = seg > 0 ? Mathf.Clamp01((d - acc) / seg) : 1;
                        v.T.localPosition = Vector3.Lerp(pts[i], pts[i + 1], f);
                        var dir = pts[i + 1] - pts[i];
                        if (dir.sqrMagnitude > 1e-4f) v.T.localRotation = Quaternion.Slerp(v.T.localRotation, Quaternion.LookRotation(dir.normalized, Vector3.up), Time.unscaledDeltaTime * 10f);
                        break;
                    }
                    acc += seg;
                }
                yield return null;
            }
            v.T.localPosition = pts[n];   // arrives exactly on the legal hex
        }

        IEnumerator Jump(int id, Commandfall.Core.Hex from, Commandfall.Core.Hex to, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(id, out v)) yield break;
            var a = World(from); var b = World(to);
            float total = 0.45f * k, t = 0;
            Flash(a + Vector3.up * 0.4f, new Color(0.6f, 0.8f, 1f) * 2, 0.8f, 0.25f * k);
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / total);
                v.T.localPosition = Vector3.Lerp(a, b, u * u * (3 - 2 * u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.8f;
                yield return null;
            }
            v.T.localPosition = b;
            Flash(b + Vector3.up * 0.4f, new Color(0.6f, 0.8f, 1f) * 2, 0.8f, 0.25f * k);
        }

        IEnumerator Fire(int attackerId, Commandfall.Core.Hex target, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(attackerId, out v)) yield break;
            var to = World(target) + Vector3.up * 0.4f;
            var dir = to - v.T.localPosition; dir.y = 0;
            float turn = 0, turnTime = 0.18f * k;
            var q0 = v.T.localRotation; var q1 = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward, Vector3.up);
            while (turn < turnTime) { turn += Time.unscaledDeltaTime; v.T.localRotation = Quaternion.Slerp(q0, q1, turn / turnTime); yield return null; }
            var from = v.T.localPosition + Vector3.up * 0.42f + (q1 * Vector3.forward) * 0.5f;
            bool craigon = v.DefId.Contains("craigon");
            var col = craigon ? new Color(0.85f, 0.4f, 1f) : new Color(0.45f, 0.75f, 1f);
            _root.Audio.Play(craigon ? "fire_bio" : "fire_crystal", 0.6f);
            yield return Beam(from, to, col * 4f, 0.32f * k, craigon);
        }

        IEnumerator Beam(Vector3 a, Vector3 b, Color c, float seconds, bool bolt)
        {
            var lib = ArtLibrary.I;
            var go = new GameObject("Beam");
            go.transform.SetParent(transform, false);
            var mesh = new Mesh();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = Look.Unlit(lib.fx, c, "Beam");
            m.SetFloat("_Soft", 0.6f);
            mr.sharedMaterial = m;
            float t = 0;
            Flash(a, c * 0.6f, 0.45f, seconds);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                var head = bolt ? Vector3.Lerp(a, b, u) : b;
                var tail = bolt ? Vector3.Lerp(a, b, Mathf.Max(0, u - 0.35f)) : a;
                var cam = transform.InverseTransformPoint(_root.Rig.Cam.transform.position);
                var side = Vector3.Cross((head - tail).normalized, (cam - (head + tail) * 0.5f).normalized).normalized * (bolt ? 0.09f : 0.05f);
                mesh.Clear();
                mesh.vertices = new[] { tail - side, tail + side, head - side, head + side };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1) };
                mesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
                mesh.RecalculateBounds();
                m.SetFloat("_Fade", bolt ? 1 : 1 - u * u);
                yield return null;
            }
            Destroy(go);
            Flash(b, c * 0.8f, 0.9f, 0.3f);
        }

        IEnumerator Destroyed(int id, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(id, out v)) yield break;
            _root.Audio.Play("explosion");
            var p = v.T.localPosition + Vector3.up * 0.4f;
            Flash(p, new Color(1f, 0.55f, 0.25f) * 5, 2.2f, 0.7f * k);
            float t = 0, total = 0.8f * k;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Dissolve", Mathf.Clamp01(t / total));
                block.SetFloat("_Damage", 1);
                foreach (var mr in v.Model.GetComponentsInChildren<MeshRenderer>()) mr.SetPropertyBlock(block);
                yield return null;
            }
            v.Dead = true;
            v.T.gameObject.SetActive(false);
        }

        void Flash(Vector3 p, Color c, float size, float seconds)
        {
            var lib = ArtLibrary.I;
            var go = Mesh("Flash", MeshFactory.Quad(), new Material(lib.corona));
            var m = go.GetComponent<MeshRenderer>().sharedMaterial;
            m.SetColor("_Color", c); m.SetFloat("_StarRadius", 0.05f); m.SetFloat("_Intensity", 1.2f);
            go.transform.localPosition = p; go.transform.localScale = Vector3.one * size;
            StartCoroutine(FadeOut(go, m, seconds));
        }

        IEnumerator FadeOut(GameObject go, Material m, float seconds)
        {
            float t = 0, i0 = m.GetFloat("_Intensity");
            while (t < seconds) { t += Time.unscaledDeltaTime; m.SetFloat("_Intensity", i0 * (1 - t / seconds)); yield return null; }
            Destroy(go);
        }

        public readonly List<Floater> Floaters = new List<Floater>();
        public struct Floater { public Vector3 Pos; public string Text; public Color Color; public float Born; }
        void Float(Vector3 p, string text, Color c) { Floaters.Add(new Floater { Pos = transform.TransformPoint(p + Vector3.up * 0.9f), Text = text, Color = c, Born = Time.unscaledTime }); }
    }
}