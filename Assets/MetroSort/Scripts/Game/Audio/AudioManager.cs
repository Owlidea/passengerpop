using UnityEngine;

namespace MetroSort.Game
{
    public enum Sfx
    {
        Click, Select, Invalid, Step, Assign, Match, TrainArrive, Doors, TrainDepart,
        LevelComplete, LevelFailed, Coins, Booster, Achievement
    }

    /// <summary>
    /// Music and sound effects with separate volumes. All clips are synthesised at startup so the game has
    /// complete, original audio without any imported files; drop real clips into Overrides to replace them.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>Optional replacements, indexed by the Sfx enum. Null entries keep the synthesised clip.</summary>
        public AudioClip[] Overrides;
        public AudioClip MusicOverride;

        private AudioSource _music, _sfx;
        private AudioClip[] _clips;
        private float _lastStepTime;

        public float MusicVolume
        {
            get { return _music.volume / 0.5f; }
            set { _music.volume = Mathf.Clamp01(value) * 0.5f; }
        }

        public float SfxVolume
        {
            get { return _sfx.volume; }
            set { _sfx.volume = Mathf.Clamp01(value); }
        }

        public void Init(float musicVolume, float sfxVolume)
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;

            _clips = new AudioClip[System.Enum.GetValues(typeof(Sfx)).Length];
            for (int i = 0; i < _clips.Length; i++)
            {
                AudioClip over = Overrides != null && i < Overrides.Length ? Overrides[i] : null;
                _clips[i] = over != null ? over : SynthClips.Build((Sfx)i);
            }
            _music.clip = MusicOverride != null ? MusicOverride : SynthClips.BuildMusic();
            _music.Play();
        }

        public void Play(Sfx sfx)
        {
            if (_clips == null) return;
            if (sfx == Sfx.Step)
            {
                // Several passengers can arrive in the same frame; one footstep is enough.
                if (Time.unscaledTime - _lastStepTime < 0.05f) return;
                _lastStepTime = Time.unscaledTime;
            }
            AudioClip clip = _clips[(int)sfx];
            if (clip != null) _sfx.PlayOneShot(clip);
        }
    }

    /// <summary>Minimal additive synthesiser used for placeholder audio.</summary>
    public static class SynthClips
    {
        private const int Rate = 22050;

        private enum Wave { Sine, Triangle, Square }

        private sealed class Buffer
        {
            public readonly float[] Data;
            public readonly bool Wrap;
            private uint _noise = 22222;

            public Buffer(float seconds, bool wrap)
            {
                Data = new float[Mathf.CeilToInt(seconds * Rate)];
                Wrap = wrap;
            }

            public void Note(float start, float duration, float freq, float amp, Wave wave, float freqEnd = 0f, float decay = 6f)
            {
                if (freqEnd <= 0f) freqEnd = freq;
                int s0 = (int)(start * Rate);
                int n = (int)(duration * Rate);
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    float u = (float)i / n;
                    float f = Mathf.Lerp(freq, freqEnd, u);
                    phase += f / Rate;
                    float p = (float)(phase - System.Math.Floor(phase));
                    float v;
                    if (wave == Wave.Sine) v = Mathf.Sin(p * 2f * Mathf.PI);
                    else if (wave == Wave.Triangle) v = 4f * Mathf.Abs(p - 0.5f) - 1f;
                    else v = p < 0.5f ? 0.6f : -0.6f;
                    float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (n - i) / (0.01f * Rate));
                    Add(s0 + i, v * amp * env);
                }
            }

            public void Noise(float start, float duration, float amp, float smooth, bool fadeIn)
            {
                int s0 = (int)(start * Rate);
                int n = (int)(duration * Rate);
                float last = 0f;
                for (int i = 0; i < n; i++)
                {
                    _noise ^= _noise << 13; _noise ^= _noise >> 17; _noise ^= _noise << 5;
                    float white = (_noise & 0xFFFF) / 32768f - 1f;
                    last = Mathf.Lerp(white, last, smooth);
                    float u = (float)i / n;
                    float env = fadeIn ? Mathf.Sin(u * Mathf.PI) : (1f - u) * (1f - u);
                    Add(s0 + i, last * amp * env);
                }
            }

            private void Add(int index, float value)
            {
                if (index >= Data.Length)
                {
                    if (!Wrap) return;
                    index %= Data.Length;
                }
                Data[index] += value;
            }

            public AudioClip ToClip(string name)
            {
                for (int i = 0; i < Data.Length; i++) Data[i] = Mathf.Clamp(Data[i], -1f, 1f);
                AudioClip clip = AudioClip.Create(name, Data.Length, 1, Rate, false);
                clip.SetData(Data, 0);
                return clip;
            }
        }

        public static AudioClip Build(Sfx sfx)
        {
            Buffer b;
            switch (sfx)
            {
                case Sfx.Click:
                    b = new Buffer(0.08f, false); b.Note(0f, 0.07f, 880f, 0.35f, Wave.Sine, 660f, 30f); break;
                case Sfx.Select:
                    b = new Buffer(0.12f, false); b.Note(0f, 0.11f, 620f, 0.35f, Wave.Triangle, 980f, 18f); break;
                case Sfx.Invalid:
                    b = new Buffer(0.22f, false); b.Note(0f, 0.2f, 210f, 0.30f, Wave.Square, 150f, 9f); break;
                case Sfx.Step:
                    b = new Buffer(0.07f, false); b.Noise(0f, 0.05f, 0.25f, 0.6f, false); b.Note(0f, 0.05f, 320f, 0.2f, Wave.Sine, 220f, 40f); break;
                case Sfx.Assign:
                    b = new Buffer(0.12f, false); b.Note(0f, 0.11f, 523f, 0.3f, Wave.Triangle, 0f, 20f); break;
                case Sfx.Match:
                    b = new Buffer(0.5f, false);
                    b.Note(0.00f, 0.2f, 523f, 0.3f, Wave.Triangle); b.Note(0.07f, 0.2f, 659f, 0.3f, Wave.Triangle);
                    b.Note(0.14f, 0.2f, 784f, 0.3f, Wave.Triangle); b.Note(0.21f, 0.28f, 1047f, 0.32f, Wave.Triangle);
                    break;
                case Sfx.TrainArrive:
                    b = new Buffer(0.6f, false); b.Noise(0f, 0.55f, 0.22f, 0.92f, true); b.Note(0f, 0.55f, 190f, 0.22f, Wave.Sine, 110f, 2f); break;
                case Sfx.Doors:
                    b = new Buffer(0.22f, false); b.Note(0f, 0.08f, 1175f, 0.22f, Wave.Sine, 0f, 14f); b.Note(0.1f, 0.1f, 880f, 0.22f, Wave.Sine, 0f, 14f); break;
                case Sfx.TrainDepart:
                    b = new Buffer(0.6f, false); b.Noise(0f, 0.55f, 0.18f, 0.92f, true); b.Note(0f, 0.55f, 110f, 0.22f, Wave.Sine, 250f, 2f); break;
                case Sfx.LevelComplete:
                    b = new Buffer(1.3f, false);
                    b.Note(0.00f, 0.3f, 523f, 0.3f, Wave.Triangle); b.Note(0.12f, 0.3f, 659f, 0.3f, Wave.Triangle);
                    b.Note(0.24f, 0.3f, 784f, 0.3f, Wave.Triangle); b.Note(0.36f, 0.9f, 1047f, 0.3f, Wave.Triangle, 0f, 3f);
                    b.Note(0.36f, 0.9f, 1319f, 0.2f, Wave.Sine, 0f, 3f);
                    break;
                case Sfx.LevelFailed:
                    b = new Buffer(0.8f, false);
                    b.Note(0f, 0.25f, 392f, 0.3f, Wave.Triangle); b.Note(0.2f, 0.25f, 330f, 0.3f, Wave.Triangle); b.Note(0.4f, 0.4f, 262f, 0.3f, Wave.Triangle, 0f, 4f);
                    break;
                case Sfx.Coins:
                    b = new Buffer(0.3f, false); b.Note(0f, 0.12f, 1319f, 0.25f, Wave.Sine, 0f, 16f); b.Note(0.09f, 0.2f, 1760f, 0.25f, Wave.Sine, 0f, 12f); break;
                case Sfx.Booster:
                    b = new Buffer(0.3f, false); b.Note(0f, 0.28f, 440f, 0.3f, Wave.Triangle, 900f, 7f); break;
                default: // Achievement
                    b = new Buffer(0.8f, false);
                    b.Note(0f, 0.2f, 784f, 0.28f, Wave.Triangle); b.Note(0.1f, 0.2f, 988f, 0.28f, Wave.Triangle);
                    b.Note(0.2f, 0.2f, 1175f, 0.28f, Wave.Triangle); b.Note(0.3f, 0.5f, 1568f, 0.28f, Wave.Triangle, 0f, 4f);
                    break;
            }
            return b.ToClip("sfx_" + sfx);
        }

        /// <summary>A light, seamlessly looping 8-bar tune. Notes that ring past the end wrap to the start.</summary>
        public static AudioClip BuildMusic()
        {
            const float beat = 0.6f;
            var b = new Buffer(beat * 32f, true);
            // C  Am  F  G, twice. Each row: bass, then three chord tones.
            float[][] chords =
            {
                new[] { 130.8f, 261.6f, 329.6f, 392.0f },
                new[] { 110.0f, 220.0f, 261.6f, 329.6f },
                new[] { 87.3f, 174.6f, 220.0f, 261.6f },
                new[] { 98.0f, 196.0f, 246.9f, 293.7f }
            };
            float[] melody = { 523.3f, 0f, 659.3f, 587.3f, 0f, 523.3f, 440.0f, 0f, 523.3f, 587.3f, 0f, 659.3f, 784.0f, 0f, 659.3f, 587.3f };
            int[] arp = { 1, 2, 3, 2, 1, 3, 2, 3 };
            for (int bar = 0; bar < 8; bar++)
            {
                float[] chord = chords[bar % 4];
                float t0 = bar * 4 * beat;
                b.Note(t0, beat * 1.8f, chord[0], 0.16f, Wave.Sine, 0f, 2f);
                b.Note(t0 + 2 * beat, beat * 1.8f, chord[0], 0.13f, Wave.Sine, 0f, 2f);
                for (int e = 0; e < 8; e++)
                    b.Note(t0 + e * beat * 0.5f, beat * 0.9f, chord[arp[e]] * 2f, 0.055f, Wave.Triangle, 0f, 5f);
                for (int m = 0; m < 2; m++)
                {
                    float f = melody[(bar * 2 + m) % melody.Length];
                    if (f > 0f) b.Note(t0 + m * 2 * beat + beat, beat * 1.6f, f, 0.07f, Wave.Sine, 0f, 3f);
                }
            }
            return b.ToClip("music_loop");
        }
    }
}
