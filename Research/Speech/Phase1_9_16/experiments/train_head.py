"""Phase 1.9.16 — Experiment B1: frozen encoder + trainable CTC head adaptation.

Smallest reasonable child-domain adaptation:
  - encoder: facebook/wav2vec2-xlsr-53-espeak-cv-ft, FROZEN
  - head:   2-layer Conv1d (1024 -> 512 -> vocab) trained with CTC
  - targets: canonical phones of the expected words (so762 ref-phones; SIAK target word)
  - weights: so762 per-utterance weight = mean human phone score / 2;
             SIAK per-utterance weight = expert rating / 100 (score >= 60 only)
  - splits:  so762 official TRAIN children (58 spk) split by speaker into fit/val;
             official TEST children, SIAK test speakers and LWE stay untouched

Outputs:
  artifacts/ab/features_<source>.pt   cached frozen encoder features
  artifacts/ab/head.pt, head_config.json, training_log.json
"""
from __future__ import annotations

import argparse
import csv
import json
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf
import torch
import torch.nn as nn

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

SO = Path(r"D:\speech-lab\data\speechocean762")
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
AUDIT = REPO / "Research/Speech/Phase1_9_16/artifacts/audit"
SI_SPLIT = REPO / "Research/Speech/Phase1_9_15/artifacts/siak/calibration_train.csv"
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/ab"
OUT.mkdir(parents=True, exist_ok=True)

VOCAB = None


class CTCHead(nn.Module):
    def __init__(self, in_dim=1024, hidden=512, vocab=392, dropout=0.1):
        super().__init__()
        self.net = nn.Sequential(
            nn.Conv1d(in_dim, hidden, 5, padding=2), nn.GELU(), nn.Dropout(dropout),
            nn.Conv1d(hidden, vocab, 5, padding=2))

    def forward(self, x):  # x: [B, T, 1024]
        return self.net(x.transpose(1, 2)).transpose(1, 2)  # [B, T, vocab]


def primary_ids(pev):
    out = {}
    for canon, ids in pev._canon_ids.items():
        out[canon] = sorted(ids)[0]
    return out


def targets_from_arpabet(pev, prim, arpa):
    canon = pev.inv.arpa_seq_to_canon([str(p) for p in arpa])
    ids = []
    for c in canon:
        if c in prim:
            ids.append(prim[c])
    return ids


def extract_features(pev, items, tag, smoke=False):
    """items: list of dicts {id, wav, arpa, weight}"""
    cache = OUT / f"features_{tag}.pt"
    if cache.exists() and not smoke:
        print(f"features_{tag} exists; loading", flush=True)
        return torch.load(cache, weights_only=False)
    feats, targs, weights, ids = [], [], [], []
    model = pev._model
    t0 = time.perf_counter()
    for i, it in enumerate(items):
        x, sr = sf.read(str(it["wav"]))
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        if sr != 16000:
            n = int(len(x) * 16000 / sr)
            x = np.interp(np.linspace(0, 1, n, endpoint=False),
                          np.linspace(0, 1, len(x), endpoint=False), x).astype(np.float32)
        with torch.no_grad():
            inp = pev._feat(x, sampling_rate=16000, return_tensors="pt").input_values
            h = model.wav2vec2(inp).last_hidden_state[0]  # [T,1024]
        feats.append(h.to(torch.float16))
        targs.append(torch.tensor(it["ids"], dtype=torch.long))
        weights.append(float(it["weight"]))
        ids.append(it["id"])
        if (i + 1) % 50 == 0:
            print(f"  {tag} {i+1}/{len(items)} ({time.perf_counter()-t0:.0f}s)", flush=True)
        if smoke and i >= 5:
            break
    data = {"feats": feats, "targets": targs, "weights": weights, "ids": ids}
    if not smoke:
        torch.save(data, cache)
    return data


def prepare_so762(pev, prim, smoke=False):
    man = list(csv.DictReader((AUDIT / "so762_manifest.csv").open(encoding="utf-8")))
    train_child = [m for m in man if m["split"] == "train" and m["is_child"] == "1"]
    items = []
    for m in train_child:
        scores = [float(s) for s in m["phone_scores"].split() if s not in ("", "nan")]
        if not scores:
            continue
        w = sum(scores) / (2.0 * len(scores))
        if w < 0.5:
            continue
        arpa = m["ref_phones"].split()
        ids = targets_from_arpabet(pev, prim, arpa)
        if not ids:
            continue
        spk = int(m["speaker_id"])
        wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{m['utt_id']}.WAV"
        items.append({"id": m["utt_id"], "wav": wav, "ids": ids, "weight": round(w, 3),
                      "speaker": m["speaker_id"]})
    if smoke:
        items = items[:8]
    return items


def prepare_siak(pev, prim, smoke=False):
    rows = list(csv.DictReader(SI_SPLIT.open(encoding="utf-8")))
    tgt = CmuDictTargetAdapter()
    idx = {p.name: p for p in (SIAK / "flac").rglob("*.flac")}
    items = []
    for r in rows:
        sc = float(r["siak_score"])
        if sc < 60:
            continue
        st = tgt.build(r["utterance"])
        ids = targets_from_arpabet(pev, prim, [str(p) for p in st.arpabet])
        if not ids:
            continue
        items.append({"id": r["file"], "wav": idx.get(r["file"]), "ids": ids,
                      "weight": round(sc / 100.0, 3), "speaker": r["speaker_id"]})
    items = [it for it in items if it["wav"]]
    if smoke:
        items = items[:8]
    return items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--smoke", action="store_true")
    ap.add_argument("--epochs", type=int, default=15)
    ap.add_argument("--batch", type=int, default=8)
    ap.add_argument("--lr", type=float, default=1e-3)
    ap.add_argument("--val-speakers", type=int, default=10)
    args = ap.parse_args()

    pev = PhoneEvidenceV2()
    pev._ensure()
    prim = primary_ids(pev)
    device = torch.device("cpu")

    so_items = prepare_so762(pev, prim, args.smoke)
    si_items = prepare_siak(pev, prim, args.smoke)
    print(f"so762 train-child items {len(so_items)} | siak train items {len(si_items)}", flush=True)

    so_spk = sorted(set(it["speaker"] for it in so_items))
    val_spk = set(so_spk[-args.val_speakers:]) if not args.smoke else set(so_spk[:2])
    fit_items = [it for it in so_items if it["speaker"] not in val_spk] + si_items
    val_items = [it for it in so_items if it["speaker"] in val_spk]
    print(f"fit {len(fit_items)} (so762 fit speakers {len(so_spk)-len(val_spk)}) "
          f"| val {len(val_items)} | val speakers {sorted(val_spk)}", flush=True)

    fit_feats = extract_features(pev, fit_items, "fit", args.smoke)
    val_feats = extract_features(pev, val_items, "val", args.smoke)

    head = CTCHead(vocab=len(pev._id2tok)).to(device)
    opt = torch.optim.AdamW(head.parameters(), lr=args.lr, weight_decay=1e-4)
    ctc = nn.CTCLoss(blank=pev._blank, zero_infinity=True, reduction="none")

    def batch_iter(data, batch):
        n = len(data["feats"])
        order = np.random.RandomState(1516).permutation(n)
        for i in range(0, n, batch):
            idx = order[i:i + batch]
            fs = [data["feats"][j].to(torch.float32) for j in idx]
            ts = [data["targets"][j] for j in idx]
            ws = torch.tensor([data["weights"][j] for j in idx], dtype=torch.float32)
            T = max(f.shape[0] for f in fs)
            padded = torch.stack([torch.nn.functional.pad(f, (0, 0, 0, T - f.shape[0])) for f in fs])
            in_len = torch.tensor([f.shape[0] for f in fs], dtype=torch.long)
            tgt = torch.cat(ts) if ts else torch.tensor([], dtype=torch.long)
            tgt_len = torch.tensor([len(t) for t in ts], dtype=torch.long)
            yield padded, in_len, tgt, tgt_len, ws

    def eval_loss(data):
        head.eval()
        tot, n = 0.0, 0
        with torch.no_grad():
            for padded, in_len, tgt, tgt_len, ws in batch_iter(data, args.batch):
                logits = head(padded).log_softmax(-1).transpose(0, 1)
                loss = ctc(logits, tgt, in_len, tgt_len)
                loss = (loss[torch.isfinite(loss)] * ws[torch.isfinite(loss)]).mean() if torch.isfinite(loss).any() else torch.tensor(0.0)
                tot += float(loss) * len(ws)
                n += len(ws)
        return tot / max(1, n)

    log = {"config": vars(args), "epochs": [], "n_fit": len(fit_items), "n_val": len(val_items)}
    best = None
    for ep in range(1, args.epochs + 1):
        head.train()
        t0 = time.perf_counter()
        losses, nb = 0.0, 0
        for padded, in_len, tgt, tgt_len, ws in batch_iter(fit_feats, args.batch):
            logits = head(padded).log_softmax(-1).transpose(0, 1)
            loss = ctc(logits, tgt, in_len, tgt_len)
            ok = torch.isfinite(loss)
            loss = (loss[ok] * ws[ok]).sum() / ok.sum().clamp(min=1)
            opt.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(head.parameters(), 5.0)
            opt.step()
            losses += float(loss)
            nb += 1
        vl = eval_loss(val_feats)
        rec = {"epoch": ep, "train_loss": round(losses / max(1, nb), 4),
               "val_loss": round(vl, 4), "sec": round(time.perf_counter() - t0, 1)}
        log["epochs"].append(rec)
        print(rec, flush=True)
        if best is None or vl < best[0]:
            best = (vl, ep, {k: v.clone() for k, v in head.state_dict().items()})
    head.load_state_dict(best[2])
    torch.save({"state_dict": head.state_dict(), "config": vars(args),
                "in_dim": 1024, "hidden": 512, "vocab": len(pev._id2tok),
                "blank": pev._blank, "best_val_loss": best[0], "best_epoch": best[1]},
               OUT / "head.pt")
    (OUT / "training_log.json").write_text(json.dumps(log, indent=2), encoding="utf-8")
    print(f"best epoch {best[1]} val {best[0]:.4f} saved to {OUT/'head.pt'}", flush=True)


if __name__ == "__main__":
    main()
