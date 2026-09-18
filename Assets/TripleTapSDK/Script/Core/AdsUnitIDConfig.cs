using UnityEngine;

namespace TripleTapSDK
{
    [CreateAssetMenu(fileName = "TTAdsConfig", menuName = "TripleTapSDK/TTAdsConfig")]
    public class TTAdsConfig : ScriptableObject
    {
        [Header("iOS IDs")]
        [Tooltip("iOS Interstitial ad unit id")]
        public string iosInterstitialID;

        [Tooltip("iOS Rewarded ad unit id")]
        public string iosRewardedID;

        [Tooltip("iOS Banner ad unit id")]
        public string iosBannerID;

        [Header("Android IDs")]
        [Tooltip("Android Interstitial ad unit id")]
        public string androidInterstitialID;

        [Tooltip("Android Rewarded ad unit id")]
        public string androidRewardedID;

        [Tooltip("Android Banner ad unit id")]
        public string androidBannerID;

        // Helper properties to get the correct ID based on platform
        public string interstitialID =>
#if UNITY_IOS
            iosInterstitialID;
#else
            androidInterstitialID;
#endif

        public string rewardedID =>
#if UNITY_IOS
            iosRewardedID;
#else
            androidRewardedID;
#endif

        public string bannerID =>
#if UNITY_IOS
            iosBannerID;
#else
            androidBannerID;
#endif
    }
}
