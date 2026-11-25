using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MetroSort.Analytics;
using MetroSort.Utilities;
using UnityEngine;
#if METROSORT_ADMOB
using GoogleMobileAds.Ump.Api;
#endif

namespace MetroSort.Game
{
    /// <summary>Hands every event to each sink in turn (the console in development builds, Firebase on a phone).</summary>
    public sealed class AnalyticsRouter : IAnalyticsService
    {
        private readonly List<IAnalyticsService> _sinks = new List<IAnalyticsService>();

        public void Add(IAnalyticsService sink)
        {
            if (sink != null) _sinks.Add(sink);
        }

        public void Track(string eventName, Dictionary<string, object> parameters)
        {
            for (int i = 0; i < _sinks.Count; i++)
            {
                try { _sinks[i].Track(eventName, parameters); }
                catch (Exception e) { Debug.LogWarning("Metro Sort analytics: " + eventName + " failed: " + e.Message); }
            }
        }
    }

    /// <summary>
    /// Google Analytics for Firebase, through the native Android and iOS SDKs. The official Firebase Unity package
    /// was left out on purpose: it adds a 58 MB core package, a second dependency manager next to the one the ad
    /// plugin uses, and Swift packages next to this project's CocoaPods. The game only needs "log an event", so it
    /// talks to the native SDKs directly: FirebaseAnalytics over JNI on Android, a few C functions
    /// (Plugins/iOS/MetroFirebase.mm) on iOS. See Docs/ANALYTICS.md.
    ///
    /// Consent: collection is off by default in both builds (AndroidManifest / Info.plist). It is switched on once
    /// the consent the ad SDK asked for allows it: no consent needed in the player's region, or consent given
    /// including storing information on the device (IAB purpose 1). Events from before that moment wait in a short
    /// queue, so the first launch is not lost. A later "do not consent" in the privacy options switches it off.
    /// The answer comes from the ad SDK's consent step (AdMobAdsService.Initialize); in a build that never runs it
    /// the answer stays unknown and nothing is collected.
    /// </summary>
    public sealed class FirebaseAnalyticsService : IAnalyticsService
    {
        private const int MaxWaiting = 40;
        private const float ConsentCheckSeconds = 3f, ConsentRecheckSeconds = 30f;

        private readonly List<KeyValuePair<string, Dictionary<string, object>>> _waiting =
            new List<KeyValuePair<string, Dictionary<string, object>>>();
        private bool _ready, _enabled;
        private float _nextConsentCheck;

        /// <summary>True on an Android or iOS device; in the editor and in tests there is nothing to talk to.</summary>
        public static bool Supported
        {
            get
            {
                return !Application.isEditor
                    && (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer);
            }
        }

        public FirebaseAnalyticsService()
        {
            if (!Supported) return;
            try { _ready = NativeInit(); }
            catch (Exception e) { Debug.LogWarning("Metro Sort analytics: Firebase is not available: " + e.Message); }
            if (!_ready) Debug.Log("Metro Sort analytics: no Firebase configuration in this build; events stay local.");
        }

        public void Track(string eventName, Dictionary<string, object> parameters)
        {
            if (!_ready || string.IsNullOrEmpty(eventName)) return;
            float now = Time.realtimeSinceStartup;
            if (now >= _nextConsentCheck)
            {
                _nextConsentCheck = now + (_enabled ? ConsentRecheckSeconds : ConsentCheckSeconds);
                SetEnabled(ConsentAllows());
            }
            if (!_enabled)
            {
                if (_waiting.Count < MaxWaiting)
                    _waiting.Add(new KeyValuePair<string, Dictionary<string, object>>(eventName,
                        parameters != null ? new Dictionary<string, object>(parameters) : null));
                return;
            }
            Send(eventName, parameters);
        }

        private void SetEnabled(bool enabled)
        {
            if (enabled == _enabled) return;
            _enabled = enabled;
            NativeSetEnabled(enabled);
            if (!enabled) return;
            for (int i = 0; i < _waiting.Count; i++) Send(_waiting[i].Key, _waiting[i].Value);
            _waiting.Clear();
        }

        /// <summary>
        /// What the consent collected by the ad SDK (Google UMP) means for analytics. Unknown or still to be asked:
        /// no. Not required in this region: yes. Given: yes if it covers storing information on the device (the
        /// first IAB purpose); a message without IAB purposes (US states) does not restrict analytics.
        /// </summary>
        private static bool ConsentAllows()
        {
#if METROSORT_ADMOB
            ConsentStatus status = ConsentInformation.ConsentStatus;
            if (status == ConsentStatus.NotRequired) return true;
            if (status != ConsentStatus.Obtained) return false;
            string purposes = NativeTcfPurposes();
            return string.IsNullOrEmpty(purposes) || purposes[0] == '1';
#else
            return true;
#endif
        }

        private static void Send(string eventName, Dictionary<string, object> parameters)
        {
            try { NativeLog(CleanName(eventName), parameters); }
            catch (Exception e) { Debug.LogWarning("Metro Sort analytics: " + eventName + " not sent: " + e.Message); }
        }

        /// <summary>Firebase names: letters, digits and underscores, starting with a letter, 40 characters at most.</summary>
        public static string CleanName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                sb.Append(ok ? c : '_');
            }
            if (sb.Length == 0 || !char.IsLetter(sb[0])) sb.Insert(0, 'e');
            if (sb.Length > 40) sb.Length = 40;
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Android

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _analytics;

        private static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        private static bool NativeInit()
        {
            using (AndroidJavaObject activity = Activity())
            {
                // The app id comes from res/values of Plugins/Android/MetroFirebase.androidlib; without it the SDK
                // has nowhere to send anything.
                using (AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources"))
                {
                    int id = resources.Call<int>("getIdentifier", "google_app_id", "string", activity.Call<string>("getPackageName"));
                    if (id == 0) return false;
                }
                using (var cls = new AndroidJavaClass("com.google.firebase.analytics.FirebaseAnalytics"))
                    _analytics = cls.CallStatic<AndroidJavaObject>("getInstance", activity);
            }
            return _analytics != null;
        }

        private static void NativeSetEnabled(bool enabled)
        {
            _analytics.Call("setAnalyticsCollectionEnabled", enabled);
        }

        private static void NativeLog(string name, Dictionary<string, object> parameters)
        {
            using (var bundle = new AndroidJavaObject("android.os.Bundle"))
            {
                if (parameters != null)
                {
                    int count = 0;
                    foreach (KeyValuePair<string, object> kv in parameters)
                    {
                        if (kv.Value == null || ++count > 25) continue;
                        string key = CleanName(kv.Key);
                        object v = kv.Value;
                        if (v is bool) bundle.Call("putLong", key, (bool)v ? 1L : 0L);
                        else if (v is int || v is long || v is short || v is byte) bundle.Call("putLong", key, Convert.ToInt64(v));
                        else if (v is float || v is double) bundle.Call("putDouble", key, Convert.ToDouble(v));
                        else
                        {
                            string text = v.ToString();
                            bundle.Call("putString", key, text.Length > 100 ? text.Substring(0, 100) : text);
                        }
                    }
                }
                _analytics.Call("logEvent", name, bundle);
            }
        }

        private static string NativeTcfPurposes()
        {
            try
            {
                using (AndroidJavaObject activity = Activity())
                using (var manager = new AndroidJavaClass("android.preference.PreferenceManager"))
                using (AndroidJavaObject prefs = manager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", activity))
                    return prefs.Call<string>("getString", "IABTCF_PurposeConsents", "");
            }
            catch (Exception) { return ""; }
        }

        // ------------------------------------------------------------------ iOS

#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int MetroFirebase_Configure();
        [DllImport("__Internal")] private static extern void MetroFirebase_SetEnabled(int enabled);
        [DllImport("__Internal")] private static extern void MetroFirebase_LogEvent(string name, string json);

        private static bool NativeInit() { return MetroFirebase_Configure() != 0; }

        private static void NativeSetEnabled(bool enabled) { MetroFirebase_SetEnabled(enabled ? 1 : 0); }

        private static void NativeLog(string name, Dictionary<string, object> parameters)
        {
            var clean = new Dictionary<string, object>();
            if (parameters != null)
            {
                int count = 0;
                foreach (KeyValuePair<string, object> kv in parameters)
                {
                    if (kv.Value == null || ++count > 25) continue;
                    object v = kv.Value;
                    if (v is bool) v = (bool)v ? 1 : 0;
                    else if (!(v is int || v is long || v is short || v is byte || v is float || v is double))
                    {
                        string text = v.ToString();
                        v = text.Length > 100 ? text.Substring(0, 100) : text;
                    }
                    clean[CleanName(kv.Key)] = v;
                }
            }
            MetroFirebase_LogEvent(name, MiniJson.Serialize(clean));
        }

        // The consent SDK writes the IAB strings to the standard user defaults, which is where PlayerPrefs lives on iOS.
        private static string NativeTcfPurposes() { return PlayerPrefs.GetString("IABTCF_PurposeConsents", ""); }

        // ------------------------------------------------------------------ editor and other platforms

#else
        private static bool NativeInit() { return false; }
        private static void NativeSetEnabled(bool enabled) { }
        private static void NativeLog(string name, Dictionary<string, object> parameters) { }
        private static string NativeTcfPurposes() { return ""; }
#endif
    }
}
