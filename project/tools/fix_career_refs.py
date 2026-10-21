# -*- coding: utf-8 -*-
"""Проставляет в родах занятий id умений и талантов вместо названий.

В «Сквозь шторм» часть родов занятий (круут-наёмник, орксар) перечисляет
стартовые умения и таланты словами, а не id: «Бдительность (Вос)»,
«Владение лёгким оружием (Всё)». Название приводится к тому, что есть
в паке: сначала убираем характеристику в скобках, затем ищем по названию.
"""
import json, os, re, sys

sys.stdout.reconfigure(encoding="utf-8")

PK = r"D:\RogueTraderBuilder\project\data\builtin"


def norm(t):
    t = (t or "").lower().replace("ё", "е")
    t = re.sub(r"[«»\"']", "", t)
    t = re.sub(r"\s+", " ", t).strip(" .,;:")
    return t


def index(items):
    out = {}
    for it in items:
        for key in (it.get("name"), it.get("nameEn"), it.get("id")):
            k = norm(key)
            if k:
                out.setdefault(k, it["id"])
    return out


def resolve(name, idx):
    """Ищет по названию; убирает скобки с характеристикой, пока не найдёт."""
    raw = (name or "").strip()
    if not raw:
        return None
    direct = idx.get(norm(raw))
    if direct:
        return direct
    # «Языкознание (Круутский, Низкий готический, Язык тау)» — в книге это
    # перечисление специализаций; пробуем каждую по отдельности.
    m = re.search(r"^(.*?)\s*\(([^()]*)\)\s*$", raw)
    if m:
        base, group = m.group(1), m.group(2)
        for part in [p.strip() for p in group.split(",")]:
            hit = idx.get(norm(f"{base} ({part})"))
            if hit:
                return hit
    work = raw
    while True:
        cut = work.rfind("(")
        if cut <= 0:
            break
        work = work[:cut].strip()
        hit = idx.get(norm(work))
        if hit:
            return hit
    return None


def main():
    skills = json.load(open(os.path.join(PK, "02-skills.json"), encoding="utf-8"))["skills"]
    talents = json.load(open(os.path.join(PK, "03-talents.json"), encoding="utf-8"))["talents"]
    # Дополнения: таланты и умения из «Сквозь шторм» и «Руководства по Тау».
    extra_path = os.path.join(PK, "13-extra-skills.json")
    if os.path.exists(extra_path):
        extra = json.load(open(extra_path, encoding="utf-8"))
        skills += extra.get("skills", [])
        talents += extra.get("talents", [])
    sidx, tidx = index(skills), index(talents)

    path = os.path.join(PK, "04-careers.json")
    pack = json.load(open(path, encoding="utf-8"))
    fixed_s = fixed_t = missed_s = missed_t = 0
    for c in pack.get("careers", []):
        new_sk = []
        for x in c.get("startingSkills") or []:
            if x in sidx.values() or x in tidx.values():
                new_sk.append(x)
                continue
            hit = resolve(x, sidx)
            if hit:
                new_sk.append(hit)
                fixed_s += 1
            else:
                new_sk.append(x)
                missed_s += 1
        new_t = []
        for x in c.get("startingTalents") or []:
            if x in tidx.values() or x in sidx.values():
                new_t.append(x)
                continue
            hit = resolve(x, tidx)
            if hit:
                new_t.append(hit)
                fixed_t += 1
            else:
                new_t.append(x)
                missed_t += 1
        c["startingSkills"] = new_sk
        c["startingTalents"] = new_t

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(pack, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"исправлено: умений {fixed_s}, талантов {fixed_t}; не найдено: умений {missed_s}, талантов {missed_t}")
    for c in pack.get("careers", []):
        for x in (c.get("startingSkills") or []) + (c.get("startingTalents") or []):
            if x not in sidx.values() and x not in tidx.values():
                print("   не найдено:", c["id"], "→", x)


if __name__ == "__main__":
    main()
