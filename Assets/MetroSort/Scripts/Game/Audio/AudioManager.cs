using UnityEngine;

namespace MetroSort.Game
{
    public enum Sfx
    {
        Click, Select, Invalid, Step, Assign, Match, TrainArrive, Doors, TrainDepart,
        LevelComplete, LevelFailed, Coins, Booster, Achievement,
        // Appended so the indices above stay stable for Overrides.
        Star, Cheer, CoinTick,
        // Recordings only (no synthesised stand-in): silent when the file is missing.
        BenchSit, GateFlap, Barrier, BoardFlip, ChestOpen, MascotPop, Reveal, StarStamp,
        // Synthesised musical note for the boarding chain (pitched per passenger).
        Board,
        // Popups opening and closing (UiKit.PopIn; Tools/art/ui_sfx.py).
        PopupOpen, PopupClose
    }

    /// <summary>
    /// Music and sound effects with separate volumes. Clips come from Resources/Audio (copied there by
    /// Tools/art/media_import.py); any that is missing falls back to a clip synthesised at startup, so the
    /// game always has complete audio.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        /// <summary>Optional replacements, indexed by the Sfx enum. Null entries keep the default clip.</summary>
        public AudioClip[] Overrides;
        public AudioClip MusicOverride;

        // Indexed by the Sfx enum; null means there is no recording and the synthesised clip is used.
        private static readonly string[] Files =
        {
            "ui_tap", "passenger_select", "error", "footsteps", "passenger_land", "platform_full", "train_arriving", "train_doors", "train_departing",
            "win_jingle", "level_failed", "coins_drop", "booster", "achievement", "star_collect", "crowd_yay", "coin_tick",
            "bench_sit", "gate_flap", "barrier", "board_flip", "chest_open", "mascot_pop", "mystery_reveal", "star_stamp",
            null,
            "popup_open", "popup_close"
        };

        // The mix, indexed by the Sfx enum: the loudness (dBFS of the loudest 50 ms) every clip is brought to when
        // it loads, so a quiet recording and a hot one sit where they belong whatever file is dropped in. Small,
        // frequent sounds sit low; rewards and trains sit high.
        private static readonly float[] Level =
        {
            -23f, -21f, -20f, -24f, -21f, -15f, -17f, -21f, -17f,
            -14f, -16f, -18f, -17f, -16f, -17f, -20f, -24f,
            -20f, -21f, -21f, -22f, -16f, -19f, -18f, -16f,
            -19f,
            -24f, -26f
        };

        // How much each play may wander in pitch (fraction) so repeated sounds never sound copy-pasted. Music-like
        // clips (jingles, chords) stay in tune.
        private static readonly float[] Jitter =
        {
            0.03f, 0.05f, 0.03f, 0.08f, 0.06f, 0f, 0.03f, 0.04f, 0.03f,
            0f, 0f, 0.04f, 0.02f, 0f, 0.03f, 0.04f, 0.05f,
            0.07f, 0.05f, 0.04f, 0.03f, 0f, 0.05f, 0.02f, 0.03f,
            0f,
            0.04f, 0.04f
        };

        // Clips with alternative takes in Resources/Audio (<file>_b, <file>_c): picked in turn, never twice in a row.
        private static readonly Sfx[] WithTakes = { Sfx.Click, Sfx.Select, Sfx.Invalid, Sfx.Step, Sfx.Assign, Sfx.BenchSit, Sfx.Doors, Sfx.CoinTick };

        // Major pentatonic steps: each passenger boarding in quick succession plays the next note up.
        private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };
        private const float ChainWindow = 2.5f;
        private const int Voices = 14;

        private AudioSource _music;
        private AudioSource[] _voices;
        private float[] _voiceStarted;
        private AudioClip[][] _takes;
        private int[] _lastTake;
        private float[] _lastPlay;
        private bool[] _fromFile;
        private AudioClip _menuMusic, _gameMusic, _wanted;
        private float _lastStepTime, _musicVolume, _sfxVolume = 1f, _fade = 1f, _duckUntil, _lastBoard = -10f;
        private int _chain;

        public float MusicVolume
        {
            get { return _musicVolume; }
            set { _musicVolume = Mathf.Clamp01(value); ApplyMusicVolume(); }
        }

        public float SfxVolume
        {
            get { return _sfxVolume; }
            set { _sfxVolume = Mathf.Clamp01(value); }
        }

        public void Init(float musicVolume, float sfxVolume)
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _voices = new AudioSource[Voices];
            _voiceStarted = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
            SfxVolume = sfxVolume;

            int count = System.Enum.GetValues(typeof(Sfx)).Length;
            _takes = new AudioClip[count][];
            _lastTake = new int[count];
            _fromFile = new bool[count];
            _lastPlay = new float[count];
            for (int i = 0; i < _lastPlay.Length; i++) _lastPlay[i] = -10f;
            for (int i = 0; i < count; i++)
            {
                var takes = new System.Collections.Generic.List<AudioClip>();
                AudioClip clip = Overrides != null && i < Overrides.Length ? Overrides[i] : null;
                if (clip == null && i < Files.Length && Files[i] != null)
                {
                    clip = Resources.Load<AudioClip>("Audio/" + Files[i]);
                    _fromFile[i] = clip != null;
                    if (clip != null && System.Array.IndexOf(WithTakes, (Sfx)i) >= 0)
                        foreach (string suffix in new[] { "_b", "_c" })
                        {
                            AudioClip alt = Resources.Load<AudioClip>("Audio/" + Files[i] + suffix);
                            if (alt != null) takes.Add(AudioMix.Normalise(alt, Level[i]));
                        }
                }
                // The cheer only exists as a recording; everything else has a synthesised stand-in.
                if (clip == null && (Sfx)i != Sfx.Cheer && ((Sfx)i < Sfx.BenchSit || (Sfx)i == Sfx.Board)) clip = SynthClips.Build((Sfx)i);
                if (clip != null) takes.Insert(0, AudioMix.Normalise(clip, Level[i]));
                _takes[i] = takes.ToArray();
            }

            _menuMusic = MusicOverride != null ? MusicOverride : Resources.Load<AudioClip>("Audio/menu_theme");
            _gameMusic = MusicOverride != null ? MusicOverride : Resources.Load<AudioClip>("Audio/gameplay_loop");
            if (_menuMusic == null || _gameMusic == null)
            {
                AudioClip synth = SynthClips.BuildMusic();
                if (_menuMusic == null) _menuMusic = synth;
                if (_gameMusic == null) _gameMusic = synth;
            }
            _wanted = _menuMusic;
            _music.clip = _wanted;
            MusicVolume = musicVolume;
            _music.Play();
        }

        /// <summary>Chooses the menu theme or the gameplay loop; the change is cross-faded in Update.</summary>
        public void SetMusic(bool menu)
        {
            _wanted = menu ? _menuMusic : _gameMusic;
        }

        private void ApplyMusicVolume()
        {
            float duck = Time.unscaledTime < _duckUntil ? 0.3f : 1f;
            // 0.9: at the default setting (0.6) the music sits about 6-10 dB under the sound effects. It used to
            // be halved, which left it some 15 dB under them and the menu sounded nearly silent on phones.
            _music.volume = _musicVolume * 0.9f * _fade * duck;
        }

        /// <summary>While true no new track starts (the studio intro plays in silence); released, it fades in.</summary>
        public bool MusicHeld;

        private void Update()
        {
            if (_music == null) return;
            float step = Time.unscaledDeltaTime / 0.3f;
            if (_music.clip != _wanted)
            {
                _fade = Mathf.Max(0f, _fade - step);
                if (_fade <= 0f && !MusicHeld)
                {
                    _music.clip = _wanted;
                    _music.Play();
                }
            }
            else _fade = Mathf.Min(1f, _fade + step);
            ApplyMusicVolume();
        }

        public void Play(Sfx sfx) { Play(sfx, 0f, 0f); }

        /// <summary>World x of something on screen to a stereo position (the station is about ten units wide).</summary>
        public static float PanAt(float worldX) { return Mathf.Clamp(worldX / 5.5f, -0.75f, 0.75f); }

        /// <summary>
        /// A passenger reaches their train: a short note that climbs the scale with each passenger boarded in quick
        /// succession, so a good run sounds like a little melody. Parking on the bench breaks the run.
        /// </summary>
        public void PlayBoarding(float pan, bool fillsTrain)
        {
            float now = Time.unscaledTime;
            _chain = now - _lastBoard > ChainWindow ? 0 : Mathf.Min(_chain + 1, Scale.Length - 1);
            _lastBoard = now;
            Play(Sfx.Step, pan, 0f);
            AudioSource v = Play(Sfx.Board, pan, Scale[_chain]);
            if (v != null && fillsTrain) v.volume *= 1.25f;
        }

        public void BreakChain() { _chain = 0; _lastBoard = -10f; }

        /// <summary>Plays a sound that travels across the stereo field (a train driving in or away).</summary>
        public void PlaySweep(Sfx sfx, float panFrom, float panTo, float seconds)
        {
            AudioSource v = Play(sfx, panFrom, 0f);
            if (v != null) StartCoroutine(Sweep(v, v.clip, panFrom, panTo, seconds));
        }

        private System.Collections.IEnumerator Sweep(AudioSource v, AudioClip clip, float from, float to, float seconds)
        {
            for (float t = 0f; t < 1f && v.isPlaying && v.clip == clip; t += Time.unscaledDeltaTime / Mathf.Max(0.05f, seconds))
            {
                v.panStereo = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
        }

        /// <summary>Plays a sound at a stereo position, shifted by semitones. Returns the voice, or null when skipped.</summary>
        public AudioSource Play(Sfx sfx, float pan, float semitones)
        {
            if (_takes == null) return null;
            AudioClip[] takes = _takes[(int)sfx];
            if (takes.Length == 0) return null;
            bool file = _fromFile[(int)sfx];
            if (sfx == Sfx.Step)
            {
                // Several passengers can arrive in the same frame; one footstep is enough.
                if (Time.unscaledTime - _lastStepTime < (file ? takes[0].length * 0.7f : 0.05f)) return null;
                _lastStepTime = Time.unscaledTime;
            }
            // Several gates or barriers can move together: one sound for the bunch.
            if (sfx >= Sfx.BenchSit && sfx != Sfx.Board)
            {
                if (Time.unscaledTime - _lastPlay[(int)sfx] < 0.12f) return null;
                _lastPlay[(int)sfx] = Time.unscaledTime;
            }

            int take = 0;
            if (takes.Length > 1)
            {
                take = Random.Range(0, takes.Length - 1);
                if (take >= _lastTake[(int)sfx]) take++;
                _lastTake[(int)sfx] = take;
            }
            AudioClip clip = takes[take];
            // The win jingle is a few seconds of music of its own: pull the loop down underneath it.
            if (sfx == Sfx.LevelComplete && file) _duckUntil = Time.unscaledTime + clip.length;
            if (sfx == Sfx.BenchSit) BreakChain();

            AudioSource v = FreeVoice();
            float jitter = Jitter[(int)sfx];
            v.clip = clip;
            v.pitch = Mathf.Pow(2f, semitones / 12f) * (1f + Random.Range(-jitter, jitter));
            v.volume = _sfxVolume * (jitter > 0f ? Random.Range(0.88f, 1f) : 1f);
            v.panStereo = pan;
            v.Play();
            return v;
        }

        /// <summary>A voice that is idle, or else the one that has been playing longest.</summary>
        private AudioSource FreeVoice()
        {
            int best = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].isPlaying) { best = i; break; }
                if (_voiceStarted[i] < _voiceStarted[best]) best = i;
            }
            _voiceStarted[best] = Time.unscaledTime;
            return _voices[best];
        }
    }

    /// <summary>Loudness matching for sound effects, done once when a clip loads.</summary>
    public static class AudioMix
    {
        /// <summary>
        /// Returns a copy of the clip whose loudest 50 ms sits at the given level (dBFS RMS), with a soft limiter so
        /// raising a quiet clip never clips. Clips whose samples cannot be read are returned unchanged.
        /// </summary>
        public static AudioClip Normalise(AudioClip clip, float targetDb)
        {
            if (clip == null) return null;
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            int channels = clip.channels;
            var data = new float[clip.samples * channels];
            if (data.Length == 0 || !clip.GetData(data, 0)) return clip;

            int window = Mathf.Max(1, clip.frequency * channels / 20);
            double sum = 0, loudest = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[i];
                if (i >= window) sum -= data[i - window] * data[i - window];
                if (sum > loudest) loudest = sum;
            }
            float rms = Mathf.Sqrt((float)(loudest / Mathf.Min(window, data.Length)));
            if (rms < 1e-5f) return clip;
            float gainDb = Mathf.Clamp(targetDb - 20f * Mathf.Log10(rms), -24f, 14f);
            float gain = Mathf.Pow(10f, gainDb / 20f);
            for (int i = 0; i < data.Length; i++) data[i] = Limit(data[i] * gain);

            AudioClip copy = AudioClip.Create(clip.name, clip.samples, channels, clip.frequency, false);
            copy.SetData(data, 0);
            return copy;
        }

        /// <summary>Unity gain below 0.8, then a smooth knee that never exceeds 1.</summary>
        private static float Limit(float x)
        {
            float a = Mathf.Abs(x);
            if (a <= 0.8f) return x;
            float over = (a - 0.8f) / 0.2f;
            float y = 0.8f + 0.2f * (over / (1f + over));
            return x < 0f ? -y : y;
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
                case Sfx.Star:
                case Sfx.CoinTick:
                    b = new Buffer(0.3f, false); b.Note(0f, 0.12f, 1319f, 0.25f, Wave.Sine, 0f, 16f); b.Note(0.09f, 0.2f, 1760f, 0.25f, Wave.Sine, 0f, 12f); break;
                case Sfx.Booster:
                    b = new Buffer(0.3f, false); b.Note(0f, 0.28f, 440f, 0.3f, Wave.Triangle, 900f, 7f); break;
                case Sfx.Board:
                    // A soft marimba-like pluck on C5; AudioManager pitches it up the scale.
                    b = new Buffer(0.55f, false);
                    b.Note(0f, 0.55f, 523.25f, 0.42f, Wave.Sine, 0f, 7f);
                    b.Note(0f, 0.3f, 1046.5f, 0.16f, Wave.Sine, 0f, 13f);
                    b.Note(0f, 0.15f, 2093f, 0.10f, Wave.Sine, 0f, 28f);
                    b.Note(0f, 0.06f, 4186f, 0.04f, Wave.Sine, 0f, 70f);
                    break;
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
