"""Synthesises the toy-metro train sounds: arriving (rails, brakes, air) and departing (chime, singing motor,
rails, whoosh). Timed to the game's animation: a train pulls in within about 0.4 s and leaves within about 0.5 s
after its doors close (AnimationCoordinator).

Run with Blender's Python (it has numpy):
    /Applications/Blender.app/Contents/Resources/5.3/python/bin/python3.13 Tools/art/train_sfx.py
Output: Assets/MetroSort/Resources/Audio/train_arriving.wav, train_departing.wav (44.1 kHz, 16-bit mono)
"""
import os
import wave
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Audio")
SR = 44100
rng = np.random.default_rng(7)


def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def onepole_low(x, cutoff):
    """Simple low-pass (cutoff in Hz; scalar or per-sample array)."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=np.float64), x.shape)
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1 - a[i]) * x[i] + a[i] * acc
        y[i] = acc
    return y


def band(x, lo, hi):
    return onepole_low(x, hi) - onepole_low(x, lo)


def env(t, points):
    """Piecewise-linear envelope through (time, level) points."""
    ts, vs = zip(*points)
    return np.interp(t, ts, vs)


def bell(t, start, freq, decay=0.9, level=1.0):
    """A soft bell: a few inharmonic partials with an exponential decay, starting at `start` seconds."""
    k = t - start
    on = k >= 0
    k = np.where(on, k, 0)
    attack = np.clip(k / 0.004, 0, 1)
    tone = (np.sin(2 * np.pi * freq * k) + 0.45 * np.sin(2 * np.pi * freq * 2.0 * k) * np.exp(-k * 3)
            + 0.22 * np.sin(2 * np.pi * freq * 3.01 * k) * np.exp(-k * 5) + 0.10 * np.sin(2 * np.pi * freq * 4.2 * k) * np.exp(-k * 8))
    return on * attack * np.exp(-k / decay * 2.2) * tone * level


def clacks(t, times, level=1.0):
    """Wheels over rail joints: each a pair of short knocks (front and rear bogie) with a low thump."""
    out = np.zeros_like(t)
    noise = rng.standard_normal(len(t))
    knock = band(noise, 900, 4200)
    for at in times:
        for dt, lv in ((0.0, 1.0), (0.065, 0.8)):
            k = t - (at + dt)
            on = (k >= 0) & (k < 0.09)
            kk = np.where(on, k, 0)
            out += on * lv * (knock * np.exp(-kk * 70) * 0.9 + np.sin(2 * np.pi * 85 * kk) * np.exp(-kk * 38) * 0.8)
    return out * level


def normalise(x, rms_db=-15.5, peak_db=-1.0):
    x = x - np.mean(x)
    rms = np.sqrt(np.mean(x ** 2))
    x = x * (10 ** (rms_db / 20) / max(rms, 1e-9))
    peak = np.max(np.abs(x))
    limit = 10 ** (peak_db / 20)
    if peak > limit:
        # soft knee: tame the loudest knocks instead of scaling everything down
        x = np.tanh(x / limit) * limit
    return x


def fade(x, seconds=0.06):
    n = int(seconds * SR)
    x[-n:] *= np.linspace(1, 0, n)
    x[:64] *= np.linspace(0, 1, 64)
    return x


def save(name, x):
    os.makedirs(OUT, exist_ok=True)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("saved", name, "%.2fs" % (len(x) / SR))


def arriving():
    t = t_axis(1.45)
    # rumble and motor hum, loud while it rolls in, settling as it stops
    rumble = onepole_low(rng.standard_normal(len(t)), 140) * 3.2
    hum_f = env(t, [(0, 150), (0.42, 92), (1.45, 70)])
    hum = np.sin(2 * np.pi * np.cumsum(hum_f) / SR) + 0.35 * np.sin(4 * np.pi * np.cumsum(hum_f) / SR)
    roll = env(t, [(0, 0.0), (0.06, 0.9), (0.38, 1.0), (0.62, 0.35), (1.45, 0.0)])
    x = (rumble * 0.8 + hum * 0.22) * roll
    # rail joints coming quickly, slowing to a stop
    x += clacks(t, [0.03, 0.15, 0.27, 0.40], level=0.55)
    # brake squeal: a bright whistle that bends down as it bites
    sq = env(t, [(0, 0), (0.28, 0), (0.36, 0.13), (0.52, 0.16), (0.66, 0.0), (1.45, 0)])
    sq_f = env(t, [(0, 2900), (0.30, 2900), (0.62, 2350), (1.45, 2350)]) * (1 + 0.006 * np.sin(2 * np.pi * 9 * t))
    phase = 2 * np.pi * np.cumsum(sq_f) / SR
    x += sq * (np.sin(phase) + 0.3 * np.sin(2 * phase))
    # air brakes letting go: "pssshh"
    hiss = band(rng.standard_normal(len(t)), 2500, 9000) * 1.6
    x += hiss * env(t, [(0, 0), (0.58, 0), (0.61, 0.55), (0.80, 0.30), (1.25, 0.0), (1.45, 0)])
    return fade(normalise(x))


def departing():
    t = t_axis(1.75)
    # doors-closed chime: three bright rising notes, a friendly "all aboard"
    x = 0.55 * (bell(t, 0.00, 784.0, 0.55) + bell(t, 0.11, 988.0, 0.55) + bell(t, 0.22, 1175.0, 0.9))
    # the motor sings up a scale as it pulls away (inverter whine), with its octave
    steps = [0.30, 0.38, 0.46, 0.54, 0.62, 0.70, 0.80]
    notes = [330, 370, 415, 440, 494, 554, 622]
    f = np.full_like(t, notes[0])
    for at, n in zip(steps, notes):
        f = np.where(t >= at, n, f)
    f = onepole_low(f, 18)                         # glide between steps
    ph = 2 * np.pi * np.cumsum(f) / SR
    whine = np.sin(ph) + 0.4 * np.sin(2 * ph) + 0.15 * np.sin(3 * ph)
    x += 0.20 * whine * env(t, [(0, 0), (0.28, 0), (0.36, 1), (1.05, 0.8), (1.5, 0)])
    # rumble and rail joints speeding up, then fading into the tunnel
    rumble = onepole_low(rng.standard_normal(len(t)), 160) * 3.0
    x += 0.75 * rumble * env(t, [(0, 0), (0.25, 0.2), (0.45, 1.0), (1.0, 0.7), (1.6, 0)])
    x += clacks(t, [0.30, 0.56, 0.76, 0.91, 1.03, 1.12, 1.20], level=0.5) * env(t, [(0, 1), (0.9, 1), (1.35, 0.15)])
    # a whoosh as it rushes off
    sweep_hi = env(t, [(0, 600), (0.45, 900), (0.95, 3200), (1.75, 1800)])
    air = rng.standard_normal(len(t))
    whoosh = onepole_low(air, sweep_hi) - onepole_low(air, 300)
    x += 0.9 * whoosh * env(t, [(0, 0), (0.40, 0), (0.80, 0.9), (1.20, 0.4), (1.7, 0)])
    return fade(normalise(x))


if __name__ == "__main__":
    save("train_arriving.wav", arriving())
    save("train_departing.wav", departing())
