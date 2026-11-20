using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>Turns the minute and second hands of the station clock; the second hand ticks with a little overshoot.</summary>
    public sealed class ClockHands : MonoBehaviour
    {
        public Transform Minute, Second;
        private float _start;

        private void OnEnable() { _start = Time.time; }

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            float t = Time.time - _start;
            float whole = Mathf.Floor(t), part = t - whole;
            // Each second: snap forward in the first 0.18 s, overshooting slightly, then rest.
            float tick = whole + (part < 0.18f ? Ease.OutBack(part / 0.18f) : 1f);
            // Seen from the camera (looking along +Z) a negative turn about Z runs clockwise.
            if (Second != null) Second.localRotation = Quaternion.AngleAxis(-tick * 6f, Vector3.forward);
            if (Minute != null) Minute.localRotation = Quaternion.AngleAxis(-t * 0.1f, Vector3.forward);
        }
    }

    /// <summary>
    /// Foliage that sways gently about a pivot above its holder's origin, and is blown about for a moment
    /// whenever a train rushes through (Gust).
    /// </summary>
    public sealed class Sway : MonoBehaviour
    {
        private static float _gust, _gustTime = -10f;

        public Vector3 Home;
        public float PivotHeight;
        private float _phase;

        /// <summary>A passing train: every plant leans away and rustles. Positive blows towards +X.</summary>
        public static void Gust(float strength)
        {
            if (GameSettings.ReducedMotion) return;
            _gust = strength;
            _gustTime = Time.time;
        }

        private void Awake() { _phase = transform.position.x * 1.7f + transform.position.z * 0.9f; }

        private void Update()
        {
            if (GameSettings.ReducedMotion) return;
            float t = Time.time;
            float since = t - _gustTime;
            // The gust arrives as a lean that rings down over about a second and a half.
            float blow = since < 2f ? _gust * Mathf.Exp(-since * 2.6f) * Mathf.Cos(since * 11f) : 0f;
            float rustle = since < 2f ? Mathf.Abs(_gust) * Mathf.Exp(-since * 3.5f) : 0f;
            float lean = Mathf.Sin(t * 1.3f + _phase) * 2.2f - blow * 11f;
            float nod = Mathf.Sin(t * 1.7f + _phase * 1.3f) * 1.8f + Mathf.Sin(t * 23f + _phase) * rustle * 2.5f;
            float puff = 1f + Mathf.Sin(t * 2.1f + _phase) * 0.012f + rustle * 0.04f * Mathf.Sin(t * 19f);
            Quaternion turn = Quaternion.Euler(nod, 0f, lean);
            var pivot = new Vector3(0f, PivotHeight, 0f);
            transform.localRotation = turn;
            transform.localScale = new Vector3(puff, 2f - puff, puff);
            transform.localPosition = Home + pivot - turn * pivot * (2f - puff);
        }
    }
}

namespace MetroSort.Game
{
    /// <summary>The two flaps of one ticket gate: they spring open while passengers walk through, then swing shut.</summary>
    public sealed class GateFlaps : MonoBehaviour
    {
        public Transform Left, Right;
        private float _openAt = -1f, _closeAt = -1f, _open, _speed;
        private bool _wasOpen;

        public void Open(float delay, float hold)
        {
            _openAt = Time.time + delay;
            _closeAt = _openAt + hold;
        }

        private void Update()
        {
            float target = Time.time >= _openAt && Time.time < _closeAt ? 1f : 0f;
            if (target > 0f && !_wasOpen) PlaySound(Sfx.GateFlap);
            _wasOpen = target > 0f;
            if (target == 0f && _open == 0f && _speed == 0f) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            _speed += (-190f * (_open - target) - 13f * _speed) * dt;
            _open += _speed * dt;
            if (target == 0f && Mathf.Abs(_open) < 0.003f && Mathf.Abs(_speed) < 0.03f) _open = _speed = 0f;
            // Each flap is hinged at its post and swings away in the direction of travel (+Z).
            if (Left != null) Left.localRotation = Quaternion.Euler(0f, -82f * _open, 0f);
            if (Right != null) Right.localRotation = Quaternion.Euler(0f, 82f * _open, 0f);
        }

        /// <summary>Plays a station sound if the game is running (these props also exist in tests and previews).</summary>
        public static void PlaySound(Sfx sfx)
        {
            if (GameApp.Instance != null && GameApp.Instance.Audio != null) GameApp.Instance.Audio.Play(sfx);
        }
    }

    /// <summary>
    /// A barrier whose arm is hinged at one end. Opening swings the arm up with a springy overshoot, then the
    /// whole barrier shrinks away; closing puts it back at once.
    /// </summary>
    public sealed class BoomGate : MonoBehaviour
    {
        public Transform Arm;
        private float _t = -1f;

        public bool Opening { get { return _t >= 0f; } }

        public void Close()
        {
            _t = -1f;
            if (Arm != null) Arm.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Open()
        {
            if (!gameObject.activeSelf || _t >= 0f) return;
            if (GameSettings.ReducedMotion || Arm == null) { Hide(); return; }
            _t = 0f;
            GateFlaps.PlaySound(Sfx.Barrier);
        }

        private void Hide()
        {
            _t = -1f;
            if (Arm != null) Arm.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            // Damped spring: shoots past upright and wobbles back.
            float lift = 1f - Mathf.Exp(-7f * _t) * Mathf.Cos(17f * _t);
            Arm.localRotation = Quaternion.Euler(0f, 0f, 80f * lift);
            if (_t > 0.62f)
            {
                float k = Mathf.Clamp01((_t - 0.62f) / 0.18f);
                transform.localScale = Vector3.one * (1f - k * k);
                if (k >= 1f) Hide();
            }
        }
    }
}

namespace MetroSort.Game
{
    /// <summary>How the station should animate from what it showed before a rebuild to the new state.</summary>
    public enum RebuildStyle { None, Undo, Shuffle, ExtraPlatform }

    /// <summary>
    /// The departure board on the back wall: its rows flip over one after another, like a split-flap display,
    /// whenever a train is announced (Flip).
    /// </summary>
    public sealed class DepartureBoard : MonoBehaviour
    {
        private static float _flipTime = -10f;
        private static int _flips;

        public Transform[] Rows;
        public GameObject[] Chips;
        private int _shown;

        public static void Flip()
        {
            if (GameSettings.ReducedMotion) return;
            GateFlaps.PlaySound(Sfx.BoardFlip);
            _flipTime = Time.time;
            _flips++;
        }

        private void Update()
        {
            if (Rows == null) return;
            float since = Time.time - _flipTime;
            if (since > 1.2f && _shown == _flips) return;
            for (int i = 0; i < Rows.Length; i++)
            {
                float k = Mathf.Clamp01((since - i * 0.09f) / 0.26f);
                // Each row folds shut and opens again; the line chip changes colour while it is edge-on.
                Rows[i].localScale = new Vector3(1f, Mathf.Max(0.04f, Mathf.Abs(Mathf.Cos(k * Mathf.PI))), 1f);
                if (k > 0.5f && _shown != _flips && Chips != null && i < Chips.Length)
                    Prims.SetColor(Chips[i], Palette.Lines[(i * 2 + _flips) % Palette.Lines.Length]);
            }
            if (since > 1.2f) _shown = _flips;
        }
    }
}
