// A_World/VillageCourtyard/ChildVoiceCaptureService.cs — Audio Pipeline & Child VAD Frontend.
// Ring-buffer 5s @ 16kHz Mono, 150Hz High-pass DSP, Real-time RMS, 900ms Hangover VAD, Whisper Mode AGC (+12dB).
// Pure C# 9.0 only. EditMode-testable without hardware microphone.
using System;
using UnityEngine;

public class ChildVoiceCaptureService : IDisposable {
  public const int SampleRate = 16000;
  public const float BufferDurationSec = 5.0f;
  public const int BufferCapacity = (int)(SampleRate * BufferDurationSec); // 80,000 samples
  public const float HighPassCutoffHz = 150f;
  public const float VadTailSilenceSec = 0.9f; // 900ms hangover
  public const float WhisperTimeoutSec = 4.0f;
  public const float WhisperGainMultiplier = 3.981f; // +12 dB = 10^(12/20)

  public Action<float> onRMSUpdated;
  public Action onSpeechStarted;
  public Action<float[]> onSpeechEnded;

  public bool IsRecording { get; private set; }
  public bool IsSpeaking { get; private set; }
  public bool WhisperModeActive { get; private set; }
  public float CurrentRMS { get; private set; }
  public float SilenceTimer { get; private set; }
  public float SpeechDuration { get; private set; }

  // High-pass filter state
  float _hpAlpha;
  float _prevX;
  float _prevY;

  // Ring buffer & recording accumulator
  readonly float[] _ringBuffer = new float[BufferCapacity];
  readonly float[] _speechBuffer = new float[BufferCapacity];
  int _ringHead;
  int _speechWriteIndex;
  float _silenceHangover;

  AudioClip _micClip;
  string _deviceName;
  int _lastMicPos;

  public ChildVoiceCaptureService() {
    InitFilter(HighPassCutoffHz, SampleRate);
  }

  void InitFilter(float cutoff, float fs) {
    float dt = 1f / fs;
    float rc = 1f / (2f * Mathf.PI * cutoff);
    _hpAlpha = rc / (rc + dt);
  }

  public void StartCapture(string deviceName = null) {
    if (IsRecording) StopCapture(); // one microphone stream only — retry flows re-enter StartCapture
    _deviceName = deviceName;
    _ringHead = 0;
    _speechWriteIndex = 0;
    _silenceHangover = 0f;
    SilenceTimer = 0f;
    SpeechDuration = 0f;
    IsSpeaking = false;
    WhisperModeActive = false;
    CurrentRMS = 0f;
    _prevX = 0f;
    _prevY = 0f;

    if (Microphone.devices != null && Microphone.devices.Length > 0) {
      try {
        _micClip = Microphone.Start(_deviceName, true, 5, SampleRate);
        _lastMicPos = 0;
        IsRecording = true;
      } catch (Exception) {
        IsRecording = false;
      }
    } else {
      // Offline / Test mode seam: capture is logically active for PushSampleBlock
      IsRecording = true;
    }
  }

  public void StopCapture() {
    if (IsRecording) {
      if (!string.IsNullOrEmpty(_deviceName) && Microphone.IsRecording(_deviceName)) {
        try { Microphone.End(_deviceName); } catch (Exception) { }
      }
      IsRecording = false;
    }
    IsSpeaking = false;
  }

  public void Tick(float dt) {
    if (!IsRecording) return;

    SilenceTimer += dt;
    if (SilenceTimer >= WhisperTimeoutSec && !WhisperModeActive) {
      WhisperModeActive = true;
    }

    if (_micClip != null && Microphone.IsRecording(_deviceName)) {
      int curPos = Microphone.GetPosition(_deviceName);
      if (curPos < 0 || curPos == _lastMicPos) return;

      int sampleCount = curPos >= _lastMicPos ? curPos - _lastMicPos : (BufferCapacity - _lastMicPos) + curPos;
      if (sampleCount > 0) {
        float[] chunk = new float[sampleCount];
        _micClip.GetData(chunk, _lastMicPos);
        _lastMicPos = curPos;
        ProcessSamples(chunk);
      }
    }
  }

  // External / Test injection entry point
  public void PushSampleBlock(float[] samples) {
    if (samples == null || samples.Length == 0) return;
    ProcessSamples(samples);
  }

  public void ProcessSamples(float[] rawSamples) {
    if (rawSamples == null || rawSamples.Length == 0) return;

    float sumSq = 0f;
    float gain = WhisperModeActive ? WhisperGainMultiplier : 1.0f;

    for (int i = 0; i < rawSamples.Length; i++) {
      // 1. High-pass filter (150Hz cutoff)
      float x = rawSamples[i] * gain;
      float y = _hpAlpha * (_prevY + x - _prevX);
      _prevX = x;
      _prevY = y;

      float filtered = Mathf.Clamp(y, -1f, 1f);

      // 2. Accumulate in ring buffer
      _ringBuffer[_ringHead] = filtered;
      _ringHead = (_ringHead + 1) % BufferCapacity;

      sumSq += filtered * filtered;

      // 3. Child speech accumulator
      if (IsSpeaking && _speechWriteIndex < BufferCapacity) {
        _speechBuffer[_speechWriteIndex++] = filtered;
      }
    }

    // 4. Calculate RMS Energy
    CurrentRMS = Mathf.Sqrt(sumSq / rawSamples.Length);
    onRMSUpdated?.Invoke(CurrentRMS);

    // 5. Child-Tuned VAD with Hangover Time (900ms)
    float vadThreshold = WhisperModeActive ? 0.008f : 0.016f;
    bool frameIsVoice = CurrentRMS >= vadThreshold;

    if (frameIsVoice) {
      SilenceTimer = 0f; // reset quiet timer
      if (WhisperModeActive) WhisperModeActive = false; // speech resumed — drop the +12dB boost
      _silenceHangover = VadTailSilenceSec;

      if (!IsSpeaking) {
        IsSpeaking = true;
        SpeechDuration = 0f;
        _speechWriteIndex = 0;
        onSpeechStarted?.Invoke();
      }
    } else {
      if (IsSpeaking) {
        float frameDt = (float)rawSamples.Length / SampleRate;
        _silenceHangover -= frameDt;
        if (_silenceHangover <= 0f) {
          IsSpeaking = false;
          _silenceHangover = 0f;
          float[] finalRecording = GetLastRecordingSamples();
          onSpeechEnded?.Invoke(finalRecording);
        }
      }
    }

    if (IsSpeaking) {
      SpeechDuration += (float)rawSamples.Length / SampleRate;
    }
  }

  public float[] GetLastRecordingSamples() {
    int count = Mathf.Max(0, _speechWriteIndex);
    if (count == 0) return Array.Empty<float>();
    float[] result = new float[count];
    Array.Copy(_speechBuffer, result, count);
    return result;
  }

  public AudioClip GetLastRecordingClip(string clipName = "ChildVoiceRecording") {
    float[] data = GetLastRecordingSamples();
    if (data.Length == 0) return null;
    AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
    clip.SetData(data, 0);
    return clip;
  }

  public void ResetSpeechBuffer() {
    _speechWriteIndex = 0;
    IsSpeaking = false;
    _silenceHangover = 0f;
  }

  public void Dispose() {
    StopCapture();
    if (_micClip != null) {
      try { UnityEngine.Object.DestroyImmediate(_micClip); } catch (Exception) { }
      _micClip = null;
    }
  }
}
