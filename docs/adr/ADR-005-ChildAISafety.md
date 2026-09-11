# ADR-005 — No LLM trực tiếp với trẻ + AI Decision Policy

Decision: LLM không bao giờ nói trực tiếp với trẻ. Mọi câu qua SafetyFilter → Validator (≤8 từ, Level-1 vocab) → TTS. Policy: known intent → script, safe topic → small model constrained, else → fallback + hint.
Context: trẻ 4 tuổi, COPPA, latency/cost, unpredictable.
Alternatives: child → GPT trực tiếp (nhanh demo, rủi ro cao), cấm hẳn AI (an toàn nhưng Milo đơ).
Why: 80% local / 15% small / 5% large đủ sống động mà kiểm soát được. Context gửi model chỉ quest+object+intent+target, never child id / raw voice / PII.
Consequences: vượt 8 từ / gửi full history / lưu voice default = reject. Cache TTS câu lặp để tiết kiệm.
