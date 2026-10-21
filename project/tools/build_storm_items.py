# -*- coding: utf-8 -*-
"""Разбор сырых данных «Сквозь шторм» → 14-storm-items.json."""
import json, os, re, sys
sys.stdout.reconfigure(encoding="utf-8")

RAW = r"D:\RogueTraderBuilder\raw\raw\stormstout_equipment.json"
OUT = r"D:\RogueTraderBuilder\project\data\builtin\14-storm-items.json"

d = json.load(open(RAW, encoding="utf-8"))

# Сырые категории → категории паке.
CATEGORY = {
    "ammunition": "ammo",
    "armor": "armor",
    "orc_armor": "armor",
    "chain_weapon": "chain_weapon",
    "clothing": "clothing",
    "kroot_gear": "clothing",
    "curiosity": "personal",
    "deflector_field": "personal",
    "drugs": "drug",
    "force_weapon": "melee_weapon",
    "general_gear": "general_gear",
    "grenade": "grenade_rocket",
    "orc_grenade": "grenade_rocket",
    "launcher": "launcher",
    "melee_weapon": "melee_weapon",
    "orc_melee_weapon": "melee_weapon",
    "kroot_weapon": "melee_weapon",
    "personal": "personal",
    "power_weapon": "power_weapon",
    "ranged_weapon": "ranged_weapon",
    "orc_ranged_weapon": "ranged_weapon",
    "weapon_upgrade": "weapon_upgrade",
}


def weight_parts(text):
    """«4,5 кг» → (4, «4,5 кг»)."""
    if not text or text in ("-", "—"):
        return 0, ""
    t = str(text).strip()
    m = re.match(r"^([0-9]+(?:[.,][0-9]+)?)", t)
    if not m:
        return 0, ""
    num = float(m.group(1).replace(",", "."))
    tail = t[m.end():].strip()
    if tail:
        return int(num), t
    return int(num), f"{int(num)} кг" if num == int(num) else f"{m.group(1).replace('.', ',')} кг"


def as_int(v):
    if v is None:
        return 0
    if isinstance(v, (int, float)):
        return int(v)
    m = re.search(r"-?\d+", str(v))
    return int(m.group(0)) if m else 0


def as_text(v):
    if v is None:
        return ""
    if isinstance(v, float) and v == int(v):
        return str(int(v))
    return str(v).strip()


items = []
core_ids = set()
core_path = r"D:\RogueTraderBuilder\project\data\builtin\05-items.json"
if os.path.exists(core_path):
    with open(core_path, encoding="utf-8") as f:
        core_ids = {i["id"] for i in json.load(f)["items"]}
        core_ids |= {"aug_" + a["id"] for a in json.load(open(core_path, encoding="utf-8")).get("augmetics", [])}

for i in d.get("items", []):
    cat = CATEGORY.get(i.get("category", ""), "general_gear")
    enc, weight = weight_parts(i.get("encumbrance"))
    item_id = i["id"]
    # У части предметов «Сквозь шторм» те же id, что в базовой книге,
    # но это другие вещи («Мушкетон» и «Мушкет»). Разводим их префиксом.
    if item_id in core_ids:
        item_id = "storm_" + item_id
    item = {
        "id": item_id,
        "name": i.get("name", ""),
        "category": cat,
        "weaponClass": i.get("weapon_class", "") or "",
        "cost": i.get("cost", "") or "",
        "availability": i.get("availability_ru", "") or "",
        "encumbrance": enc,
        "protection": as_int(i.get("protection")),
        "melee": as_int(i.get("melee")),
        "range": as_text(i.get("range")),
        "fireMode": as_text(i.get("rof")),
        "damage": as_text(i.get("damage")),
        "penetration": as_text(i.get("penetration")),
        "magazine": as_text(i.get("magazine")),
        "traits": list(i.get("traits") or []),
        "specialRules": list(i.get("special_rules_ru") or []),
        "description": i.get("description", "") or "",
        "source": "stormstout",
    }
    if weight:
        item["weight"] = weight
    if i.get("special_rules"):
        item["note"] = " ".join(str(x) for x in i["special_rules"])
    items.append(item)

# Приделы к орочьему оружию (гребёнки) — это тоже предметы.
for g in d.get("orc_greebles", []):
    enc, weight = weight_parts(g.get("weight"))
    items.append({
        "id": g["id"] if g["id"] not in core_ids else "storm_" + g["id"],
        "name": g.get("name", ""),
        "category": "weapon_upgrade",
        "weaponClass": "",
        "cost": "",
        "availability": g.get("availability_ru", "") or "",
        "encumbrance": enc,
        "protection": 0,
        "melee": 0,
        "range": "",
        "traits": [],
        "specialRules": [g.get("effect", "") or ""],
        "description": f"Приделка к орочьему оружию: {g.get('applies_to', '')}. {g.get('effect', '')}",
        "source": "stormstout",
    })

augmetics = []
for a in d.get("augmetics", []):
    augmetics.append({
        "id": a["id"],
        "name": a.get("name", ""),
        "cost": a.get("cost", "") or "",
        "availability": a.get("availability_ru", "") or "",
        "effect": a.get("effect", "") or "",
        "description": a.get("description", "") or "",
        "location": a.get("location", "") or "",
        "source": "stormstout",
    })

out = {"items": items, "augmetics": augmetics}

# Правила книги — сохраняем текстом, чтобы информация не потерялась.
cr = json.load(open(r"D:\RogueTraderBuilder\raw\raw\stormstout_creation.json", encoding="utf-8"))
ca = json.load(open(r"D:\RogueTraderBuilder\raw\raw\stormstout_careers.json", encoding="utf-8"))
rules = {}
xr = ca.get("xenos_rules") or {}
if xr.get("text"):
    rules["stormstout_xenos"] = xr["text"]
oq = d.get("orc_qualities") or {}
if oq.get("text"):
    rules["stormstout_orc_qualities"] = oq["text"]
for key, name in (("career_path_expansion", "stormstout_career_path_expansion"),
                   ("patent_and_dynasty", "stormstout_patent_and_dynasty")):
    v = cr.get(key) or {}
    if v.get("text"):
        rules[name] = v["text"]
for key, name in (("tools_intro", "stormstout_tools"), ("curiosities_intro", "stormstout_curiosities"),
                   ("orc_arsenal_intro", "stormstout_orc_arsenal"), ("kroot_arsenal_intro", "stormstout_kroot_arsenal"),
                   ("arms_manufacturers", "stormstout_arms_manufacturers"),
                   ("shield_fields_intro", "stormstout_shield_fields")):
    v = d.get(key)
    if isinstance(v, str) and v.strip():
        rules[name] = v
out["rules"] = rules

# Подраздел для орочьего и круутовского стрелкового оружия: в базовой книге
# у стрелкового оружия всегда есть класс, а здесь — нет.
out["itemGroups"] = [
    {
        "id": "ranged_xenos",
        "sectionId": "weapons",
        "title": "Прочее стрелковое оружие",
        "page": "114",
        "order": 19,
        "categories": ["ranged_weapon"],
        "weaponClasses": [],
    }
]

with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("записано:", OUT, "предметов:", len(items), "аугметики:", len(augmetics), "правил:", len(rules))
