// Assets/A_World/VillageMarket/SpatialAudioDuckingService.cs — Audio Mixer Ducking & Spatial Pan Service.
// Manages:
//   1. Dynamic Audio Ducking: -24dB attenuation on Ambient BGM during voice prompt playback (100ms attack, 300ms release).
//   2. 3D Spatial Audio Cue (panStereo from -0.8f to +0.8f) for auditory localization and scaffolding direction.
//   3. Playback speed adaptation (1.0x normal, 0.8x slow-emphasized).
// Pure C# 9.0 only. EditMode-safe.
using System;
using UnityEngine;

public class SpatialAudioDuckingService : IDisposable {
  public const float DuckingAttenuationDb = -24.0f; // -24 dB
  public const float DuckingLinearFloor = 0.0631f;  // 10^(-24/20) ~ 0.0631
  public const float DuckingAttackTime = 0.10f;    // 100ms
  public const float DuckingReleaseTime = 0.30f;   // 300ms

  public AudioSource AmbientSource { get; set; }
  public AudioSource VoiceSource { get; set; }
  public AudioSource SfxSource { get; set; }

  public bool IsDucking { get; private set; }
  public float CurrentAmbientGain { get; private set; } = 1.0f;
  public float CurrentPan { get; private set; } = 0.0f;
  public float PlaybackSpeed { get; private set; } = 1.0f;

  public string LastPlayedClipName { get; private set; } = "";
  public float LastPlayedVolume { get; private set; } = 1.0f;
  public int DuckingActivationCount { get; private set; }

  // Fallback programmatic tick tracking
  float _duckingTransitionT = 0f;

  public SpatialAudioDuckingService(AudioSource ambient = null, AudioSource voice = null, AudioSource sfx = null) {
    AmbientSource = ambient;
    VoiceSource = voice;
    SfxSource = sfx;
    CurrentAmbientGain = 1.0f;
    IsDucking = false;
  }

  // Calculate pan from UI horizontal position (-1f = far left, +1f = far right)
  public static float CalculatePan(float normalizedX) {
    // normalizedX in [0..1] -> pan in [-0.8f .. +0.8f]
    float centered = Mathf.Clamp01(normalizedX) * 2f - 1f;
    return Mathf.Clamp(centered * 0.8f, -0.8f, 0.8f);
  }

  public void SetStereoPan(float pan) {
    CurrentPan = Mathf.Clamp(pan, -1.0f, 1.0f);
    if (VoiceSource != null) VoiceSource.panStereo = CurrentPan;
    if (SfxSource != null) SfxSource.panStereo = CurrentPan;
  }

  public void StartDucking() {
    IsDucking = true;
    DuckingActivationCount++;
  }

  public void StopDucking() {
    IsDucking = false;
  }

  public void PlayVoicePrompt(AudioClip clip, float speed = 1.0f, float pan = 0f) {
    SetStereoPan(pan);
    PlaybackSpeed = speed;
    LastPlayedClipName = clip != null ? clip.name : ""; // honest: no clip => nothing was played
    LastPlayedVolume = 1.0f;

    if (clip == null) {
      StopDucking(); // never duck the ambient for a silent "prompt"
      return;
    }

    StartDucking();
    if (VoiceSource != null) {
      VoiceSource.clip = clip;
      VoiceSource.pitch = speed;
      VoiceSource.Play();
    }
  }

  public void PlaySoundCue(AudioClip clip, float pan = 0f, float volume = 1.0f) {
    SetStereoPan(pan);
    LastPlayedClipName = clip != null ? clip.name : "";
    LastPlayedVolume = volume;

    if (SfxSource != null && clip != null) {
      SfxSource.PlayOneShot(clip, volume);
    }
  }

  // Update loop for smooth ducking transition (100ms attack, 300ms release)
  public void Tick(float dt) {
    if (dt <= 0f) return;

    float targetGain = IsDucking ? DuckingLinearFloor : 1.0f;
    float rate = IsDucking ? (1.0f / DuckingAttackTime) : (1.0f / DuckingReleaseTime);

    CurrentAmbientGain = Mathf.MoveTowards(CurrentAmbientGain, targetGain, rate * dt);

    if (AmbientSource != null) {
      AmbientSource.volume = CurrentAmbientGain;
    }

    // Auto-release ducking when VoiceSource finishes playing
    if (IsDucking && VoiceSource != null && !VoiceSource.isPlaying && VoiceSource.clip != null) {
      StopDucking();
    }
  }

  public void Dispose() {
    StopDucking();
    CurrentAmbientGain = 1.0f;
  }
}
