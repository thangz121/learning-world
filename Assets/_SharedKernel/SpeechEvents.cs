// _SharedKernel/SpeechEvents.cs — Phase 2.1 typed speech events (Lead owns).
// Additive file: frozen Events.cs untouched. Same contract style (C#9 readonly
// structs, IEquatable, no Action<string>). Payloads are semantic only:
// NO raw audio, NO provider SDK types (§38).
using System;

// Fired when a speaking attempt starts (correlate logs by AttemptId).
[Serializable]
public readonly struct SpeechAttemptedEvent : IEquatable<SpeechAttemptedEvent> {
  public readonly string AttemptId;
  public readonly WordId Target;
  public readonly DateTime At;
  public SpeechAttemptedEvent(string AttemptId, WordId Target, DateTime At) {
    this.AttemptId = AttemptId; this.Target = Target; this.At = At;
  }
  public bool Equals(SpeechAttemptedEvent other) {
    return AttemptId == other.AttemptId
      && System.Collections.Generic.EqualityComparer<WordId>.Default.Equals(Target, other.Target)
      && At.Equals(other.At);
  }
  public override bool Equals(object obj) => obj is SpeechAttemptedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + (AttemptId != null ? AttemptId.GetHashCode() : 0);
      h = h * 31 + System.Collections.Generic.EqualityComparer<WordId>.Default.GetHashCode(Target);
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(SpeechAttemptedEvent l, SpeechAttemptedEvent r) => l.Equals(r);
  public static bool operator !=(SpeechAttemptedEvent l, SpeechAttemptedEvent r) => !l.Equals(r);
  public override string ToString() => $"SpeechAttemptedEvent {{ AttemptId = {AttemptId}, Target = {Target}, At = {At} }}";
}

// Fired when recognition resolves (transcript + confidence available).
[Serializable]
public readonly struct SpeechRecognizedEvent : IEquatable<SpeechRecognizedEvent> {
  public readonly string AttemptId;
  public readonly WordId Target;
  public readonly string Transcript;
  public readonly float Confidence;
  public SpeechRecognizedEvent(string AttemptId, WordId Target, string Transcript, float Confidence) {
    this.AttemptId = AttemptId; this.Target = Target; this.Transcript = Transcript; this.Confidence = Confidence;
  }
  public bool Equals(SpeechRecognizedEvent other) {
    return AttemptId == other.AttemptId
      && System.Collections.Generic.EqualityComparer<WordId>.Default.Equals(Target, other.Target)
      && Transcript == other.Transcript && Confidence.Equals(other.Confidence);
  }
  public override bool Equals(object obj) => obj is SpeechRecognizedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + (AttemptId != null ? AttemptId.GetHashCode() : 0);
      h = h * 31 + System.Collections.Generic.EqualityComparer<WordId>.Default.GetHashCode(Target);
      h = h * 31 + (Transcript != null ? Transcript.GetHashCode() : 0);
      h = h * 31 + Confidence.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(SpeechRecognizedEvent l, SpeechRecognizedEvent r) => l.Equals(r);
  public static bool operator !=(SpeechRecognizedEvent l, SpeechRecognizedEvent r) => !l.Equals(r);
  public override string ToString() => $"SpeechRecognizedEvent {{ AttemptId = {AttemptId}, Target = {Target}, Confidence = {Confidence} }}";
}

// Fired with the final assessment. Learning/quest layers consume THIS.
[Serializable]
public readonly struct SpeechAssessedEvent : IEquatable<SpeechAssessedEvent> {
  public readonly string AttemptId;
  public readonly WordId Target;
  public readonly SpeakingAssessment Assessment;
  public SpeechAssessedEvent(string AttemptId, WordId Target, SpeakingAssessment Assessment) {
    this.AttemptId = AttemptId; this.Target = Target; this.Assessment = Assessment;
  }
  public bool Equals(SpeechAssessedEvent other) {
    return AttemptId == other.AttemptId
      && System.Collections.Generic.EqualityComparer<WordId>.Default.Equals(Target, other.Target)
      && Assessment.Decision == other.Assessment.Decision
      && Assessment.FailureReason == other.Assessment.FailureReason;
  }
  public override bool Equals(object obj) => obj is SpeechAssessedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + (AttemptId != null ? AttemptId.GetHashCode() : 0);
      h = h * 31 + System.Collections.Generic.EqualityComparer<WordId>.Default.GetHashCode(Target);
      h = h * 31 + Assessment.Decision.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(SpeechAssessedEvent l, SpeechAssessedEvent r) => l.Equals(r);
  public static bool operator !=(SpeechAssessedEvent l, SpeechAssessedEvent r) => !l.Equals(r);
  public override string ToString() => $"SpeechAssessedEvent {{ AttemptId = {AttemptId}, Target = {Target}, Decision = {Assessment.Decision} }}";
}

// Fired when microphone capability changes (hot-plug §12/§15).
[Serializable]
public readonly struct SpeechCapabilityChangedEvent : IEquatable<SpeechCapabilityChangedEvent> {
  public readonly MicStatus Status;
  public readonly string DeviceName;
  public SpeechCapabilityChangedEvent(MicStatus Status, string DeviceName) {
    this.Status = Status; this.DeviceName = DeviceName;
  }
  public bool Equals(SpeechCapabilityChangedEvent other) {
    return Status == other.Status && DeviceName == other.DeviceName;
  }
  public override bool Equals(object obj) => obj is SpeechCapabilityChangedEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + Status.GetHashCode();
      h = h * 31 + (DeviceName != null ? DeviceName.GetHashCode() : 0);
      return h;
    }
  }
  public static bool operator ==(SpeechCapabilityChangedEvent l, SpeechCapabilityChangedEvent r) => l.Equals(r);
  public static bool operator !=(SpeechCapabilityChangedEvent l, SpeechCapabilityChangedEvent r) => !l.Equals(r);
  public override string ToString() => $"SpeechCapabilityChangedEvent {{ Status = {Status}, Device = {DeviceName} }}";
}
