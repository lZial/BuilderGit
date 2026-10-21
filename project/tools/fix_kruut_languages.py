# -*- coding: utf-8 -*-
"""Круут по «Сквозь шторм» знает три языка: круутский, низкий готический и язык тау.

В книге это одно умение с тремя специализациями: «Языкознание (Круутский,
Низкий готический, Язык тау) (Инт)». В паке специализации — отдельные
умения, поэтому перечисляем все три.
"""
import json, os, sys

sys.stdout.reconfigure(encoding="utf-8")

PATH = r"D:\RogueTraderBuilder\project\data\builtin\04-careers.json"

with open(PATH, encoding="utf-8") as f:
    pack = json.load(f)

for c in pack.get("careers", []):
    if c.get("id") != "kruut_mercenary":
        continue
    skills = c.get("startingSkills", [])
    if "speak_language" in skills and "speak_language_kruut" not in skills:
        i = skills.index("speak_language")
        skills[i:i + 1] = ["speak_language_kruut", "speak_language_low_gothic", "speak_language_tau"]
        print("исправлено:", c["name"], "→", skills)

with open(PATH, "w", encoding="utf-8", newline="\n") as f:
    json.dump(pack, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("готово")
