#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
build_datapack.py — сборка канонического дата-пака приложения из «сырых» файлов,
извлечённых из книг правил.

Вход :  tools/raw/*.json      (результат извлечения из PDF)
Выход:  data/builtin/*.json   (9 файлов, которые читает приложение)

Запуск:
    python tools/build_datapack.py --raw tools/raw --out data/builtin

Ключевая особенность этого проекта: приложение реализует Dark Heresy 2e —
именно ту систему, которая реально записана в приложенных PDF
(характеристики WS/BS/STR/TOU/DEX/INT/PER/WILL/FEL, очки опыта, ранги 1-8).
Поэтому здесь же приводятся к канону id характеристик.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from collections import OrderedDict
from typing import Any

# ────────────────────────────── канон ──────────────────────────────

# id -> (полное имя, сокращение, колонка)  — порядок как в таблицах книг
CHARACTERISTICS = OrderedDict([
    ("ws", ("Ближний бой", "ББ", 0)),
    ("bs", ("Дальний бой", "ДБ", 1)),
    ("str", ("Сила", "С", 2)),
    ("tou", ("Выносливость", "Вын", 3)),
    ("dex", ("Ловкость", "Лов", 4)),
    ("int", ("Интеллект", "Инт", 5)),
    ("per", ("Восприятие", "Вос", 6)),
    ("wil", ("Сила воли", "СВ", 7)),
    ("fel", ("Товарищество", "Тов", 8)),
])

# Приводит ЛЮБУЮ форму написания характеристики к каноническому id.
CHAR_ALIASES = {
    # русские полные
    "ближний бой": "ws", "ближний": "ws", "бб": "ws", "ws": "ws", "weapon skill": "ws",
    "дальний бой": "bs", "дальний": "bs", "дб": "bs", "bs": "bs", "ballistic skill": "bs",
    "сила": "str", "с": "str", "сил": "str", "str": "str", "strength": "str",
    "выносливость": "tou", "вын": "tou", "tou": "tou", "toughness": "tou",
    "ловкость": "dex", "лов": "dex", "dex": "dex", "agility": "dex", "dexterity": "dex",
    "интеллект": "int", "инт": "int", "int": "int", "intelligence": "int",
    "восприятие": "per", "вос": "per", "per": "per", "perception": "per",
    "сила воли": "wil", "св": "wil", "wil": "wil", "willpower": "wil", "will": "wil",
    "товарищество": "fel", "тов": "fel", "fel": "fel", "fellowship": "fel",
    # служебные
    "movement": "movement", "скорость": "movement",
    "size": "size", "размер": "size",
    "psy": "psy", "пси": "psy", "пси-рейтинг": "psy", "psy rating": "psy", "psy_rating": "psy",
    "wounds": "wounds", "wt": "wounds", "порог ранений": "wounds", "wound threshold": "wounds",
    "fate": "fate", "fate points": "fate", "очки судьбы": "fate",
}

# id, которые агенты-извлекатели ошибочно проставляли по схеме RT1e
STALE_ID_FIX = {
    "melee": "ws", "ranged": "bs", "close": "ws", "ballistic": "bs",
    "agility": "dex", "dexterity": "dex",
    "toughness": "tou", "size": "size", "charm": "fel", "charisma": "fel",
    "leadership": "fel", "knowledge": "int", "luck": "fate",
    "willpower": "wil", "perception": "per", "fellowship": "fel",
}

WARNINGS: list[str] = []
STATS: dict[str, int] = {}


def warn(msg: str) -> None:
    if msg not in WARNINGS:
        WARNINGS.append(msg)


def bump(key: str, n: int = 1) -> None:
    STATS[key] = STATS.get(key, 0) + n


def norm_char(value: Any) -> str | None:
    """Любая форма названия характеристики -> канонический id."""
    if value is None:
        return None
    s = str(value).strip()
    if not s:
        return None
    s = s.replace("﻿", "").replace("_", " ").replace(".", " ")
    s = re.sub(r"\s+", " ", s).strip().lower()
    s = s.replace("(и)", "").strip()
    if s in CHAR_ALIASES:
        return CHAR_ALIASES[s]
    if s in STALE_ID_FIX:
        return STALE_ID_FIX[s]
    # «Дальн. бой», «Инт.», «Сил+5» и т.п.
    for alias, cid in CHAR_ALIASES.items():
        if alias and (s.startswith(alias) or s.rstrip(".,;:").endswith(alias)):
            return cid
    return None


def char_list(*values: Any) -> list[str]:
    out: list[str] = []
    for v in values:
        if isinstance(v, list):
            out.extend(char_list(*v))
        elif v is not None:
            cid = norm_char(v)
            if cid and cid not in out:
                out.append(cid)
    return out


def b(value: Any) -> bool:
    """'False'/'True' (строки из PDF-извлечения) -> bool."""
    if isinstance(value, bool):
        return value
    if value is None:
        return False
    s = str(value).strip().lower()
    return s in ("true", "1", "да", "yes", "истина")


def i(value: Any, default: int = 0) -> int:
    """'100', ' 100 ОО', '+5' -> int."""
    if value is None:
        return default
    if isinstance(value, (int, float)):
        return int(value)
    m = re.search(r"[-+]?\d+", str(value).replace(" ", " "))
    return int(m.group()) if m else default


def s(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, (list, dict)):
        return json.dumps(value, ensure_ascii=False)
    return str(value).strip()


def norm_start_wounds(value: Any) -> dict | None:
    """Формула начальных ранений варианта пути из сырья."""
    if not isinstance(value, dict):
        return None
    return {
        "const": i(value.get("const")),
        "dice": i(value.get("dice")),
        "useBonus": b(value.get("use_bonus", value.get("useBonus", True))),
        "unmodified": b(value.get("unmodified")),
        "fixed": i(value.get("fixed")),
        "text": s(value.get("text")),
    }


def norm_start_fate(value: Any) -> dict | None:
    """Правило начальных очков судьбы варианта пути из сырья."""
    if not isinstance(value, dict):
        return None
    ranges = []
    for r in value.get("ranges") or []:
        if not isinstance(r, dict):
            continue
        ranges.append({
            "from": i(r.get("from")),
            "to": i(r.get("to")),
            "points": i(r.get("points")),
        })
    return {
        "dice": i(value.get("dice"), 10),
        "ranges": ranges,
        "text": s(value.get("text")),
    }


def sl(value: Any) -> list[str]:
    """Список строк из строки/списка/None."""
    if value is None:
        return []
    if isinstance(value, str):
        return [x.strip() for x in value.split("\n") if x.strip()]
    if isinstance(value, list):
        out = []
        for v in value:
            if isinstance(v, str):
                out.extend(x.strip() for x in v.split("\n") if x.strip())
            elif v is not None:
                out.append(s(v))
        return out
    return [s(value)]


def slug(text: str, fallback: str = "x") -> str:
    """Русское название -> латинский id (транслитерация)."""
    TR = {
        "а": "a", "б": "b", "в": "v", "г": "g", "д": "d", "е": "e", "ё": "e", "ж": "zh",
        "з": "z", "и": "i", "й": "y", "к": "k", "л": "l", "м": "m", "н": "n", "о": "o",
        "п": "p", "р": "r", "с": "s", "т": "t", "у": "u", "ф": "f", "х": "h", "ц": "ts",
        "ч": "ch", "ш": "sh", "щ": "sch", "ъ": "", "ы": "y", "ь": "", "э": "e", "ю": "yu", "я": "ya",
    }
    out = []
    for ch in (text or "").lower():
        if ch.isascii() and ch.isalnum():
            out.append(ch)
        elif ch in TR:
            out.append(TR[ch])
        elif ch in " -_()«»\"'":
            out.append("_")
    res = re.sub(r"_+", "_", "".join(out)).strip("_")
    return res or fallback


def unique_id(base: str, used: set[str]) -> str:
    base = base or "x"
    if base not in used:
        used.add(base)
        return base
    n = 2
    while f"{base}_{n}" in used:
        n += 1
    used.add(f"{base}_{n}")
    return f"{base}_{n}"


# ────────────────────────────── нормализация разделов ──────────────────────────────

def spec_list(value: Any) -> list[str]:
    """Специализации из сырья: список строк или строка через запятую.

    В книге это «Связи (Выберите одни)»: игрок выбирает одно из направлений,
    поэтому список должен быть виден игроку.
    """
    items: list[str] = []
    if isinstance(value, str):
        value = [value]
    for v in value or []:
        t = s(v)
        if not t:
            continue
        for part in re.split(r",\s*", t):
            part = part.strip(" .;:")
            if part:
                items.append(part)
    return items


def with_specs(description: str, specs: list[str]) -> str:
    """Дописывает к описанию список специализаций — чтобы выбор был виден."""
    if not specs:
        return description
    line = "Специализации (выберите одно): " + ", ".join(specs)
    return (description + "\n\n" + line).strip() if description else line


def norm_skills(raw: dict, used: set[str]) -> list[dict]:
    out = []
    for sk in raw.get("skills", []) or []:
        sid = slug(sk.get("id") or sk.get("name", ""), "skill")
        sid = unique_id(sid, used)

        # Характеристика у умения одна, и берётся из книги.
        # Поле `characteristic` в сырье — устаревшее: агенты-извлекатели
        # проставляли id по схеме RT1e («Ловкость» → toughness, «Восприятие»
        # → charm). Складывать оба источника нельзя — в лист попадали две
        # характеристики, и вклад в умение удваивался.
        chars = char_list(sk.get("characteristic_book"))
        if not chars:
            chars = char_list(sk.get("characteristic"), sk.get("characteristics"))

        adv = []
        a = sk.get("advancements")
        if isinstance(a, dict):
            for lvl, val in a.items():
                if isinstance(val, dict):
                    adv.append({"level": i(lvl, 3), "name": s(val.get("name")), "effect": s(val.get("effect"))})
                else:
                    adv.append({"level": i(lvl, 3), "name": "", "effect": s(val)})
        elif isinstance(a, list):
            for val in a:
                adv.append({"level": i(val.get("level"), 3), "name": s(val.get("name")), "effect": s(val.get("effect"))})

        expertise = []
        for ex in sk.get("expertise", []) or []:
            if isinstance(ex, dict):
                expertise.append({"name": s(ex.get("name")), "effect": s(ex.get("effect"))})
            else:
                expertise.append({"name": s(ex), "effect": ""})

        rec = {
            "id": sid,
            "name": s(sk.get("name")),
            "description": with_specs(s(sk.get("description")),
                                      spec_list(sk.get("specializations"))),
            "specializations": spec_list(sk.get("specializations")),
            "characteristics": chars,
            "advanced": b(sk.get("is_advanced")),
            "specialized": b(sk.get("specialized")),
            "source": s(sk.get("source")) or "core",
            "expertise": expertise,
            "advancements": adv,
        }
        if sk.get("parent"):
            rec["parentId"] = slug(str(sk["parent"]), "skill")
        for k in ("_note", "characteristic_note", "name_en"):
            if sk.get(k):
                rec[k] = s(sk[k])
        out.append(rec)
        bump("skills")

    # Список специализаций: у «Запретное знание (Варп)» есть родитель
    # «Запретное знание» — в книге это «Запретное знание (Выберите одно)».
    by_parent: dict[str, list[str]] = {}
    for rec in out:
        parent = str(rec.get("parentId") or "")
        if parent:
            by_parent.setdefault(parent, []).append(str(rec.get("name") or ""))
    for rec in out:
        kids = by_parent.get(str(rec.get("id") or ""), [])
        if not kids:
            continue
        rec["specializations"] = kids
        rec["description"] = with_specs(str(rec.get("description") or ""), kids)
    return out


def norm_requirement(req: Any) -> list[dict]:
    """Требование из извлечения -> список RequirementDef."""
    res: list[dict] = []
    if req is None:
        return res
    items = req if isinstance(req, list) else [req]
    for r in items:
        if not isinstance(r, dict):
            continue
        t = (r.get("type") or "").strip().lower()
        entry: dict[str, Any] = {}
        if t in ("characteristic", "char"):
            cid = norm_char(r.get("characteristic") or r.get("id") or r.get("value"))
            if cid:
                entry = {"type": "characteristic", "characteristic": cid, "min": i(r.get("min") or r.get("value"))}
        elif t in ("talent",):
            tl = [slug(str(x)) for x in (r.get("talents") or ([r["id"]] if r.get("id") else []))]
            if tl:
                entry = {"type": "talent", "talents": tl}
        elif t in ("skill",):
            sk = []
            for sub in (r.get("skills") or ([r] if r.get("id") else [])):
                if isinstance(sub, dict):
                    sk.append({"id": slug(str(sub.get("id", ""))), "rank": i(sub.get("rank"), 1)})
            if sk:
                entry = {"type": "skill", "skills": sk}
        elif t in ("species", "career", "rank"):
            entry = {"type": t}
            if t == "species":
                entry["species"] = [slug(str(x)) for x in (r.get("species") or [])]
            else:
                entry["career"] = slug(str(r.get("career", "")))
                entry["rankAtLeast"] = i(r.get("rankAtLeast") or r.get("rank"), 1)
        elif t in ("any", "group"):
            entry = {"type": t, "of": norm_requirement(r.get("of"))}
        else:
            txt = s(r.get("text") or r.get("description"))
            if txt:
                entry = {"type": "text", "text": txt}
        if entry:
            entry.setdefault("text", s(r.get("text")))
            res.append(entry)
    return res


def norm_talents(raw: dict, used: set[str], default_source: str = "core") -> list[dict]:
    out = []
    for t in raw.get("talents", []) or []:
        tid = slug(str(t.get("id") or t.get("name", "")), "talent")
        tid = unique_id(tid, used)

        effects = []
        for e in t.get("effects", []) or []:
            if isinstance(e, dict):
                effects.append({"trigger": s(e.get("trigger")) or "passive", "text": s(e.get("text") or e.get("description"))})
            elif e:
                effects.append({"trigger": "passive", "text": s(e)})

        ranks = i(t.get("ranks"), 1) or 1
        prereq = t.get("prerequisites")
        if isinstance(prereq, str):
            prereq = [prereq] if prereq.strip() else []
        prereq = sl(prereq) or ([s(t.get("table_requirements"))] if s(t.get("table_requirements")) not in ("", "-") else [])

        rec = {
            "id": tid,
            "name": s(t.get("name")),
            "ranks": ranks,
            "advanced": b(t.get("is_advanced")),
            "prerequisites": prereq,
            "requirements": norm_requirement(t.get("requirement")),
            "description": with_specs(s(t.get("description")),
                                      spec_list(t.get("specializations"))),
            "specializations": spec_list(t.get("specializations")),
            "effects": effects,
            "source": s(t.get("source")) or default_source,
        }
        for k in ("_note", "name_en", "multiple_ranks_note", "ranks_note"):
            if t.get(k):
                rec[k] = s(t[k])
        out.append(rec)
        bump("talents")
    return out


def norm_bonuses(bons: Any) -> list[dict]:
    out = []
    if isinstance(bons, dict):
        for k, v in bons.items():
            cid = norm_char(k)
            if cid and i(v):
                out.append({"characteristic": cid, "value": i(v)})
    elif isinstance(bons, list):
        for bo in bons:
            cid = norm_char(bo.get("characteristic") or bo.get("id"))
            if cid:
                out.append({"characteristic": cid, "value": i(bo.get("value"))})
    return out


ADV_LEVELS = ["простой", "средний", "отточенный", "предельный"]

# Таблица 2-2 «Ранги»: ранг → (мин, макс) накопленного опыта
XP_RANGES = {
    1: (5000, 6900), 2: (7000, 9999), 3: (10000, 12999), 4: (13000, 16999),
    5: (17000, 20999), 6: (21000, 24999), 7: (25000, 29099), 8: (30000, 34999),
}


def parse_requirements(text: str) -> list[dict]:
    """«Сила воли 30, Ловкость 35» -> [{type:characteristic,...}, ...]."""
    out: list[dict] = []
    if not text or text.strip() in ("-", "—"):
        return out
    for part in re.split(r"[,;]", text):
        p = part.strip()
        if not p or p in ("-", "—"):
            continue
        m = re.match(r"^([A-Za-zА-Яа-яЁё\. ]{3,40}?)\s*(\d+)?\s*$", p)
        if not m:
            continue
        cid = norm_char(m.group(1))
        if cid and cid in CHARACTERISTICS:
            out.append({"type": "characteristic", "characteristic": cid,
                        "min": i(m.group(2)) if m.group(2) else 30,
                        "text": p})
    return out


def _advance_skill(step: int) -> str:
    return "+" + str(step * 10)


def _key_name(text: str) -> str:
    """Ключ для сопоставления названий: без скобок, характеристик в скобках и знаков."""
    t = (text or "").lower()
    t = re.sub(r"[«»\"']", "", t)
    t = re.sub(r"\([^)]*\)", "", t)          # убрать специализации в скобках
    t = re.sub(r"\+?\d+\s*$", "", t).strip()   # убрать хвостовые +10/+20
    t = re.sub(r"\s*[×x]\s*\d+", "", t)
    t = re.sub(r"[^0-9a-zа-яё]+", " ", t)
    return re.sub(r"\s+", " ", t).strip()


SKILL_INDEX: dict[str, str] = {}
TALENT_INDEX: dict[str, str] = {}


def _plain(text: str) -> str:
    """Название без кавычек: в книге таланты перечислены как «Наёмник»."""
    return re.sub(r"[«»\"']", "", (text or "").strip()).strip()


def build_indexes(skills_raw: dict, talents_raw: dict) -> None:
    SKILL_INDEX.clear(); TALENT_INDEX.clear()
    for sk in skills_raw.get("skills", []) or []:
        sid = s(sk.get("id"))
        if not sid:
            continue
        for key in (s(sk.get("name")), s(sk.get("name_en")), sid):
            k = _key_name(key)
            if k:
                SKILL_INDEX.setdefault(k, sid)
    for t in talents_raw.get("talents", []) or []:
        tid = s(t.get("id"))
        if not tid:
            continue
        for key in (s(t.get("name")), s(t.get("name_en")), tid):
            k = _key_name(key)
            if k:
                TALENT_INDEX.setdefault(k, tid)
    # Дополнения из «Сквозь шторм» и «Руководства по Тау» — если файл уже
    # собран, его умения и таланты тоже участвуют в сопоставлении.
    extra_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                               "data", "builtin", "13-extra-skills.json")
    if os.path.exists(extra_path):
        try:
            with open(extra_path, encoding="utf-8") as f:
                extra = json.load(f)
        except (OSError, ValueError):
            extra = {}
        for sk in extra.get("skills", []) or []:
            sid = s(sk.get("id"))
            for key in (s(sk.get("name")), s(sk.get("name_en")), sid):
                k = _key_name(key)
                if k and sid:
                    SKILL_INDEX.setdefault(k, sid)
        for t in extra.get("talents", []) or []:
            tid = s(t.get("id"))
            for key in (s(t.get("name")), s(t.get("name_en")), tid):
                k = _key_name(key)
                if k and tid:
                    TALENT_INDEX.setdefault(k, tid)


def resolve_skill_id(name: str, hint: str | None = None) -> str:
    if hint:
        k = _key_name(hint)
        if k in SKILL_INDEX:
            return SKILL_INDEX[k]
    k = _key_name(name)
    if k in SKILL_INDEX:
        return SKILL_INDEX[k]
    return slug(hint or name, "skill")


def resolve_talent_id(name: str, hint: str | None = None) -> str:
    if hint:
        k = _key_name(hint)
        if k in TALENT_INDEX:
            return TALENT_INDEX[k]
    k = _key_name(name)
    if k in TALENT_INDEX:
        return TALENT_INDEX[k]
    return slug(hint or name, "talent")


def _mk_advance(career_id: str, rank: int, idx: int, *,
                name: str, cost: Any, kind: str, ref_id: str | None,
                text: str, times: Any = None,
                spec: str | None = None, step: int = 0,
                char_points: int = 0, char_level: int = 0) -> dict:
    aid = f"{career_id}#{rank}#{idx}"
    mx = i(times, 1) if times is not None else 1
    adv = {
        "id": aid,
        "name": name,
        "type": kind,
        "cost": i(cost),
        "requirements": text,
        "requirementList": parse_requirements(text),
        "repeatable": mx > 1,
        "maxRepeats": max(1, mx),
    }
    if ref_id:
        adv["skillId" if kind == "Умение" else "talentId"] = ref_id
    if kind == "Умение":
        adv["skillStep"] = step
    if kind == "Характеристика":
        adv["characteristicId"] = ref_id
        adv["characteristicPoints"] = char_points or 5
        adv["characteristicLevel"] = char_level or 1
    if spec:
        adv["specialization"] = spec
    return adv


def _skill_step_from_name(name: str) -> tuple[str, int]:
    """«Атлетика+20» -> (Атлетика, 2)."""
    m = re.match(r"^(.*?)\s*\+(\d+)\s*$", name.strip())
    if m:
        return m.group(1).strip(), int(m.group(2)) // 10
    return name.strip(), 0


def norm_careers(raw: dict, used: set[str], source: str) -> list[dict]:
    out = []
    for c in raw.get("careers", []) or []:
        cid = slug(str(c.get("id") or c.get("name", "")), "career")
        cid = unique_id(cid, used)

        ranks: list[dict] = []
        raw_ranks = c.get("ranks") or {}
        if isinstance(raw_ranks, dict):
            items = sorted(raw_ranks.items(), key=lambda kv: i(kv[0], 99))
        else:
            items = [(str(x.get("level")), x) for x in (raw_ranks or [])]

        for rank_key, val in items:
            level = i(rank_key, len(ranks) + 1)
            val = val if isinstance(val, dict) else {}
            advances: list[dict] = []
            idx = 0

            # ── умения ──
            for sk in val.get("skills", []) or []:
                if not isinstance(sk, dict):
                    continue
                raw_name = s(sk.get("name"))
                base_name, step = _skill_step_from_name(raw_name)
                step = step or 1
                sid = resolve_skill_id(base_name, sk.get("canonical_id") or sk.get("id"))
                advances.append(_mk_advance(
                    cid, level, idx,
                    name=raw_name, cost=sk.get("points"), kind="Умение",
                    ref_id=sid, text=s(sk.get("requirements")),
                    spec=s(sk.get("specialization")) or None, step=step,
                ))
                idx += 1

            # ── таланты (talent_details несёт цены) ──
            td = {s(t.get("canonical_id") or t.get("id")): t
                  for t in (val.get("talent_details") or []) if isinstance(t, dict)}
            for tid in val.get("talents", []) or []:
                t = td.get(s(tid)) if isinstance(td, dict) else None
                t = t if isinstance(t, dict) else {"id": tid}
                advances.append(_mk_advance(
                    cid, level, idx,
                    name=s(t.get("name")) or s(tid), cost=t.get("points"), kind="Талант",
                    ref_id=resolve_talent_id(s(t.get("name")) or s(tid), t.get("canonical_id") or t.get("id")),
                    text=s(t.get("requirements")), times=t.get("times"),
                    spec=s(t.get("specialization")) or None,
                ))
                idx += 1

            # ── таблица улучшений (альтернативные ранги негодяя) ──
            pkg = (val.get("advance_package") or val.get("upgrades")
                   or c.get("advance_package") or [])
            for row in pkg:
                if not isinstance(row, dict):
                    continue
                nm = s(row.get("improvement")) or s(row.get("name"))
                base_name, step = _skill_step_from_name(nm)
                kind = s(row.get("type")) or "Прочее"
                ref = None
                if kind.startswith("Умение"):
                    ref = slug(str(row.get("canonical_id") or base_name), "skill")
                    ref = resolve_skill_id(base_name, row.get("canonical_id") or None)
                    step = step or 1
                elif kind.startswith("Талант"):
                    ref = resolve_talent_id(base_name, row.get("canonical_id") or None)
                times_m = re.search(r"[×x]\s*(\d)", nm)
                advances.append(_mk_advance(
                    cid, level, idx, name=nm, cost=row.get("cost"), kind=kind,
                    ref_id=ref, text=s(row.get("requirements")),
                    times=times_m.group(1) if times_m else None,
                    step=step,
                ))
                idx += 1

            for other in (val.get("other") or []):
                advances.append(_mk_advance(
                    cid, level, idx, name=s(other), cost=other.get("cost") if isinstance(other, dict) else None,
                    kind="Прочее", ref_id=None, text=""))
                idx += 1

            (mn, mx) = XP_RANGES.get(level, (0, 0))
            ranks.append({
                "level": level,
                "name": s(val.get("name")),
                "xpMin": mn,
                "xpMax": mx,
                "advances": advances,
                "requirements": "\n".join(sl(val.get("requirements"))),
                "notes": "\n".join(sl(val.get("notes"))) or s(val.get("notes")),
                "alternate": b(val.get("alternate")) or source == "darkmarkets",
            })
            bump("ranks")

        # ── таблица улучшений характеристик ──
        char_adv: dict[str, list[int]] = {}
        ca = c.get("characteristic_advancement") or {}
        if isinstance(ca, dict):
            for key, levels in ca.items():
                if not isinstance(levels, dict):
                    continue
                cid2 = norm_char(key)
                if not cid2 or cid2 not in CHARACTERISTICS:
                    continue
                row = []
                for lv in ADV_LEVELS:
                    hit = None
                    for k, v in levels.items():
                        if str(k).strip().lower().startswith(lv[:6]):
                            hit = i(v); break
                    row.append(hit if hit is not None else 0)
                char_adv[cid2] = row

        # Требования к рангу: по ним видно, что это альтернативный ранг
        # («Заменяет ранг: ранг 2 или выше»), а не отдельный род занятий,
        # и на каком основном роде занятий он берётся.
        req_text = "\n".join(sl(c.get("rank_requirements") or c.get("requirements")))
        is_alt = bool(c.get("alternate_rank")) or c.get("kind") == "alternate_rank" \
            or "заменяет ранг" in req_text.lower()
        base_req = s(c.get("required_career"))
        if not base_req:
            m = re.search(r"Требуемый род занятий:\s*([^\n]+)", req_text, re.IGNORECASE)
            if m:
                base_req = m.group(1).strip()

        rec = {
            "id": cid,
            "name": s(c.get("name")),
            "source": s(c.get("source")) or source,
            "startingRank": s(c.get("starting_rank")) or (ranks[0]["name"] if ranks else ""),
            "description": s(c.get("overview")),
            "rankRequirements": "\n".join(sl(c.get("rank_requirements") or c.get("requirements"))),
            "startingEquipment": sl(c.get("starting_equipment")),
            # У части родов занятий («Сквозь шторм») умения и таланты
            # перечислены названиями, а не id — приводим к id паке.
            "startingSkills": [resolve_skill_id(_plain(x), None) for x in sl(c.get("starting_skills"))],
            "startingTalents": [resolve_talent_id(_plain(x), None) for x in sl(c.get("starting_talents"))],
            "startingFeatures": sl(c.get("starting_features")),
            "shipUpgrade": s(c.get("ship_upgrade")) or None,
            "shipHullPoints": i(c.get("ship_hull_points")) or None,
            "ranks": ranks,
            "characteristicAdvancement": char_adv,
            "advancementLevelNames": ["Простой", "Средний", "Отточенный", "Предельный"],
            "startingSkillsVerbatim": s(c.get("starting_skills_verbatim")) or None,
            "startingTalentsVerbatim": s(c.get("starting_talents_verbatim")) or None,
            # Альтернативный ранг («Генетор», «Механьяк» из «Сквозь шторм»,
            # «Архиеретех» из «Чёрных рынков») — это тот же род занятий с
            # заменой ранга, поэтому начальные умения, таланты и снаряжение
            # берутся у основного рода занятий.
            "baseCareer": base_req,
        }
        if is_alt:
            rec["alternateRanks"] = True
        if c.get("epigraph"):
            epi = c["epigraph"]
            rec["epigraph"] = s(epi.get("text")) if isinstance(epi, dict) else s(epi)
        if c.get("special_abilities"):
            rec["specialAbilities"] = [
                {"id": slug(str(a.get("name", "")), "ability"), "name": s(a.get("name")),
                 "description": "", "effect": s(a.get("text"))}
                for a in c["special_abilities"] if isinstance(a, dict)
            ]
        out.append(rec)
        bump("careers")
    return out


def _camel(k: str) -> str:
    parts = k.split("_")
    return parts[0] + "".join(p.title() for p in parts[1:])


CATEGORY_MAP = {
    "ranged_weapon": "ranged_weapon", "launcher": "launcher", "grenade_rocket": "grenade_rocket",
    "grenade": "grenade_rocket", "rocket": "grenade_rocket", "melee_weapon": "melee_weapon",
    "chain_weapon": "chain_weapon", "power_weapon": "power_weapon", "force_weapon": "force_weapon",
    "exotic_melee": "melee_weapon", "armor": "armor", "armour": "armor", "clothing": "clothing",
    "consumable": "consumable", "tool": "tool", "augmetic": "augmetic", "cybernetics": "cybernetics",
    "general_gear": "general_gear", "ship_weapon": "ship_weapon", "ship_general": "ship_general",
    "drug": "drug", "ammunition": "ammo", "ammo": "ammo", "personal": "personal",
    "armor_upgrade": "armor_upgrade", "weapon_upgrade": "weapon_upgrade",
}


def norm_items(raw: dict, used: set[str], source: str) -> tuple[list[dict], list[dict], list[dict]]:
    items, augmetics, drugs = [], [], []
    for it in raw.get("items", []) or []:
        iid = slug(str(it.get("id") or it.get("name", "")), "item")
        iid = unique_id(iid, used)
        traits = sl(it.get("traits"))
        rec = {
            "id": iid,
            "name": s(it.get("name")),
            "category": CATEGORY_MAP.get(s(it.get("category")), s(it.get("category")) or "general_gear"),
            "weaponClass": s(it.get("weapon_class")),
            "cost": s(it.get("cost")),
            "availability": s(it.get("availability")),
            "encumbrance": i(it.get("encumbrance")),
            "protection": i(it.get("protection")),
            "melee": i(it.get("melee")),
            "range": s(it.get("range")),
            "strengthBonus": i(it.get("strength_bonus")),
            "traits": traits,
            "specialRules": sl(it.get("special_rules")),
            "description": s(it.get("description")),
            "source": s(it.get("source")) or source,
            "isUpgrade": "улучшен" in s(it.get("name")).lower(),
        }
        for k in ("damage", "penetration", "magazine", "reload", "weight", "accuracy", "fire_mode", "_note"):
            if it.get(k) not in (None, ""):
                rec[_camel(k)] = it[k]
        items.append(rec)
        bump("items")

    for a in raw.get("augmetics", []) or []:
        aid = slug(str(a.get("id") or a.get("name", "")), "augm")
        augmetics.append({
            "id": unique_id(aid, used), "name": s(a.get("name")), "cost": s(a.get("cost")),
            "availability": s(a.get("availability")), "location": s(a.get("location")),
            "effect": s(a.get("effect")), "description": s(a.get("description")),
            "source": source,
        })
        bump("augmetics")
    for d in raw.get("drugs_and_poisons", []) or raw.get("drugs", []) or []:
        did = slug(str(d.get("id") or d.get("name", "")), "drug")
        drugs.append({
            "id": unique_id(did, used), "name": s(d.get("name")), "cost": s(d.get("cost")),
            "effect": s(d.get("effect")), "description": s(d.get("description")), "source": source,
        })
        bump("drugs")
    return items, augmetics, drugs


def norm_options(raw: dict, key: str, used: set[str], source: str,
                 id_prefix: str) -> list[dict]:
    out = []
    src_list = raw.get(key)
    if isinstance(src_list, dict):
        src_list = (src_list.get("ranks") or src_list.get("options")
                    or src_list.get("packages") or src_list.get("benefits")
                    or src_list.get("options_list"))
    for o in src_list or []:
        if not isinstance(o, dict):
            o = {"name": s(o)}
        oid = slug(str(o.get("id") or o.get("name", "")), id_prefix)
        grants = []
        for g in o.get("grants", []) or []:
            if isinstance(g, dict):
                grants.append(f"{s(g.get('name'))}: {s(g.get('effect'))}".strip(": ").strip())
            else:
                grants.append(s(g))
        # Бонусы раздела могут лежать отдельным списком «effects» — как у
        # «Испытаний и страданий» в базовой книге. Это не бонусы, а варианты
        # выбора («выберите один из приведённых ниже вариантов»), поэтому их
        # показываем отдельным списком, а не как награды.
        choices = []
        for list_key in ("options", "effects"):
            for e in o.get(list_key, []) or []:
                if isinstance(e, dict):
                    nm = s(e.get("name"))
                    ef = s(e.get("effect")) or s(e.get("description"))
                    if not nm and not ef:
                        continue
                    choices.append({"name": nm, "effect": ef})
                elif e:
                    choices.append({"name": "", "effect": s(e)})
        # Варианты раздела («Сквозь шторм»): свой текст, своя цена и свои бонусы.
        variants = []
        for v in o.get("variants", []) or []:
            if not isinstance(v, dict):
                continue
            variants.append({
                "id": slug(str(v.get("id") or v.get("name", "")), "variant"),
                "name": s(v.get("name")),
                "costXp": i(v.get("cost_xp")),
                "description": s(v.get("description")),
                "effect": s(v.get("effect")),
            })
        # Варианты выбора из базовой книги («Путь в пустоту», «Испытания и
        # страдания»): «выберите один из приведённых ниже вариантов».
        for c in choices:
            variants.append({
                "id": slug(c["name"] or c["effect"][:40], "variant"),
                "name": c["name"],
                "costXp": 0,
                "description": "",
                "effect": c["effect"],
            })
        # Начальные ранения и очки судьбы по варианту пути: у родных миров
        # это «2 × бонус Выносливости + 1к5+1» и бросок 1к10 (базовая книга,
        # «Сквозь шторм»). Текст правила показываем игроку в списке бонусов.
        for key in ("starting_ranks", "starting_fate_points"):
            t = s(o.get(key))
            if t:
                grants.append(t)
        out.append({
            "id": unique_id(oid, used),
            "name": s(o.get("name")),
            "description": s(o.get("description") or o.get("effect")),
            "effect": s(o.get("effect")),
            "prerequisite": s(o.get("prerequisite")),
            "requirements": s(o.get("requirements")),
            "grants": grants,
            "variants": variants,
            "replaces": sl(o.get("replaces")),
            "startingWounds": norm_start_wounds(o.get("starting_wounds")),
            "startingFate": norm_start_fate(o.get("starting_fate")),
            "source": s(o.get("source")) or source,
        })
        bump(id_prefix)
    return out


def norm_species(raw: dict, used: set[str], book: str) -> list[dict]:
    out = []
    for sp in raw.get("species", []) or []:
        sid = slug(str(sp.get("id") or sp.get("name", "")), "species")
        bonus = {}
        for k, v in (sp.get("characteristic_bonus") or {}).items():
            cid = norm_char(k)
            if cid and cid in CHARACTERISTICS:
                bonus[cid] = i(v)
        for k, v in (sp.get("bonus") or {}).items():
            cid = norm_char(k)
            if cid and cid in CHARACTERISTICS:
                bonus[cid] = bonus.get(cid, 0) + i(v)

        talents = [slug(str(t.get("id") or t.get("name", ""))) if isinstance(t, dict) else slug(str(t))
                   for t in (sp.get("talents") or [])]
        skills = [slug(str(x.get("id") or x.get("name", ""))) if isinstance(x, dict) else slug(str(x))
                  for x in (sp.get("skills") or [])]
        packages = []
        for p in sp.get("elite_advance_packages", []) or []:
            if isinstance(p, dict):
                packages.append({"name": s(p.get("name")), "effect": s(p.get("effect") or p.get("cost"))})
            elif p:
                packages.append({"name": s(p), "effect": ""})

        out.append({
            "id": unique_id(sid, used),
            "name": s(sp.get("name")),
            "book": s(sp.get("book")) or book,
            "lang": s(sp.get("lang")) or s(raw.get("lang")) or "ru",
            "description": s(sp.get("characteristics")),
            "bonus": bonus,
            "talents": [t for t in talents if t],
            "skills": [x for x in skills if x],
            "startingEquipment": sl(sp.get("starting_equipment")),
            "careers": sl(sp.get("careers")),
            "specialRules": sl(sp.get("special_rules")),
            "elitePackages": packages,
            "source": book,
        })
        if sp.get("notes"):
            out[-1]["notes"] = s(sp["notes"])
        bump("species")
    return out


def norm_tau(raw: dict, used: set[str]) -> tuple[list[dict], list[dict], list[dict], list[dict], dict]:
    castes = []
    for c in raw.get("castes", []) or []:
        cid = slug(str(c.get("id") or c.get("name", "")), "caste")
        chars = {}
        if isinstance(c.get("characteristics"), dict):
            for k, v in c["characteristics"].items():
                cc = norm_char(k)
                if cc:
                    chars[cc] = i(v)
        castes.append({
            "id": unique_id(cid, used),
            "name": s(c.get("name")),
            "description": s(c.get("description") or c.get("career_block")),
            "talents": [slug(str(x)) for x in (c.get("talents") or [])],
            "skills": [slug(str(x)) for x in (c.get("skills") or [])],
            "startingEquipment": sl(c.get("starting_equipment")),
            "specialRules": sl(c.get("special_rules")),
            "source": "tau",
        })
        if chars:
            castes[-1]["_characteristics"] = chars
        for k in ("ranks", "speed", "starting_weapons", "starting_armour", "notes", "roleplaying"):
            if c.get(k):
                castes[-1][_camel(k)] = c[k]
        bump("castes")

    talents = norm_talents({"talents": raw.get("talents", []) or []}, used, "tau")
    skills = norm_skills({"skills": raw.get("skills", []) or []}, used)
    suits = []
    for b in raw.get("combat_suits", []) or []:
        suits.append({
            "id": unique_id(slug(str(b.get("id") or b.get("name", "")), "suit"), used),
            "name": s(b.get("name")),
            "characteristics": s(b.get("characteristics")),
            "protection": s(b.get("protection") or b.get("hardpoints")),
            "description": s(b.get("description")),
            "source": "tau",
        })
        bump("combat_suits")

    rules = {}
    for k in ("talents_intro", "skills_intro", "battlesuits_intro", "combat_suits_intro"):
        if raw.get(k):
            rules[f"tau_{k}"] = s(raw[k])
    if raw.get("progression"):
        rules["tau_progression"] = json.dumps(raw["progression"], ensure_ascii=False)
    if raw.get("traits"):
        rules["tau_traits"] = json.dumps(raw["traits"], ensure_ascii=False)
    return castes, talents, skills, suits, rules


# ────────────────────────────── сборка ──────────────────────────────

def load_raw(raw_dir: str, name: str) -> dict | None:
    p = os.path.join(raw_dir, name)
    if not os.path.exists(p):
        warn(f"Нет файла {name} — раздел пропущен.")
        return None
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def dump(path: str, obj: dict) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1)
    print(f"  → {os.path.basename(path)}  ({os.path.getsize(path)//1024} КБ)")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw", default="tools/raw")
    ap.add_argument("--out", default="data/builtin")
    args = ap.parse_args()

    raw, out = args.raw, args.out
    print(f"Сырьё : {raw}")
    print(f"Выход : {out}\n")

    used = set()
    creation = load_raw(raw, "creation.json") or {}
    skills_raw = load_raw(raw, "skills.json") or {}
    talents_raw = load_raw(raw, "talents_core.json") or {}
    careers_raw = load_raw(raw, "careers_core.json") or {}
    equip_raw = load_raw(raw, "equipment_core.json") or {}
    prog_raw = load_raw(raw, "progression.json") or {}
    abh_raw = load_raw(raw, "species_abhuman.json") or {}
    tau_raw = load_raw(raw, "species_tau.json") or {}
    st_cre = load_raw(raw, "stormstout_creation.json") or {}
    st_car = load_raw(raw, "stormstout_careers.json") or {}
    st_eq = load_raw(raw, "stormstout_equipment.json") or {}
    dm_car = load_raw(raw, "darkmarkets_careers.json") or {}
    dm_eq = load_raw(raw, "darkmarkets_equipment.json") or {}
    dm_npc = load_raw(raw, "darkmarkets_npcs.json") or {}

    build_indexes(skills_raw, talents_raw)

    # ── 01 характеристики ──
    print("[01] характеристики")
    chars = []
    src_chars = creation.get("characteristics", []) or []
    by_name = {str(c.get("name", "")).strip().lower(): c for c in src_chars}
    for cid, (name, abbrev, col) in CHARACTERISTICS.items():
        sc = by_name.get(name.lower(), {})
        chars.append({
            "id": cid, "name": name, "abbrev": abbrev, "column": col,
            "description": s(sc.get("description")),
        })
    note = s(creation.get("characteristics_note"))
    for c in chars:
        if note:
            c["description"] = (c["description"] + "\n\n" + note).strip()
    dump(os.path.join(out, "01-characteristics.json"), {"characteristics": chars, "order": list(CHARACTERISTICS)})
    bump("characteristics", len(chars))

    # ── 02 умения ──
    print("[02] умения")
    skills = norm_skills(skills_raw, used)
    dump(os.path.join(out, "02-skills.json"), {"skills": skills})

    # ── 03 таланты ──
    print("[03] таланты")
    talents = norm_talents(talents_raw, used, "core")
    if dm_car:
        talents += norm_talents({"talents": dm_car.get("talents", []) or []}, used, "darkmarkets")
    if st_cre:
        talents += norm_talents({"talents": st_cre.get("new_talents_and_features", []) or []}, used, "stormstout")
    if dm_car:
        nt = []
        for t in dm_car.get("new_talents", []) or []:
            if isinstance(t, dict):
                nt.append({
                    "id": t.get("id") or t.get("name"), "name": t.get("name"),
                    "ranks": t.get("ranks", 1), "description": t.get("description"),
                    "effects": [{"trigger": "passive", "text": t.get("description", "")}],
                    "requirement": t.get("requirement"),
                })
        if nt:
            talents += norm_talents({"talents": nt}, used, "darkmarkets")
    dump(os.path.join(out, "03-talents.json"), {"talents": talents})

    # ── 04 профессии ──
    print("[04] роды занятий")
    careers = norm_careers(careers_raw, used, "core")
    if dm_car:
        careers += norm_careers(dm_car, used, "darkmarkets")
    if st_car:
        careers += norm_careers(st_car, used, "stormstout")

    # Альтернативные ранги («Сквозь шторм») заменяют ранг в обычном роде
    # занятий, поэтому начальные умения, таланты и снаряжение у них — от
    # основного рода занятий («Генетор» — это «Эксплоратор» и т. п.).
    by_key: dict[str, dict] = {}
    for rec in careers:
        for k in (str(rec.get("id", "")), str(rec.get("name", ""))):
            if k:
                by_key[k.strip().lower()] = rec
    inherited = 0
    for rec in careers:
        base_key = str(rec.pop("baseCareer", "") or "").strip().lower()
        if base_key.startswith("любой"):
            # «Аугмент», «Ксенограф», «Архиеретех» — подходит любой род занятий,
            # кроме перечисленных в требованиях.
            head = base_key.split("(")[0]
            text = re.sub(r"[^0-9a-zа-я]+", "", head)
            excluded = set()
            for c in careers:
                if c.get("alternateRanks"):
                    continue
                st = re.sub(r"[^0-9a-zа-я]+", "", str(c.get("name", "")).lower())[:3]
                if len(st) >= 3 and st in text:
                    excluded.add(c["id"])
            rec["anyBaseCareer"] = True
            rec["excludedBaseCareers"] = sorted(excluded)
            rec["humanBaseCareerOnly"] = "человеческ" in text
            continue
        if not base_key:
            continue
        base = by_key.get(base_key)
        if base is None:
            continue
        for field in ("startingSkills", "startingTalents", "startingEquipment"):
            if not rec.get(field) and base.get(field):
                rec[field] = list(base[field])
        for field in ("startingSkillsVerbatim", "startingTalentsVerbatim"):
            if not rec.get(field) and base.get(field):
                rec[field] = base[field]
        inherited += 1
    if inherited:
        print(f"     альтернативных рангов с основой: {inherited}")
    dump(os.path.join(out, "04-careers.json"), {"careers": careers})

    # ── 05 предметы ──
    print("[05] арсенал")
    items, augmetics, drugs = norm_items(equip_raw, used, "core")
    if dm_eq:
        it2, au2, dr2 = norm_items(dm_eq, used, "darkmarkets")
        items += it2; augmetics += au2; drugs += dr2
        for sh in dm_eq.get("ship_items", []) or []:
            items.append({
                "id": unique_id(slug(str(sh.get("id") or sh.get("name", "")), "ship"), used),
                "name": s(sh.get("name")), "category": "ship_general", "cost": s(sh.get("cost")),
                "availability": "", "encumbrance": 0, "protection": 0, "melee": 0,
                "range": "", "strengthBonus": 0, "traits": [], "specialRules": [],
                "description": s(sh.get("description") or sh.get("effect")), "source": "darkmarkets",
            })
            bump("items")
    if abh_raw.get("items"):
        # Предметы «Руководства по абхуманам»: эфирное оружие (таблица 3-2).
        it3, au3, dr3 = norm_items(abh_raw, used, "abhuman")
        items += it3; augmetics += au3; drugs += dr3
    if st_eq:
        # Предметы «Сквозь шторм» собираются отдельным файлом
        # (14-storm-items.json, tools/build_storm_items.py): там же предметы
        # получают префикс storm_ там, где id совпадает с базовой книгой.
        # Если слить их ещё и сюда, оружие покажется в арсенале дважды.
        bump("storm_items", len(st_eq.get("items", []) or []))
    dump(os.path.join(out, "05-items.json"), {"items": items, "augmetics": augmetics, "drugs": drugs})
    # ── 06 создание персонажа ──
    print("[06] создание персонажа")
    rules: dict[str, str] = {}

    def put_rules(prefix: str, src: dict):
        for k, v in (src or {}).items():
            if isinstance(v, str) and v.strip():
                rules[f"{prefix}_{k}"] = v
            elif isinstance(v, (dict, list)) and v:
                rules[f"{prefix}_{k}"] = json.dumps(v, ensure_ascii=False)

    put_rules("creation", creation)
    put_rules("skills", skills_raw.get("rules_text"))
    put_rules("talents", talents_raw.get("rules_text"))
    put_rules("progression", {k: v for k, v in (prog_raw.get("xp") or {}).items() if isinstance(v, str)})
    if dm_car and dm_car.get("overview"):
        rules["creation_nemesis_play"] = s(dm_car["overview"])

    creation_out = {
        "homeworlds": norm_options(creation, "homeworlds", used, "core", "homeworld")
        + norm_options(st_cre, "homeworlds", used, "stormstout", "homeworld"),
        "destiny": norm_options(creation, "foretold_destiny", used, "core", "destiny")
        + norm_options(st_cre, "foretold_destiny", used, "stormstout", "destiny"),
        "voidPath": norm_options(creation, "void_path", used, "core", "voidpath")
        + norm_options(st_cre, "void_path", used, "stormstout", "voidpath"),
        "hardship": norm_options(creation, "hardship", used, "core", "hardship")
        + norm_options(st_cre, "hardship", used, "stormstout", "hardship"),
        "aspiration": norm_options(creation, "aspiration", used, "core", "aspiration")
        + norm_options(st_cre, "aspiration", used, "stormstout", "aspiration"),
        "legacy": norm_options(st_cre, "legacy", used, "stormstout", "legacy"),
        "inclinations": norm_options(tau_raw, "inclinations", used, "tau", "inclination"),
        "ascension": norm_options(creation, "ascension", used, "core", "ascension"),
        "elitePackages": norm_options(abh_raw, "elite_advance_packages", used, "abhuman", "elite")
        + norm_options(st_car, "elite_advance_packages", used, "stormstout", "elite"),
        "traits": norm_options(abh_raw, "elite_advance_packages", used, "abhuman", "trait"),
        "rules": {},
    }

    # ── 07 прокачка ──
    print("[07] прокачка")
    xp = prog_raw.get("xp", {}) or {}
    wounds = []
    for w in prog_raw.get("wounds", []) or []:
        if not isinstance(w, dict):
            continue
        wid = slug(str(w.get("id") or w.get("name", "")), "wound")
        wounds.append({
            "id": unique_id(wid, used), "name": s(w.get("name")),
            "effect": s(w.get("effect")), "healing": s(w.get("healing") or w.get("recovery")),
        })
        bump("wounds")

    prog_out = {
        "wounds": wounds,
        "incomeRanks": [],
        "advancementCosts": [100, 200, 300, 400, 500],
        "careerRankCosts": [500, 1000],
        "talentRankCosts": [100, 200, 300],
        "skillCosts": [100, 200],
        "xpPerSession": 500,
        "movementBySize": {"3": 6, "4": 9, "5": 12, "6": 18},
        "rules": {},
    }
    if isinstance(xp.get("xp_thresholds"), list):
        prog_out["rules"]["xp_thresholds"] = json.dumps(xp["xp_thresholds"], ensure_ascii=False)
    for k in ("rules_text", "spending_rules", "model", "starting_xp",
              "characteristic_upgrades", "elite_advances", "skills_acquisition",
              "talents_acquisition", "dark_heresy_equivalence"):
        if xp.get(k):
            prog_out["rules"][f"xp_{k}"] = json.dumps(xp[k], ensure_ascii=False) \
                if not isinstance(xp[k], str) else xp[k]
    for key, label in (("force_points", "psychic"), ("other_progression", "extra")):
        if prog_raw.get(key):
            prog_out["rules"][label] = json.dumps(prog_raw[key], ensure_ascii=False)
    if dm_npc:
        prog_out["rules"]["nemesis"] = json.dumps(dm_npc.get("nemesis", {}), ensure_ascii=False)
    inc = creation.get("income_rank_and_ship_points", {}) or {}
    for row in inc.get("table", []) or []:
        if isinstance(row, dict):
            prog_out["incomeRanks"].append({
                "rank": i(row.get("rank"), len(prog_out["incomeRanks"]) + 1),
                "name": s(row.get("name")), "income": s(row.get("income")),
                "shipPoints": s(row.get("ship_points")), "perCrew": s(row.get("per_crew")),
                "notes": s(row.get("notes")),
            })
    dump(os.path.join(out, "07-progression.json"), prog_out)

    # ── 08 виды ──
    print("[08] виды и подвиды")
    species = norm_species(abh_raw, used, "abhuman")
    if st_car:
        species += norm_species(st_car, used, "stormstout")
    if not any(sp["id"] == "human" for sp in species):
        species.insert(0, {
            "id": "human", "name": "Человек", "book": "core", "lang": "ru",
            "description": "Обычный человек Империума.", "bonus": {},
            "talents": [], "skills": [], "startingEquipment": [], "careers": [],
            "specialRules": [], "elitePackages": [], "source": "core",
        })
    # 08-species.json собирает build_species_traits.py: он дописывает
    # русские названия, размеры, черты и базовые характеристики видов.
    # Здесь только каркас — иначе пересборка стирала бы эти данные.
    # dump(os.path.join(out, "08-species.json"), {"species": species})

    # ── 09 тау (вычисляем заранее, чтобы правила тау попали в 06) ──
    print("[09] тау")
    castes, tau_talents, tau_skills, suits, tau_rules = norm_tau(tau_raw, used)
    for k, v in tau_rules.items():
        rules.setdefault(k, v)
    creation_out["rules"] = rules
    dump(os.path.join(out, "06-creation.json"), creation_out)
    dump(os.path.join(out, "09-tau.json"), {
        "castes": castes, "combatSuits": suits,
        "talents": tau_talents, "skills": tau_skills, "rules": {},
    })

    # ── отчёт ──
    print("\n" + "─" * 60)
    print("Итого записей:")
    for k in sorted(STATS):
        print(f"  {k:16} {STATS[k]}")
    if WARNINGS:
        print(f"\nПредупреждений: {len(WARNINGS)}")
        for w in WARNINGS[:20]:
            print("  !", w)

    with open(os.path.join(out, "_build_report.json"), "w", encoding="utf-8") as f:
        json.dump({"stats": STATS, "warnings": WARNINGS}, f, ensure_ascii=False, indent=1)

    return 0


if __name__ == "__main__":
    sys.exit(main())