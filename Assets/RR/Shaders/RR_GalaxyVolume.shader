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
            CBUFFER_START(UnityPerMaterial)
                float _Steps, _Emission, _Extinction;
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
                int n = (int)_Steps;
                float dt = len / n;
                float jitter = RR_Rand(RR_Hash((uint)(suv.x * 4096) * 1973U + (uint)(suv.y * 4096) * 9277U + (uint)(_RR_Time * 60) * 26699U));
                float t = hit.x + dt * jitter;
                float3 sum = 0;
                float trans = 1;
                float3 scale = length(TransformObjectToWorld(float3(1, 0, 0)) - TransformObjectToWorld(float3(0, 0, 0))).xxx;
                float worldDt = dt * length(TransformObjectToWorldDir(rdir, false));
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
                    // Gas right in front of a close camera fades out instead of becoming a wall.
                    float nearFade = saturate(t * (worldDt / max(dt, 1e-5)) / 22.0);
                    s *= nearFade * nearFade;
                    float ext = s.a * _Extinction * worldDt * 0.06;
                    float st = exp(-ext);
                    sum += s.rgb * _Emission * worldDt * 0.02 * trans;
                    trans *= st;
                    if (trans < 0.01) break;
                    t += dt;
                }
                return float4(sum, trans);
            }
            ENDHLSL
        }
    }
}