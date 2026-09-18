using System;
using GameAnalyticsSDK;
using UnityEngine;

namespace TripleTapSDK
{
    /// <summary>
    /// Ad placement locations for interstitial and rewarded ads.
    /// </summary>
    public enum TTAdLocation
    {
        LevelComplete,
        LoadNextLevel,
        LevelFailed,
        RestartLevel,
        MainMenu,
        PauseMenu,
        Shop,
        Settings,
        Custom
    }

    /// <summary>
    /// Main manager for TripleTapSDK. Singleton that persists across scene loads.
    /// </summary>
    public class TTManager : MonoBehaviour
    {
        private static TTManager _instance;

        public static TTManager Instance
        {
            get
            {
                return _instance;
            }
        }

        [Header("SDK Components")]
        public TTAdsManager AdsManager;
        public TTFacebookInit FacebookInit;
        public TTRemoteConfigInit RemoteConfigInit;
        public TTCrashlyticsInit CrashlyticsInit;
        public GameObject analyticsServicePrefab;
        public TTGAService GAService;
        public TTAnalyticsService  TTAnalyticsService;

        /// <summary>
        /// Invoked when all SDK components are initialized.
        /// </summary>
        public event Action OnSDKInitialized;

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Initialize()
        {
            // Subscribe to initialization events
            if (AdsManager != null)
            {
                AdsManager.OnAdsInitialized += OnComponentInitialized;
            }

            if (FacebookInit != null)
            {
                FacebookInit.OnFbInitialized += OnComponentInitialized;
            }

            if (RemoteConfigInit != null)
            {
                RemoteConfigInit.OnRemoteConfigsReady += OnComponentInitialized;
            }

            if (CrashlyticsInit != null)
            {
                CrashlyticsInit.OnCrashlyticsInitialized += OnComponentInitialized;
            }

            CheckAllInitialized();
        }

        private void OnComponentInitialized()
        {
            CheckAllInitialized();
        }

        private void CheckAllInitialized()
        {
            bool adsReady = AdsManager == null || AdsManager.IsInitialized;
            bool fbReady = FacebookInit == null || FacebookInit.IsInitialized;
            bool crashlyticsReady = CrashlyticsInit == null || CrashlyticsInit.IsInitialized;

            if (adsReady && fbReady && crashlyticsReady && !IsInitialized)
            {
                IsInitialized = true;
                OnSDKInitialized?.Invoke();
                Debug.Log("[TTManager] TripleTapSDK fully initialized!");

                 // Auto-track MAX ad impressions in GameAnalytics
                analyticsServicePrefab.SetActive(true);
                GameAnalyticsILRD.SubscribeMaxImpressions();

            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            // Unsubscribe from events
            if (AdsManager != null)
            {
                AdsManager.OnAdsInitialized -= OnComponentInitialized;
            }

            if (FacebookInit != null)
            {
                FacebookInit.OnFbInitialized -= OnComponentInitialized;
            }

            if (RemoteConfigInit != null)
            {
                RemoteConfigInit.OnRemoteConfigsReady -= OnComponentInitialized;
            }

            if (CrashlyticsInit != null)
            {
                CrashlyticsInit.OnCrashlyticsInitialized -= OnComponentInitialized;
            }
        }

    }
}
