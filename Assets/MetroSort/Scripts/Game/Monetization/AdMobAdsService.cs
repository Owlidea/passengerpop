#if METROSORT_ADMOB
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using MetroSort.Ads;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Google AdMob behind IAdsService. Consent (UMP) is asked first where the law needs it (EEA, UK); after that
    /// one rewarded and one interstitial ad are kept loaded, reloaded after each showing, and a failed load is
    /// retried after a pause that grows up to two minutes. All ad events come back on Unity's main thread.
    /// </summary>
    public sealed class AdMobAdsService : IAdsService
    {
        private const float FirstRetry = 4f, MaxRetry = 120f;

        private readonly string _rewardedId, _interstitialId;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;
        private bool _started, _loadingRewarded, _loadingInterstitial;
        private float _rewardedRetryAt, _interstitialRetryAt, _rewardedBackoff = FirstRetry, _interstitialBackoff = FirstRetry;

        public AdMobAdsService(string rewardedId, string interstitialId)
        {
            _rewardedId = rewardedId;
            _interstitialId = interstitialId;
        }

        public void Initialize()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null) Debug.LogWarning("Metro Sort ads: consent info failed: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("Metro Sort ads: consent form failed: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) Start();
                });
            });
            // A previous session's consent already allows ads: no need to wait for the update.
            if (ConsentInformation.CanRequestAds()) Start();
        }

        private void Start()
        {
            if (_started) return;
            _started = true;
            MobileAds.Initialize(status =>
            {
                LoadRewarded();
                LoadInterstitial();
            });
        }

        public bool IsRewardedAdReady()
        {
            Refill();
            return _rewarded != null && _rewarded.CanShowAd();
        }

        public bool IsInterstitialReady()
        {
            Refill();
            return _interstitial != null && _interstitial.CanShowAd();
        }

        public void ShowRewardedAd(Action<AdResult> onFinished)
        {
            if (!IsRewardedAdReady())
            {
                if (onFinished != null) onFinished(AdResult.Unavailable);
                return;
            }
            RewardedAd ad = _rewarded;
            _rewarded = null;
            bool earned = false, done = false;
            Action<AdResult> finish = result =>
            {
                if (done) return;
                done = true;
                ad.Destroy();
                if (onFinished != null) onFinished(result);
                LoadRewarded();
            };
            // The reward can be reported just after the close, so the close waits a moment for it.
            ad.OnAdFullScreenContentClosed += () => MonetizationRunner.After(0.3f, () => finish(earned ? AdResult.Rewarded : AdResult.Skipped));
            ad.OnAdFullScreenContentFailed += error => finish(AdResult.Failed);
            ad.Show(reward => earned = true);
        }

        public void ShowInterstitialAd(Action onClosed)
        {
            if (!IsInterstitialReady())
            {
                if (onClosed != null) onClosed();
                return;
            }
            InterstitialAd ad = _interstitial;
            _interstitial = null;
            bool done = false;
            Action finish = () =>
            {
                if (done) return;
                done = true;
                ad.Destroy();
                if (onClosed != null) onClosed();
                LoadInterstitial();
            };
            ad.OnAdFullScreenContentClosed += finish;
            ad.OnAdFullScreenContentFailed += error => finish();
            ad.Show();
        }

        public bool PrivacyOptionsRequired
        {
            get { return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
        }

        public void ShowPrivacyOptions(Action<string> onClosed)
        {
            ConsentForm.ShowPrivacyOptionsForm(formError =>
            {
                if (formError != null) Debug.LogWarning("Metro Sort ads: privacy options failed: " + formError.Message);
                // The new choice may allow ads that were not allowed before.
                if (ConsentInformation.CanRequestAds()) Start();
                if (onClosed != null) onClosed(formError != null ? formError.Message : null);
            });
        }

        /// <summary>Loads whatever is missing once its retry pause is over (checked whenever the game asks).</summary>
        private void Refill()
        {
            if (!_started) return;
            float now = Time.realtimeSinceStartup;
            if (_rewarded == null && !_loadingRewarded && now >= _rewardedRetryAt) LoadRewarded();
            if (_interstitial == null && !_loadingInterstitial && now >= _interstitialRetryAt) LoadInterstitial();
        }

        private void LoadRewarded()
        {
            if (string.IsNullOrEmpty(_rewardedId) || _loadingRewarded) return;
            _loadingRewarded = true;
            RewardedAd.Load(_rewardedId, new AdRequest(), (ad, error) =>
            {
                _loadingRewarded = false;
                if (error != null || ad == null)
                {
                    _rewardedRetryAt = Time.realtimeSinceStartup + _rewardedBackoff;
                    _rewardedBackoff = Mathf.Min(_rewardedBackoff * 2f, MaxRetry);
                    return;
                }
                _rewardedBackoff = FirstRetry;
                _rewarded = ad;
            });
        }

        private void LoadInterstitial()
        {
            if (string.IsNullOrEmpty(_interstitialId) || _loadingInterstitial) return;
            _loadingInterstitial = true;
            InterstitialAd.Load(_interstitialId, new AdRequest(), (ad, error) =>
            {
                _loadingInterstitial = false;
                if (error != null || ad == null)
                {
                    _interstitialRetryAt = Time.realtimeSinceStartup + _interstitialBackoff;
                    _interstitialBackoff = Mathf.Min(_interstitialBackoff * 2f, MaxRetry);
                    return;
                }
                _interstitialBackoff = FirstRetry;
                _interstitial = ad;
            });
        }
    }
}
#endif
