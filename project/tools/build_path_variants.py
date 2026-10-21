# -*- coding: utf-8 -*-
"""Дополняет 06-creation.json вариантами и эффектами из сырых данных.

В «Сквозь шторм» у судьбы, пути в пустоту, испытания, устремления и
наследия есть варианты (по три на каждый раздел): свой текст, своя цена
в очках опыта и свои бонусы. В базовой книге у «испытаний» эффекты лежат
отдельным списком. При сборке пака эти данные терялись — скрипт их
возвращает.
"""
import json, os, re, sys

sys.stdout.reconfigure(encoding="utf-8")

RAW = r"D:\RogueTraderBuilder\raw\raw"
PACK = r"D:\RogueTraderBuilder\project\data\builtin\06-creation.json"

# Раздел пака → ключ в сырых файлах.
SECTIONS = [
    ("homeworlds", ["homeworlds"]),
    ("destiny", ["foretold_destiny"]),
    ("voidPath", ["void_path"]),
    ("hardship", ["hardship"]),
    ("aspiration", ["aspiration"]),
    ("legacy", ["legacy"]),
]


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def slug(text, prefix):
    t = re.sub(r"[^a-zA-Z0-9]+", "_", str(text or "").strip().lower()).strip("_")
    return t or prefix


def collect(raw_files, key):
    """Все записи раздела из всех сырых файлов: по id и по имени."""
    out = {}
    for f in raw_files:
        data = load(os.path.join(RAW, f))
        for o in data.get(key, []) or []:
            if not isinstance(o, dict):
                continue
            out[slug(o.get("id") or o.get("name"), "opt")] = o
            out[slug(o.get("name"), "opt")] = o
    return out


def effect_lines(o):
    """Список бонусов: «Название: текст» — как в книге."""
    lines = []
    for e in o.get("effects", []) or []:
        if isinstance(e, dict):
            name = str(e.get("name", "")).strip()
            txt = str(e.get("effect", "")).strip()
            lines.append(f"{name}: {txt}".strip(": ").strip() if name else txt)
        elif e:
            lines.append(str(e))
    return lines


def variant_lines(o):
    out = []
    for v in o.get("variants", []) or []:
        if not isinstance(v, dict):
            continue
        out.append({
            "id": slug(v.get("id") or v.get("name"), "variant"),
            "name": str(v.get("name", "")).strip(),
            "costXp": int(v.get("cost_xp") or 0),
            "description": str(v.get("description", "") or "").strip(),
            "effect": str(v.get("effect", "") or "").strip(),
        })
    return out


def main():
    core = "creation.json"
    storm = "stormstout_creation.json"
    pack = load(PACK)
    changed = 0
    for section, keys in SECTIONS:
        raw_map = {}
        for f in (core, storm):
            for k in keys:
                raw_map.update(collect([f], k))
        for opt in pack.get(section, []) or []:
            raw = raw_map.get(slug(opt.get("id"), "opt")) or raw_map.get(slug(opt.get("name"), "opt"))
            if raw is None:
                continue
            # Бонусы раздела «Испытания и страдания» — отдельным списком в книге.
            if not opt.get("grants"):
                lines = effect_lines(raw)
                if lines:
                    opt["grants"] = lines
                    changed += 1
            # Варианты из «Сквозь шторм».
            if not opt.get("variants"):
                vs = variant_lines(raw)
                if vs:
                    opt["variants"] = vs
                    changed += 1
    with open(PACK, "w", encoding="utf-8", newline="\n") as f:
        json.dump(pack, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("обновлено записей:", changed)


if __name__ == "__main__":
    main()
