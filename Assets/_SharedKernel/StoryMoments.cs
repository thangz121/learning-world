// _SharedKernel/StoryMoments.cs — Lead owns. Narrative presentation moments.
//
// A StoryMoment is a PRESENTATION-LEVEL event (not quest rules): it tells
// presenters that something story-relevant happened so each character can
// react according to narrative ownership (see docs/GOLDEN_CHARACTER_STANDARD
// §11). Quest rules stay in QuestManager/HintService; this bus only routes
// reactions. C# 9.0 readonly-struct pattern (matches Events.cs).
using System;

public enum StoryMoment {
  StoryIntro,     // quest opened: Milo narrates, bubble asks
  CorrectChoice,  // player chose right (bring to Mia): Mia happy, Milo waves
  WrongChoice,    // player chose wrong: Mia sad, Milo encourages, retry stays
  QuestComplete,  // quest done: Mia+Milo celebrate, player hops, camera focus
}

[Serializable]
public readonly struct StoryMomentEvent : IEquatable<StoryMomentEvent> {
  public readonly StoryMoment Moment;
  public readonly DateTime At;
  public StoryMomentEvent(StoryMoment Moment, DateTime At) {
    this.Moment = Moment; this.At = At;
  }
  public void Deconstruct(out StoryMoment moment, out DateTime at) {
    moment = Moment; at = At;
  }
  public bool Equals(StoryMomentEvent other) {
    return Moment == other.Moment && At.Equals(other.At);
  }
  public override bool Equals(object obj) => obj is StoryMomentEvent o && Equals(o);
  public override int GetHashCode() {
    unchecked {
      int h = 17;
      h = h * 31 + Moment.GetHashCode();
      h = h * 31 + At.GetHashCode();
      return h;
    }
  }
  public static bool operator ==(StoryMomentEvent l, StoryMomentEvent r) => l.Equals(r);
  public static bool operator !=(StoryMomentEvent l, StoryMomentEvent r) => !l.Equals(r);
  public override string ToString() => $"StoryMomentEvent {{ Moment = {Moment}, At = {At} }}";
}
