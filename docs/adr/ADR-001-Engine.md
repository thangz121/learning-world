# ADR-001 — Unity 6 + URP cho Slice

Decision: Unity 6 LTS + URP + Addressables + NavMesh. Pin 1 version suốt Slice.
Context: target Windows, stylized Pixar-like, hardware thấp, AI agent dễ hỗ trợ, iteration nhanh.
Alternatives: Unreal (nặng, BehaviorTree mạnh nhưng overkill 4 tuổi), Godot (nhẹ nhưng asset/AI tooling yếu hơn).
Why: Windows tốt, asset ecosystem, C#, dễ tối ưu máy yếu.
Consequences: cấm đổi engine/render pipeline giữa Slice. Asset phải URP-compatible.

## Compiler pin (verified 2026-09-11, Unity 6000.6.0f1)
Pin không chỉ Editor version mà cả **C# LangVersion 9.0** (verified `-langversion:9.0`
mọi assembly trong `Library/Bee` rsp; target netstandard2.1; không `csc.rsp`).
`record struct`/`record class`, `with`, `global using` bị cấm (CS8773 ở C# 9).
Chi tiết: `ARCHITECTURE.md` §1, `contracts/Events.md`, `contracts/Services.md`.
