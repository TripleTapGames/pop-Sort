using System;
using AppLovinMax;
// using KnitFlow.Scripts.Gameplay.Managers;
using UnityEngine;

namespace TripleTapSDK
{
    public class TTAdsManager : MonoBehaviour
    {
        [SerializeField] private TTAdsService adsSetupService;
        public bool IsInitialized { get; private set; }
        private TTAdLocation _currentAdLocation;
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
            OnRewardedCompleted?.Invoke(_currentAdLocation, isSuccess);
            Debug.Log("RewardedCompleted called with isSuccess: " + isSuccess);

            if (isSuccess)
            {
                // Start/reset cooldown after a successful rewarded ad
            }
        }

        public bool IsRewardedAdAvailable()
        {
            return adsSetupService.IsRewardedAdAvailable();
        }

        public bool IsInterstitialAdAvailable()
        {
            // Check level gating
                    // var levelManager = AudioManager.Instance?.levelProgressionManager;
                    // if (remoteConfigKeys != null && levelManager != null)
                    // {
                    //     int requiredLevel = PlayerPrefs.GetInt(
                    //         remoteConfigKeys.adsLevelStart_RemoteConfigKey, 
                    //         remoteConfigKeys.adsLevelStart_RemoteDefaultValue);


                    //         Debug.Log($"Checking interstitial availability: Player level {levelManager.DisplayLevelNumber}, Required level {requiredLevel}");

                    //     if (requiredLevel > levelManager.DisplayLevelNumber)
                    //     {
                    //         return false;
                    //     }
                    // }

                    // return adsSetupService.IsInterAvailable();
            return false;
        }

        public void ShowInterstitial(TTAdLocation adLocation)
        {
            _currentAdLocation = adLocation;
            adsSetupService.ShowInterstitialAd();
        }

        public void InterstitialCompleted(bool isSuccess)
        {
            OnInterstitialCompleted?.Invoke(_currentAdLocation, isSuccess);

            if (isSuccess)
            {
            }
        }
      
    }
}
