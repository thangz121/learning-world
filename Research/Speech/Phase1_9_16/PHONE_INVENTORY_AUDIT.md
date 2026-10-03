# PHONE INVENTORY AUDIT — Phase 1.9.16

**Script:** `experiments/verify_phone_inventory.py`
**Machine-readable:** `PHONE_INVENTORY_VERIFIED.json` · Status: VERIFIED_COMPLETE

---

## 1. Inventories compared

| source | version | size |
|---|---|---|
| espeak-CTC vocab (pinned snapshot `2c73378…`) | model vocab | 392 symbols |
| `PhonemeInventoryAdapter` | phone-inventory-v1.4.0 | 42 ARPA + 33 alias rules |
| CMUdict (`cmudict.dict`, 3.6 MB, re-downloaded) | upstream master | ~134k entries |
| SIAK / LWE target inventory | derived via `CmuDictTargetAdapter` | n/a (word→ARPA at runtime) |

## 2. Mapping result

- 389/392 vocab symbols normalize to a canonical phone; 3 unmapped are the
  expected structural drops (`<pad>`-family/specials → empty by design, no
  silent loss: they are recorded as `drop` in the adapter, never as phones).
- All 7 LWE control words + `red apple` resolve via CMUdict
  (red R EH1 D; cat K AE1 T; apple AE1 P AH0 L; blue B L UW1; big B IH1 G;
  book B UH1 K; dog D AO1 G). No control word needs a fallback path.
- Stress digits are stripped before canonicalization (documented in adapter);
  length marks (`ː`) fold per alias table — lossy cases are flagged `lossy:true`
  in mapping records, never silent.

## 3. Watch-list pairs (§6)

All required contrasts are distinct canonical phones with soft-similarity
entries (not merges): p/b, t/d, k/ɡ, m/n/ŋ, f/v, s/z, θ/ð, ɹ/l, plus
affricates tʃ/dʒ and all front/back vowel groups. Final consonants are
ordinary phones in the inventory (no special-casing); the deletion problem
lives in alignment/aggregation, per 1.9.14–1.9.15 forensics — not in missing
symbols.

## 4. Verdict

No inventory gap blocks baseline reproduction or any future adaptation:
every phone the frozen model can emit maps deterministically, and every LWE
target word maps to phones. The inventory is FROZEN at v1.4.0 for this phase.
