# -*- coding: utf-8 -*-
"""
Сопоставляет блоки «Изначальное количество ран / очков судьбы» из книг
с вариантами жизненного пути в паке (по строке «Изменения характеристик»)
и вносит формулы в 06-creation.json.
"""
import io, json, os, re, sys
sys.stdout.reconfigure(encoding="utf-8")

BOOKS = {
    "core": r"C:\Users\shist\AppData\Local\Temp\opencode\rt_extract\books\core.txt",
    "stormstout": r"C:\Users\shist\AppData\Local\Temp\opencode\rt_extract\books\stormstout.txt",
}
D = r"D:\RogueTraderBuilder\project\data\builtin"
WORDS = {"одно": 1, "один": 1, "два": 2, "две": 2, "три": 3, "четыре": 4, "пять": 5}


def norm(s):
    return re.sub(r"[^0-9a-zA-Zа-яА-ЯёЁ]+", " ", s.lower()).strip()


def parse_wounds(text):
    """«… равно удвоенному бонусу Выносливости плюс 1к5+1» → (const, dice)."""
    m = re.search(r"1к5\s*([+-]\s*(\d+))?", text)
    if not m:
        return None
    const = int(m.group(2)) if m.group(2) else 0
    return const, 5


def parse_fate(text):
    """«На 1-5 он получает два очка судьбы, на 6-10 – три» → диапазоны."""
    ranges = []
    for m in re.finditer(r"на\s+(\d+)\s*[–\-—]\s*(\d+)([^,;]*)", text, re.IGNORECASE):
        frm, to, tail = int(m.group(1)), int(m.group(2)), m.group(3)
        pts = None
        for wm in re.finditer(r"\b(одно|один|два|две|три|четыре|пять)\b", tail, re.IGNORECASE):
            pts = WORDS[wm.group(1).lower()]
            break
        if pts is not None:
            ranges.append({"from": frm, "to": to, "points": pts})
    return ranges or None


def head_of_block(lines, idx):
    """Заголовок блока: ближайшая строка-с Name: (без «Изменения…» и «Начальные…»)."""
    for j in range(idx - 1, max(0, idx - 60), -1):
        s = lines[j].strip()
        if not s or s.startswith("=") or s.startswith("Изменения") or s.startswith("Начальные"):
            continue
        if s.endswith(":") and len(s) < 60:
            return s[:-1]
    return "?"


# ── Собираем блоки из книг ───────────────────────────────────────────────
blocks = []
for book, path in BOOKS.items():
    lines = io.open(path, encoding="utf-8", errors="replace").read().splitlines()
    for i, l in enumerate(lines):
        if not l.strip().startswith("Изначальное количество ран"):
            continue
        # абзац целиком
        para = [l.strip()]
        j = i + 1
        while j < len(lines) and lines[j].strip():
            para.append(lines[j].strip())
            j += 1
        wounds_text = " ".join(para)
        # следующий абзац — очки судьбы
        fate_text = ""
        k = j
        while k < len(lines) and not lines[k].strip():
            k += 1
        if k < len(lines) and lines[k].strip().startswith("Изначальное количество очков судьбы"):
            fpara = [lines[k].strip()]
            m = k + 1
            while m < len(lines) and lines[m].strip():
                fpara.append(lines[m].strip())
                m += 1
            fate_text = " ".join(fpara)
        # характеристики и умения блока — ищем выше (абзац целиком: строка переносится)
        chars = ""
        skills = ""
        for j2 in range(i - 1, max(0, i - 120), -1):
            s = lines[j2].strip()
            if not chars and s.startswith("Изменения характеристик"):
                para2 = [s]
                m2 = j2 + 1
                while m2 < len(lines) and lines[m2].strip() and not lines[m2].strip().startswith("Начальные"):
                    para2.append(lines[m2].strip())
                    m2 += 1
                chars = " ".join(para2)
            if not skills and s.startswith("Начальные умения"):
                para2 = [s]
                m2 = j2 + 1
                while m2 < len(lines) and lines[m2].strip() and not lines[m2].strip().startswith("Изначальное"):
                    para2.append(lines[m2].strip())
                    m2 += 1
                skills = " ".join(para2)
            if chars and skills:
                break
        blocks.append({"book": book, "chars": chars, "skills": skills, "line": i,
                       "head": head_of_block(lines, i),
                       "wounds": wounds_text, "fate": fate_text})

print(f"блоков в книгах: {len(blocks)}")

# ── Сопоставляем с паке ──────────────────────────────────────────────────
p = os.path.join(D, "06-creation.json")
d = json.load(open(p, encoding="utf-8"))
opts = []
for key in ("homeworlds", "destiny", "voidPath", "hardship", "aspiration", "legacy"):
    for o in d.get(key, []):
        opts.append((key, o))

matched = 0
used = []
for key, o in opts:
    grants = o.get("grants") or []
    chars = ""
    for g in grants:
        if g.strip().startswith("Изменения характеристик"):
            chars = g.strip()
            break
    if not chars:
        continue
    # ищем блок с тем же текстом характеристик (надёжный признак)
    nc = norm(chars)
    kind = "точное"
    cands = [b for b in blocks if b["chars"] and norm(b["chars"]) == nc]
    if not cands:
        kind = "вхождение"
        cands = [b for b in blocks if b["chars"] and nc in norm(b["chars"])]
    if not cands:
        # Запасной путь: в книге блоки идут в том же порядке, что и виды
        # в паке, — берём оставшийся блок этой книги по порядку.
        book = o.get("source", "core")
        left = [b for b in blocks if b["book"] == book and b not in used]
        if left:
            cands = [left[0]]
            kind = "по порядку"
        else:
            print(f"  ✗ {o['id']}: не нашёл блок в книге")
            continue
    b = cands[0]
    used.append(b)
    # Абзац про очки судьбы может идти сразу за ранением — ищем в обоих.
    w = parse_wounds(b["wounds"])
    f = parse_fate(b["fate"]) or parse_fate(b["wounds"])
    if w is None and f is None:
        print(f"  · {o['id']}: блок найден, но формулы не разобрались")
        continue
    if w:
        o["startingWounds"] = {
            "const": w[0], "dice": w[1], "useBonus": True, "unmodified": False,
            "text": "2 × бонус Выносливости + 1к5" + (f"+{w[0]}" if w[0] else ""),
        }
    if f:
        o["startingFate"] = {
            "ranges": f, "dice": 10,
            "text": "бросок 1к10: " + "; ".join(f"с {r['from']} по {r['to']} — {r['points']}" for r in f),
        }
    matched += 1
    print(f"  ✓ {o['id']:22} [{b['book']}:{b['line']} {kind}]  ранения: 1к5{'+' + str(w[0]) if w and w[0] else ''}  судьба: {f}")

with open(p, "w", encoding="utf-8", newline="\n") as fh:
    json.dump(d, fh, ensure_ascii=False, indent=2)
    fh.write("\n")
print("сопоставлено:", matched, "из", len(opts))
