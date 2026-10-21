# -*- coding: utf-8 -*-
"""
Сборка пакета по Тау: data\\builtin\\11-tau-career.json.

Берёт текст дополнения «Руководство по Тау» (PDF → текст, страницы помечены
строками "===== PAGE N =====") и выписывает из него:

  * расу «Тау» — характеристики по таблице 1-1, особенности, умения;
  * род занятий «Воин Огня» — таблицы улучшений на рангах 1–8 (стр. 14–17),
    таблицу улучшений характеристик (стр. 13) и три альтернативных пути:
    следопыт (стр. 19–20), оператор дронов (стр. 21–22), пилот боевого
    скафандра (стр. 23–24).

Названия умений и талантов из книги сопоставляются с id из уже собранного
пака (02-skills.json, 03-talents.json, 09-tau.json); если имя не найдено,
скрипт печатает его в отчёт — значит, в паке не хватает записи.

Запуск:
    python tools\\build_tau_pack.py --text <путь к tau.txt> [--pack data\\builtin]
"""
import argparse
import json
import os
import re
import shutil

# ── как называются умения и таланты в паке ────────────────────────────────
SKILL_OVERRIDE = {
    "обыденное знание (империя тау)": "common_lore_tau_empire",
    "ученое знание (империя тау)": "scholastic_lore_tau_empire",
    "ученое знание (военные доктрины тау)": "scholastic_lore_tau_empire",
    "языкознание (язык тау)": "linguistics_tau",
    "обыденное знание (война)": "common_lore_war",
    "языкознание (круутский)": "linguistics_kroot",
    "языкознание (низкий готический)": "speak_language_low_gothic",
    "вождение (глиссеры)": "drive_glider",
    "пилотирование (воздушная техника)": "pilot_flyer",
    "пилотирование (прыжковый ранец)": "pilot_jump_pack",
    "навигация (звёздная)": "navigation_stellar",
    "навигация (планетарная)": "navigation_planetary",
    "языкознание (высокий готический)": "speak_language_high_gothic",
    "шифры": "ciphers",
    "ловкость рук": "sleight_of_hand",
    "обыденное знание (техника)": "common_lore_tech",
    "запретное знание (ксеносы)": "forbidden_lore_xenos",
    "ремесло (техномат)": "trade_technomancer",
    "ремесло (оружейник)": "trade_gunsmith",
    "ремесло (химик)": "trade_chemist",
}
TALENT_OVERRIDE = {
    "огневая поддержка": "supporting_fire",
    "владение оружием касты огня": "fire_cast_weapon_training",
    "ритуал единения": "bonding_ritual",
    "целое больше суммы его частей": "greater_than_the_sum",
    "знаток технологий": "master_of_technology",
    "превосходные линии снабжения": "superior_supply_chain",
    "ракетный удар": "fire_support",
    "шквальный огонь": "furious_fusillade",
    "солдатское озарение": "technological_insight",
    "благословение эфирных": "blessing_of_the_ethereals",
    "посланец высшего блага": "envoy_of_the_greater_good",
    "тактическая гибкость": "tactical_flexibility",
    "сила в единстве": "strength_through_unity",
    "новые союзники": "new_allies",
    "образец самоотверженности": "exemplar_of_selfless_cause",
    "единство и разрушение": "through_unity_devastation",
    "приверженец кауйона": "disciple_of_kauyon",
    "приверженец монт'ка": "disciple_of_mont_ka",
    "приверженец монт’ка": "disciple_of_mont_ka",
    "точечное целеуказание": "guided_precision",
    "точное целеуказание": "guided_precision",
    "рискованное уклонение": "opportunists_evasion",
    "первоочередная цель": "priority_fire",
    "стайная тактика": "swarm_protocols",
    "совершенная подготовка": "advanced_battlesuit_training",
    "рефлексы ветерана": "veteran_reflexes",
    "боевое построение": "combat_formation",
    "хорошая репутация (крууты или иная раса империи тау)": "good_reputation_kroot",
    "связи (крууты или иная раса империи тау)": "kroot_bonds",
    "акимбо": "akimbo",
    "ас": "ace",
    "бугрящиеся бицепсы": "bulging_biceps",
    "всё видавший": "all_seeing",
    "владение зажигательным оружием (всё)": "flame_weapon_training",
    "владение зажигательным оружием (все)": "flame_weapon_training",
    "владение тяжёлым оружием (выберите одно)": "heavy_weapon_training",
    "владение холодным оружием (всё)": "melee_weapon_training",
    "владение холодным оружием (примитивное)": "melee_weapon_training_primitive",
    "владение экзотическим оружием (выберите одно)": "exotic_weapon_training",
    "владение экзотическим оружием (одно любое)": "exotic_weapon_training",
    "ненависть (орки)": "hate_orks",
    "зачищай и очищай!": "clean_and_clear",
    "ритуал освобождения": "ritual_of_emancipation",
    "мастер-хирургеон": "master_surgeon",
    "независимое прицеливание": "independent_sighting",
    "связи": "kroot_bonds",
    "спринт": "sprint",
    "одарённый": "gifted",
    "вложенное знание": "infused_knowledge",
}

CHAR_IDS = {
    "ближний бой": "ws", "дальний бой": "bs", "сила": "str", "выносливость": "tou",
    "ловкость": "dex", "интеллект": "int", "восприятие": "per",
    "сила воли": "wil", "товарищество": "fel",
}

RANK_XP = {
    1: (5000, 6900), 2: (7000, 9999), 3: (10000, 12999), 4: (13000, 16999),
    5: (17000, 20999), 6: (21000, 24999), 7: (25000, 29000), 8: (30000, 34999),
}

CAREER_ID = "fire_warrior"


def norm(s):
    s = s.strip().lower().replace("\u00a0", " ")
    s = s.replace("\u0451", "\u0435").replace("\u2019", "'")
    s = re.sub(r"[\u2013\u2014\u2212]", "-", s)
    return re.sub(r"\s+", " ", s)


def load(path, name):
    with open(os.path.join(path, name), encoding="utf-8") as f:
        return json.load(f)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--text", required=True, help="путь к tau.txt (текст книги)")
    ap.add_argument("--pack", default=os.path.join("data", "builtin"), help="папка с паком")
    ap.add_argument("--out", default="11-tau-career.json")
    args = ap.parse_args()

    skills_doc = load(args.pack, "02-skills.json")
    talents_doc = load(args.pack, "03-talents.json")
    tau_doc = load(args.pack, "09-tau.json")
    skills = skills_doc.get("skills") or skills_doc
    talents = talents_doc.get("talents") or talents_doc
    tau_skills = tau_doc.get("skills") or []
    tau_talents = tau_doc.get("talents") or []

    skill_map, talent_map = {}, {}
    for s in skills + tau_skills:
        skill_map.setdefault(norm(s["name"]), s["id"])
    for t in talents + tau_talents:
        talent_map.setdefault(norm(t["name"]), t["id"])

    unresolved = set()

    def resolve_skill(raw):
        n = re.sub(r"\s*\(\s*[×x]\s*2\s*\)\s*$", "", norm(raw)).replace("†", "").strip()
        if n in SKILL_OVERRIDE:
            return SKILL_OVERRIDE[n]
        if n in skill_map:
            return skill_map[n]
        base = re.sub(r"\s*\([^)]*\)\s*$", "", n).strip()
        cands = {k: v for k, v in skill_map.items()
                 if norm(re.sub(r"\s*\([^)]*\)\s*$", "", k)) == base}
        if len(cands) == 1:
            return list(cands.values())[0]
        unresolved.add("умение: " + raw)
        return None

    def resolve_talent(raw):
        n = re.sub(r"\s*\(\s*[×x]\s*2\s*\)\s*$", "", norm(raw)).replace("†", "").strip()
        if n in TALENT_OVERRIDE:
            return TALENT_OVERRIDE[n]
        if n in talent_map:
            return talent_map[n]
        base = re.sub(r"\s*\([^)]*\)\s*$", "", n).strip()
        cands = {k: v for k, v in talent_map.items()
                 if norm(re.sub(r"\s*\([^)]*\)\s*$", "", k)) == base}
        if len(cands) == 1:
            return list(cands.values())[0]
        unresolved.add("талант: " + raw)
        return None

    lines = open(args.text, encoding="utf-8", errors="replace").read().splitlines()
    page_marks = [i for i, l in enumerate(lines) if l.startswith("===== PAGE")]
    head = re.compile(
        r"^Улучшения\s+(воина огня на ранге\s+(\d+)|следопыта|оператора дронов|"
        r"пилота боев?скафандра|характеристик воина огня)\s*$", re.IGNORECASE)
    sections = []
    for i, l in enumerate(lines):
        m = head.match(l.strip())
        if m:
            sections.append((l.strip(), m.group(2), i))

    def page_end(start):
        for p in page_marks:
            if p > start:
                return p
        return len(lines)

    def parse_rows(start, end):
        rows = []
        i = start + 1
        while i < end:
            raw = lines[i].strip()
            i += 1
            if not raw or raw in ("Улучшение", "Цена", "Тип", "Требования"):
                continue
            if not re.fullmatch(r"\d+", raw):
                continue
            price = int(raw)
            name = None
            k = i - 2
            while k >= start:
                if lines[k].strip():
                    name = lines[k].strip()
                    break
                k -= 1
            typ = lines[i].strip() if i < end else ""
            req = lines[i + 1].strip() if i + 1 < end else ""
            i += 2
            if name and typ in ("Умение", "Талант", "Характеристика", "Особенность"):
                rows.append((name, price, typ, req))
        return rows

    def build_advance(key, index, name, price, typ, req):
        opt = {
            "id": f"{CAREER_ID}#{key}#{index}",
            "name": name,
            "type": typ,
            "cost": price,
            "requirements": "" if req in ("-", "") else req,
            "requirementList": [],
            "repeatable": False,
            "maxRepeats": 1,
        }
        if re.search(r"\(\s*[×x]\s*2\s*\)", name):
            opt["repeatable"] = True
            opt["maxRepeats"] = 2
        clean = re.sub(r"\s*\(\s*[×x]\s*2\s*\)\s*$", "", name).replace("†", "").strip()
        if typ == "Умение":
            m = re.match(r"^(.*?)\s*\+(\d+)$", clean)
            base = m.group(1).strip() if m else clean
            step = max(1, int(m.group(2)) // 10) if m else 1
            sid = resolve_skill(base)
            if sid:
                opt["skillId"] = sid
                opt["skillStep"] = step
        elif typ == "Талант":
            tid = resolve_talent(clean)
            if tid:
                opt["talentId"] = tid
        return opt

    ranks, char_adv, paths = [], {}, []
    for j, (title, rank_s, start) in enumerate(sections):
        end = page_end(start)
        if j + 1 < len(sections):
            end = min(end, sections[j + 1][2])
        low = title.lower()
        if "характеристик воина огня" in low:
            i, cur, vals = start + 1, None, []
            while i < end:
                s = lines[i].strip()
                i += 1
                if not s:
                    continue
                if s in ("Характеристика", "Простой", "Средний", "Отточенный", "Предельный"):
                    continue
                if re.fullmatch(r"\d+", s):
                    vals.append(int(s))
                    if len(vals) == 4:
                        if cur:
                            char_adv[cur] = vals
                        vals = []
                    continue
                if s.lower() in CHAR_IDS:
                    cur, vals = CHAR_IDS[s.lower()], []
            continue

        rows = parse_rows(start, end)
        if rank_s:
            r = int(rank_s)
            ranks.append({
                "level": r,
                "name": "Воин Огня" if r == 1 else f"Воин Огня (ранг {r})",
                "xpMin": RANK_XP[r][0],
                "xpMax": RANK_XP[r][1],
                "advances": [build_advance(str(r), i, *row) for i, row in enumerate(rows)],
                "requirements": "",
                "notes": "",
                "alternate": False,
            })
        else:
            key = ("scout" if "следопыт" in low else
                   "drone_operator" if "оператор" in low else "battlesuit_pilot")
            meta = {
                "scout": ("Следопыт", 2, 7000,
                          "Ловкость 40, умение «Бесшумный шаг», талант «Стальные нервы»."),
                "drone_operator": ("Оператор дронов", 3, 10000,
                                   "Интеллект 40, умение «Пользование техники+10»."),
                "battlesuit_pilot": ("Пилот боевого скафандра", 5, 17000, "Ловкость 45"),
            }[key]
            paths.append({
                "id": key,
                "name": meta[0],
                "minRank": meta[1],
                "minXp": meta[2],
                "replacesRank": f"ранг {meta[1]} или выше ({meta[2]:,} ОО)".replace(",", " "),
                "requirements": meta[3],
                "careerRequired": "Воин Огня",
                "advances": [build_advance("path_" + key, i, *row)
                             for i, row in enumerate(rows)],
            })
    ranks.sort(key=lambda r: r["level"])

    species = {
        "id": "tau",
        "name": "Тау",
        "book": "tau",
        "lang": "ru",
        "source": "tau",
        "tau": True,
        "size": 0,
        "sizeNote": "В дополнении размер тау не указан — считается средним (4).",
        "description": (
            "Тау — раса Империи Тау, живущей по ту сторону варп-врата Иерихон. "
            "Правила создания персонажа-тау заменяют обычные правила «Базовой книги правил Rogue Trader»: "
            "игрок выбирает выявленную склонность, затем определяет характеристики по таблице 1-1 "
            "«Характеристики тау», а затем тратит начальный опыт. Персонажи-тау используют таблицы "
            "рода занятий «Воин Огня».\n\n"
            "Характеристики тау (таблица 1-1): Ближний бой 2к10+20, Дальний бой 2к10+25, Сила 2к10+20, "
            "Выносливость 2к10+25, Ловкость 2к10+30, Интеллект 2к10+30, Восприятие 2к10+25, "
            "Сила воли 2к10+25, Товарищество 2к10+25.\n\n"
            "Все авантюристы-тау начинают игру с особенностями «Во имя Высшего блага!», "
            "«Чуждый Империуму» и «С чужаком не заговаривай», а также с доступом к умениям "
            "«Обыденное знание (Империя Тау)», «Учёное знание (Империя Тау)» и «Языкознание (Язык тау)».\n\n"
            "Количество ран и очки судьбы для тау в дополнении не указаны: родной мир тау выбрать нельзя, "
            "вместо него выбираются выявленные склонности, поэтому это решает ведущий."
        ),
        "characteristicBase": {
            "ws": 20, "bs": 25, "str": 20, "tou": 25, "dex": 30,
            "int": 30, "per": 25, "wil": 25, "fel": 25,
        },
        "bonus": {},
        "talents": [],
        "skills": ["common_lore_tau_empire", "scholastic_lore_tau_empire", "linguistics_tau"],
        "startingEquipment": [],
        "careers": ["fire_warrior"],
        "specialRules": [
            "Во имя Высшего блага! (For the Greater Good!): Один раз за сцену авантюрист-тау может получить бонус +10 к тесту, совершая который, он подчиняется прямому приказу другого тау. Кроме того, если исполнение этого приказа подвергнет персонажа значительной опасности ради блага всей группы, бонус может возрасти до +30 (по решению ведущего).",
            "С чужаком не заговаривай (Speak Not Unto The Alien): Это существо считает все основанные на Товариществе тесты, обращённые к людям, сложнее на две ступени. Аналогично, сами люди считают все направленные на этого персонажа тесты Товарищества сложнее на две ступени. Эти штрафы не применяются в отношении тех, кому персонаж-ксенос доверяет (последнее слово за ведущим). Наконец, присутствие чужака на борту человеческого корабля беспокоит экипаж, вызывает слухи и ощутимое недовольство. Долгое присутствие одно или нескольких персонажей-ксеносов на борту корабля снижает боевой дух на 2.",
            "Чуждый Империуму (Non-Imperial): Этот персонаж рос не среди людей, а потому очень мало знает об истории и культуре Империума. Законы, традиции, верования и суеверия Человечества для него неведомы и чужды. Такой персонаж получает штраф -10 на все тесты «Обыденного», «Учёного» и «Запретного знания», касающиеся Империума.",
        ],
        "elitePackages": [],
    }

    career = {
        "id": CAREER_ID,
        "name": "Воин Огня",
        "source": "tau",
        "startingRank": "Воин Огня",
        "description": (
            "В касту Огня входят прекрасно обученные и снаряжённые защитники Империи Тау. "
            "У каждого рождённого в ней есть лишь одна дорога – стать солдатом и сражаться во имя "
            "Высшего блага, всю жизнь оттачивая своё мастерство. Команды составляют воины низшего "
            "ранга – шас’ла, которыми руководят шас’уи; искусные ветераны получают титул шас’вре и "
            "пилотируют почитаемые боевые скафандры; воины, удостоенные командирского ранга, "
            "называются шас’эль, а высший командный состав носит ранг шас’о.\n\n"
            "Блок рода занятий (стр. 7 дополнения): Ближний бой 35, Дальний бой 25, Сила 30, "
            "Выносливость 30, Ловкость 20, Интеллект 30, Восприятие 35, Сила воли 30, "
            "Товарищество 25. Количество ран: 13. Скорость: 6/9/12/18 (по размеру).\n"
            "Умения: «Бдительность (Вос)», «Командование (Тов)», «Медика (Инт)», "
            "«Обыденное знание (Империя Тау) (Инт)», «Уклонение (Лов)», "
            "«Учёное знание (Военные доктрины тау) (Инт)+10». Таланты: «Боевое построение», "
            "«Владение оружием касты Огня», «Огневая поддержка», «Ритуал единения», "
            "«Целое больше суммы его частей». Особенности: «Во имя Высшего блага!».\n"
            "Оружие: импульсная винтовка (лёгкое, 150 м., 1/2/4, 2к10+3 Э, ББ 4, Маг 36, ЧД, "
            "Гиростабилизованное) или импульсный карабин (лёгкое, 60 м., 1/-/3, 2к10+2 Э, ББ 4, "
            "Маг 24, ЧД, Гиростабилизованное) или рельсовая винтовка (лёгкое, 150 м., 1/-/-, "
            "2к10+3 У, ББ 4, Маг 12, ПД, Разящее, Проверенное (3)). Броня: боевая броня тау "
            "(всё по 6). Снаряжение: два магазина для главного оружия, вокс-бусина, две фотонные "
            "гранаты тау.\n\n"
            "Начальные умения: «Грамотность (Инт)», «Логика (Инт)», «Обыденное знание (Империя Тау) (Инт)», "
            "«Пользование техники (Инт)», «Уклонение (Лов)», «Языкознание (Язык Тау) (Инт)».\n"
            "Начальные таланты: «Быстрая перезарядка», «Владение оружием касты Огня», «Меткий стрелок», "
            "«Непоколебимая вера», «Огневая поддержка».\n"
            "Начальные особенности: «Во имя Высшего блага!», «Выявленная склонность», "
            "«Чуждый Империуму», «С чужаком не заговаривай».\n"
            "Начальное снаряжение: импульсная винтовка хорошего качества или импульсный карабин "
            "хорошего качества, импульсный пистолет высшего качества, три фотонные гранаты тау, "
            "боевая броня тау со встроенной вокс-бусиной, передатчиком и черносолнечным фильтром.\n\n"
            "Примечание: в дополнении сказано, что у рода занятий 13 рангов, но таблицы улучшений "
            "напечатаны только для рангов 1-8; ранг персонажа, как и у всех, определяется по таблице "
            "2-2 «Ранги» базовой книги."
        ),
        "rankRequirements": (
            "Ранги и требования к ним — общие, по таблице 2-2 «Ранги» базовой книги. Таблица "
            "улучшений воина Огня напечатана для рангов 1-8; улучшения характеристик — по таблице "
            "«Улучшения характеристик воина огня» (стр. 13 дополнения). Альтернативные ранги "
            "(следопыт, оператор дронов, пилот боевого скафандра) заменяют обычную таблицу улучшений "
            "с указанного ранга."
        ),
        "startingEquipment": [
            "импульсная винтовка хорошего качества или импульсный карабин хорошего качества",
            "импульсный пистолет высшего качества",
            "два магазина для главного оружия",
            "три фотонные гранаты тау",
            "боевая броня тау со встроенной вокс-бусиной, передатчиком и черносолнечным фильтром",
        ],
        "startingSkills": ["literacy", "logic", "common_lore_tau_empire", "tech_use", "dodge", "linguistics_tau"],
        "startingTalents": ["rapid_reload", "fire_cast_weapon_training", "marksman", "unshakable_faith", "supporting_fire"],
        "startingFeatures": ["Во имя Высшего блага!", "Выявленная склонность", "Чуждый Империуму", "С чужаком не заговаривай"],
        "shipUpgrade": None,
        "shipHullPoints": None,
        "ranks": ranks,
        "paths": paths,
        "characteristicAdvancement": char_adv,
        "advancementLevelNames": ["Простой", "Средний", "Отточенный", "Предельный"],
        "startingSkillsVerbatim": None,
        "startingTalentsVerbatim": None,
        "alternateRanks": False,
    }

    out = os.path.join(args.pack, args.out)
    with open(out, "w", encoding="utf-8") as f:
        json.dump({"species": [species], "careers": [career]}, f, ensure_ascii=False, indent=1)
    print("записано:", out, os.path.getsize(out), "байт")
    print("рангов:", len(ranks), "· путей:", len(paths),
          "· улучшений:", sum(len(r["advances"]) for r in ranks) + sum(len(p["advances"]) for p in paths))
    if unresolved:
        print("НЕ НАЙДЕНЫ В ПАКЕ:")
        for u in sorted(unresolved):
            print("  ", u)
    else:
        print("все умения и таланты сопоставлены с паком")


if __name__ == "__main__":
    main()
