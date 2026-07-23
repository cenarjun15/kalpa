// ============================================================================
// AudioSystem.cs
// ----------------------------------------------------------------------------
// Central audio manager. Loads all sound clips from Resources/Audio at boot,
// exposes typed play methods for the rest of the game, and manages a small
// pool of AudioSource objects to avoid clip-cutting when many sounds play at
// once.
// ============================================================================

using System.Collections.Generic;
using Kalpa.Blocks;
using Kalpa.Core;
using UnityEngine;

namespace Kalpa.Audio
{
    /// <summary>
    /// Root audio controller. Placed once in the scene alongside GameManager.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class AudioSystem : MonoBehaviour
    {
        // Singleton
        public static AudioSystem Instance { get; private set; }

        [Header("Volume defaults (0..1)")]
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float defaultAmbientVolume = 0.35f;

        [Header("Pool")]
        [SerializeField, Range(4, 32)] private int sfxSourceCount = 12;

        private const string PrefMaster = "Kalpa.Vol.Master";
        private const string PrefSfx = "Kalpa.Vol.Sfx";
        private const string PrefAmbient = "Kalpa.Vol.Ambient";

        public float MasterVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float AmbientVolume { get; private set; }

        private AudioClip breakStone;
        private AudioClip breakGrass;
        private AudioClip placeBlock;
        private AudioClip jump;
        private AudioClip ambientWind;
        private readonly List<AudioClip> stepGrass = new List<AudioClip>();
        private readonly List<AudioClip> stepStone = new List<AudioClip>();

        private AudioSource[] sfxPool;
        private int nextSfxIndex;
        private AudioSource ambientSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadVolumes();
            LoadClips();
            BuildPool();
            StartAmbient();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadVolumes()
        {
            MasterVolume = PlayerPrefs.GetFloat(PrefMaster, defaultMasterVolume);
            SfxVolume = PlayerPrefs.GetFloat(PrefSfx, defaultSfxVolume);
            AmbientVolume = PlayerPrefs.GetFloat(PrefAmbient, defaultAmbientVolume);
        }

        public void SetMasterVolume(float v)
        {
            MasterVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(PrefMaster, MasterVolume);
            ApplyAmbientVolume();
        }

        public void SetSfxVolume(float v)
        {
            SfxVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(PrefSfx, SfxVolume);
        }

        public void SetAmbientVolume(float v)
        {
            AmbientVolume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(PrefAmbient, AmbientVolume);
            ApplyAmbientVolume();
        }

        private void LoadClips()
        {
            breakStone = TryLoad("break_stone");
            breakGrass = TryLoad("break_grass");
            placeBlock = TryLoad("place_block");
            jump = TryLoad("jump");
            ambientWind = TryLoad("ambient_wind");

            AddIfLoaded(stepGrass, TryLoad("step_grass_1"));
            AddIfLoaded(stepGrass, TryLoad("step_grass_2"));
            AddIfLoaded(stepStone, TryLoad("step_stone_1"));
            AddIfLoaded(stepStone, TryLoad("step_stone_2"));

            Debug.Log(
                $"[Audio] Loaded — break_stone:{Bool(breakStone)} break_grass:{Bool(breakGrass)} " +
                $"place:{Bool(placeBlock)} jump:{Bool(jump)} ambient:{Bool(ambientWind)} " +
                $"grass_steps:{stepGrass.Count} stone_steps:{stepStone.Count}");
        }

        private static AudioClip TryLoad(string nameWithoutExt)
            => Resources.Load<AudioClip>("Audio/" + nameWithoutExt);

        private static void AddIfLoaded(List<AudioClip> list, AudioClip clip)
        {
            if (clip != null) list.Add(clip);
        }

        private static string Bool(AudioClip c) => c != null ? "✓" : "✗";

        private void BuildPool()
        {
            sfxPool = new AudioSource[sfxSourceCount];
            for (int i = 0; i < sfxSourceCount; i++)
            {
                var go = new GameObject($"SfxSource_{i:D2}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.loop = false;
                sfxPool[i] = src;
            }
        }

        private void StartAmbient()
        {
            var go = new GameObject("AmbientSource");
            go.transform.SetParent(transform, false);
            ambientSource = go.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f;

            if (ambientWind != null)
            {
                ambientSource.clip = ambientWind;
                ApplyAmbientVolume();
                ambientSource.Play();
            }
        }

        private void ApplyAmbientVolume()
        {
            if (ambientSource != null)
                ambientSource.volume = MasterVolume * AmbientVolume;
        }

        public void PlayBreak(BlockData block)
        {
            var clip = block != null && block.Category == BlockCategory.Natural
                ? (breakGrass ?? breakStone)
                : (breakStone ?? breakGrass);
            PlayOneShot(clip, pitchVariance: 0.10f);
        }

        public void PlayPlace(BlockData _)
            => PlayOneShot(placeBlock, pitchVariance: 0.08f);

        public void PlayJump()
            => PlayOneShot(jump, pitchVariance: 0.06f, volume: 0.7f);

        public void PlayFootstep(BlockCategory groundCategory)
        {
            var list = groundCategory == BlockCategory.Stone ? stepStone : stepGrass;
            if (list.Count == 0) list = stepStone.Count > 0 ? stepStone : stepGrass;
            if (list.Count == 0) return;

            var clip = list[Random.Range(0, list.Count)];
            PlayOneShot(clip, pitchVariance: 0.12f, volume: 0.55f);
        }

        private void PlayOneShot(AudioClip clip, float pitchVariance = 0f, float volume = 1f)
        {
            if (clip == null) return;

            var src = sfxPool[nextSfxIndex];
            nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Length;

            src.clip = clip;
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.volume = MasterVolume * SfxVolume * volume;
            src.Play();
        }
    }
}