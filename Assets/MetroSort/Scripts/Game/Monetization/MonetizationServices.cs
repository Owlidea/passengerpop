using System;
using System.Collections;
using MetroSort.Ads;
using MetroSort.Purchases;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Picks the real ad network and store on a phone (AdMob, RevenueCat; Android and iOS each with their own units
    /// and key) and the development stand-ins in the editor and in tests. A phone whose platform has no unit or key
    /// yet gets no ads and a store that refuses every purchase: never free coins or free rewards in a shipped build.
    /// </summary>
    public static class MonetizationServices
    {
        private static bool OnAndroid { get { return !Application.isEditor && Application.platform == RuntimePlatform.Android; } }
        private static bool OnIos { get { return !Application.isEditor && Application.platform == RuntimePlatform.IPhonePlayer; } }
        private static bool OnDevice { get { return OnAndroid || OnIos; } }

        public static IAdsService CreateAds(MonetizationConfig config)
        {
#if METROSORT_ADMOB
            string rewarded = config.RewardedUnitFor(OnIos);
            if (OnDevice && !string.IsNullOrEmpty(rewarded)) return new AdMobAdsService(rewarded, config.InterstitialUnitFor(OnIos));
#endif
            return OnDevice ? new MockAdsService { RewardedReady = false, InterstitialReady = false } : new MockAdsService();
        }

        public static IPurchaseService CreatePurchases(MonetizationConfig config)
        {
#if METROSORT_REVENUECAT
            string key = OnIos ? config.RevenueCatAppleKey : config.RevenueCatGoogleKey;
            if (OnDevice && !string.IsNullOrEmpty(key)) return new RevenueCatPurchaseService(key, config.RemoveAdsEntitlement);
#endif
            return OnDevice ? new MockPurchaseService { NextSucceeds = false } : new MockPurchaseService();
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
