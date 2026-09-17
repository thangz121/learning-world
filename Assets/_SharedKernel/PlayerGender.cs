// _SharedKernel/PlayerGender.cs — Lead owns. Phase 2.4 FINAL POLISH.
// Player avatar gender choice. Pure enum, no UnityEngine.
// Boy/Girl are the two authored player presentations sharing the same
// gameplay contract (CharacterRoot yaw, collider, NavMeshAgent, quest
// wiring). Visual differences (tint, clothing) are applied in
// PlayerVisual.BuildVisual without touching movement/collision/quest/camera.
// Persisted via PlayerProgress.PlayerGender + LocalSave DTO.
public enum PlayerGender {
  Boy = 0,
  Girl = 1,
}
