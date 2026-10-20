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
                    foreach (var mr in v.Model.GetComponentsInChildren<Renderer>()) mr.SetPropertyBlock(block);
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

        public void ToggleAbility()
        {
            var u = Selected != 0 ? S.Unit(Selected) : null;
            if (u == null) { Say("Select one of your units first.", "bad"); return; }
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
            if (u == null) { Say("Select a unit to hold position with.", "bad"); return; }
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
            var v = RulesEngine.Validate(S, c);
            if (!v.Ok) { Say(v.Reason, "bad"); _root.Audio.Play("ui_invalid"); return; }
            var res = RulesEngine.Apply(S, c);
            if (!res.Accepted) { Say(res.Reason, "bad"); _root.Audio.Play("ui_invalid"); return; }
            History.Add(c.Clone());
            if (c.Seat == PlayerSeat) _root.Audio.Play("order_accepted", 0.7f);
            AbilityMode = false; Picks.Clear();
            Selected = S.ActiveSeat == PlayerSeat && S.ActiveUnitId != 0 ? S.ActiveUnitId : 0;
            StartCoroutine(Play(res.Events));
        }

        void Update()
        {
            float t = Time.unscaledTime;
            foreach (var v in Units.Values) if (!v.Dead)
            {
                if(v.Drive!=null){v.Drive.emitting=v.MotionOwned && !v.Ground && !Settings.ReducedMotion;if(Settings.ReducedMotion)v.Drive.Clear();}
                v.Recoil=Mathf.Lerp(v.Recoil,0,MotionCurve.Damping(13,Time.unscaledDeltaTime));
                float idle=v.Ground || Settings.ReducedMotion || v.MotionOwned ? 0 : Mathf.Sin(t*.65f+v.Bob)*.018f;
                v.Model.localPosition=new Vector3(0,(v.Ground?0:.32f)+idle,Settings.ReducedMotion?0:-v.Recoil);
                v.Model.localRotation=Quaternion.Euler(0,0,Settings.ReducedMotion?0:v.Bank);
            }
            if (S.IsOver || Animating) return;
            if (S.ActiveSeat != PlayerSeat)
            {
                _aiDelay += Time.unscaledDeltaTime * AnimSpeed;
                if (_aiDelay < 0.45f) return;
                _aiDelay = 0;
                var c = _ai.ChooseAction(S);
                var res = RulesEngine.Apply(S, c);
                if (!res.Accepted) { c = Command.Pass(S.ActiveSeat); res = RulesEngine.Apply(S, c); if (!res.Accepted) return; }
                History.Add(c.Clone());
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
                    case GameEventType.AbilityActivated:
                        if (Units.TryGetValue(e.UnitId,out var av) && av.Visual!=null)
                        {
                            av.Visual.Play("Ability",.65f*k);
                            yield return new WaitForSecondsRealtime(.325f*k);
                            var actor=S.Unit(e.UnitId);var effect=actor?.R?.Active!=null?UnitEffects.For(actor.R.Active.Code):UnitEffect.Rally;
                            UnitEffects.Spawn(transform,av.Visual.Socket(false),av.Visual.Energy,.7f,effect);
                            if(Units.TryGetValue(e.TargetUnitId,out var targetView))UnitEffects.Spawn(transform,targetView.Visual!=null?targetView.Visual.Socket(false):targetView.T.position,av.Visual.Energy,.65f,effect);
                            yield return new WaitForSecondsRealtime(.325f*k);
                        }
                        break;
                    case GameEventType.UnitHealed:
                        if(Units.TryGetValue(e.TargetUnitId,out var healed))UnitEffects.Spawn(transform,healed.T.position+Vector3.up*.6f,new Color(.35f,1,.7f),.6f,UnitEffect.Mend);
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
                        MotionBurst.Spawn(transform,World(su != null?su.Position:e.To)+Vector3.up*.4f,new Color(.4f,.75f,1f),.85f,.55f*k,MotionBeat.Shield);
                        break;
                    case GameEventType.DamageApplied:
                        if(Units.TryGetValue(e.TargetUnitId,out var hit) && hit.Visual!=null) {hit.Visual.Play("Hit",.24f*k); UnitEffects.Spawn(transform,hit.Visual.Socket(false),hit.Visual.Energy,.4f,UnitEffect.Impact);}
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
                        MotionBurst.Spawn(transform,World(e.To)+Vector3.up*.05f,Look.GoldBright,.9f,.65f*k,MotionBeat.Objective);
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
            v.MotionOwned=true;
            if(v.Visual!=null)v.Visual.Play("Move");
            int n = path.Count - 1;
            float total = Mathf.Min(1.3f, 0.26f * n) * k;
            var pts = path.Select(World).ToList();
            float len = 0; for (int i = 0; i < n; i++) len += Vector3.Distance(pts[i], pts[i + 1]);
            if(n<=0){v.MotionOwned=false;yield break;}
            var initialHeading=v.T.localRotation;
            var heading=Quaternion.LookRotation((pts[1]-pts[0]).normalized,Vector3.up);
            float anticipation=Settings.ReducedMotion?0:.12f*k, prepare=0;
            while(prepare<anticipation){prepare+=Time.unscaledDeltaTime;v.T.localRotation=Quaternion.Slerp(initialHeading,heading,MotionCurve.Smooth(prepare/anticipation));yield return null;}
            float t = 0;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / total);
                float ease = MotionCurve.Smooth(u);
                float d = ease * len, acc = 0;
                for (int i = 0; i < n; i++)
                {
                    float seg = Vector3.Distance(pts[i], pts[i + 1]);
                    if (d <= acc + seg || i == n - 1)
                    {
                        float f = seg > 0 ? Mathf.Clamp01((d - acc) / seg) : 1;
                        v.T.localPosition = Vector3.Lerp(pts[i], pts[i + 1], f);
                        var dir = pts[i + 1] - pts[i];
                        if (dir.sqrMagnitude > 1e-4f)
                        {
                            var goal=Quaternion.LookRotation(dir.normalized,Vector3.up);
                            float delta=Mathf.DeltaAngle(v.T.localEulerAngles.y,goal.eulerAngles.y);
                            v.Bank=Settings.ReducedMotion?0:Mathf.Lerp(v.Bank,Mathf.Clamp(-delta*.12f,-7,7)*Mathf.Sin(u*Mathf.PI),MotionCurve.Damping(12,Time.unscaledDeltaTime));
                            v.T.localRotation=Quaternion.Slerp(v.T.localRotation,goal,MotionCurve.Damping(12,Time.unscaledDeltaTime));
                        }
                        break;
                    }
                    acc += seg;
                }
                yield return null;
            }
            v.T.localPosition = pts[n];   // arrives exactly on the legal hex
            v.Bank=0;v.MotionOwned=false;
            if(v.Visual!=null){v.Visual.Play("Idle");if(v.Ground)UnitEffects.Spawn(transform,v.T.position+Vector3.up*.06f,v.Visual.Energy,.25f,UnitEffect.Step);}
        }

        IEnumerator Jump(int id, Commandfall.Core.Hex from, Commandfall.Core.Hex to, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(id, out v)) yield break;
            var a = World(from); var b = World(to);
            float total = 0.45f * k, t = 0;
            MotionBurst.Spawn(transform,a+Vector3.up*.4f,new Color(.45f,.75f,1f),.8f,.4f*k,MotionBeat.Jump);
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / total);
                v.T.localPosition = Vector3.Lerp(a, b, MotionCurve.Smooth(u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * (Settings.ReducedMotion?0:.55f);
                yield return null;
            }
            v.T.localPosition = b;
            MotionBurst.Spawn(transform,b+Vector3.up*.4f,new Color(.45f,.75f,1f),.8f,.4f*k,MotionBeat.Jump);
        }

        IEnumerator Fire(int attackerId, Commandfall.Core.Hex target, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(attackerId, out v)) yield break;
            var to = World(target) + Vector3.up * 0.4f;
            var dir = to - v.T.localPosition; dir.y = 0;
            float turn = 0, turnTime = 0.18f * k;
            var q0 = v.T.localRotation; var q1 = Quaternion.LookRotation(dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward, Vector3.up);
            while (turn < turnTime) { turn += Time.unscaledDeltaTime; v.T.localRotation = Quaternion.Slerp(q0, q1, MotionCurve.Smooth(turn / turnTime)); yield return null; }
            var from = v.Visual!=null?v.Visual.Socket(true):v.T.localPosition + Vector3.up * 0.42f + (q1 * Vector3.forward) * 0.5f;
            if(v.Visual!=null)v.Visual.Play("Attack",.55f*k);
            bool craigon = v.DefId.Contains("craigon");
            var col = craigon ? new Color(0.85f, 0.4f, 1f) : new Color(0.45f, 0.75f, 1f);
            yield return new WaitForSecondsRealtime(.209f*k);
            if(v.Visual!=null)from=v.Visual.Socket(true);
            UnitEffects.Spawn(transform,from,col,.32f,UnitEffect.Discharge,to-from);
            v.Recoil=Settings.ReducedMotion?0:.09f;
            _root.Audio.Play(craigon ? "fire_bio" : "fire_crystal", 0.6f);
            if(v.Ground && S.Unit(attackerId).Weapon.MaxRange<=1) { UnitEffects.Spawn(transform,to,col,.5f,UnitEffect.Impact);yield return new WaitForSecondsRealtime(.32f*k); }
            else yield return Beam(from, to, col * 4f, 0.32f * k, craigon);
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
            var owned=go.AddComponent<MotionResourceOwner>();owned.Mesh=mesh;owned.Material=m;
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
            MotionBurst.Spawn(transform,b,c*.35f,.65f,.32f,MotionBeat.Impact);
        }

        IEnumerator Destroyed(int id, float k)
        {
            UnitView v;
            if (!Units.TryGetValue(id, out v)) yield break;
            _root.Audio.Play("explosion");
            var p = v.T.localPosition + Vector3.up * 0.4f;
            if(v.Visual!=null){v.Visual.Play("Death",.8f*k);UnitEffects.Spawn(transform,p,v.Visual.Energy,1.1f,UnitEffect.Shatter);}
            MotionBurst.Spawn(transform,p,new Color(1f,.48f,.17f)*1.8f,1.5f,.85f*k,MotionBeat.Destruction);
            float t = 0, total = 0.8f * k;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Dissolve", MotionCurve.Smooth(t / total));
                v.Bank=Settings.ReducedMotion?0:Mathf.Sin(t/total*Mathf.PI)*12;
                block.SetFloat("_Damage", 1);
                foreach (var mr in v.Model.GetComponentsInChildren<Renderer>()) mr.SetPropertyBlock(block);
                yield return null;
            }
            v.Dead = true;
            v.T.gameObject.SetActive(false);
        }

        void Flash(Vector3 p, Color c, float size, float seconds)
        {
            MotionBurst.Spawn(transform,p,c*.35f,size,seconds,MotionBeat.Impact);
        }

        public readonly List<Floater> Floaters = new List<Floater>();
        public struct Floater { public Vector3 Pos; public string Text; public Color Color; public float Born; }
        void Float(Vector3 p, string text, Color c) { Floaters.Add(new Floater { Pos = transform.TransformPoint(p + Vector3.up * 0.9f), Text = text, Color = c, Born = Time.unscaledTime }); }
    }
}