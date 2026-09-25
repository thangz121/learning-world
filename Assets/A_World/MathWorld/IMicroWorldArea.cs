// A_World/MathWorld/IMicroWorldArea.cs — S3-P2Z15 GAMEPLAY #5 (small seam).
// Every hub-gate micro-world area (Counting Garden, Build Yard, Delivery
// Village) already exposes the same travel contract: the player reference and
// the enter/exit beats. This interface names that contract so MicroWorldPortal
// dispatches through ONE seam instead of hardcoding area types — the concrete
// portal fields stay for compatibility (no breaking change to old wiring).
// The methods are async void in the implementations; the interface keeps the
// void signature (fire-and-forget beats owned by the area).
// C# 9.0 only.
public interface IMicroWorldArea {
  ClickToMove Player { get; }
  void EnterFromHub();
  void ExitToHub();
}
