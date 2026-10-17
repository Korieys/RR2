Shader "RR/GalaxyVolume"
{
    // Ray-marched nebular structure of the galactic disk: emission (rgb) and extinction (a) from a baked 3D texture.
    // Rays stop at scene depth, so fleets and markers inside the clouds sort correctly against them.
    Properties
    {
        _Density ("Density (rgb emission, a extinction)", 3D) = "black" {}
        _Steps ("Steps", Range(8, 96)) = 48
        _Emission ("Emission", Range(0, 8)) = 1
        _Extinction ("Extinction", Range(0, 8)) = 1
        _EclipseCenter ("Eclipse centre (object)", Vector) = (0, 0, 0, 0.1)
        _EmissionMax ("Emission encoding range (rgb stores sqrt(e / max))", Float) = 1
        _Clear ("Clearing around the camera focus (xyz world, w radius)", Vector) = (0, 0, 0, 0)
        _ClearStrength ("Clearing strength", Range(0, 1)) = 0
        _Detail ("Detail noise (r billow, g ridges, b slow)", 3D) = "grey" {}
        _DetailScale ("Detail tiles per world unit", Float) = 0.04
        _DetailStrength ("Detail strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One SrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "RR_Common.hlsl"
            TEXTURE3D(_Density); SAMPLER(sampler_Density);
            TEXTURE3D(_Detail); SAMPLER(sampler_Detail);
            CBUFFER_START(UnityPerMaterial)
                float _Steps, _Emission, _Extinction, _EmissionMax, _DetailScale, _DetailStrength, _ClearStrength;
                float4 _Clear;
                float4 _EclipseCenter;
            CBUFFER_END
            struct A { float4 posOS : POSITION; };
            struct V { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float4 screen : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.screen = ComputeScreenPos(o.posCS);
                return o;
            }
            float2 BoxHit(float3 ro, float3 rdir)
            {
                float3 inv = 1.0 / rdir;
                float3 t0 = (-0.5 - ro) * inv, t1 = (0.5 - ro) * inv;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                return float2(max(max(tmin.x, tmin.y), tmin.z), min(min(tmax.x, tmax.y), tmax.z));
            }
            float4 frag(V i) : SV_Target
            {
                float3 camWS = GetCameraPositionWS();
                float3 dirWS = normalize(i.posWS - camWS);
                // Object space ray (box is a unit cube scaled to the galaxy).
                float3 ro = TransformWorldToObject(camWS);
                float3 rdir = normalize(TransformWorldToObjectDir(dirWS));
                float2 hit = BoxHit(ro, rdir);
                hit.x = max(hit.x, 0);
                if (hit.y <= hit.x) return float4(0, 0, 0, 1);
                // Clamp the ray against scene depth (converted to object-space ray length).
                float2 suv = i.screen.xy / i.screen.w;
                float rawD = SampleSceneDepth(suv);
                float eye = LinearEyeDepth(rawD, _ZBufferParams);
                float3 fwd = -UNITY_MATRIX_V[2].xyz;
                float worldT = eye / max(dot(dirWS, fwd), 1e-3);
                float3 hitWS = camWS + dirWS * worldT;
                float objT = length(TransformWorldToObject(hitWS) - ro);
                hit.y = min(hit.y, objT);
                float len = hit.y - hit.x;
                if (len <= 0) return float4(0, 0, 0, 1);
                // Step count follows the world-space path length (about 1.5 units per step), capped at _Steps.
                float pathWorld = len * length(TransformObjectToWorldDir(rdir, false));
                int n = (int)clamp(pathWorld / 1.5, 12.0, _Steps);
                float dt = len / n;
                float jitter = RR_Rand(RR_Hash((uint)(suv.x * 4096) * 1973U + (uint)(suv.y * 4096) * 9277U + (uint)(_RR_Time * 60) * 26699U));
                float t = hit.x + dt * jitter;
                float3 sum = 0;
                float trans = 1;
                float3 scale = length(TransformObjectToWorld(float3(1, 0, 0)) - TransformObjectToWorld(float3(0, 0, 0))).xxx;
                float worldDt = dt * length(TransformObjectToWorldDir(rdir, false));
                float objToWorld = worldDt / max(dt, 1e-6);
                float pixAngle = 2.0 / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);   // world size of one pixel per unit distance
                [loop] for (int k = 0; k < n; k++)
                {
                    float3 p = ro + rdir * t;
                    float3 uvw = p + 0.5;
                    // Subtle environmental motion inside the Eclipse Sector: a slow swirl of its dust.
                    float2 e = uvw.xz - _EclipseCenter.xz;
                    float er = length(e) / max(_EclipseCenter.w, 1e-3);
                    if (er < 1.4)
                    {
                        float ang = (1.4 - er) * 0.06 * sin(_RR_Time * 0.05);
                        float cs = cos(ang), sn = sin(ang);
                        uvw.xz = _EclipseCenter.xz + float2(cs * e.x - sn * e.y, sn * e.x + cs * e.y);
                    }
                    float4 s = SAMPLE_TEXTURE3D_LOD(_Density, sampler_Density, uvw, 0);
                    s.rgb = s.rgb * s.rgb * _EmissionMax;
                    // Gas right in front of a close camera fades out instead of becoming a wall.
                    float nearFade = saturate(t * (worldDt / max(dt, 1e-5)) / 22.0);
                    s *= nearFade * nearFade;
                    // Zoomed in on a fleet or system, the gas thins around the camera focus so units stay clear.
                    if (_ClearStrength > 0)
                    {
                        float2 hp = camWS.xz + dirWS.xz * (t * objToWorld) - _Clear.xz;
                        float clearMask = 1.0 - smoothstep(_Clear.w * 0.45, _Clear.w * 1.35, length(hp));
                        s *= 1.0 - _ClearStrength * clearMask;
                    }
                    float ext = s.a * _Extinction * worldDt * 0.06;
                    float st = exp(-ext);
                    sum += s.rgb * _Emission * worldDt * 0.02 * trans;
                    trans *= st;
                    if (trans < 0.01) break;
                    t += dt;
                }
                // Sub-voxel detail, applied once per pixel where the ray crosses the galactic plane: the emission sits in a
                // thin sheet, so plane-locked detail stays crisp from any angle and costs two samples instead of two per step.
                // Faint gas breaks into frilly wisps; bright cores only vary gently.
                if (_DetailStrength > 0 && sum.r + sum.g + sum.b + (1 - trans) > 0.002)
                {
                    float planeY = TransformObjectToWorld(float3(0, 0, 0)).y;
                    float dy = abs(dirWS.y) > 1e-3 ? dirWS.y : 1e-3;
                    float tp = clamp((planeY - camWS.y) / dy, 0, worldT);
                    float3 pp = camWS + dirWS * tp;
                    float lodA = round(log2(max(1.0, tp * pixAngle * _DetailScale * 64.0)));
                    float lodB = round(log2(max(1.0, tp * pixAngle * _DetailScale * 2.71 * 64.0)));
                    // Two layers rotated off the world axes at a non-integer scale ratio, the fine one warped by the coarse one,
                    // so the 64-texel tile never shows as a grid.
                    // The two layers drift slowly against each other (about 0.3 world units a second), so the wisps evolve
                    // instead of sitting still; reduced motion slows this to a fifth.
                    float drift = _RR_Time * _RR_Motion;
                    float2 qa = float2(0.799 * pp.x - 0.602 * pp.z, 0.602 * pp.x + 0.799 * pp.z) + float2(0.31, 0.12) * drift;
                    float4 da = SAMPLE_TEXTURE3D_LOD(_Detail, sampler_Detail, float3(qa.x, 11.3, qa.y) * _DetailScale, lodA);
                    float2 qb = float2(0.326 * pp.x + 0.945 * pp.z, -0.945 * pp.x + 0.326 * pp.z) + (da.br - 0.5) * 9.0 - float2(0.12, 0.2) * drift;
                    float4 db = SAMPLE_TEXTURE3D_LOD(_Detail, sampler_Detail, float3(qb.x, 4.7, qb.y) * _DetailScale * 2.71, lodB);
                    float lum = max(sum.r, max(sum.g, sum.b)) * 3.0 + (1 - trans) * 1.2;
                    float edge = 1.0 - saturate(lum);
                    float billow = (da.r * 0.65 + db.r * 0.35) * (0.8 + 0.4 * da.b);
                    float inner = 0.35 + 1.3 * billow;
                    float frill = smoothstep(0.42, 0.7, billow) * 2.1;
                    float m = lerp(inner, frill, edge) + (da.g * da.g * da.g * 0.7 + db.g * db.g * db.g * 0.5);
                    m = lerp(1.0, m, _DetailStrength);
                    sum *= m;
                    trans = 1 - saturate((1 - trans) * lerp(1.0, m, 0.5));
                }
                return float4(sum, trans);
            }
            ENDHLSL
        }
    }
}