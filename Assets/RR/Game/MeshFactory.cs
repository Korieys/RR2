using System.Collections.Generic;
using UnityEngine;

namespace RR.Game
{
    // Procedural meshes shared by the presentation layers.
    public static class MeshFactory
    {
        static readonly Dictionary<int, Mesh> _spheres = new Dictionary<int, Mesh>();

        /// <summary>Normalised cube sphere (radius 1): no pole pinching, even texel density for cubemap shading.</summary>
        public static Mesh Sphere(int res)
        {
            if (_spheres.TryGetValue(res, out var cached) && cached != null) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in faces)
            {
                var a = new Vector3(n.y, n.z, n.x);
                var b = Vector3.Cross(n, a);
                int start = verts.Count;
                for (int y = 0; y <= res; y++)
                for (int x = 0; x <= res; x++)
                {
                    float u = x / (float)res * 2 - 1, v = y / (float)res * 2 - 1;
                    var p = n + a * u + b * v;
                    // Equal-angle remap reduces area distortion near the cube corners.
                    var q = new Vector3(p.x * Mathf.Sqrt(1 - p.y * p.y / 2 - p.z * p.z / 2 + p.y * p.y * p.z * p.z / 3),
                                        p.y * Mathf.Sqrt(1 - p.z * p.z / 2 - p.x * p.x / 2 + p.z * p.z * p.x * p.x / 3),
                                        p.z * Mathf.Sqrt(1 - p.x * p.x / 2 - p.y * p.y / 2 + p.x * p.x * p.y * p.y / 3));
                    verts.Add(q.normalized);
                }
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int i = start + y * (res + 1) + x;
                    tris.Add(i); tris.Add(i + res + 1); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + res + 1); tris.Add(i + res + 2);
                }
            }
            var m = new Mesh { name = "RR_Sphere_" + res, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetNormals(verts);
            m.SetTriangles(tris, 0);
            // Make winding outward-facing regardless of the face basis.
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var c = Vector3.Cross(verts[t[i + 1]] - verts[t[i]], verts[t[i + 2]] - verts[t[i]]);
                if (Vector3.Dot(c, verts[t[i]]) < 0) { var s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            }
            m.triangles = t;
            m.RecalculateBounds();
            _spheres[res] = m;
            return m;
        }

        /// <summary>Flat annulus in the XZ plane (object units), finely segmented so it stays round up close.</summary>
        public static Mesh Ring(float inner, float outer, int segments = 256)
        {
            var v = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * inner); v.Add(d * outer);
                if (i < segments)
                {
                    int k = i * 2;
                    tris.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }
            var m = new Mesh { name = "RR_Ring" };
            m.SetVertices(v); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh _quad;
        /// <summary>Unit quad with corners at (-1..1); used by camera-facing glows (the shader orients it).</summary>
        public static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "RR_Quad" };
            _quad.SetVertices(new List<Vector3> { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0), new Vector3(-1, 1, 0) });
            _quad.SetUVs(0, new List<Vector2> { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2.5f);
            return _quad;
        }

        /// <summary>Closed polyline as a thin camera-independent ribbon lying in the XZ plane (orbit lines).</summary>
        public static Mesh Circle(float radius, float width, int segments = 192)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * (radius - width * 0.5f)); v.Add(d * (radius + width * 0.5f));
                uv.Add(new Vector2(i / (float)segments, 0)); uv.Add(new Vector2(i / (float)segments, 1));
                if (i < segments) { int k = i * 2; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            var m = new Mesh { name = "RR_Circle" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tris, 0); m.RecalculateBounds();
            return m;
        }
    }
}