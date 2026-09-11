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
    if len(active) != 15: errors.append(f"active must be 15, got {len(active)}")
    if len(passive) != 35: errors.append(f"passive must be 35, got {len(passive)}")
    if len(vocabs) != 50: errors.append(f"total must be 50, got {len(vocabs)}")
    for vid, v in vocabs.items():
        if v.get("id") != vid:
            errors.append(f"{vid}.json: id field '{v.get('id')}' != filename")
        blob = json.dumps(v)
        for banned in ("Neural2", "Wavenet", "WaveNet", "Chirp", "googlesamples"):
            if banned.lower() in blob.lower():
                errors.append(f"{vid}.json: cấm tên Google voice trong content (chỉ VoiceProfileId)")
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
