using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TripleTapSDK
{
    [CreateAssetMenu(fileName = "TTRemoteConfigKeys", menuName = "TripleTapSDK/TTRemoteConfigKeys")]
    public class TTRemoteConfigKeys : ScriptableObject
    {
        [Header("RemoteConfigKey")]
         public string adsLevelStart_RemoteConfigKey;

        [Header("DefaultValue")]
          public int adsLevelStart_RemoteDefaultValue;

        [Header("Interstitial Interval")]
        public string interstitialInterval_RemoteConfigKey = "InterstitialAdIntervalSeconds";
        [Min(0)] public int interstitialInterval_RemoteDefaultValue = 60;
    }
}
