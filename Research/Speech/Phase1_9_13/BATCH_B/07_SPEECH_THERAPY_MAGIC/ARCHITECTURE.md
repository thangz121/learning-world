# SpeechTherapyMagic — Architecture (documented, E0/E1)

```
ADULT-MANAGED ACCOUNT (parent/SLP; child never signs up)
  ↓
CONTENT LAYER (games, decks, stories, printables; PIN sharing)
  ↓
CHILD RECORDING (tap-record; browser mic)
  ↓
DUAL SCORING:
  child-specialist engine (in-house) + industry-standard engine
  ↓
WORD SCORE + PER-SOUND BREAKDOWN (slipped sound → what it sounded like) + TIP
  ↓
PROGRESS BY SOUND/SKILL (trends, streaks, session log) → PARENT + SLP DASHBOARD
```

The dual-engine pattern and the substitution+tip output are the notable design choices.
