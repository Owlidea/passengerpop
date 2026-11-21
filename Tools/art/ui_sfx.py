"""Synthesises the popup sounds: a soft rising "bloop" with a breath of air when a popup opens, a shorter falling
one when it closes. They play on every popup (UiKit.PopIn), so they are kept quiet and short.

Run with Blender's Python (it has numpy):
    /Applications/Blender.app/Contents/Resources/5.3/python/bin/python3.13 Tools/art/ui_sfx.py
Output: Assets/MetroSort/Resources/Audio/popup_open.wav, popup_close.wav
"""
import os
import sys
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from train_sfx import SR, t_axis, env, band, normalise, fade, save

rng = np.random.default_rng(11)


def bloop(t, points, shape):
    f = env(t, points)
    ph = 2 * np.pi * np.cumsum(f) / SR
    return (np.sin(ph) + 0.22 * np.sin(2 * ph) + 0.08 * np.sin(3 * ph)) * env(t, shape)


def popup_open():
    t = t_axis(0.32)
    tone = bloop(t, [(0, 420), (0.08, 860), (0.32, 920)], [(0, 0), (0.006, 1), (0.07, 0.75), (0.30, 0)])
    air = band(rng.standard_normal(len(t)), 900, 5200) * env(t, [(0, 0), (0.04, 0.35), (0.14, 0.18), (0.30, 0)])
    return fade(normalise(0.85 * tone + 0.55 * air, rms_db=-21.0, peak_db=-3.0), 0.03)


def popup_close():
    t = t_axis(0.22)
    tone = bloop(t, [(0, 760), (0.12, 380), (0.22, 350)], [(0, 0), (0.005, 1), (0.05, 0.6), (0.20, 0)])
    air = band(rng.standard_normal(len(t)), 700, 4000) * env(t, [(0, 0), (0.03, 0.25), (0.12, 0.08), (0.20, 0)])
    return fade(normalise(0.85 * tone + 0.45 * air, rms_db=-24.0, peak_db=-4.0), 0.03)


if __name__ == "__main__":
    save("popup_open.wav", popup_open())
    save("popup_close.wav", popup_close())
