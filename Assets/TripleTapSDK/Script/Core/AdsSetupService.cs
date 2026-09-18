using System;
using System.Collections.Generic;
using UnityEngine;
using GameAnalyticsSDK;
using Facebook.Unity;

namespace TripleTapSDK
{
    public class TTAdsService : MonoBehaviour
    {

        public  event Action<string,string, string, double, MaxSdkBase.AdInfo> AdRevenuePaid = delegate { };

        public event Action<bool> OnInterAdsClosedStatus = delegate { };
        public event Action<bool> OnRewardedAdsClosedStatus = delegate { };
        
        public event Action OnInterAdOpened = delegate { };
        public event Action OnRewardedAdOpened = delegate { };

        private string interstitialAdsID = "";
        private string rewardVideoAdsID = "";

        private string bannerAdsID = "";

        [SerializeField]
        private TTAdsConfig _adsUnitIDConfig;

        [SerializeField] private TTRemoteConfigKeys remoteConfigKeys;

        bool isBannerLoaded = false;

        // Track whether the current rewarded ad granted its reward
        private bool _rewardedAdEarnedReward = false;

        // For deley
        private bool startInterDelay, startRewardDelay;
        private double interDelayCntr, rewardDelayCntr;
        private double interDelay, rewardDelay;


        void Update()
        {
            CheckInterDelay();
            CheckRewardDelay();
        }

        void CheckInterDelay()
        {
            if (!startInterDelay)
                return;

            interDelayCntr += Time.deltaTime;

            if (interDelayCntr >= interDelay)
            {
                interDelayCntr = 0;
                startInterDelay = false;
                interstitialLoading = false;
                LoadInterstitial();
            }
        }

        void CheckRewardDelay()
        {
            if (!startRewardDelay)
                return;

            rewardDelayCntr += Time.deltaTime;

            if (rewardDelayCntr >= rewardDelay)
            {
                rewardDelayCntr = 0;
                startRewardDelay = false;
                rewardedLoading = false;
                LoadRewardedAd();
            }
        }

        public void LoadAds()
        {
            interstitialAdsID = _adsUnitIDConfig.interstitialID.ToString();
            rewardVideoAdsID = _adsUnitIDConfig.rewardedID.ToString();
            bannerAdsID = _adsUnitIDConfig.bannerID.ToString();

            InitializeInterstitialAds();
            InitializeRewardedAds();
           // InitializeBannerAds();

        }

        public void InitializeBannerAds()
        {
            // Banners are automatically sized to 320×50 on phones and 728×90 on tablets
            // You may call the utility method MaxSdkUtils.isTablet() to help with view sizing adjustments
            var adViewConfiguration = new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.BottomCenter);

            MaxSdk.StartBannerAutoRefresh(_adsUnitIDConfig.bannerID.ToString());
            MaxSdk.CreateBanner(_adsUnitIDConfig.bannerID.ToString(), adViewConfiguration);

            // Set background color for banners to be fully functional
            MaxSdk.SetBannerBackgroundColor(_adsUnitIDConfig.bannerID.ToString(), Color.black);

            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;

        }
        
        private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
           // AdRevenuePaid?.Invoke("AppLovin", adInfo.NetworkName, adInfo.AdFormat, adInfo.Revenue, adInfo);
        }

        private void OnBannerAdLoadedEvent(string adUnitId, MaxSdk.AdInfo adInfo)
        {
                MaxSdk.ShowBanner(_adsUnitIDConfig.bannerID.ToString());
                isBannerLoaded = true;
        }

        #region "InterstitialAd"

        int interstitialRetryAttempt;
        bool interstitialLoading;

        public void InitializeInterstitialAds()
        {
            // Attach callback
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaidEvent;

            // Load the first interstitial
            MaxSdk.LoadInterstitial(interstitialAdsID);
            //  LoadInterstitial();
        }

        private void LoadInterstitial()
        {

            MaxSdk.LoadInterstitial(interstitialAdsID);
        }

        public bool IsInterAvailable()
        {

            return MaxSdk.IsInterstitialReady(interstitialAdsID);
        }

        public void ShowInterstitialAd()
        {
               MaxSdk.ShowInterstitial(interstitialAdsID);
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

            // Reset retry attempt
            interstitialRetryAttempt = 0;
            interstitialLoading = false;
        }

        private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Interstitial ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)

            if (!interstitialLoading)
            {
                interstitialRetryAttempt++;
                double retryDelay = Math.Pow(2, Math.Min(6, interstitialRetryAttempt));

                interDelay = retryDelay;
                if (interstitialRetryAttempt < 8)
                {
                    DelayInterstitialLoad(retryDelay);
                }
            }

        }

        //async void DelayInterstitialLoad(double delay)
        void DelayInterstitialLoad(double delay)
        {
            interstitialLoading = true;
            startInterDelay = true;
        }

        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnInterAdOpened?.Invoke();
        }

        private void OnInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
           // OninterstitialAdsEnded?.Invoke();
           // AdsManager.Instance.InterstitialCompleted(false);
            OnInterAdsClosedStatus?.Invoke(false);
            // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
            interDelay = 2;
            DelayInterstitialLoad(2.0f);
        }

        private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
        }

        private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnInterAdsClosedStatus?.Invoke(true);
            interDelay = 2;
            DelayInterstitialLoad(2.0f);
        }

        private void OnInterstitialAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdRevenuePaid?.Invoke("AppLovin", adInfo.NetworkName, adInfo.AdFormat, adInfo.Revenue, adInfo);
        }

        #endregion

        #region "Reward"

        int rewardedRetryAttempt, loadrewardedAds;
        bool rewardedLoading;
        public void InitializeRewardedAds()
        {
            // Attach callback
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;

            // Load the first rewarded ad
            MaxSdk.LoadRewardedAd(rewardVideoAdsID);
           
        }

        private void LoadRewardedAd()
        {
            MaxSdk.LoadRewardedAd(rewardVideoAdsID);
        }

        public bool IsRewardedAdAvailable()
        {
            return MaxSdk.IsRewardedAdReady(rewardVideoAdsID);
        }

        public void ShowRewardedAd()
        {
                MaxSdk.ShowRewardedAd(rewardVideoAdsID);
        }

        private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is ready for you to show. MaxSdk.IsRewardedAdReady(adUnitId) now returns 'true'.
            // Reset retry attempt
            rewardedRetryAttempt = 0;
            rewardedLoading = false;
        }

        private void OnRewardedAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Rewarded ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds).
            if (!rewardedLoading)
            {
                rewardedRetryAttempt++;
                rewardDelay = Math.Pow(2, Math.Min(6, rewardedRetryAttempt));

                if (rewardedRetryAttempt < 8)
                {
                    DelayRewardedLoad(rewardDelay);
                }
            }
        }

        void DelayRewardedLoad(double delay)
        {
            rewardedLoading = true;
            startRewardDelay = true;
        }

        private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardedAdOpened?.Invoke();
        }

        private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardedAdsClosedStatus?.Invoke(false);
            rewardDelay = 2;
            DelayRewardedLoad(2);
        }

        private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
        }

        private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            OnRewardedAdsClosedStatus?.Invoke(_rewardedAdEarnedReward);
            // Reset flag for next ad
            _rewardedAdEarnedReward = false;

            // Pre-load the next ad
            rewardDelay = 2;
            DelayRewardedLoad(2);
        }

        private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            _rewardedAdEarnedReward = true;
        }

        private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            AdRevenuePaid?.Invoke("AppLovin", adInfo.NetworkName, adInfo.AdFormat, adInfo.Revenue, adInfo);
            // Ad revenue paid. Use this callback to track user revenue via AnalyticsService.
        }

        #endregion

    }
}