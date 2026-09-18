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
    }
}