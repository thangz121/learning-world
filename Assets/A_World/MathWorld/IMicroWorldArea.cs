// A_World/MathWorld/IMicroWorldArea.cs — S3-P2Z15 (small shared seam).
// Every live hub-gate micro-world area (Counting Garden, Discovery Garden)
// exposes the same travel contract: the player reference and the enter/exit
// beats. This interface names that contract so MicroWorldPortal dispatches
// through ONE seam instead of hardcoding area types. (Build Yard / Delivery /
// Match gameplay was removed in the 2026-09-29 cleanup; those gates remain
// landmark skeletons with no area.)
// The methods are async void in the implementations; the interface keeps the
// void signature (fire-and-forget beats owned by the area).
// C# 9.0 only.
public interface IMicroWorldArea {
  ClickToMove Player { get; }
  void EnterFromHub();
  void ExitToHub();
}
