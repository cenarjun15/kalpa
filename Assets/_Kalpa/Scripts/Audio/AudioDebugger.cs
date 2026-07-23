// AudioDebugger.cs
// Attach to any GameObject in the scene. Press these keys in Play mode:
//   F1 = print all diagnostic info
//   F2 = force-play a test sound at 100% volume
//   F3 = reset audio PlayerPrefs to defaults
//   F4 = play jump sound

using UnityEngine;
using Kalpa.Audio;
using Kalpa.Blocks;

namespace Kalpa.Diagnostics
{
    public sealed class AudioDebugger : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) PrintDiagnostics();
            if (Input.GetKeyDown(KeyCode.F2)) ForcePlayTestSound();
            if (Input.GetKeyDown(KeyCode.F3)) ResetVolumes();
            if (Input.GetKeyDown(KeyCode.F4)) PlayJumpDirect();
        }

        private void PrintDiagnostics()
        {
            Debug.Log("=== AUDIO DIAGNOSTICS ===");

            // Volume state
            var audio = AudioSystem.Instance;
            if (audio == null)
            {
                Debug.LogError("[Debug] AudioSystem.Instance is NULL!");
                return;
            }
            Debug.Log($"[Debug] MasterVolume: {audio.MasterVolume}");
            Debug.Log($"[Debug] SfxVolume: {audio.SfxVolume}");
            Debug.Log($"[Debug] AmbientVolume: {audio.AmbientVolume}");

            // AudioListener check
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            Debug.Log($"[Debug] AudioListeners in scene: {listeners.Length}");
            foreach (var l in listeners)
            {
                Debug.Log($"[Debug]   Listener on: {l.gameObject.name} (enabled: {l.enabled}, obj active: {l.gameObject.activeInHierarchy})");
            }
            Debug.Log($"[Debug] AudioListener.volume (global): {AudioListener.volume}");
            Debug.Log($"[Debug] AudioListener.pause: {AudioListener.pause}");

            // Check clips
            var clips = Resources.LoadAll<AudioClip>("Audio");
            Debug.Log($"[Debug] Clips found in Resources/Audio/: {clips.Length}");
            foreach (var c in clips)
            {
                Debug.Log($"[Debug]   {c.name}: length={c.length}s, channels={c.channels}, freq={c.frequency}, loadState={c.loadState}");
            }

            // AudioSources
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            Debug.Log($"[Debug] AudioSources in scene: {sources.Length}");
        }

        private void ForcePlayTestSound()
        {
            Debug.Log("[Debug] Force-playing test sound at 100% volume via AudioSource.PlayClipAtPoint...");
            var clip = Resources.Load<AudioClip>("Audio/jump");
            if (clip == null)
            {
                Debug.LogError("[Debug] Could not load Audio/jump!");
                return;
            }
            Debug.Log($"[Debug] Loaded clip: {clip.name}, length={clip.length}s");

            // PlayClipAtPoint is the simplest possible way to play a sound.
            // If this doesn't work, the problem is 100% Unity/Windows, not our code.
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1f);
            Debug.Log("[Debug] Called PlayClipAtPoint. If you hear nothing, issue is Unity/Windows.");
        }

        private void ResetVolumes()
        {
            PlayerPrefs.DeleteKey("Kalpa.Vol.Master");
            PlayerPrefs.DeleteKey("Kalpa.Vol.Sfx");
            PlayerPrefs.DeleteKey("Kalpa.Vol.Ambient");
            PlayerPrefs.Save();
            Debug.Log("[Debug] PlayerPrefs volume keys cleared. RESTART the game to reload defaults.");
        }

        private void PlayJumpDirect()
        {
            AudioSystem.Instance?.PlayJump();
            Debug.Log("[Debug] Called AudioSystem.PlayJump()");
        }
    }
}