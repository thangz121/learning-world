// _SharedKernel/IClickTarget.cs — Lead owns. Click contract for world NPCs and
// objects whose interaction is NOT a WordSeenEvent (Milo, Mia).
// Implemented by B_Brain presenters, consumed by A_World ClickRouter. Keeps
// World->Brain decoupled (no asmdef reference, no reflection, no strings).
public interface IClickTarget {
  void OnClicked();
}
