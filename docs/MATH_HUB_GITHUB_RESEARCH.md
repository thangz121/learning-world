# MATH HUB GITHUB RESEARCH — classification (§12)

Date: 2026-09-22. Rule: small proven techniques over frameworks. No new
packages were added. Inspection level recorded honestly per candidate.

## A. yanivnizan/unity3d-levelup — REFERENCE ONLY
- Inspected: README + model overview (World/Level/Gate/Mission/Reward) + license.
- Facts: SOOMLA F2P progression library (2012-2014, Apache-2.0), Unity 4-era
  .unitypackage distribution, needs submodules + Core/Store/Profile modules.
- Useful ideas taken: World contains innerWorlds (matches Hub→Micro-World
  hierarchy); Gate = closed portal with open conditions; GatesList AND/OR
  composition (future lock/progression design input).
- Verdict: DO NOT IMPORT (obsolete Unity, economy/IAP focus, dependency chain).

## B. fertilesoilproductions/MAST — REFERENCE ONLY
- Inspected: README + feature list + source layout + requirements.
- Facts: Unity 6 LTS, URP-compatible, MIT, editor-only assembly, no
  third-party deps. Grid placement, cell-accurate occupancy, greeble,
  material painter with journal, prefab finalizing.
- Useful ideas taken: cell-accurate occupancy (our equivalent: spoke-distance
  checks + CT-P45C pins); face-building (our gates face approach spurs).
- Verdict: DO NOT IMPORT/DEPEND — it is a Scene-view painting tool; our
  worlds are code-built deterministic + headless-tested by architecture
  decision. Techniques adapted, dependency rejected.

## C. mjstarrett/Landmarks — REFERENCE ONLY
- Inspected: repo README (one-line: spatial navigation experiment framework).
- Facts: minimal public documentation; license/Unity version/URP status not
  verifiable from README.
- Verdict: DO NOT IMPORT (unverifiable license — GPL risk per §12; our
  landmarks are hand-designed: Great Abacus, return arch, gate clusters).

## D. Unity-Technologies/Graphics — REFERENCE ONLY
- Inspection level: prior foundation knowledge (URP samples, ShaderGraph
  examples); no code taken.
- Verdict: visual/technical reference for stylized URP lighting only. Our hub
  stays on the locked URP/Lit flat-material pipeline (no custom shaders).

## E. Unity URP Sample Scene / environment examples — REFERENCE ONLY
- Verdict: composition reference (garden modularity, visual hierarchy).
  Assets/licenses do not permit direct reuse in our pipeline; all hub content
  uses the already-curated CC0 kits (Quaternius/Kenney, ENVIRONMENT_ASSET_SOURCES.md).

## F. tomicz/one-shot-prompt-world-generation-unity — REFERENCE ONLY
- Verdict: layered FG/MG/BG + deterministic-generation ideas noted; generator
  itself REJECTED (our placement is curated + test-pinned, never procedural).

## G. codand/Unity3DPortals + portal repos — REJECT (import), concepts noted
- Verdict: portal RENDERING systems are explicitly out of scope — we build
  physical walk-to gates, not portal cameras. No source inspected beyond the
  architectural mismatch; nothing taken.

## H. Unity 6 environment examples — REFERENCE ONLY
- Verdict: capability reference for what Unity 6 + URP can achieve; no imports.

## Summary: REUSE 0, ADAPT 0 (techniques only), REFERENCE ONLY 7, REJECT 1.
No dependency soup. All hub geometry is hand-placed primitives + curated CC0.
