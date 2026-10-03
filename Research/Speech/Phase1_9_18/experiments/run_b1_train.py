"""Phase 1.9.18 STEP 5 — train B1 head-only (frozen encoder + trainable head).

Deterministic; logs every hyperparameter; saves config + checkpoint hash.
Features are computed once with the frozen encoder and cached to disk so the
CPU-only box can train the small head in seconds. NO encoder weight changes.
NO production file touched.
"""
from __future__ import annotations

import hashlib
import json
import random
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from b1_head import B1Head, span_features  # noqa: E402
from so762_common import (CHILD_AGE_MAX, MODEL_REV, SEED, build_targets,  # noqa
                          iter_rows, make_child_split, read_wav_bytes)

REPO = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
CKPT = OUT / "checkpoints"

CFG = {
    "seed": SEED,
    "hidden": 32,
    "lr": 1e-3,
    "weight_decay": 1e-4,
    "batch_size": 64,
    "max_epochs": 60,
    "patience": 8,
    "pos_weight": "auto = n_neg/n_pos",
    "optimizer": "AdamW",
    "loss": "weighted BCEWithLogits",
    "early_stopping": "on validation AUC (max)",
    "checkpoint_selection": "best validation AUC",
    "target_positive": "expert_acc >= 1.0",
    "target_negative": "expert_acc < 0.5",
    "ambiguous_excluded": "0.5 <= expert_acc < 1.0",
}


def extract_features(pev, samples, canonical_arpa):
    probs, dur, _ = pev.logits_from_array(samples) if hasattr(
        pev, "logits_from_array") else _logits_from_array(pev, samples)
    spans = pev.ctc_align(probs, pev.inv.arpa_seq_to_canon(canonical_arpa))
    target = pev.inv.arpa_seq_to_canon(canonical_arpa)
    return span_features(probs, spans, target, pev.inv, pev._canon_ids)


def _logits_from_array(pev, samples):
    import torch
    pev._ensure()
    with torch.no_grad():
        inp = pev._feat(samples, sampling_rate=16000,
                        return_tensors="pt").input_values
        logits = pev._model(inp).logits[0]
    probs = torch.softmax(logits, dim=-1)
    return probs, len(samples) / 16000.0, len(samples)


def cache_split(pev, speakers, cache_path: Path):
    if cache_path.exists():
        d = np.load(cache_path, allow_pickle=True)
        return d["X"], d["y"], d["meta"]
    X, y, meta = [], [], []
    n = 0
    for r in iter_rows(splits=("train", "test"), with_audio=True):
        if r["age"] > CHILD_AGE_MAX or r["speaker"] not in speakers:
            continue
        n += 1
        samples = read_wav_bytes(r["wav_bytes"])
        tgt = build_targets(r["text"], r["words"])
        try:
            f = extract_features(pev, samples, tgt["canonical_arpa"])
        except Exception:  # noqa: BLE001
            continue
        for j, acc in enumerate(tgt["expert_acc"]):
            if acc >= 1.0:
                yv = 1.0
            elif acc < 0.5:
                yv = 0.0
            else:
                continue
            X.append(f[j])
            y.append(yv)
            meta.append((r["speaker"], r["age"], tgt["canonical_arpa"][j],
                         acc, tgt["error_type"][j]))
        if n % 50 == 0:
            print(f"    features {n}", flush=True)
    X = np.asarray(X, dtype=np.float32)
    y = np.asarray(y, dtype=np.float32)
    np.savez(cache_path, X=X, y=y, meta=np.asarray(meta, dtype=object))
    return X, y, meta


def main():
    import torch
    from sklearn.metrics import roc_auc_score
    torch.manual_seed(SEED)
    random.seed(SEED)
    np.random.seed(SEED)

    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import (
        PhoneEvidenceV2)
    pev = PhoneEvidenceV2()
    pev._ensure()

    meta_rows = list(iter_rows(splits=("train", "test"), with_audio=False))
    _, assigned, _ = make_child_split(meta_rows)
    CKPT.mkdir(parents=True, exist_ok=True)

    print("  caching train features ...", flush=True)
    Xtr, ytr, _ = cache_split(pev, assigned["train"],
                              CKPT / "feat_train.npz")
    print("  caching valid features ...", flush=True)
    Xva, yva, _ = cache_split(pev, assigned["validation"],
                              CKPT / "feat_valid.npz")
    print(f"  train={Xtr.shape} pos={int(ytr.sum())} neg={int((1-ytr).sum())}")
    print(f"  valid={Xva.shape} pos={int(yva.sum())} neg={int((1-yva).sum())}")

    n_pos = float(ytr.sum())
    n_neg = float((1 - ytr).sum())
    pos_weight = torch.tensor([n_neg / max(1.0, n_pos)], dtype=torch.float32)

    model = B1Head(Xtr.shape[1], CFG["hidden"])
    opt = torch.optim.AdamW(model.parameters(), lr=CFG["lr"],
                            weight_decay=CFG["weight_decay"])
    lossfn = torch.nn.BCEWithLogitsLoss(pos_weight=pos_weight)

    Xtr_t = torch.tensor(Xtr)
    ytr_t = torch.tensor(ytr)
    Xva_t = torch.tensor(Xva)
    yva_t = torch.tensor(yva)

    best_auc = -1.0
    best_state = None
    best_epoch = -1
    history = []
    patience = 0
    bs = CFG["batch_size"]
    n = len(Xtr)
    t0 = time.perf_counter()
    for epoch in range(CFG["max_epochs"]):
        model.train()
        perm = torch.randperm(n)
        ep_loss = 0.0
        for i in range(0, n, bs):
            idx = perm[i:i + bs]
            opt.zero_grad()
            out = model(Xtr_t[idx])
            loss = lossfn(out, ytr_t[idx])
            loss.backward()
            opt.step()
            ep_loss += float(loss.item()) * len(idx)
        ep_loss /= n
        model.eval()
        with torch.no_grad():
            va_logit = model(Xva_t)
            va_prob = torch.sigmoid(va_logit).numpy()
        try:
            auc = roc_auc_score(yva, va_prob)
        except ValueError:
            auc = float("nan")
        history.append({"epoch": epoch, "train_loss": round(ep_loss, 6),
                        "valid_auc": round(float(auc), 6)})
        if auc > best_auc:
            best_auc = auc
            best_epoch = epoch
            best_state = {k: v.clone() for k, v in model.state_dict().items()}
            patience = 0
        else:
            patience += 1
            if patience >= CFG["patience"]:
                break

    model.load_state_dict(best_state)
    ckpt_path = CKPT / "b1_head.pt"
    torch.save({"state_dict": model.state_dict(),
                "in_dim": Xtr.shape[1], "hidden": CFG["hidden"],
                "config": CFG, "seed": SEED}, ckpt_path)
    h = hashlib.sha256(ckpt_path.read_bytes()).hexdigest()

    cfg_out = {
        **CFG,
        "in_dim": int(Xtr.shape[1]),
        "pos_weight": float(pos_weight.item()),
        "n_train_tokens": int(n),
        "n_valid_tokens": int(len(Xva)),
        "train_pos": int(ytr.sum()), "train_neg": int((1 - ytr).sum()),
        "valid_pos": int(yva.sum()), "valid_neg": int((1 - yva).sum()),
        "best_epoch": best_epoch,
        "best_valid_auc": round(float(best_auc), 6),
        "epochs_run": len(history),
        "wall_s": round(time.perf_counter() - t0, 2),
        "encoder": f"frozen facebook/wav2vec2-xlsr-53-espeak-cv-ft@{MODEL_REV}",
        "encoder_weights_changed": False,
        "checkpoint": "checkpoints/b1_head.pt",
        "checkpoint_sha256": h,
        "git_commit": _git_commit(),
        "dataset_revision": "06385584fad212b26134c656fdd3ccf9f093f33e",
        "feature_columns": ["post_mass", "top1", "top2", "top1_top2_margin",
                            "span_frac", "mean_entropy", "has_ids", "max_post"],
    }
    (OUT / "b1_config.json").write_text(json.dumps(cfg_out, indent=2),
                                        encoding="utf-8")
    (OUT / "b1_results.json").write_text(json.dumps(
        {"history": history, "best_valid_auc": round(float(best_auc), 6),
         "best_epoch": best_epoch, "checkpoint_sha256": h}, indent=2),
        encoding="utf-8")
    print(json.dumps({k: v for k, v in cfg_out.items()
                      if k in ("best_epoch", "best_valid_auc", "epochs_run",
                               "checkpoint_sha256", "n_train_tokens",
                               "pos_weight")}, indent=2))
    return cfg_out


def _git_commit():
    import subprocess
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=str(REPO),
            stderr=subprocess.DEVNULL).decode().strip()
    except Exception:  # noqa: BLE001
        return None


if __name__ == "__main__":
    main()
