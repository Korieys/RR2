using System.Collections.Generic;
using UnityEngine;

namespace RR.Game
{
    // Authored sound presentation: restrained cues for events and a spacious ambience bed per scale.
    // Duplicate cues within a short window are suppressed; hovering never plays sound.
    public sealed class AudioService : MonoBehaviour
    {
        readonly List<AudioSource> _pool = new List<AudioSource>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        AudioSource _bedA, _bedB;
        AudioSource _activeBed;
        string _bedName;
        float _bedTarget = 0.5f;

        public static AudioService Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioService>();
            for (int i = 0; i < 10; i++) { var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = 0; a._pool.Add(s); }
            a._bedA = go.AddComponent<AudioSource>(); a._bedB = go.AddComponent<AudioSource>();
            foreach (var b in new[] { a._bedA, a._bedB }) { b.loop = true; b.playOnAwake = false; b.volume = 0; b.spatialBlend = 0; }
            return a;
        }

        float Fx => Settings.Muted ? 0 : Settings.Master * Settings.Effects;
        float Mus => Settings.Muted ? 0 : Settings.Master * Settings.Music;

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = ArtLibrary.I.Clip(name);
            if (clip == null || Fx <= 0) return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out var t) && now - t < 0.08f) return;
            _last[name] = now;
            foreach (var s in _pool)
                if (!s.isPlaying) { s.clip = clip; s.volume = volume * Fx; s.pitch = pitch; s.Play(); return; }
        }

        // Cross-fade to an ambience bed (galaxy, system, battle); the old bed fades out smoothly.
        public void Bed(string name, float volume = 0.55f)
        {
            if (_bedName == name) { _bedTarget = volume; return; }
            var clip = ArtLibrary.I.Clip(name);
            _bedName = name;
            _bedTarget = volume;
            var next = _activeBed == _bedA ? _bedB : _bedA;
            if (clip != null) { next.clip = clip; next.volume = 0; next.time = 0; next.Play(); }
            _activeBed = clip != null ? next : null;
        }

        public void ApplyVolumes() { }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var b in new[] { _bedA, _bedB })
            {
                float target = b == _activeBed ? _bedTarget * Mus : 0;
                b.volume = Mathf.MoveTowards(b.volume, target, dt * 0.35f);
                if (b != _activeBed && b.volume <= 0.001f && b.isPlaying) b.Stop();
            }
        }
    }
}