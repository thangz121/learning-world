// _SharedKernel/WorldFoundation.cs — Phase 3.0 WORLD FOUNDATION (Lead owns).
// World-navigation types ONLY. This file knows NOTHING about learning:
// no Lesson, no Topic, no Question, no Activity, no Curriculum (phases 3.1+).
// World State (CurrentWorld/CurrentSubject/EntryPoint/ReturnPoint) lives here;
// Learning State lives elsewhere and MUST NOT be mixed into this file.
// C# 9.0 only (no record/with/file-scoped namespaces). Matches the value-
// semantics pattern of sibling SharedKernel structs (Ids.cs / Services.cs).
using System;
using System.Collections.Generic;

// Typed subject identity. Raw strings parse to this at the boundary
// (gate wiring, tests); gameplay compares SubjectId, never raw strings.
[Serializable]
public readonly struct SubjectId : IEquatable<SubjectId> {
  public readonly string Value;

  public SubjectId(string v) {
    string t = string.IsNullOrEmpty(v) ? "" : v.ToLowerInvariant().Trim();
    Value = string.IsNullOrEmpty(t) ? "main" : t;
  }

  public void Deconstruct(out string value) { value = Value; }
  public bool Equals(SubjectId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
  public override bool Equals(object obj) => obj is SubjectId o && Equals(o);
  public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
  public static bool operator ==(SubjectId l, SubjectId r) => l.Equals(r);
  public static bool operator !=(SubjectId l, SubjectId r) => !l.Equals(r);
  public override string ToString() => Value;
  public static implicit operator string(SubjectId id) => id.Value;
}

// Canonical Phase 3.0 subjects. "main" is the Main World (hub), not a subject.
public static class SubjectIds {
  public static readonly SubjectId Main = new SubjectId("main");
  public static readonly SubjectId Math = new SubjectId("math");
  public static readonly SubjectId Thinking = new SubjectId("thinking");
  public static readonly SubjectId English = new SubjectId("english");
  public static readonly SubjectId Vietnamese = new SubjectId("vietnamese");

  public static readonly SubjectId[] Subjects =
    { Math, Thinking, English, Vietnamese };
}

// Published exactly once per world change (enter subject / return to main).
// HUD, camera and future systems react to this; NOBODY polls the service.
[Serializable]
public readonly struct WorldChangedEvent : IEquatable<WorldChangedEvent> {
  public readonly SubjectId From;
  public readonly SubjectId To;
  public readonly DateTime At;

  public WorldChangedEvent(SubjectId From, SubjectId To, DateTime At) {
    this.From = From; this.To = To; this.At = At;
  }

  public void Deconstruct(out SubjectId from, out SubjectId to, out DateTime at) {
    from = From; to = To; at = At;
  }

  public bool Equals(WorldChangedEvent other) {
    return EqualityComparer<SubjectId>.Default.Equals(From, other.From)
      && EqualityComparer<SubjectId>.Default.Equals(To, other.To)
      && At.Equals(other.At);
  }

  public override bool Equals(object obj) => obj is WorldChangedEvent o && Equals(o);

  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + EqualityComparer<SubjectId>.Default.GetHashCode(From);
      h = h * 31 + EqualityComparer<SubjectId>.Default.GetHashCode(To);
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }

  public static bool operator ==(WorldChangedEvent l, WorldChangedEvent r) => l.Equals(r);
  public static bool operator !=(WorldChangedEvent l, WorldChangedEvent r) => !l.Equals(r);
  public override string ToString() => $"WorldChangedEvent {{ From = {From}, To = {To}, At = {At} }}";
}

// World-navigation service contract (in-memory Scene/Session lifetime, like
// QuestManager). Implementations MUST be idempotent: entering the current
// world publishes NOTHING, so trigger spam and polling rising-edges are safe.
public interface IWorldNavService {
  SubjectId Current { get; }
  bool IsInSubject { get; }
  void Enter(SubjectId subject);
  void ReturnToMain();
}
