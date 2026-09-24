using System;
using AppLovinMax;
using UnityEngine;

namespace TripleTapSDK
{
    public class TTAdsManager : MonoBehaviour
    {
        [SerializeField] private TTAdsService adsSetupService;
        public bool IsInitialized { get; private set; }
        private TTAdLocation _currentAdLocation;
        private TTAdLocation interstitialLocation;
        private bool interstitialInFlight;
        private double lastAdCooldownResetTime = double.NegativeInfinity;
        public event System.Action<TTAdLocation, bool> OnRewardedCompleted = delegate { };
        public event System.Action<TTAdLocation, bool> OnInterstitialCompleted = delegate { };
        public Action OnAdsInitialized = delegate { };

        public TTRemoteConfigKeys remoteConfigKeys;

        

        private void Awake()
        {
            adsSetupService.OnInterAdsClosedStatus += InterstitialCompleted;
            adsSetupService.OnRewardedAdsClosedStatus += RewardedCompleted;

            StartInit();
            MaxSdk.InitializeSdk();
        }

        void StartInit()
        {
            // Attach event listeners for ad callbacks
            MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfiguration =>
            {
                adsSetupService.gameObject.SetActive(true);
                IsInitialized = true;
                OnAdsInitialized?.Invoke();
                adsSetupService.LoadAds();
                
               
            };
        }

        private void OnDestroy()
        {
            // Cleanup if needed
        }

        public void ShowRewarded(TTAdLocation adLocation)
        {
            _currentAdLocation = adLocation;
            adsSetupService.ShowRewardedAd();
        }

        public void RewardedCompleted(bool isSuccess)
        {
            // Reset before notifying gameplay, which may immediately check ad availability.
            if (isSuccess) lastAdCooldownResetTime = Time.realtimeSinceStartupAsDouble;
            OnRewardedCompleted?.Invoke(_currentAdLocation, isSuccess);
            Debug.Log("RewardedCompleted called with isSuccess: " + isSuccess);
        }

        public bool IsRewardedAdAvailable()
        {
            return adsSetupService.IsRewardedAdAvailable();
        }

        public bool IsInterstitialAdAvailable(int progressionLevelNumber)
        {
            if (!IsInitialized || adsSetupService == null || remoteConfigKeys == null || progressionLevelNumber < 1)
            {
                return false;
            }

            int requiredLevel = PlayerPrefs.GetInt(
                remoteConfigKeys.adsLevelStart_RemoteConfigKey,
                remoteConfigKeys.adsLevelStart_RemoteDefaultValue);

            return progressionLevelNumber >= requiredLevel && CanShowInterstitialNow();
        }

        private bool CanShowInterstitialNow()
        {
            if (!IsInitialized || adsSetupService == null || remoteConfigKeys == null || interstitialInFlight)
                return false;

            int intervalSeconds = Mathf.Max(0, PlayerPrefs.GetInt(
                remoteConfigKeys.interstitialInterval_RemoteConfigKey,
                remoteConfigKeys.interstitialInterval_RemoteDefaultValue));
            return Time.realtimeSinceStartupAsDouble - lastAdCooldownResetTime >= intervalSeconds &&
                adsSetupService.IsInterAvailable();
        }

        public void ShowInterstitial(TTAdLocation adLocation)
        {
            // Callers check progression eligibility; also enforce the interval
            // here so repeated show requests cannot bypass the cooldown.
            if (!CanShowInterstitialNow())
            {
                OnInterstitialCompleted?.Invoke(adLocation, false);
                return;
            }
            interstitialLocation = adLocation;
            interstitialInFlight = true;
            adsSetupService.ShowInterstitialAd();
        }

        public void InterstitialCompleted(bool isSuccess)
        {
            if (!interstitialInFlight) return;
            interstitialInFlight = false;
            if (isSuccess) lastAdCooldownResetTime = Time.realtimeSinceStartupAsDouble;
            OnInterstitialCompleted?.Invoke(interstitialLocation, isSuccess);
        }
      
    }
}
