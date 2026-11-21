using System;
using System.Collections;
using MetroSort.Ads;
using MetroSort.Purchases;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Picks the real ad network and store on an Android device (AdMob, RevenueCat) and the development stand-ins
    /// everywhere else: in the editor, in tests, and while the config has no key for a service.
    /// </summary>
    public static class MonetizationServices
    {
        private static bool OnDevice { get { return !Application.isEditor && Application.platform == RuntimePlatform.Android; } }

        public static IAdsService CreateAds(MonetizationConfig config)
        {
#if METROSORT_ADMOB
            if (OnDevice && !string.IsNullOrEmpty(config.RewardedUnit)) return new AdMobAdsService(config.RewardedUnit, config.InterstitialUnit);
#endif
            return new MockAdsService();
        }

        public static IPurchaseService CreatePurchases(MonetizationConfig config)
        {
#if METROSORT_REVENUECAT
            if (OnDevice && !string.IsNullOrEmpty(config.RevenueCatGoogleKey))
                return new RevenueCatPurchaseService(config.RevenueCatGoogleKey, config.RemoveAdsEntitlement);
#endif
            return new MockPurchaseService();
        }
    }

    /// <summary>Runs SDK work on Unity's main loop: delayed calls without a scene object of their own.</summary>
    public sealed class MonetizationRunner : MonoBehaviour
    {
        private static MonetizationRunner _instance;

        private static MonetizationRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("MonetizationRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MonetizationRunner>();
                return _instance;
            }
        }

        /// <summary>Runs the action after the given seconds (0: on the next frame), in real time.</summary>
        public static void After(float seconds, Action action)
        {
            Instance.StartCoroutine(Instance.Delay(seconds, action));
        }

        private IEnumerator Delay(float seconds, Action action)
        {
            if (seconds <= 0f) yield return null;
            else yield return new WaitForSecondsRealtime(seconds);
            if (action != null) action();
        }
    }
}
