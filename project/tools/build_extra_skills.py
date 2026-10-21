# -*- coding: utf-8 -*-
"""
Собирает 13-extra-skills.json: умения и таланты из «Руководства по тау»
и «Сквозь шторм», которых нет в основном паке, и чинит битые ссылки
видов на умения и таланты.
"""
import json, os, sys
sys.stdout.reconfigure(encoding="utf-8")

D = r"D:\RogueTraderBuilder\project\data\builtin"

# ── Новые умения и таланты ────────────────────────────────────────────────
SKILLS = [
    # «Обыденное знание (Империя Тау)» и «Учёное знание (Империя Тау)» уже есть
    # в 09-tau.json — здесь только то, чего в паке не было.
    {
        "id": "scholastic_lore_tau_doctrines",
        "name": "Учёное знание (Военные доктрины тау)",
        "description": "Знание военных доктрин тау: кауйон, монт’ка, построения и огневой поддержки.\n\nПрименение: свободное действие.",
        "characteristics": ["int"],
        "advanced": True,
        "specialized": True,
        "parentId": "scholastic_lore",
        "name_en": "Scholastic Lore (Tau Military Doctrines)",
        "source": "tau",
    },
    {
        "id": "speak_language_tau",
        "name": "Языкознание (Язык тау)",
        "description": "Язык тау не имеет ничего общего с человеческими: овладеть им непросто, и лишь немногие крууты служат во вспомогательных частях, где учат его наравне с низким готическим.\n\nПрименение: свободное действие.",
        "characteristics": ["int"],
        "advanced": True,
        "specialized": True,
        "parentId": "speak_language",
        "name_en": "Speak Language (Tau)",
        "source": "tau",
    },
    {
        "id": "literacy_ork",
        "name": "Грамотность (Орочья)",
        "description": "Поскольку устная и письменная речь у орков не совсем строго связаны, для того, чтобы читать и писать по-орочьи требуется отдельное умение. В остальном этот навык работает как обычно.\n\nОрочья: это грубое глифовое письмо и фонетические руны годятся только для того, чтобы выражать самые простые идеи.",
        "characteristics": ["int"],
        "advanced": True,
        "specialized": True,
        "parentId": "literacy",
        "name_en": "Literacy (Ork)",
        "source": "stormstout",
    },
    {
        # В книге это умение есть (в таблицах улучшений), но отдельного описания
        # для него не напечатано — поэтому описание пустое.
        "id": "speak_language_kruut",
        "name": "Языкознание (Круутский)",
        "description": "",
        "characteristics": ["int"],
        "advanced": False,
        "specialized": True,
        "parentId": "speak_language",
        "name_en": "Speak Language (Kruut)",
        "source": "stormstout",
    },
    {
        "id": "common_lore_orks",
        "name": "Обыденное знание (Орки)",

        "description": "Врождённое, инстинктивное знание языка и культуры орков: записано у них в генах.\n\nПрименение: свободное действие.",
        "characteristics": ["int"],
        "advanced": False,
        "specialized": True,
        "parentId": "common_lore",
        "name_en": "Common Lore (Orks)",
        "source": "stormstout",
    },
]

TALENTS = [
    # Таланты тау («Владение оружием касты Огня», «Огневая поддержка»,
    # «Ритуал единения», «Целое больше суммы его частей») уже есть в 09-tau.json —
    # здесь только то, чего в паке не было.
    {
        "id": "mercenary",
        "name": "Наёмник",
        "description": "Пытаясь сопротивляться страху и подавлению огнём, крууты-наёмники могут бросать тесты "
                       "не против Силы воли, а против уровня дохода. Крууты обладают этим талантом с самого начала.",
        "prerequisites": ["круут"],
        "source": "stormstout",
    },
    {
        "id": "xenos_weapon_training",
        "name": "Владение ксенооружием (Орочье)",
        "description": "Орки умеют обращаться с оружием с того мига, как вылезли из споры. Орк может использовать "
                       "без штрафов стрелялы, пулялы, рубилы, большие рубилы и всё примитивное холодное оружие.",
        "prerequisites": ["орк"],
        "source": "stormstout",
    },
]

# Таланты круутов и орков («Благословение предков», «Жадность — это хорошо»,
# «Кровь охотника», «Пророческие сны», «ВОЙНА!», «Все на одного») — текст
# дословно из книги, в tools/storm_talents_data.py.
from storm_talents_data import STORM_TALENTS  # noqa: E402

TALENTS = TALENTS + STORM_TALENTS

out = {"skills": SKILLS, "talents": TALENTS}
path = os.path.join(D, "13-extra-skills.json")
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("записано:", path, "умений:", len(SKILLS), "талантов:", len(TALENTS))

# ── Чиним битые ссылки видов ─────────────────────────────────────────────
SKILL_FIX = {
    "afriel_strain": {"skill_any": []},
    "longshanks": {"trade_any": ["trade"]},
    "nightsiders": {},
    "pelagers": {"scholastic_lore_chymistry": ["scholastic_lore"]},
    "ratlings": {"trade_cook": ["trade"]},
    "squats": {"forbidden_lore_archeotech": ["forbidden_lore"], "trade_any": ["trade"]},
    "beastman_turnskin": {"skill_any": ["forbidden_lore"]},
}
TALENT_FIX = {
    "neandors": {"melee_weapon_training_primitive": ["melee_weapon_training"],
                  "resistance_cold": ["resistance"]},
    "nightsiders": {"heightened_senses_any": ["heightened_senses"]},
    "ogryn": {"exotic_weapon_training_ripper_gun": ["exotic_weapon_training"]},
    "scalies": {"melee_weapon_training_primitive": ["melee_weapon_training"]},
    "troth": {"melee_weapon_training_primitive": ["melee_weapon_training"]},
}
# Круут и орк: по «Сквозь шторм» получают часть талантов и умений сразу.
SPECIAL = {
    "kruut": {"talents": ["mercenary"], "skills": []},
    "ork": {"talents": ["iron_jaw", "true_grit", "furious_assault", "xenos_weapon_training"],
            "skills": ["common_lore_orks", "intimidate", "speak_language_ork"]},
}

sp_path = os.path.join(D, "08-species.json")
d = json.load(open(sp_path, encoding="utf-8"))
for s in d["species"]:
    sid = s["id"]
    if sid in SKILL_FIX:
        fix = SKILL_FIX[sid]
        s["skills"] = [(fix[i][0] if i in fix and fix[i] else ("" if i in fix else i))
                       for i in (s.get("skills") or [])]
        s["skills"] = [x for x in s["skills"] if x]
    if sid in TALENT_FIX:
        fix = TALENT_FIX[sid]
        s["talents"] = [(fix[i][0] if i in fix and fix[i] else ("" if i in fix else i))
                        for i in (s.get("talents") or [])]
        s["talents"] = [x for x in s["talents"] if x]
    if sid in SPECIAL:
        s["talents"] = SPECIAL[sid]["talents"]
        s["skills"] = SPECIAL[sid]["skills"]
    # круут и орк получают формулы из «Сквозь шторм»
    if sid == "kruut":
        s["characteristicBase"] = {"ws": 30, "bs": 25, "str": 25, "tou": 25, "dex": 30,
                                   "int": 20, "per": 25, "wil": 25, "fel": 20}
        s["startingWounds"] = {"const": 3, "dice": 5, "useBonus": True, "unmodified": False,
                               "text": "2 × бонус Выносливости + 1к5+3"}
        s["startingFate"] = {"low": 2, "high": 3, "dice": 10,
                             "text": "бросок 1к10: на 1-5 — два очка судьбы, на 6-10 — три"}
        s["traits"] = ["Трупоед", "Выживание в диких краях", "Круутский клюв", "Физиология круутов",
                       "Медицинская помощь", "Бионика", "Крууты и технологии", "Пол",
                       "Крууты и порча", "Крууты и безумие"]
    if sid == "ork":
        s["characteristicBase"] = {"ws": 35, "bs": 15, "str": 35, "tou": 30, "dex": 20,
                                   "int": 15, "per": 20, "wil": 20, "fel": 15}
        s["startingWounds"] = {"const": 1, "dice": 5, "useBonus": True, "unmodified": True,
                               "text": "2 × бонус Выносливости + 1к5+1 (без «Сверхъестественной Выносливости»)"}
        s["startingFate"] = {"low": 1, "high": 2, "dice": 10,
                             "text": "бросок 1к10: на 1-5 — одно очко судьбы, на 6-10 — два"}
        s["traits"] = ["А ну работай!", "Здоровяк", "Кто сильнее, тот и прав", "Создан для драки",
                       "Чувство локтя", "Медицинская помощь", "Бионика и ауга", "Налезет — наденем, не налезет — сломаем",
                       "Бойцы", "Хаос и Гадость", "Дурилы", "Обучение или инстинкт?"]

with open(sp_path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("виды обновлены:", len(d["species"]))

# ── Тау: id языка в видах и роде занятий ─────────────────────────────────
for path, key in ((sp_path, "species"), (os.path.join(D, "11-tau-career.json"), "careers"),
                   (os.path.join(D, "11-tau-career.json"), "species")):
    d2 = json.load(open(path, encoding="utf-8"))
    changed = False
    for rec in d2.get(key, []) or []:
        for field in ("skills", "startingSkills"):
            lst = rec.get(field) or []
            if "linguistics_tau" in lst:
                rec[field] = ["speak_language_tau" if x == "linguistics_tau" else x for x in lst]
                changed = True
    if changed:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(d2, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print("исправлен id языка тау в", os.path.basename(path))
