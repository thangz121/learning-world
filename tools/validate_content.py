#!/usr/bin/env python3
"""Content+Audio validator v6.1 — JSON là source of truth.
Authoring (default): schema + metadata + word-count + voice compat. approved=true mà thiếu file thật => FAIL ngay.
Ship gate (--ship): generated=true + approved=true + file tồn tại/không rỗng/đúng ext cho mọi active + manifest lines.
Usage:
  python tools/validate_content.py Content/vocab Content/quests Content/dialogues
  python tools/validate_content.py --ship Content/vocab Content/quests Content/dialogues
"""
import json, sys
from pathlib import Path

ALLOWED_VOICES = {"milo_v1", "learning_v1", "mia_v1",
    "npc_female_01", "npc_female_02", "npc_female_03",
    "npc_male_01", "npc_male_02", "npc_male_03"}
# Part D: npc/voice compatibility
NPC_VOICE_RULES = {"milo": {"milo_v1"}, "mia": {"mia_v1"}}
FEMALE = {f"npc_female_0{i}" for i in (1, 2, 3)}
MALE = {f"npc_male_0{i}" for i in (1, 2, 3)}
VALID_EXT = (".mp3", ".wav")

def load_json(p: Path):
    try:
        return json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"FAIL: {p} invalid JSON: {e}")
        sys.exit(1)

def check_audio_file(root: Path, rel: str, errors: list, ctx: str, ship: bool, required: bool):
    """required: file bắt buộc ở ship gate; ở authoring chỉ check khi approved/generated=true."""
    if not rel:
        if required and ship: errors.append(f"{ctx}: thiếu audio path (ship gate)")
        return
    f = root / rel
    if f.suffix.lower() not in VALID_EXT:
        errors.append(f"{ctx}: ext không hợp lệ '{rel}' (chỉ .mp3/.wav)")
        return
    if f.exists():
        if f.stat().st_size == 0: errors.append(f"{ctx}: file rỗng '{rel}'")
    elif required:
        errors.append(f"{ctx}: thiếu file '{rel}'" + (" (ship gate)" if ship else " (approved/generated=true nhưng không có file thật)"))

def main():
    try: sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception: pass
    args = sys.argv[1:]
    ship = "--ship" in args
    args = [a for a in args if a != "--ship"]
    if len(args) != 3:
        print("usage: validate_content.py [--ship] Content/vocab Content/quests Content/dialogues"); sys.exit(2)
    vocab_dir, quests_dir, dialogues_dir = (Path(a) for a in args)
    root = vocab_dir.parent  # Content/
    vocabs = {p.stem: load_json(p) for p in sorted(vocab_dir.glob("*.json"))}
    if not vocabs:
        print(f"FAIL: no vocab in {vocab_dir}"); sys.exit(1)
    active = [v for v in vocabs.values() if v.get("active") is True]
    passive = [v for v in vocabs.values() if v.get("active") is False]
    print(f"Mode: {'SHIP GATE' if ship else 'authoring'} | Active: {len(active)} / Passive: {len(passive)} / Total: {len(vocabs)}")
    errors = []
    # Phase 2A: +ball (first pipeline quest target) -> 16 active / 51 total.
    if len(active) != 16: errors.append(f"active must be 16, got {len(active)}")
    if len(passive) != 35: errors.append(f"passive must be 35, got {len(passive)}")
    if len(vocabs) != 51: errors.append(f"total must be 51, got {len(vocabs)}")
    for vid, v in vocabs.items():
        if v.get("id") != vid:
            errors.append(f"{vid}.json: id field '{v.get('id')}' != filename")
        # Phase 2C progression block (optional; absent = defaults). Present
        # block must be well-formed: explicit introOrder int >= 1 (orders
        # start at 1; 0 is reserved = unordered, which closes the
        # JsonUtility auto-instantiation ambiguity) + prerequisites
        # resolving to vocab ids.
        prog = v.get("progression")
        if prog is not None:
            if not isinstance(prog, dict):
                errors.append(f"{vid}.json: progression must be an object")
            else:
                if "introOrder" not in prog or not isinstance(prog["introOrder"], int) or prog["introOrder"] < 1:
                    errors.append(f"{vid}.json: progression.introOrder must be an explicit int >= 1")
                pre = prog.get("prerequisites", [])
                if not isinstance(pre, list) or any(not isinstance(p, str) for p in pre):
                    errors.append(f"{vid}.json: progression.prerequisites must be a string list")
                else:
                    for p in pre:
                        if p not in vocabs:
                            errors.append(f"{vid}.json: prerequisite '{p}' has no vocab/{p}.json")
        blob = json.dumps(v)
        for banned in ("Neural2", "Wavenet", "WaveNet", "Chirp", "googlesamples"):
            if banned.lower() in blob.lower():
                errors.append(f"{vid}.json: cấm tên Google voice trong content (chỉ VoiceProfileId)")
        # Phase 2.1-local: optional speech.phonemes (ARPAbet ids, content-owned).
        # Absent/empty = no phoneme-level assessment (backward compatible: 45 files omit it).
        # Present = must be a non-empty list of short uppercase alphabetic ids.
        sp = (v.get("speech") or {})
        if "phonemes" in sp and sp.get("phonemes") is not None:
            ph = sp.get("phonemes")
            if not isinstance(ph, list) or len(ph) == 0:
                errors.append(f"{vid}.json: speech.phonemes must be a non-empty list when present")
            else:
                seen_ph = set()
                for p in ph:
                    if not isinstance(p, str) or not p.strip():
                        errors.append(f"{vid}.json: speech.phonemes entries must be non-empty strings")
                        break
                    norm = p.strip().upper()
                    if len(norm) > 4 or not norm.replace("3", "").replace("2", "").isalpha():
                        errors.append(f"{vid}.json: speech.phonemes entry '{p}' must look like ARPAbet (<=4 letters)")
                        break
                    if norm in seen_ph:
                        errors.append(f"{vid}.json: speech.phonemes duplicate '{norm}'")
                        break
                    seen_ph.add(norm)
        if v.get("active") is True:
            tags = ((v.get("semantic") or {}).get("tags") or [])
            forms = ((v.get("speech") or {}).get("expectedForms") or [])
            if not tags: errors.append(f"active {vid}: missing semantic.tags")
            if not forms: errors.append(f"active {vid}: missing speech.expectedForms")
            a = v.get("audio") or {}
            if not a.get("normal"): errors.append(f"active {vid}: missing audio.normal")
            if not a.get("slow"): errors.append(f"active {vid}: missing audio.slow")
            if a.get("voice") != "learning_v1": errors.append(f"active {vid}: audio.voice phải learning_v1")
            if a.get("lang") != "en-US": errors.append(f"active {vid}: audio.lang phải freeze en-US")
            # Part C: approved=true viết tay mà không có file thật => FAIL mọi mode
            claimed = a.get("approved") is True or a.get("generated") is True
            for key in ("normal", "slow", "syllable"):
                rel = a.get(key)
                if rel or claimed or (ship and key in ("normal", "slow")):
                    check_audio_file(root, rel, errors, f"active {vid}/{key}", ship, required=claimed or ship)
            if ship and not (a.get("generated") is True and a.get("approved") is True):
                errors.append(f"active {vid}: ship gate cần generated=true + approved=true")
    quests = {p.stem: load_json(p) for p in sorted(quests_dir.glob("*.json"))}
    if len(quests) < 5: errors.append(f"quests must be >=5, got {len(quests)}")
    for qid, q in quests.items():
        if q.get("id") != qid: errors.append(f"quest {qid}: id mismatch")
        hl = q.get("hint_levels") or []
        if hl != ["visual_glow", "milo_point", "milo_demo", "auto_simplify"]:
            errors.append(f"quest {qid}: hint_levels must be 4-level freeze, got {hl}")
        if not q.get("simplify_path"): errors.append(f"quest {qid}: missing simplify_path")
        if q.get("one_objective_at_a_time") is not True:
            errors.append(f"quest {qid}: one_objective_at_a_time must be true")
        for o in q.get("objectives", []):
            t = o.get("target")
            if t and t not in vocabs:
                errors.append(f"quest {qid}: target '{t}' has no vocab/{t}.json")
            a = (o.get("action") or "").capitalize()
            if a not in ("Find", "Bring", "Speak", "Give", "Select"):
                errors.append(f"quest {qid}: action '{o.get('action')}' must be PlayerAction enum")
    # ---- Phase 2D: audio-manifest <-> binary consistency ----
    # Content/audio_manifest.json is the ship list (consumed by pregen_w1.py,
    # mirrored to StreamingAssets/audio/manifest.json for PregenSeeder).
    # Authoring gate: every listed entry must have its binary on disk, valid
    # mp3, mirrored, with no orphans either direction. Actives WITHOUT a
    # manifest entry are reported informationally (their binaries are future
    # 2D+ work); --ship requires them.
    audio_manifest = root / "audio_manifest.json"
    ship_dir = root.parent / "Assets" / "StreamingAssets" / "audio"
    manifest_entries = []
    if not audio_manifest.is_file():
        errors.append("Content/audio_manifest.json missing (pregen ship list)")
    else:
        raw_manifest = load_json(audio_manifest)
        manifest_entries = raw_manifest if isinstance(raw_manifest, list) else raw_manifest.get("entries", [])
        seen_mid, seen_mfile = set(), set()
        for e in manifest_entries:
            if not isinstance(e, dict):
                errors.append("audio_manifest: entry must be an object"); continue
            missing = [k for k in ("id", "text", "voice", "lang", "rate", "pitch", "style", "format", "file") if k not in e]
            if missing:
                errors.append(f"audio_manifest {e.get('id')}: missing keys {missing}"); continue
            eid = e["id"]
            if eid in seen_mid: errors.append(f"audio_manifest: duplicate id '{eid}'")
            seen_mid.add(eid)
            if e["file"] in seen_mfile: errors.append(f"audio_manifest: duplicate file '{e['file']}'")
            seen_mfile.add(e["file"])
            if not (1 <= len(e["text"] or "") <= 200):
                errors.append(f"audio_manifest {eid}: text length out of 1..200 contract")
            if Path(e["file"]).name != e["file"]:
                errors.append(f"audio_manifest {eid}: file must be a plain filename")
            if Path(e["file"]).suffix.lower() not in VALID_EXT:
                errors.append(f"audio_manifest {eid}: ext must be .mp3/.wav")
            if e.get("voice") not in ALLOWED_VOICES:
                errors.append(f"audio_manifest {eid}: voice '{e.get('voice')}' not in VoiceProfile pool")
            if e.get("lang") != "en-US":
                errors.append(f"audio_manifest {eid}: lang must freeze en-US")
            f = ship_dir / e["file"]
            if not f.is_file():
                errors.append(f"audio_manifest {eid}: MISSING BINARY '{e['file']}'")
            elif f.stat().st_size == 0:
                errors.append(f"audio_manifest {eid}: file rỗng '{e['file']}'")
            elif f.suffix.lower() == ".mp3":
                head = f.read_bytes()[:2]
                if len(head) < 2 or head[0] != 0xFF or (head[1] & 0xE0) != 0xE0:
                    errors.append(f"audio_manifest {eid}: '{e['file']}' missing MPEG frame sync (corrupt?)")
        # mirror check (PregenSeeder reads the mirror, not Content/)
        mirror = ship_dir / "manifest.json"
        if not mirror.is_file():
            errors.append("Assets/StreamingAssets/audio/manifest.json missing (seeder mirror)")
        else:
            mraw = load_json(mirror)
            mids = [x.get("id") for x in (mraw.get("entries") or []) if isinstance(x, dict)]
            if set(mids) != seen_mid or len(mids) != len(seen_mid):
                errors.append(f"seeder mirror ids != Content manifest ids (mirror={len(mids)}, content={len(seen_mid)})")
        # orphan binaries (on disk, no manifest entry)
        for bf in sorted(ship_dir.glob("*.mp3")) + sorted(ship_dir.glob("*.wav")):
            if bf.name not in seen_mfile:
                errors.append(f"orphan binary '{bf.name}' on disk with no manifest entry")
        # actives without binaries: informational in authoring, FAIL in --ship
        manifest_files = set(seen_mfile)
        for vid, v in vocabs.items():
            if v.get("active") is True:
                a = v.get("audio") or {}
                for key in ("normal", "slow"):
                    rel = a.get(key)
                    base = Path(rel).name if rel else ""
                    if not base or base not in manifest_files:
                        msg = f"active {vid}/{key}: no shipped binary (mapping only)"
                        if ship: errors.append(msg + " (ship gate)")
                        else: print(f"NOTE: {msg}")
    for p in sorted(dialogues_dir.glob("*.json")):
        if p.stem == "manifest": continue
        d = load_json(p)
        if not d.get("npcId"): errors.append(f"{p.name}: missing npcId")
        if not d.get("intents"): errors.append(f"{p.name}: missing intents")
        for intent, text in (d.get("responses") or {}).items():
            n = len(str(text).split())
            limit = 8 if d.get("npcId") == "milo" else 6
            if n > limit: errors.append(f"{p.name}/{intent}: {n} từ > trần ({limit}): '{text}'")
    m = dialogues_dir / "manifest.json"
    if not m.exists():
        errors.append("dialogues/manifest.json missing (pack 30–40 câu pre-gen)")
    else:
        pack = load_json(m)
        lines = pack.get("lines") or []
        print(f"Dialogue pack: {len(lines)} lines (count field={pack.get('count')})")
        if pack.get("count") != len(lines):
            errors.append(f"manifest.count={pack.get('count')} khớp {len(lines)} lines thật")
        if not (30 <= len(lines) <= 40): errors.append(f"dialogue pack phải 30–40, got {len(lines)}")
        seen, seen_text = set(), set()
        for ln in lines:
            lid = ln.get("id")
            if lid in seen: errors.append(f"pack: duplicate line id '{lid}'")
            seen.add(lid)
            key = (ln.get("npc"), ln.get("text"))
            if key in seen_text: errors.append(f"pack {lid}: duplicate dialogue '{key}'")
            seen_text.add(key)
            voice, npc = ln.get("voice"), ln.get("npc")
            if voice not in ALLOWED_VOICES:
                errors.append(f"pack {lid}: voice '{voice}' không thuộc VoiceProfile pool")
            # Part D: npc/voice compatibility
            if npc in NPC_VOICE_RULES and voice not in NPC_VOICE_RULES[npc]:
                errors.append(f"pack {lid}: npc '{npc}' phải dùng voice {NPC_VOICE_RULES[npc]}, got '{voice}'")
            if str(npc).startswith("npc_") and voice not in FEMALE | MALE:
                errors.append(f"pack {lid}: npc phụ '{npc}' phải dùng npc_female_*/npc_male_*, got '{voice}'")
            n = len(str(ln.get("text", "")).split())
            limit = 8 if voice == "milo_v1" else 6
            if n > limit: errors.append(f"pack {lid}: {n} từ > trần ({limit})")
            rel = ln.get("audio")
            if not rel: errors.append(f"pack {lid}: thiếu audio asset mapping")
            elif pack.get("approved") is True or ship:
                check_audio_file(dialogues_dir.parent, rel, errors, f"pack {lid}", ship, required=True)
        if pack.get("lang") != "en-US": errors.append("manifest.lang phải freeze en-US")
    if errors:
        print("FAIL:")
        for e in errors: print(f" - {e}")
        sys.exit(1)
    print("PASS: content+audio valid" + (" (SHIP GATE)" if ship else " (authoring)"))

if __name__ == "__main__":
    main()
