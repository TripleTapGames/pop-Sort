using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TripleTapSDK
{
    public class TTLoadingView : MonoBehaviour
    {
        [Header("UI")]
        public Slider loadingBar;

        [Header("Scene Loading")]
        [SerializeField] private int sceneIndexToLoad = 1;
        [SerializeField] private bool loadSceneInBackground = true;

        [Header("SDK Reference")]
        [SerializeField] private TTManager ttManager;

        // Tracking
        private bool _sdkReady;
        private AsyncOperation _sceneLoadOperation;
        private bool _sceneReady;
        private float _sceneLoadProgress;

        // Progress weights
        private const float SDK_WEIGHT = 0.5f;
        private const float SCENE_WEIGHT = 0.5f;

        void Awake()
        {
#if !UNITY_EDITOR
            Debug.unityLogger.logEnabled = false;
#endif

            if (loadingBar != null)
            {
                loadingBar.minValue = 0f;
                loadingBar.maxValue = 1f;
                loadingBar.value = 0f;
            }
        }

        void Start()
        {
            // Subscribe to TTManager
            if (ttManager != null)
            {
                if (ttManager.IsInitialized)
                {
                    OnSDKInitialized();
                }
                else
                {
                    ttManager.OnSDKInitialized += OnSDKInitialized;
                }
            }
            else
            {
                _sdkReady = true;
            }

            // Start scene loading
            if (loadSceneInBackground)
            {
                StartCoroutine(LoadSceneAsync());
            }
            else
            {
                _sceneReady = true;
            }

            UpdateProgress();
        }

        private IEnumerator LoadSceneAsync()
        {
            yield return null;

            _sceneLoadOperation = SceneManager.LoadSceneAsync(sceneIndexToLoad);
            _sceneLoadOperation.allowSceneActivation = false;

            while (_sceneLoadOperation.progress < 0.9f)
            {
                _sceneLoadProgress = _sceneLoadOperation.progress / 0.9f;
                UpdateProgress();
                yield return null;
            }

            _sceneLoadProgress = 1f;
            _sceneReady = true;
            UpdateProgress();
            TryOpenGame();
        }

        private void OnSDKInitialized()
        {
            if (ttManager != null) ttManager.OnSDKInitialized -= OnSDKInitialized;
            _sdkReady = true;
            Debug.Log("[TTLoadingView] SDK initialized");
            UpdateProgress();
            TryOpenGame();
        }

        private void UpdateProgress()
        {
            if (loadingBar == null) return;

            float progress = 0f;

            if (_sdkReady) progress += SDK_WEIGHT;

            if (loadSceneInBackground)
            {
                progress += _sceneLoadProgress * SCENE_WEIGHT;
            }
            else if (_sceneReady)
            {
                progress += SCENE_WEIGHT;
            }

            loadingBar.value = progress;
        }

        private void TryOpenGame()
        {
            if (_sdkReady && _sceneReady)
            {
                Debug.Log("[TTLoadingView] All ready. Opening game...");
                StartCoroutine(WaitForProgressAndActivate());
            }
        }

        private IEnumerator WaitForProgressAndActivate()
        {
            while (loadingBar != null && loadingBar.value < 0.98f)
            {
                yield return null;
            }

            if (_sceneLoadOperation != null)
            {
                _sceneLoadOperation.allowSceneActivation = true;
            }

            // gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (ttManager != null) ttManager.OnSDKInitialized -= OnSDKInitialized;
        }
    }
}