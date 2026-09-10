using UnityEngine;

namespace PopSort
{
    /// <summary>
    /// Single point for short gameplay sounds. Add one to the scene and assign its clips.
    /// </summary>
    public class SfxManager : MonoBehaviour
    {
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip ballPopSfx;
        [SerializeField] private AudioClip ballLandedInTraySfx;
        [SerializeField] private AudioClip trayFilledSfx;

        private static SfxManager instance;
        private static bool hasLoggedMissingManager;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            EnsureAudioSource();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void PlayBallPop() => GetInstance()?.Play(manager => manager.ballPopSfx);

        public static void PlayBallLandedInTray() => GetInstance()?.Play(manager => manager.ballLandedInTraySfx);

        public static void PlayTrayFilled() => GetInstance()?.Play(manager => manager.trayFilledSfx);

        private static SfxManager GetInstance()
        {
            if (instance != null) return instance;

            instance = FindFirstObjectByType<SfxManager>();
            if (instance == null && !hasLoggedMissingManager)
            {
                Debug.LogWarning("No SfxManager is present in the scene. Gameplay SFX will not play.");
                hasLoggedMissingManager = true;
            }

            return instance;
        }

        private void Play(System.Func<SfxManager, AudioClip> getClip)
        {
            AudioClip clip = getClip(this);
            if (clip == null) return;

            EnsureAudioSource();
            sfxSource.PlayOneShot(clip);
        }

        private void EnsureAudioSource()
        {
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
            if (sfxSource != null) return;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 0f;
            sfxSource.playOnAwake = false;
        }
    }
}
