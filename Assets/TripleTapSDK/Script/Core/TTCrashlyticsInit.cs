using UnityEngine;
using System;
using Firebase;
using Firebase.Extensions;

namespace TripleTapSDK
{
    public class TTCrashlyticsInit : MonoBehaviour
    {
        public Action OnCrashlyticsInitialized = delegate { };
        public bool IsInitialized { get; private set; }

        private FirebaseApp app;

        private void Start()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    app = FirebaseApp.DefaultInstance;
                    IsInitialized = true;
                    OnCrashlyticsInitialized?.Invoke();
                    Debug.Log("[TTCrashlyticsInit] Firebase initialized successfully.");
                }
                else
                {
                    Debug.LogError($"[TTCrashlyticsInit] Could not resolve all Firebase dependencies: {dependencyStatus}");
                }
            });
        }
    }
}