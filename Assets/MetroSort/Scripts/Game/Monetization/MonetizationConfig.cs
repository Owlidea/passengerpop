using MetroSort.Utilities;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Ad unit ids and store keys, read from Resources/monetization.json. The file ships with Google's public test
    /// ids (test ads, no earnings); swap in the real AdMob ids and the RevenueCat key before release.
    /// </summary>
    public sealed class MonetizationConfig
    {
        public const string GoogleTestAppId = "ca-app-pub-3940256099942544~3347511713";
        /// <summary>Google's sample iOS app id; stands in until Metro Sort has an iOS app in AdMob.</summary>
        public const string GoogleTestIosAppId = "ca-app-pub-3940256099942544~1458002511";
        // Google's public test ad units: they work with any app id and never pay or count as clicks.
        public const string GoogleTestRewardedId = "ca-app-pub-3940256099942544/5224354917";
        public const string GoogleTestInterstitialId = "ca-app-pub-3940256099942544/1033173712";

        public string AdMobAndroidAppId = "";
        public string AdMobIosAppId = "";
        public string RewardedId = "";
        public string InterstitialId = "";
        public string RevenueCatGoogleKey = "";
        public string RemoveAdsEntitlement = "no_ads";
        /// <summary>The studio's privacy policy (English and Turkish pages), opened from Settings.</summary>
        public string PrivacyPolicyUrl = "";
        public string PrivacyPolicyUrlTr = "";

        /// <summary>
        /// True in the Google Play build: MetroSortBuild.BuildAndroid adds METROSORT_STORE_BUILD, and that build always
        /// requests the real ad units. The test APK follows useTestAds.
        /// </summary>
#if METROSORT_STORE_BUILD
        public const bool StoreBuild = true;
#else
        public const bool StoreBuild = false;
#endif

        /// <summary>Request Google's test ad units instead of the real ones (testing on your own device).</summary>
        public bool UseTestAds = !StoreBuild;

        public string PrivacyPolicyFor(string language)
        {
            return language == "tr" && !string.IsNullOrEmpty(PrivacyPolicyUrlTr) ? PrivacyPolicyUrlTr : PrivacyPolicyUrl;
        }

        public bool UsesGoogleTestAds { get { return AdMobAndroidAppId == GoogleTestAppId; } }

        public string RewardedUnit { get { return UseTestAds ? GoogleTestRewardedId : RewardedId; } }
        public string InterstitialUnit { get { return UseTestAds ? GoogleTestInterstitialId : InterstitialId; } }

        public static MonetizationConfig Load()
        {
            var c = new MonetizationConfig();
            TextAsset file = Resources.Load<TextAsset>("monetization");
            if (file == null) return c;
            return Parse(file.text) ?? c;
        }

        public static MonetizationConfig Parse(string json)
        {
            var root = JsonRead.AsDict(MiniJson.Parse(json));
            if (root == null) return null;
            return new MonetizationConfig
            {
                AdMobAndroidAppId = JsonRead.Str(root, "admobAndroidAppId", "").Trim(),
                AdMobIosAppId = JsonRead.Str(root, "admobIosAppId", "").Trim(),
                RewardedId = JsonRead.Str(root, "admobAndroidRewardedId", "").Trim(),
                InterstitialId = JsonRead.Str(root, "admobAndroidInterstitialId", "").Trim(),
                RevenueCatGoogleKey = JsonRead.Str(root, "revenueCatGoogleApiKey", "").Trim(),
                UseTestAds = !StoreBuild && JsonRead.Bool(root, "useTestAds", true),
                RemoveAdsEntitlement = JsonRead.Str(root, "removeAdsEntitlement", "no_ads").Trim(),
                PrivacyPolicyUrl = JsonRead.Str(root, "privacyPolicyUrl", "").Trim(),
                PrivacyPolicyUrlTr = JsonRead.Str(root, "privacyPolicyUrlTr", "").Trim()
            };
        }
    }
}
