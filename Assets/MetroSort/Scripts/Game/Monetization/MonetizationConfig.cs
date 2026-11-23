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
        public const string GoogleTestIosRewardedId = "ca-app-pub-3940256099942544/1712485313";
        public const string GoogleTestIosInterstitialId = "ca-app-pub-3940256099942544/4411468910";

        public string AdMobAndroidAppId = "";
        public string AdMobIosAppId = "";
        public string RewardedId = "";
        public string InterstitialId = "";
        public string IosRewardedId = "";
        public string IosInterstitialId = "";
        public string RevenueCatGoogleKey = "";
        public string RevenueCatAppleKey = "";
        public string RemoveAdsEntitlement = "no_ads";
        /// <summary>The studio's privacy policy (English and Turkish pages), opened from Settings.</summary>
        public string PrivacyPolicyUrl = "";
        public string PrivacyPolicyUrlTr = "";

        /// <summary>
        /// True in the store builds: MetroSortBuild.BuildAndroid (Google Play bundle) and BuildIOS (App Store export)
        /// add METROSORT_STORE_BUILD, and those always request the real ad units. The test APK and the simulator
        /// build follow useTestAds.
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

        public string RewardedUnit { get { return RewardedUnitFor(false); } }
        public string InterstitialUnit { get { return InterstitialUnitFor(false); } }

        /// <summary>The rewarded unit for Android or iOS: Google's test unit while UseTestAds, else the real one.</summary>
        public string RewardedUnitFor(bool ios)
        {
            if (UseTestAds) return ios ? GoogleTestIosRewardedId : GoogleTestRewardedId;
            return ios ? IosRewardedId : RewardedId;
        }

        public string InterstitialUnitFor(bool ios)
        {
            if (UseTestAds) return ios ? GoogleTestIosInterstitialId : GoogleTestInterstitialId;
            return ios ? IosInterstitialId : InterstitialId;
        }

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
                IosRewardedId = JsonRead.Str(root, "admobIosRewardedId", "").Trim(),
                IosInterstitialId = JsonRead.Str(root, "admobIosInterstitialId", "").Trim(),
                RevenueCatAppleKey = JsonRead.Str(root, "revenueCatAppleApiKey", "").Trim(),
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
