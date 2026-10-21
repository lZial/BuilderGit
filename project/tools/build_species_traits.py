# -*- coding: utf-8 -*-
"""
Вносит в 08-species.json данные из «Руководства по абхуманам»:
черты вида («Starting Traits»), начальные ранения и очки судьбы.
Всё — дословно по блокам вида (стр. 7–56).
"""
import json, os, sys

P = r"D:\RogueTraderBuilder\project\data\builtin\08-species.json"

# id → (черты, начальные ранения, очки судьбы)
# Формула ранений: (постоянная, кость, «без правок вида», текст из книги)
DATA = {
    "afriel_strain": (
        ["Bred for Purpose", "Fortune Unanswered", "Non-Imperial", "Not Quite Human",
         "Socially Bereft", "Valuable Investment"],
        (3, 5, False, "1d5+3 + 2xToughness Bonus"), 1),
    "beastman_gors": (
        ["Improved Natural Weapons (Horns)", "Non-Imperial", "Not Quite Human"],
        (1, 5, False, "1d5+1 + 2xToughness Bonus"), 2),
    "beastman_bray": (
        ["Non-Imperial", "Not Quite Human"],
        (0, 5, False, "1d5 + 2xToughness Bonus"), 2),
    # Тёрнскины: в книге для них нет ни характеристик, ни умений — только
    # черта с рогами «по желанию» (стр. 12).
    "beastman_turnskin": (
        ["Non-Imperial", "Not Quite Human"],
        None, 0),
    # Шаманы — подраса зверей, одарённых связью с варпом (стр. 12);
    # их данные — в списке NEW ниже.
    "beastman_minotaurs": (
        ["Brutal Charge", "Improved Natural Weapons (Horns)", "Non-Imperial", "Not Quite Human",
         "Size (Hulking)", "Unnatural Strength (x2)", "Unnatural Toughness (x2)"],
        (3, 5, True, "1d5+3 + 2x Unmodified Toughness Bonus"), 1),
    "felinids": (
        ["Dark Sight", "Non-Imperial", "Not Quite Human"],
        (1, 5, False, "1d5+1 + 2xToughness Bonus"), 2),
    "longshanks": (
        ["Longshank Biology", "Non-Imperial", "Not Quite Human", "Size (Hulking)",
         "Void Accustomed (See page 19 of the Rogue Trader Core Rulebook)"],
        (0, 5, False, "1d5 + 2x Toughness Bonus"), 2),
    "neandors": (
        ["High Gravity Accustomed", "Non-Imperial", "Not Quite Human"],
        (2, 5, False, "1d5+2 + 2xToughness Bonus"), 2),
    "nightsiders": (
        ["Nightsider", "Non-Imperial", "Not Quite Human"],
        (0, 5, False, "1d5 + 2xToughness Bonus"), 2),
    "ogryn": (
        ["Claustrophobic", "Clumsy", "Non-Imperial", "Not Quite Human",
         "Unnatural Strength (x2)", "Unnatural Toughness (x2)"],
        (3, 5, True, "1d5+3 + 2x Unmodified Toughness Bonus"), 1),
    "pelagers": (
        ["Amphibious", "Improved Natural Weapons (Jaws)", "Non-Imperial", "Not Quite Human"],
        (1, 5, False, "1d5+1 + 2xToughness Bonus"), 2),
    "ratlings": (
        ["Non-Imperial", "Not Quite Human", "Size (Scrawny)"],
        (0, 5, False, "1d5 + 2xToughness Bonus"), 2),
    "scalies": (
        ["Limb Regeneration", "Non-Imperial", "Not Quite Human", "Scaly Hide", "Size (Hulking)"],
        (2, 5, False, "1d5+2 + 2x Toughness Bonus"), 2),
    "squats": (
        ["High Gravity Accustomed", "Non-Imperial", "Not Quite Human"],
        (2, 5, False, "1d5+2 + 2xToughness Bonus"), 2),
    "troth": (
        ["Arboreal", "Bio-Organic Recycling", "Non-Imperial", "Not Quite Human"],
        (1, 5, False, "1d5+1 + 2xToughness Bonus"), 2),
}

# Новые виды — подрасы зверей (стр. 12 «Руководства по абхуманам»).
NEW = [
    {
        "id": "beastman_gors",
        "name": "Beastman — Горс (Gors)",
        "nameEn": "Gors",
        "book": "abhuman",
        "lang": "en",
        "description": "Самый распространённый вид зверей — их узнают по рогам.",
        "bonus": {"ws": 5, "bs": 0, "str": 5, "tou": 0, "dex": 0, "int": -5, "per": -5, "wil": 0, "fel": 5},
        "skills": ["command", "intimidate"],
        "talents": ["air_of_authority"],
        "traits": ["Improved Natural Weapons (Horns)", "Non-Imperial", "Not Quite Human"],
        "startingWounds": {"const": 1, "dice": 5, "unmodified": False,
                           "text": "1d5+1 + 2xToughness Bonus"},
        "startingFatePoints": 2,
        "startingEquipment": [],
        "careers": [],
        "specialRules": [],
        "elitePackages": [{"name": "One of the Masses",
                            "effect": "Gain the They Were The Most...Human trait (detailed below)."}],
        "source": "abhuman",
        "size": 0,
        "sizeNote": "Размер в книге для горсов не указан.",
    },
    {
        "id": "beastman_bray",
        "name": "Beastman — Брей (Bray)",
        "nameEn": "Bray",
        "book": "abhuman",
        "lang": "en",
        "description": "Большинство зверей, что не горсы: их узнают по отсутствию рогов и звуку, который они издают, когда собираются вместе.",
        "bonus": {"ws": 0, "bs": 0, "str": 5, "tou": 5, "dex": 5, "int": -5, "per": 0, "wil": 0, "fel": -5},
        "skills": ["acrobatics", "carouse", "dodge"],
        "talents": ["decadence"],
        "traits": ["Non-Imperial", "Not Quite Human"],
        "startingWounds": {"const": 0, "dice": 5, "unmodified": False,
                           "text": "1d5 + 2xToughness Bonus"},
        "startingFatePoints": 2,
        "startingEquipment": [],
        "careers": [],
        "specialRules": [],
        "elitePackages": [{"name": "One of the Masses",
                            "effect": "Gain the They Were The Most...Human trait (detailed below)."}],
        "source": "abhuman",
        "size": 0,
        "sizeNote": "Размер в книге для бреев не указан.",
    },
    {
        "id": "beastman_turnskin",
        "name": "Beastman — Тёрнскин (Turnskin)",
        "nameEn": "Turnskin",
        "book": "abhuman",
        "lang": "en",
        "description": "Рождённые людьми звери; они пользуются путём происхождения как обычно, но могут дополнительно взять черту «Улучшенное природное оружие (Рога)» — либо отказаться от неё.",
        "bonus": {"ws": 0, "bs": 0, "str": 0, "tou": 0, "dex": 0, "int": 0, "per": 0, "wil": 0, "fel": 0},
        "skills": [],
        "talents": [],
        "traits": ["Non-Imperial", "Not Quite Human"],
        "startingWounds": None,
        "startingFatePoints": 0,
        "startingEquipment": [],
        "careers": [],
        "specialRules": [
            "В книге у тёрнскинов нет ни характеристик, ни умений, ни талантов: «Turnskin utilize the Origin Path as normal, but may also choose to also gain the Improved Natural Weapons (Horns) trait, though they may decline it» (стр. 12).",
            "Шаманы — отдельная подраса зверей, одарённых связью с варпом: черта «Психозверь» ограничивает их путём Астропат-Трансцендент.",
        ],
        "elitePackages": [{"name": "One of the Masses",
                            "effect": "Gain the They Were The Most...Human trait (detailed below)."}],
        "source": "abhuman",
        "size": 0,
        "sizeNote": "Размер в книге для тёрнскинов не указан.",
    },
    {
        "id": "beastman_shaman",
        "name": "Beastman — Шаманы (Shamans)",
        "nameEn": "Shamans",
        "book": "abhuman",
        "lang": "en",
        "description": "Особый вид зверей: шаманы связаны с варпом и служат посредниками между зверями и Хаосом.",
        "bonus": {"ws": -5, "bs": 0, "str": 0, "tou": -5, "dex": 0, "int": 5, "per": 5, "wil": 5, "fel": 0},
        "skills": ["forbidden_lore"],
        "talents": [],
        "traits": ["Beastman Psyker", "Improved Natural Weapons (Horns)", "Non-Imperial", "Not Quite Human"],
        "startingWounds": {"const": 0, "dice": 5, "unmodified": False,
                           "text": "1d5 + 2x Toughness Bonus"},
        "startingFatePoints": 2,
        "startingEquipment": [],
        "careers": [],
        "specialRules": ["Шаманы получают черту «Психозверь»: она ограничивает их путём Астропат-Трансцендент."],
        "elitePackages": [{"name": "One of the Masses",
                            "effect": "Gain the They Were The Most...Human trait (detailed below)."}],
        "source": "abhuman",
        "size": 0,
        "sizeNote": "Размер в книге для шаманов не указан.",
    },
    {
        "id": "beastman_minotaurs",
        "name": "Beastman — Минотавры (Minotaurs)",
        "nameEn": "Minotaurs",
        "book": "abhuman",
        "lang": "en",
        "description": "Крупная и агрессивная порода зверей: сопоставимы по размеру и силе с огрином.",
        "bonus": {"ws": 0, "bs": 0, "str": 5, "tou": 5, "dex": 0, "int": -10, "per": 0, "wil": -10, "fel": -10},
        "skills": ["intimidate"],
        "talents": ["frenzy"],
        "traits": ["Brutal Charge", "Improved Natural Weapons (Horns)", "Non-Imperial", "Not Quite Human",
                   "Size (Hulking)", "Unnatural Strength (x2)", "Unnatural Toughness (x2)"],
        "startingWounds": {"const": 3, "dice": 5, "unmodified": True,
                           "text": "1d5+3 + 2x Unmodified Toughness Bonus"},
        "startingFatePoints": 1,
        "startingEquipment": [],
        "careers": [],
        "specialRules": [],
        "elitePackages": [{"name": "One of the Masses",
                            "effect": "Gain the They Were The Most...Human trait (detailed below)."}],
        "source": "abhuman",
        "size": 5,
        "sizeNote": "Черта породы «Размер (Hulking)» — Крупный (5).",
    },
]

d = json.load(open(P, encoding="utf-8"))
species = d["species"]
by_id = {s["id"]: s for s in species}

for sid, (traits, wounds, fate) in DATA.items():
    s = by_id.get(sid)
    if s is None:
        print("нет вида:", sid)
        continue
    s["traits"] = traits
    if wounds is None:
        s.pop("startingWounds", None)
    else:
        const, dice, unmod, text = wounds
        s["startingWounds"] = {"const": const, "dice": dice, "unmodified": unmod, "text": text}
    s["startingFatePoints"] = fate

# Подрасы зверей — добавляем, если их ещё нет, и обновляем, если есть.
for n in NEW:
    if n["id"] in by_id:
        cur = by_id[n["id"]]
        for k, v in n.items():
            if k == "id":
                continue
            cur[k] = v
    else:
        species.append(n)
        by_id[n["id"]] = n

with open(P, "w", encoding="utf-8", newline="\n") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
    f.write("\n")

print("видов в паке:", len(species))
for s in species:
    sw = s.get("startingWounds")
    print(f"  {s['id']:22} черт: {len(s.get('traits') or []):2}  ранения: "
          f"{(sw['text'] if sw else '—')}  судьба: {s.get('startingFatePoints')}")
