// A_World/MathWorld/IMicroWorldArea.cs — the shared travel seam (PHASE 5).
// The live walk-in portal exposes one contract: the player reference and the
// enter/exit beats, so MicroWorldPortal dispatches through ONE seam. The old
// counting garden / discovery garden areas are deleted; the Selection Yard is
// the only implementer.
// The methods are async void in the implementations; the interface keeps the
// void signature (fire-and-forget beats owned by the area).
// C# 9.0 only.
public interface IMicroWorldArea {
  ClickToMove Player { get; }
  void EnterFromHub();
  void ExitToHub();
}
