# KILL SWITCH — WP-1.9.27 Part 18

> **Tóm tắt (VI):** Công tắc dừng: STOP_B2 nếu bất kỳ điều kiện nào trong FAILURE_CRITERIA xảy ra;
> STOP_B2_DATA nếu không có route license rõ; STOP_B2_GPU nếu không có GPU/ngân sách. Mục đích:
> ngăn "một metric đẹp → mở rộng B2 vô hạn".

## STOP conditions

| id | condition | action |
|---|---|---|
| STOP_B2_PILOT | pilot FAILURE (any FAILURE_CRITERIA item) | stop B2; no training; report; keep production frozen |
| STOP_B2_SINGLE_SPEAKER | gains only for 1 test speaker | stop; require a new label round before any retry |
| STOP_B2_LABELS | labelled test set < 30/side or single-reviewer without limitation | INCONCLUSIVE; collect labels; do not train |
| STOP_B2_DATA | no signed/cleared data route for the intended scope (MyST quote, research registration, or local-only) | no full B2; pilot-only work continues |
| STOP_B2_GPU | no GPU/budget available for B2-C | no encoder adaptation; CPU-only B2-D/B2-E allowed |
| STOP_B2_LEAK | any leakage check fails (speaker/duplicate/label/machine score) | stop; fix split; rerun checks; invalidate results |
| STOP_B2_CLAIM | any result claims production readiness from the pilot | retract; pilot is GO/NO-GO only |

## Never permitted by any positive result in this WP

Encoder fine-tuning, LoRA, head training, model replacement, production acceptance changes, Unity
integration, thresholds written back to the product. Training requires a future, separate,
explicitly-approved work package with a signed data route and GPU.

## Trigger ownership

The kill switch is evaluated mechanically from the frozen criteria files after the test readout. No
manual exception may be applied to an individual case; exceptions would be a new preregistration.
