// _SharedKernel/IDragTarget.cs — Lead owns. Pointer-drag contract for world
// objects that are manipulated directly (dragged) rather than clicked: the
// rabbit arena's fed carrots are dragged off the bowl back to the garden.
// Implemented by A_World RabbitCarrot; consumed by A_World ClickRouter so a
// drag press is never ALSO routed as a walk/click. Same decoupling discipline
// as IClickTarget (no World->Brain reference, no reflection, no strings).
public interface IDragTarget {
  // True while the object wants the pointer: the router must leave the press
  // alone and let the scene's drag handler own it.
  bool CanDragNow { get; }
}
