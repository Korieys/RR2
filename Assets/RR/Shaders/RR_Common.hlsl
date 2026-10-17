#ifndef RR_COMMON_INCLUDED
#define RR_COMMON_INCLUDED

// Shared helpers for Rule and Ruin slice shaders (original code).
// Global lighting contract: every lit surface in a local system is lit by the visible star, never by a camera light.
float4 _RR_StarPosWS;      // xyz = star position (world), w = star radius
float4 _RR_StarColor;      // rgb = colour * intensity
float4 _RR_Ambient;        // rgb = faint indirect fill, a = rim/bounce strength
float4 _RR_Highlight;      // rgb = selection tint (per draw via material), unused globally
float  _RR_Time;           // presentation time in seconds (paused with reduced motion)

// ---------------------------------------------------------------------------------------- hashing
uint RR_Hash(uint x)
{
    x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
    return x;
}
uint RR_Hash3(int3 p) { return RR_Hash((uint)p.x * 73856093U ^ RR_Hash((uint)p.y * 19349663U ^ RR_Hash((uint)p.z * 83492791U))); }
float RR_Rand(uint h) { return (h & 0x00FFFFFFU) / 16777216.0; }

float3 RR_Grad(int3 c)
{
    uint h = RR_Hash3(c);
    float u = RR_Rand(h) * 2.0 - 1.0;
    float a = RR_Rand(RR_Hash(h)) * 6.2831853;
    float r = sqrt(saturate(1.0 - u * u));
    return float3(r * cos(a), r * sin(a), u);
}

// Gradient noise in [-1, 1] with quintic fade.
float RR_Noise(float3 p)
{
    int3 i = (int3)floor(p);
    float3 f = p - floor(p);
    float3 w = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float n000 = dot(RR_Grad(i + int3(0,0,0)), f - float3(0,0,0));
    float n100 = dot(RR_Grad(i + int3(1,0,0)), f - float3(1,0,0));
    float n010 = dot(RR_Grad(i + int3(0,1,0)), f - float3(0,1,0));
    float n110 = dot(RR_Grad(i + int3(1,1,0)), f - float3(1,1,0));
    float n001 = dot(RR_Grad(i + int3(0,0,1)), f - float3(0,0,1));
    float n101 = dot(RR_Grad(i + int3(1,0,1)), f - float3(1,0,1));
    float n011 = dot(RR_Grad(i + int3(0,1,1)), f - float3(0,1,1));
    float n111 = dot(RR_Grad(i + int3(1,1,1)), f - float3(1,1,1));
    float x00 = lerp(n000, n100, w.x), x10 = lerp(n010, n110, w.x);
    float x01 = lerp(n001, n101, w.x), x11 = lerp(n011, n111, w.x);
    return lerp(lerp(x00, x10, w.y), lerp(x01, x11, w.y), w.z) * 1.6;
}

float RR_Fbm(float3 p, int octaves)
{
    float s = 0, a = 0.5, n = 0;
    [loop] for (int o = 0; o < octaves; o++) { s += RR_Noise(p) * a; n += a; p = p * 2.03 + 17.1; a *= 0.5; }
    return s / n;
}

// Cellular (Worley) distance: x = nearest, y = second nearest.
float2 RR_Cell(float3 p)
{
    int3 i = (int3)floor(p);
    float3 f = p - floor(p);
    float d1 = 8, d2 = 8;
    [unroll] for (int z = -1; z <= 1; z++)
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        int3 c = int3(x, y, z);
        uint h = RR_Hash3(i + c);
        float3 o = float3(RR_Rand(h), RR_Rand(RR_Hash(h)), RR_Rand(RR_Hash(h + 7U)));
        float d = length(float3(c) + o - f);
        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
    }
    return float2(d1, d2);
}

// ---------------------------------------------------------------------------------------- lighting
float3 RR_StarDir(float3 posWS) { return normalize(_RR_StarPosWS.xyz - posWS); }

float RR_GGX(float3 n, float3 v, float3 l, float rough)
{
    float3 h = normalize(v + l);
    float a = max(rough * rough, 0.002);
    float nh = saturate(dot(n, h));
    float d = (nh * nh) * (a * a - 1.0) + 1.0;
    float D = a * a / (3.14159 * d * d);
    float nl = saturate(dot(n, l)), nv = saturate(dot(n, v));
    float k = (rough + 1) * (rough + 1) / 8;
    float G = (nl / (nl * (1 - k) + k)) * (nv / (nv * (1 - k) + k));
    float F = 0.04 + 0.96 * pow(1 - saturate(dot(h, v)), 5);
    return D * G * F / max(4 * nl * nv, 0.001);
}

// Soft terminator: wrap diffuse with a narrow wrap so the terminator stays attached to the star direction.
float RR_Diffuse(float ndl, float wrap) { return saturate((ndl + wrap) / (1 + wrap)); }

float3 RR_ACESish(float3 c) { return c; } // tonemapping is done by URP post-processing

#endif