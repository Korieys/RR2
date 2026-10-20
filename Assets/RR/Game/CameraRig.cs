using UnityEngine;

namespace RR.Game
{
    public struct CamPose
    {
        public Vector3 Focus;
        public float Yaw, Pitch, Distance, Fov;
        public static CamPose Lerp(CamPose a, CamPose b, float t)
        {
            return new CamPose
            {
                Focus = Vector3.LerpUnclamped(a.Focus, b.Focus, t), Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, t), Pitch = Mathf.LerpUnclamped(a.Pitch, b.Pitch, t),
                // Distance interpolates in log space so a change of scale reads as a smooth zoom, not a lurch.
                Distance = Mathf.Exp(Mathf.LerpUnclamped(Mathf.Log(a.Distance), Mathf.Log(b.Distance), t)), Fov = Mathf.LerpUnclamped(a.Fov, b.Fov, t),
            };
        }
    }

    public struct CamLimits
    {
        public float MinDistance, MaxDistance, MinPitch, MaxPitch;
        public Bounds FocusBounds;
    }

    // The single owner of the main camera. Gameplay code asks it for poses; it damps input and plays transitions.
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public CamPose Target;
        public CamPose Current;
        public CamLimits Limits;
        public bool Transitioning => _tween;
        public float Near = 0.05f, Far = 6000f;

        bool _tween;
        CamPose _from, _to;
        float _t, _dur;
        System.Action _onDone;
        Vector3 _focusVel;
        float _distVel, _yawVel, _pitchVel;
        Vector3 _panVel;        // world units per second, carried after a drag ends (glide)
        bool _pannedThisFrame;

        /// <summary>
        /// Optional zoom-linked pitch (galaxy view): the camera looks down steeply from far out and flattens as it closes in,
        /// so the disk shows its depth. Zooming shifts the pitch by the change in this curve, so a pitch the player chose by
        /// orbiting is kept as an offset.
        /// </summary>
        public System.Func<float, float> PitchForDistance;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.Current = rig.Target = new CamPose { Focus = Vector3.zero, Yaw = 30, Pitch = 35, Distance = 100, Fov = 45 };
            rig.Limits = new CamLimits { MinDistance = 1, MaxDistance = 5000, MinPitch = 8, MaxPitch = 85, FocusBounds = new Bounds(Vector3.zero, Vector3.one * 1e5f) };
            return rig;
        }

        void ResetVelocity() { _focusVel=_panVel=Vector3.zero; _distVel=_yawVel=_pitchVel=0; _pannedThisFrame=false; }

        public void Snap(CamPose p) { _tween = false; _onDone=null; ResetVelocity(); Target = Current = Clamp(p); Apply(); }

        public void TransitionTo(CamPose to, float seconds, System.Action done = null)
        {
            if (Settings.ReducedMotion) seconds = Mathf.Min(seconds, 0.12f);
            ResetVelocity();
            _from = Current; _to = Clamp(to); _t = 0; _dur = Mathf.Max(0.01f, seconds); _tween = true; _onDone = done;
        }

        // Interrupt a transition (player input, Escape): jump to its end so state and camera agree.
        public void FinishTransition()
        {
            if (!_tween) return;
            _tween = false;
            Target = Current = _to; ResetVelocity();
            Apply();
            var d = _onDone; _onDone = null; d?.Invoke();
        }

        public CamPose Clamp(CamPose p)
        {
            p.Distance = Mathf.Clamp(p.Distance, Limits.MinDistance, Limits.MaxDistance);
            p.Pitch = Mathf.Clamp(p.Pitch, Limits.MinPitch, Limits.MaxPitch);
            p.Focus = Limits.FocusBounds.ClosestPoint(p.Focus);
            return p;
        }

        public void Orbit(float dyaw, float dpitch) { Target.Yaw += dyaw; Target.Pitch = Mathf.Clamp(Target.Pitch + dpitch, Limits.MinPitch, Limits.MaxPitch); }
        public void Zoom(float factor)
        {
            float before = Target.Distance;
            Target.Distance = Mathf.Clamp(Target.Distance * factor, Limits.MinDistance, Limits.MaxDistance);
            if (PitchForDistance != null)
                Target.Pitch = Mathf.Clamp(Target.Pitch + PitchForDistance(Target.Distance) - PitchForDistance(before), Limits.MinPitch, Limits.MaxPitch);
        }
        public void Pan(Vector2 screenDelta)
        {
            // Pan on the reference plane, scaled by distance so it feels constant on screen.
            var rot = Quaternion.Euler(0, Target.Yaw, 0);
            float k = Target.Distance * 0.0016f;
            var step = rot * new Vector3(-screenDelta.x * k, 0, -screenDelta.y * k);
            Target.Focus += step;
            Target.Focus = Limits.FocusBounds.ClosestPoint(Target.Focus);
            // Track the drag speed (smoothed) so the view can glide on briefly when the drag ends.
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-3f);
            _panVel = Vector3.ClampMagnitude(Vector3.Lerp(_panVel, step / dt, 0.35f), Target.Distance * 0.9f);   // at most a glide of ~0.14 x distance
            _pannedThisFrame = true;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (_tween)
            {
                _t = Settings.ReducedMotion ? 1 : _t + dt / _dur;
                float e = _t >= 1 ? 1 : Smoother(_t);
                Current = CamPose.Lerp(_from, _to, e);
                // Resolve subject acquisition just before the dolly settles, preserving exact endpoint framing.
                if(!Settings.ReducedMotion) Current.Focus=Vector3.Lerp(_from.Focus,_to.Focus,MotionCurve.Smooth(_t/.9f));
                Target = Current;
                if (_t >= 1)
                {
                    _tween = false;
                    Current = Target = _to; ResetVelocity();
                    var d = _onDone; _onDone = null; d?.Invoke();
                }
            }
            else
            {
                // Glide: after a drag, the focus keeps moving and eases out over about 0.15 s (not with reduced motion).
                if (!_pannedThisFrame && _panVel.sqrMagnitude > 1e-4f)
                {
                    if (Settings.ReducedMotion) _panVel = Vector3.zero;
                    else
                    {
                        Target.Focus = Limits.FocusBounds.ClosestPoint(Target.Focus + _panVel * dt);
                        _panVel *= Mathf.Exp(-dt / 0.15f);
                    }
                }
                _pannedThisFrame = false;
                const float tau = 0.09f;   // short damping: immediate response, no fighting inertia
                Current.Focus = Vector3.SmoothDamp(Current.Focus, Target.Focus, ref _focusVel, tau, Mathf.Infinity, dt);
                Current.Distance = Mathf.SmoothDamp(Current.Distance, Target.Distance, ref _distVel, tau * 1.8f, Mathf.Infinity, dt);   // zoom eases a little longer
                Current.Yaw = Mathf.SmoothDampAngle(Current.Yaw, Target.Yaw, ref _yawVel, tau, Mathf.Infinity, dt);
                Current.Pitch = Mathf.SmoothDamp(Current.Pitch, Target.Pitch, ref _pitchVel, tau, Mathf.Infinity, dt);
                Current.Fov = Mathf.Lerp(Current.Fov, Target.Fov, 1 - Mathf.Exp(-dt / tau));
            }
            Apply();
        }

        static float Smoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

        public void Apply()
        {
            var rot = Quaternion.Euler(Current.Pitch, Current.Yaw, 0);
            transform.position = Current.Focus - rot * Vector3.forward * Current.Distance;
            transform.rotation = rot;
            Cam.fieldOfView = Current.Fov;
            // Keep depth precision proportional to the viewing distance.
            Cam.nearClipPlane = Mathf.Max(Near, Current.Distance * 0.01f);
            Cam.farClipPlane = Mathf.Max(Far, Current.Distance * 40f);
        }
    }
}