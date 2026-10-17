"""Original procedural audio for the Rule and Ruin slice (numpy only; no samples, no third-party material).

python Tools/Audio/rr_audio.py   ->  Assets/RR/Audio/*.wav
Ambience beds are seamless loops (crossfaded tails); cues are short and restrained.
"""
import os, wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "RR", "Audio")
rng = np.random.default_rng(3104)


def t(sec): return np.arange(int(sec * SR)) / SR


def env(n, a=0.005, d=0.1, s=0.0, r=0.05, sustain_level=0.0):
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    s_n = max(0, n - a_n - d_n - r_n)
    e = np.concatenate([np.linspace(0, 1, max(1, a_n)), np.linspace(1, sustain_level, max(1, d_n)),
                        np.full(s_n, sustain_level), np.linspace(sustain_level, 0, max(1, r_n))])
    return e[:n] if len(e) >= n else np.pad(e, (0, n - len(e)))


def lowpass(x, cutoff):
    # one-pole low-pass, cutoff in Hz (scalar or per-sample array)
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = np.exp(-2 * np.pi * c / SR)
    y = np.empty_like(x); acc = 0.0
    for i in range(len(x)):
        acc = (1 - a[i]) * x[i] + a[i] * acc
        y[i] = acc
    return y


def lowpass_fast(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    from itertools import accumulate
    y = np.empty_like(x); acc = 0.0
    b = 1 - a
    for i, v in enumerate(x):
        acc = b * v + a * acc
        y[i] = acc
    return y


def reverb(x, mix=0.35, decay=0.55, sizes=(1557, 1617, 1491, 1422, 1277, 1356)):
    out = np.zeros(len(x) + SR)
    out[:len(x)] += x
    wet = np.zeros_like(out)
    for s in sizes:
        buf = np.zeros_like(out)
        buf[:len(x)] = x
        for k in range(1, 14):
            off = s * k * 3
            if off >= len(out): break
            buf[off:] += x[:len(out) - off] * (decay ** k) if len(x) > len(out) - off else 0
        wet += buf
    wet /= len(sizes)
    return out * (1 - mix) + wet * mix


def comb_reverb(x, mix=0.3, decay=0.6, delays_ms=(37, 43, 53, 61, 71, 83)):
    n = len(x) + SR
    y = np.zeros(n); y[:len(x)] = x
    wet = np.zeros(n)
    for dm in delays_ms:
        d = int(dm * SR / 1000)
        buf = np.zeros(n); buf[:len(x)] = x
        for i in range(d, n):
            buf[i] += buf[i - d] * decay
        wet += buf
    wet /= len(delays_ms)
    return y * (1 - mix) + lowpass_fast(wet, 5000) * mix


def save(name, x, stereo_spread=0.0):
    x = np.asarray(x, dtype=float)
    peak = np.max(np.abs(x)) + 1e-9
    x = x / peak * 0.85
    if stereo_spread > 0:
        d = int(stereo_spread * SR)
        l = x; r = np.concatenate([np.zeros(d), x[:-d]]) if d > 0 else x
        data = np.stack([l, r], axis=1)
    else:
        data = np.stack([x, x], axis=1)
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print("wrote", name, f"{len(x) / SR:.2f}s")


def tone(f, sec, kind="sine", detune=0.0):
    tt = t(sec)
    ph = 2 * np.pi * f * tt
    if kind == "sine": return np.sin(ph)
    if kind == "tri": return 2 / np.pi * np.arcsin(np.sin(ph))
    if kind == "saw":
        s = np.zeros_like(tt)
        for h in range(1, 18): s += np.sin(ph * h) / h
        return s * 0.6
    return np.sin(ph)


def bell(f, sec, partials=((1, 1), (2.01, 0.5), (3.02, 0.25), (4.17, 0.12))):
    tt = t(sec); s = np.zeros_like(tt)
    for m, a in partials:
        s += a * np.sin(2 * np.pi * f * m * tt) * np.exp(-tt * (3 + m * 2))
    return s


def noise(sec): return rng.standard_normal(int(sec * SR))


def pad(freqs, sec, cutoff=900, lfo=0.05, seed=0):
    tt = t(sec)
    s = np.zeros_like(tt)
    for i, f in enumerate(freqs):
        for d in (-0.18, 0.0, 0.21):
            ph = 2 * np.pi * (f * (1 + d / 100)) * tt + i * 1.3
            s += (np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.12 * np.sin(3 * ph)) / 3
    sweep = cutoff * (1 + 0.5 * np.sin(2 * np.pi * lfo * tt + seed))
    return lowpass(s, sweep)


def loopify(x, fade=3.0):
    n = int(fade * SR)
    head, tail = x[:n], x[-n:]
    w = np.linspace(0, 1, n)
    body = x[n:-n].copy() if len(x) > 2 * n else x[n:]
    mixed = tail * (1 - w) + head * w
    return np.concatenate([mixed, body])


def main():
    os.makedirs(OUT, exist_ok=True)
    # ---------------- cues
    x = bell(880, 0.25) * 0.6 + bell(1320, 0.25) * 0.25
    save("ui_select", x * env(len(x), 0.002, 0.2))
    x = tone(196, 0.22, "tri") * env(int(0.22 * SR), 0.004, 0.18) + tone(185, 0.22, "tri") * 0.6 * env(int(0.22 * SR), 0.004, 0.2)
    save("ui_invalid", lowpass_fast(x, 1500))
    x = np.concatenate([bell(660, 0.14), bell(990, 0.35)])
    save("order_accepted", comb_reverb(x, 0.25, 0.45))
    x = sum(bell(f, 1.2) * a for f, a in ((523, 0.6), (659, 0.45), (784, 0.4), (1046, 0.2)))
    save("build_complete", comb_reverb(x, 0.35, 0.55))
    sw = t(1.4); x = lowpass_fast(noise(1.4), 900) * np.exp(-((sw - 0.5) ** 2) / 0.08) + tone(110, 1.4, "sine") * np.exp(-sw * 2.2) * 0.6
    save("fleet_arrival", comb_reverb(x, 0.3, 0.5))
    tt = t(1.6); x = (tone(98, 1.6, "saw") * 0.5 + tone(146.8, 1.6, "saw") * 0.35) * env(len(tt), 0.05, 1.2, r=0.3, sustain_level=0.2)
    x = lowpass(x, 300 + 1200 * np.exp(-tt * 2)) + bell(1174, 1.6) * 0.2
    save("contact", comb_reverb(x, 0.4, 0.6))
    tt = t(2.2); x = pad([65.4, 98, 130.8], 2.2, cutoff=700) * env(len(tt), 0.4, 1.2, r=0.6, sustain_level=0.5)
    save("battle_prep", comb_reverb(x, 0.35, 0.6))
    tt = t(2.0); x = (tone(55, 2.0, "saw") * 0.6 + tone(82.4, 2.0, "saw") * 0.4) * env(len(tt), 0.01, 1.6, r=0.4, sustain_level=0.0)
    x = lowpass(x, 2500 * np.exp(-tt * 1.4) + 120) + lowpass_fast(noise(2.0), 400) * np.exp(-tt * 3) * 0.6
    save("battle_commit", comb_reverb(x, 0.35, 0.6))
    tt = t(0.6); x = (bell(1568, 0.6) * 0.5 + tone(392, 0.6, "tri") * np.exp(-tt * 5) * 0.6)
    save("guard", comb_reverb(x, 0.3, 0.5))
    tt = t(0.45); x = lowpass_fast(noise(0.45), 1800) * np.exp(-tt * 12) + tone(70, 0.45) * np.exp(-tt * 9) * 0.8
    save("impact", x)
    tt = t(2.4); x = lowpass(noise(2.4), 3000 * np.exp(-tt * 2.5) + 80) * env(len(tt), 0.005, 2.2, r=0.15) + tone(45, 2.4) * np.exp(-tt * 2) * 0.9
    save("explosion", comb_reverb(x, 0.35, 0.6))
    tt = t(0.5); f = 1800 * np.exp(-tt * 3) + 500
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 7) + bell(2093, 0.5) * 0.3
    save("fire_crystal", comb_reverb(x, 0.25, 0.45))
    tt = t(0.5); x = lowpass(noise(0.5), 600 + 900 * np.sin(np.pi * tt / 0.5)) * env(len(tt), 0.03, 0.4, r=0.05) + tone(140, 0.5, "tri") * np.exp(-tt * 6) * 0.5
    save("fire_bio", comb_reverb(x, 0.25, 0.45))
    tt = t(1.1); x = lowpass(noise(1.1), 250 + 300 * np.sin(np.pi * tt / 1.1)) * np.sin(np.pi * tt / 1.1) + tone(60, 1.1) * 0.3 * np.sin(np.pi * tt / 1.1)
    save("engine", x)
    x = bell(784, 0.8) * 0.5 + bell(1175, 0.8) * 0.35
    save("objective", comb_reverb(x, 0.3, 0.5))
    tt = t(1.2); x = (tone(233, 1.2, "tri") * np.sin(2 * np.pi * 6 * tt) * 0.5 + tone(220, 1.2, "tri") * 0.5) * env(len(tt), 0.02, 0.9, r=0.25, sustain_level=0.3)
    save("threat", lowpass_fast(x, 1400))
    x = sum(bell(f, 3.0) * a for f, a in ((392, 0.5), (523, 0.45), (659, 0.4), (784, 0.35), (1046, 0.2)))
    x += pad([130.8, 196, 261.6], 3.0, cutoff=1200) * env(int(3.0 * SR), 0.2, 2.0, r=0.8, sustain_level=0.4) * 0.6
    save("victory", comb_reverb(x, 0.4, 0.65))
    x = pad([110, 130.8, 164.8], 3.0, cutoff=500) * env(int(3.0 * SR), 0.3, 2.0, r=0.7, sustain_level=0.4) + bell(220, 3.0) * 0.3
    save("defeat", comb_reverb(x, 0.4, 0.65))
    tt = t(1.0); x = lowpass(noise(1.0), 200 + 4000 * tt ** 2) * np.sin(np.pi * tt) ** 2
    save("transition_in", x * 0.7)
    tt = t(0.9); x = lowpass(noise(0.9), 200 + 4000 * (1 - tt / 0.9) ** 2) * np.sin(np.pi * tt / 0.9) ** 2
    save("transition_out", x * 0.7)
    # ---------------- ambience beds (seamless loops)
    sec = 48
    x = pad([55, 82.4, 110, 164.8], sec + 3, cutoff=520, lfo=0.021, seed=0.3) * 0.8
    x += pad([220, 329.6], sec + 3, cutoff=900, lfo=0.013, seed=1.7) * 0.18
    x += lowpass_fast(noise(sec + 3), 300) * 0.05
    save("amb_galaxy", loopify(x, 3.0), stereo_spread=0.012)
    x = pad([65.4, 98, 130.8, 196], sec + 3, cutoff=600, lfo=0.017, seed=2.1) * 0.75
    tt = t(sec + 3); x += np.sin(2 * np.pi * 32.7 * tt) * 0.15
    x += lowpass_fast(noise(sec + 3), 220) * 0.06
    save("amb_system", loopify(x, 3.0), stereo_spread=0.014)
    x = pad([49, 73.4, 98, 116.5], sec + 3, cutoff=480, lfo=0.04, seed=0.9) * 0.8
    tt = t(sec + 3); pulse = (np.sin(2 * np.pi * 0.75 * tt) > 0.92).astype(float)
    x += lowpass_fast(pulse * tone(49, sec + 3), 200) * 0.6
    x += lowpass_fast(noise(sec + 3), 260) * 0.06
    save("amb_battle", loopify(x, 3.0), stereo_spread=0.01)


if __name__ == "__main__":
    main()