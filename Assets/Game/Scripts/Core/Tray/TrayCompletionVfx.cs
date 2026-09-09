using System;
using System.Collections;
using UnityEngine;

namespace PopSort
{
    public class TrayCompletionVfx : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField, Min(1)] private int particleCount = 20;

        private Coroutine releaseRoutine;

        private void Awake()
        {
            EnsureParticleSystem();
        }

        public void Play(Vector3 position, float scale, float lifetime, Action release)
        {
            EnsureParticleSystem();
            transform.position = position;
            transform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);
            gameObject.SetActive(true);
            particles.Clear(true);
            particles.Play(true);
            particles.Emit(particleCount);

            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(ReleaseAfter(lifetime, release));
        }

        public void StopAndHide()
        {
            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = null;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }

        private IEnumerator ReleaseAfter(float lifetime, Action release)
        {
            yield return new WaitForSeconds(Mathf.Max(lifetime, 0.01f));
            releaseRoutine = null;
            StopAndHide();
            release?.Invoke();
        }

        private void EnsureParticleSystem()
        {
            if (particles != null) return;

            particles = GetComponent<ParticleSystem>();
            if (particles == null) particles = gameObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.1f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.45f), 0f), new GradientColorKey(Color.white, 0.3f), new GradientColorKey(new Color(1f, 0.55f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 20;
        }
    }
}
