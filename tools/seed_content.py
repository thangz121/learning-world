#!/usr/bin/env python3
"""Seed Slice content: 15 active + 35 passive, 5 quests, 2 dialogues. Run once."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
V, Q, D = ROOT/"Content"/"vocab", ROOT/"Content"/"quests", ROOT/"Content"/"dialogues"

ACTIVE = ["apple","banana","milk","bread","basket","bag","red","blue","one","two","find","bring","help","please","thank_you"]
PASSIVE = ["egg","rice","fish","chicken","cookie","cake","candy","orange","grape","watermelon","carrot","potato","tomato","cheese","yogurt","juice","cart","box","bottle","cup","plate","spoon","money","teddy","open","close","sit","stand","eat","sleep","sorry","hello","yellow","green","big"]
assert len(ACTIVE)==15 and len(PASSIVE)==35

META = {
 "apple": ({"en":"apple","vi":"quả táo"},"food",["fruit","red","round","sweet","market"],"ˈæpəl"),
 "banana": ({"en":"banana","vi":"quả chuối"},"food",["fruit","yellow","long","sweet","market"],"bəˈnænə"),
 "milk": ({"en":"milk","vi":"sữa"},"food",["drink","white","market"],"mɪlk"),
 "bread": ({"en":"bread","vi":"bánh mì"},"food",["bakery","brown","market"],"bred"),
 "basket": ({"en":"basket","vi":"giỏ"},"object",["container","market"],"ˈbæskɪt"),
 "bag": ({"en":"bag","vi":"túi"},"object",["container","market"],"bæɡ"),
 "red": ({"en":"red","vi":"màu đỏ"},"color",["color","market"],"red"),
 "blue": ({"en":"blue","vi":"màu xanh dương"},"color",["color","market"],"bluː"),
 "one": ({"en":"one","vi":"số một"},"number",["count"],"wʌn"),
 "two": ({"en":"two","vi":"số hai"},"number",["count"],"tuː"),
 "find": ({"en":"find","vi":"tìm"},"verb",["action"],"faɪnd"),
 "bring": ({"en":"bring","vi":"mang tới"},"verb",["action"],"brɪŋ"),
 "help": ({"en":"help","vi":"giúp"},"verb",["action","social"],"help"),
 "please": ({"en":"please","vi":"làm ơn"},"social",["polite"],"pliːz"),
 "thank_you": ({"en":"thank you","vi":"cảm ơn"},"social",["polite"],"θæŋk juː"),
}
def vocab_entry(vid, active):
    if vid in META:
        disp, cat, tags, phon = META[vid]
    else:
        disp, cat, tags, phon = ({"en": vid.replace("_"," "), "vi": vid}, "world", ["market"], vid)
    return {
      "id": vid, "active": active, "language": "en",
      "display": disp,
      "learning": {"minAge": 4, "difficulty": 1, "skills": ["listen","recognize","speak","use"] if active else ["listen"]},
      "semantic": {"category": cat, "tags": tags},
      "speech": {"expectedForms": [disp["en"]] if active else [], "phonetic": phon if active else ""},
      "assets": {"prefab": f"Addressables/Market/{vid.capitalize()}", "image": f"{vid}.png"},
      # v6.1 Part C: generated=false/approved=false ở phase authoring; file thật + ship gate ở --ship
      "audio": {
        "normal": f"audio/{vid}_normal.mp3",
        "slow": f"audio/{vid}_slow.mp3" if active else None,
        "syllable": None,
        "voice": "learning_v1" if active else None,
        "lang": "en-US",
        "generated": False,
        "approved": False
      } if active else {"normal": None, "slow": None, "syllable": None, "voice": None, "lang": "en-US", "generated": False, "approved": False}
    }

QUESTS = {
 "market_help_mia": [("find","apple"),("bring","apple"),("speak","apple")],
 "lost_teddy": [("find","teddy"),("bring","teddy"),("speak","thank_you")],
 "colors_counting": [("find","red"),("bring","apple"),("speak","one")],
 "please_thank_you": [("speak","please"),("give","apple"),("speak","thank_you")],
 "big_or_small": [("find","big"),("select","apple"),("speak","big")],
}
def quest_entry(qid, objs):
    return {
      "id": qid, "npc": "shopkeeper_mia", "one_objective_at_a_time": True,
      "objectives": [{"id": f"{a}_{t}", "action": a, "target": t} for a, t in objs],
      "hint_levels": ["visual_glow","milo_point","milo_demo","auto_simplify"],
      "simplify_path": {"reduce_choices_to": 2, "demo_one_step": True},
      "reward": {"friendship_mia": 10, "world_change": "flower_pot"}
    }

for vid in ACTIVE: (V/f"{vid}.json").write_text(json.dumps(vocab_entry(vid, True), ensure_ascii=False, indent=2), encoding="utf-8")
for vid in PASSIVE: (V/f"{vid}.json").write_text(json.dumps(vocab_entry(vid, False), ensure_ascii=False, indent=2), encoding="utf-8")
for qid, objs in QUESTS.items(): (Q/f"{qid}.json").write_text(json.dumps(quest_entry(qid, objs), ensure_ascii=False, indent=2), encoding="utf-8")
(D/"shopkeeper_mia.json").write_text(json.dumps({
  "npcId": "shopkeeper_mia",
  "intents": ["GREETING","HELP_REQUEST","ITEM_REQUEST","CORRECT_ANSWER","WRONG_ANSWER","GOODBYE"],
  "responses": {
    "GREETING": "Hi! Come here!", "HELP_REQUEST": "Help me please!",
    "ITEM_REQUEST": "Apple please!", "CORRECT_ANSWER": "Great! Apple!",
    "WRONG_ANSWER": "Almost! Listen again!", "GOODBYE": "Thank you! Bye!"
  }}, ensure_ascii=False, indent=2), encoding="utf-8")
(D/"milo.json").write_text(json.dumps({
  "npcId": "milo",
  "intents": ["ENCOURAGE","HINT_POINT","HINT_DEMO","CELEBRATE"],
  "responses": {
    "ENCOURAGE": "Great! Let's try together!", "HINT_POINT": "Come with me!",
    "HINT_DEMO": "Watch me do it!", "CELEBRATE": "Perfect! Good job!"
  },
  "note": "Milo encouragement max 8 words, NPC dialogue max 6 words"}, ensure_ascii=False, indent=2), encoding="utf-8")
# v6 dialogue pack 36 câu pre-gen (zero latency): greeting/instruction/encouragement/correct/retry/hint/transition/completion/milo/mia
PACK = [
 ("greet_01","milo","milo_v1","Hi! Come here!"),("greet_02","mia","mia_v1","Hi! Come in!"),
 ("greet_03","mom","npc_female_01","Hello! Let's play!"),("greet_04","boy","npc_male_01","Let's play together!"),
 ("inst_01","milo","milo_v1","Look over here!"),("inst_02","milo","milo_v1","Find the apple, please!"),
 ("inst_03","mia","mia_v1","Apple please!"),("inst_04","mia","mia_v1","Bring one apple!"),
 ("inst_05","milo","milo_v1","Listen and point!"),("inst_06","milo","milo_v1","Touch the red one!"),
 ("enc_01","milo","milo_v1","Great! Let's try together!"),("enc_02","milo","milo_v1","Good! One more time!"),
 ("enc_03","mia","mia_v1","Nice! Well done!"),("enc_04","milo","milo_v1","You can do it!"),
 ("enc_05","mom","npc_female_01","Great job, little one!"),
 ("ok_01","mia","mia_v1","Great! Apple!"),("ok_02","milo","milo_v1","Perfect! Good job!"),
 ("ok_03","mia","mia_v1","Thank you! Yay!"),("ok_04","milo","milo_v1","Yes! That's the apple!"),
 ("retry_01","milo","milo_v1","Almost! Listen again!"),("retry_02","milo","milo_v1","Let's say it slowly!"),
 ("retry_03","mia","mia_v1","Try again please!"),("retry_04","milo","milo_v1","Say apple together!"),
 ("hint_01","milo","milo_v1","Come with me!"),("hint_02","milo","milo_v1","Watch me do it!"),
 ("hint_03","mia","mia_v1","Look at the shelf!"),("hint_04","milo","milo_v1","Choose the red fruit!"),
 ("trans_01","milo","milo_v1","Let's help Mia!"),("trans_02","milo","milo_v1","Now find the banana!"),
 ("trans_03","mia","mia_v1","Come back soon!"),
 ("done_01","milo","milo_v1","We did it! Hurray!"),("done_02","mia","mia_v1","Thank you! Bye!"),
 ("done_03","milo","milo_v1","High five, friend!"),
 ("milo_01","milo","milo_v1","I am Milo!"),("mia_01","mia","mia_v1","I am Mia!"),
 ("mia_02","mia","mia_v1","Milk and bread here!"),
]
assert len(PACK) == 36
(D/"manifest.json").write_text(json.dumps(
  {"count": len(PACK), "lang": "en-US", "approved": False,
   "lines": [{"id": i, "npc": n, "voice": v, "text": t, "audio": f"audio/dialog/{i}.mp3"} for i, n, v, t in PACK]},
  ensure_ascii=False, indent=2), encoding="utf-8")
print(f"seeded {len(ACTIVE)} active + {len(PASSIVE)} passive + {len(QUESTS)} quests + dialogue pack {len(PACK)}")
