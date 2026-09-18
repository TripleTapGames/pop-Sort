using UnityEngine;
using GameAnalyticsSDK;
using System;
using Cysharp.Threading.Tasks;

namespace TripleTapSDK
{
    public class TTRemoteConfigInit : MonoBehaviour
    {
        public event Action OnRemoteConfigsReady = delegate { };
        public TTRemoteConfigKeys remoteConfigKeys;
        private bool isReady;

    void Awake()
    {
       Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
    private void OnEnable()
    {
        GameAnalytics.onInitialize += ((_, _) => Initialized());
    }
    private void OnDisable()
    {
        GameAnalytics.onInitialize -= ((_, _) => Initialized());
    }
    private void Start()
    {
        GameAnalytics.Initialize();
    }

    async void Initialized()
    {
        Debug.Log("[TTRemoteConfigInit] GameAnalytics Initialized");
        SetDefaultValue();

#if UNITY_EDITOR
        GetDummyRemoteConfig();
#elif ( ( UNITY_ANDROID || UNITY_IOS)  && !UNITY_EDITOR)
        await UniTask.WaitUntil(() => GameAnalytics.IsRemoteConfigsReady() == true);
        if (GameAnalytics.IsRemoteConfigsReady())
                {
                    isReady = true;
                    CheckRemoteConfigState();
                }
#endif

    }

    void CheckRemoteConfigState()
    {
        if (isReady)
        {
            Debug.Log(" ready CheckRemoteConfigState");
            GetConfigValues();
        }
    }

    void GetConfigValues()
    {
        string jsonString = GameAnalytics.GetRemoteConfigsValueAsString(remoteConfigKeys.adsLevelStart_RemoteConfigKey, remoteConfigKeys.adsLevelStart_RemoteDefaultValue.ToString());
        Debug.Log("AB_TEST AdsConfig*****" + jsonString);

        int adsConfigInt;
        if (!int.TryParse((jsonString ?? string.Empty).Trim(), out adsConfigInt))
        {
            adsConfigInt = remoteConfigKeys.adsLevelStart_RemoteDefaultValue;
        }

        PlayerPrefs.SetInt(remoteConfigKeys.adsLevelStart_RemoteConfigKey, adsConfigInt);

        OnRemoteConfigsReady?.Invoke();
    }

    void GetDummyRemoteConfig()
    {
        OnRemoteConfigsReady?.Invoke();
    }

    void SetDefaultValue()
    {
        Debug.Log("SetDefaultValue called");

        if (!PlayerPrefs.HasKey(remoteConfigKeys.adsLevelStart_RemoteConfigKey))
        {
            PlayerPrefs.SetInt(remoteConfigKeys.adsLevelStart_RemoteConfigKey, remoteConfigKeys.adsLevelStart_RemoteDefaultValue);
        }
    }

    }
}