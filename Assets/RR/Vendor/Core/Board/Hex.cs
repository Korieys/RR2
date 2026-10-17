using System;
using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>
    /// Axial hex coordinate (pointy-top). Cube coordinates are (Q, R, S) with S = -Q - R.
    /// R grows toward the South (seat 0's side of the board), Q grows toward the East.
    /// </summary>
    [Serializable]
    public readonly struct Hex : IEquatable<Hex>, IComparable<Hex>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public Hex(int q, int r) { Q = q; R = r; }

        public static readonly Hex Zero = new Hex(0, 0);

        /// <summary>Six neighbour directions, clockwise starting East.</summary>
        public static readonly Hex[] Directions =
        {
            new Hex(1, 0),   // E
            new Hex(0, 1),   // SE
            new Hex(-1, 1),  // SW
            new Hex(-1, 0),  // W
            new Hex(0, -1),  // NW
            new Hex(1, -1),  // NE
        };

        public static Hex operator +(Hex a, Hex b) => new Hex(a.Q + b.Q, a.R + b.R);
        public static Hex operator -(Hex a, Hex b) => new Hex(a.Q - b.Q, a.R - b.R);
        public static Hex operator *(Hex a, int k) => new Hex(a.Q * k, a.R * k);
        public static bool operator ==(Hex a, Hex b) => a.Q == b.Q && a.R == b.R;
        public static bool operator !=(Hex a, Hex b) => !(a == b);

        public Hex Neighbor(int direction) => this + Directions[((direction % 6) + 6) % 6];

        public IEnumerable<Hex> Neighbors()
        {
            for (int i = 0; i < 6; i++) yield return this + Directions[i];
        }

        public int Length => (Math.Abs(Q) + Math.Abs(R) + Math.Abs(S)) / 2;

        /// <summary>Doubled column (2Q + R): the hex's East–West position in half-hex steps, 0 on the centre line.</summary>
        public int Lateral => 2 * Q + R;

        public static int Distance(Hex a, Hex b) => (a - b).Length;

        public int DistanceTo(Hex other) => Distance(this, other);

        /// <summary>Point-symmetric reflection through the board centre (used for balanced authored maps).</summary>
        public Hex Rotate180() => new Hex(-Q, -R);

        /// <summary>All hexes within <paramref name="radius"/> of <paramref name="center"/> in deterministic order.</summary>
        public static List<Hex> Spiral(Hex center, int radius)
        {
            var result = new List<Hex>();
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Math.Max(-radius, -q - radius);
                int r2 = Math.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++) result.Add(center + new Hex(q, r));
            }
            result.Sort();
            return result;
        }

        public static List<Hex> Ring(Hex center, int radius)
        {
            var result = new List<Hex>();
            if (radius == 0) { result.Add(center); return result; }
            var hex = center + Directions[4] * radius;
            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    result.Add(hex);
                    hex = hex.Neighbor(side);
                }
            }
            return result;
        }

        /// <summary>
        /// Deterministic hex line using cube lerp. <paramref name="nudge"/> (+1 / -1) offsets the endpoints by a tiny
        /// epsilon so lines passing exactly between two hexes resolve consistently to one side.
        /// Uses fixed-point arithmetic so results are identical on every platform.
        /// </summary>
        public static List<Hex> Line(Hex a, Hex b, int nudge)
        {
            int n = Distance(a, b);
            var results = new List<Hex>(n + 1);
            if (n == 0) { results.Add(a); return results; }
            // Fixed point with scale 1e6; epsilon (1e-4, 2e-4, -3e-4) keeps q+r+s == 0 and dwarfs integer-division error.
            const long scale = 1_000_000;
            long eq = nudge * 100, er = nudge * 200, es = -nudge * 300;
            long aq = a.Q * scale + eq, ar = a.R * scale + er, as_ = a.S * scale + es;
            long bq = b.Q * scale + eq, br = b.R * scale + er, bs = b.S * scale + es;
            for (int i = 0; i <= n; i++)
            {
                long fq = aq + (bq - aq) * i / n;
                long fr = ar + (br - ar) * i / n;
                long fs = as_ + (bs - as_) * i / n;
                results.Add(CubeRound(fq, fr, fs, scale));
            }
            return results;
        }

        private static Hex CubeRound(long fq, long fr, long fs, long scale)
        {
            long rq = RoundDiv(fq, scale), rr = RoundDiv(fr, scale), rs = RoundDiv(fs, scale);
            long dq = Math.Abs(rq * scale - fq), dr = Math.Abs(rr * scale - fr), ds = Math.Abs(rs * scale - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new Hex((int)rq, (int)rr);
        }

        private static long RoundDiv(long value, long scale)
        {
            // Round half away from zero, deterministic for negatives.
            if (value >= 0) return (value + scale / 2) / scale;
            return -((-value + scale / 2) / scale);
        }

        public bool Equals(Hex other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is Hex h && Equals(h);
        public override int GetHashCode() => (Q * 397) ^ R;

        public int CompareTo(Hex other)
        {
            int c = R.CompareTo(other.R);
            return c != 0 ? c : Q.CompareTo(other.Q);
        }

        public override string ToString() => $"({Q},{R})";
    }
}
