using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    public sealed class UnitView
    {
        public int Id;
        public int Owner;
        public string DefId;
        public Transform T, Model, Ring, LeaderRing;
        public Material RingMat;
        public bool Dead;
        public float Bob;
    }

    // Orbital Commandfall in the same universe as the strategy layer. The rules (legality, previews, resolution, AI)
    // are the vendored Commandfall engine; this class only renders state, reads input and submits commands.
    public sealed partial class BattlePresentation : MonoBehaviour
    {
        public const float Hex = 1.0f;
        /// <summary>The authoritative battle: every command, the player's and the AI's, goes through it.</summary>
        public BattleSession Session { get; private set; }
        public MatchState S { get { return Session.State; } }
        public BattleSetup Setup { get { return Session.Setup; } }
        public int PlayerSeat { get { return Setup.PlayerSeat; } }
        public bool Animating { get; private set; }
        public bool Over { get { return S.IsOver && !Animating; } }
        public event Action<string, string> Notice;          // (text, kind)
        public readonly Dictionary<int, UnitView> Units = new Dictionary<int, UnitView>();
        public int Selected { get; private set; }
        public int AbilityOption;
        public readonly List<Commandfall.Core.Hex> Picks = new List<Commandfall.Core.Hex>();
        public bool AbilityMode { get; private set; }
        public bool ShowThreat;
        public float AnimSpeed = 1f;
        public IReadOnlyList<Command> History { get { return Session.Log; } }
        public Commandfall.Core.Hex? HoverHex { get; private set; }
        public string PreviewText { get; private set; }

        GameRoot _root;
        StarSystem _sys;
        Material _gridMat;
        Texture2D _cells;
        readonly Dictionary<Commandfall.Core.Hex, GameObject> _barriers = new Dictionary<Commandfall.Core.Hex, GameObject>();
        readonly List<Transform> _objectiveRings = new List<Transform>();
        RosterAI _ai;
        float _aiDelay;
        Vector3 _starDir;
        public Vector3 StarDir { get { return _starDir; } }

        public static Vector3 World(Commandfall.Core.Hex h) { return new Vector3(1.7320508f * (h.Q + h.R * 0.5f) * Hex, 0, -1.5f * h.R * Hex); }

        public static Commandfall.Core.Hex FromWorld(Vector3 p)
        {
            float r = -p.z / (1.5f * Hex);
            float q = p.x / (1.7320508f * Hex) - r * 0.5f;
            float s = -q - r;
            int rq = Mathf.RoundToInt(q), rr = Mathf.RoundToInt(r), rs = Mathf.RoundToInt(s);
            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs; else if (dr > ds) rr = -rq - rs;
            return new Commandfall.Core.Hex(rq, rr);
        }

        public static BattlePresentation Build(GameRoot root, BattleSession session, StarSystem sys, Transform parent)
        {
            var go = new GameObject("Orbital battle");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BattlePresentation>();
            b._root = root; b.Session = session; b._sys = sys;
            b._ai = new RosterAI(AIDifficulty.Tactical, 1729);
            b.Construct();
            return b;
        }

        void Construct()
        {
            _starDir = new Vector3(-0.85f, 0.32f, 0.25f).normalized;
            BuildBackdrop();
            BuildGrid();
            BuildTerrain();
            foreach (var u in S.Units.Where(u => u.Alive)) CreateUnitView(u);
            SyncAll(true);
        }

        void BuildBackdrop()
        {
            var lib = ArtLibrary.I;
            // The system's principal world fills the lower sky: real planetary curvature beneath the engagement.
            var host = _sys.Planets.FirstOrDefault(p => p.HasShipyard) ?? _sys.Planets.FirstOrDefault(p => p.OwnerId != null) ?? _sys.Planets.FirstOrDefault();
            if (host != null)
            {
                PlanetTextures tex;
                if (!PlanetBaker.TryGet(host.Id, out tex)) { var job = PlanetBaker.Start(host, 512); job.Work.Wait(); tex = PlanetBaker.Finish(job); }
                var m = new Material(lib.planet) { name = "Battle planet" };
                m.SetTexture("_AlbedoCube", tex.Albedo); m.SetTexture("_NormalCube", tex.Normal); m.SetTexture("_DetailCube", tex.Detail); m.SetTexture("_CloudCube", tex.Clouds);
                bool airy = host.Kind == PlanetKind.Crystal || host.Kind == PlanetKind.Terran || host.Kind == PlanetKind.Ocean || host.Kind == PlanetKind.Toxic;
                m.SetFloat("_CloudAmount", airy ? 0.8f : 0f);
                m.SetColor("_AtmoColor", host.Kind == PlanetKind.Toxic ? new Color(0.55f, 0.75f, 0.3f) : new Color(0.36f, 0.62f, 1f)); m.SetFloat("_AtmoStrength", airy ? 1f : 0.2f);
                m.SetColor("_CityColor", new Color(0.55f, 0.85f, 1f)); m.SetFloat("_Glint", host.Kind == PlanetKind.Crystal ? 1.2f : 0);
                m.SetColor("_EmisColor", host.Kind == PlanetKind.Toxic ? new Color(0.65f, 0.25f, 1f) * 0.8f : new Color(1f, 0.35f, 0.06f) * 2f);
                var planet = Mesh("Planet below", MeshFactory.Sphere(128), m);
                // Off to the right and behind the board: a lit crescent frames the encounter without becoming its floor.
                planet.transform.localPosition = new Vector3(300, -120, 480);
                planet.transform.localScale = Vector3.one * 260;
                planet.transform.localRotation = Quaternion.Euler(18, 40, 0);
                if (airy)
                {
                    var am = new Material(lib.atmosphere); am.SetColor("_AtmoColor", new Color(0.36f, 0.62f, 1f)); am.SetFloat("_Intensity", 1.6f);
                    am.SetFloat("_PlanetRadius", 1f / 1.03f);
                    var atmo = Mesh("Atmosphere", MeshFactory.Sphere(96), am);
                    atmo.transform.localPosition = planet.transform.localPosition; atmo.transform.localScale = Vector3.one * 260 * 1.03f;
                    atmo.transform.localRotation = planet.transform.localRotation;
                }
            }
            var star = Mesh("Star", MeshFactory.Sphere(32), new Material(lib.starSurface));
            var sm = star.GetComponent<MeshRenderer>().sharedMaterial;
            sm.SetColor("_Color", Look.StarColor(_sys.Star)); sm.SetFloat("_Intensity", 1.8f);
            star.transform.localPosition = _starDir * 2600; star.transform.localScale = Vector3.one * 34;
            var cor = Mesh("Corona", MeshFactory.Quad(), new Material(lib.corona));
            var cm = cor.GetComponent<MeshRenderer>().sharedMaterial; cm.SetColor("_Color", Look.StarColor(_sys.Star)); cm.SetFloat("_StarRadius", 0.22f); cm.SetFloat("_Intensity", 0.6f);
            cor.transform.localPosition = star.transform.localPosition; cor.transform.localScale = Vector3.one * 34 * 4.5f;
            if (_sys.HasStation)
            {
                var st = lib.Model("station_silex_citadel");
                if (st != null)
                {
                    var g = Instantiate(st, transform);
                    SystemPresentation.ApplyShipMaterial(g, "station_silex_citadel");
                    g.transform.localPosition = new Vector3(-34, 3, 30); g.transform.localScale = Vector3.one * 3.2f;
                    g.transform.localRotation = Quaternion.Euler(0, 30, 6);
                }
            }
            // Sparse debris beyond the playable area only (it never sits on legal movement space).
            var rocks = new[] { "asteroid_0", "asteroid_1", "asteroid_2", "asteroid_3" }.Select(lib.Model).Where(x => x != null).ToArray();
            var rnd = new System.Random(_sys.Seed);
            for (int i = 0; i < 46 && rocks.Length > 0; i++)
            {
                // Behind and beside the board only, and below its plane, so debris never crowds the camera or legal space.
                float a = Mathf.Lerp(0.15f, Mathf.PI - 0.15f, (float)rnd.NextDouble()), rad = 17f + (float)rnd.NextDouble() * 24f;
                var p = new Vector3(Mathf.Cos(a) * rad * 1.2f, -1.5f - (float)rnd.NextDouble() * 5f, 6f + Mathf.Sin(a) * rad * 0.8f);
                var go = Instantiate(rocks[rnd.Next(rocks.Length)], transform);
                SystemPresentation.ApplyShipMaterial(go, "asteroid");
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 360, 0);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.6f, Mathf.Pow((float)rnd.NextDouble(), 2));
            }
        }

        GameObject Mesh(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void BuildGrid()
        {
            var lib = ArtLibrary.I;
            _cells = new Texture2D(32, 16, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Cells" };
            _gridMat = new Material(lib.hexGrid) { name = "Hex grid" };
            _gridMat.SetTexture("_Cells", _cells);
            _gridMat.SetFloat("_HexSize", Hex);
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(plane.GetComponent<Collider>());
            plane.name = "Command overlay";
            plane.transform.SetParent(transform, false);
            plane.transform.localRotation = Quaternion.Euler(90, 0, 0);
            plane.transform.localPosition = new Vector3(0, 0.01f, 0);
            plane.transform.localScale = new Vector3(30, 18, 1);
            plane.GetComponent<MeshRenderer>().sharedMaterial = _gridMat;
            RefreshCells();
        }

        void BuildTerrain()
        {
            var lib = ArtLibrary.I;
            var rnd = new System.Random(7);
            var rocks = new[] { "asteroid_0", "asteroid_1", "asteroid_2", "asteroid_3" }.Select(lib.Model).Where(x => x != null).ToArray();
            foreach (var h in S.Map.AllHexes)
            {
                var t = S.Map.Terrain(h);
                var c = World(h);
                switch (t)
                {
                    case TerrainType.Cover:
                        for (int i = 0; i < 4 && rocks.Length > 0; i++)
                        {
                            var go = Instantiate(rocks[rnd.Next(rocks.Length)], transform);
                            SystemPresentation.ApplyShipMaterial(go, "asteroid");
                            go.transform.localPosition = c + new Vector3((float)(rnd.NextDouble() - 0.5) * 1.1f, (float)rnd.NextDouble() * 0.5f - 0.1f, (float)(rnd.NextDouble() - 0.5) * 1.0f);
                            go.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 360, 0);
                            go.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.26f, (float)rnd.NextDouble());
                        }
                        break;
                    case TerrainType.Elevated:
                        Puff(c, new Color(0.55f, 0.32f, 0.95f) * 0.5f, 1.25f, 0.9f, 0);
                        break;
                    case TerrainType.Hazard:
                        Puff(c, new Color(0.3f, 0.75f, 1.0f) * 0.55f, 1.05f, 1.1f, 1);
                        break;
                    case TerrainType.Obstacle:
                        var w = lib.Model("wreck_hulk_a");
                        if (w != null)
                        {
                            var go = Instantiate(w, transform);
                            SystemPresentation.ApplyShipMaterial(go, "wreck");
                            go.transform.localPosition = c + Vector3.up * 0.15f;
                            go.transform.localRotation = Quaternion.Euler(8, (float)rnd.NextDouble() * 360, 12);
                            go.transform.localScale = Vector3.one * 0.62f;
                        }
                        break;
                }
            }
            foreach (var o in S.Objectives)
            {
                var ring = Mesh("Objective", MeshFactory.Circle(0.8f, 0.07f, 64), Look.Unlit(lib.fx, Look.Gold * 1.4f, "Objective"));
                ring.transform.localPosition = World(o.Hex) + Vector3.up * 0.03f;
                _objectiveRings.Add(ring.transform);
            }
        }

        void Puff(Vector3 c, Color col, float radius, float density, float flicker)
        {
            var lib = ArtLibrary.I;
            if (lib.haze == null) return;
            var m = new Material(lib.haze); m.SetColor("_Color", col); m.SetFloat("_Density", density); m.SetFloat("_Flicker", flicker);
            var go = Mesh("Haze", MeshFactory.Sphere(16), m);
            go.transform.localPosition = c + Vector3.up * 0.25f;
            go.transform.localScale = new Vector3(radius, radius * 0.6f, radius);
        }

        void CreateUnitView(UnitState u)
        {
            var lib = ArtLibrary.I;
            var root = new GameObject(u.Def.Name + " #" + u.Id).transform;
            root.SetParent(transform, false);
            var model = lib.Model(u.DefId) ?? lib.Model(u.Owner == 0 ? "silex_prism_escort" : "craigon_scuttler_corvette");
            var m = Instantiate(model, root);
            SystemPresentation.ApplyShipMaterial(m, u.DefId);
            float len = 1f;
            var mf = m.GetComponentInChildren<MeshFilter>();
            if (mf != null) len = Mathf.Max(mf.sharedMesh.bounds.size.z, mf.sharedMesh.bounds.size.x);
            float scale = Mathf.Clamp(1.75f / Mathf.Max(0.4f, len), 0.5f, 1.6f) * (u.R.IsCapital ? 1.1f : u.R.IsSmallCraft ? 0.85f : 1f);
            m.transform.localScale = Vector3.one * scale;
            m.transform.localPosition = new Vector3(0, 0.32f, 0);
            var fleetId = Setup.FleetIds[u.Owner];
            var col = Look.PolityColor(_root.G, _root.G.GetFleet(fleetId) != null ? _root.G.GetFleet(fleetId).OwnerId : null);
            var ringMat = Look.Unlit(lib.fx, col * 1.1f, "Owner ring");
            var ring = Mesh("Ring", MeshFactory.Circle(0.66f, 0.06f, 48), ringMat);
            ring.transform.SetParent(root, false);
            ring.transform.localPosition = new Vector3(0, 0.04f, 0);
            Transform leader = null;
            if (S.Players[u.Owner].CommanderId == u.Id)
            {
                var lr = Mesh("Leader ring", MeshFactory.Circle(0.8f, 0.05f, 48), Look.Unlit(lib.fx, Look.GoldBright * 1.6f, "Leader ring"));
                lr.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Dash", 12);
                lr.transform.SetParent(root, false);
                lr.transform.localPosition = new Vector3(0, 0.05f, 0);
                leader = lr.transform;
            }
            root.localPosition = World(u.Position);
            root.localRotation = Quaternion.Euler(0, u.Owner == 0 ? 0 : 180, 0);
            Units[u.Id] = new UnitView { Id = u.Id, Owner = u.Owner, DefId = u.DefId, T = root, Model = m.transform, Ring = ring.transform, RingMat = ringMat, LeaderRing = leader, Bob = (u.Id * 0.37f) % 6.28f };
        }    }
}
