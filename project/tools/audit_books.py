# -*- coding: utf-8 -*-
"""Аудит полноты книг: сверяет предметы пака с оригинальными PDF.

Для каждой книги:
  • выбирает из PDF строки таблиц арсенала и их названия;
  • проверяет, что каждое название есть в паке;
  • проверяет, что каждый предмет пака этой книги встречается в её тексте.

Запуск:  python tools\\audit_books.py
"""
import io, json, os, re, sys, unicodedata, collections

sys.stdout.reconfigure(encoding="utf-8")
import pymupdf

BOOKS = r"D:\RogueTraderBuilder\books"
PKG = r"D:\RogueTraderBuilder\project\data\builtin"

PDFS = {
    "core": "Rogue_Trader_baza_Bazovaya_kniga_pravil_1_03_01.pdf",
    "stormstout": "Skvoz_shtorm_1_02__Obychny.pdf",
    "darkmarkets": "Rogue Trader (дополнение) Чёрные рынки (1.03).pdf",
    "abhuman": "Край бездны (1.03).pdf",
    "tau": "Rukovodstvo_po_personazham-tau_1_01.pdf",
}

# Классы оружия и брони: по ним опознаются строки таблиц арсенала.
CLASSES = [
    "Холодное", "Метательное", "Пистолет-пулемёт", "Пистолет-пулемет", "Пистолет",
    "Ракетница", "Граната", "Лазерное", "Стабберное", "Болтерное", "Мельта",
    "Плазменное", "Зажигательное", "Примитивное", "Экзотическое", "Силовое",
    "Цепное", "Улучшение", "Броня", "Костюм", "Украшение", "Модификация",
    "Аугметика", "Имплантат", "Оружие", "Приспособление",
]


def norm(s):
    s = unicodedata.normalize("NFKC", str(s)).lower().replace("ё", "е")
    # В PDF кавычки иногда приходят битыми («ì» «î») — приводим к обычным.
    s = s.replace("ì", '"').replace("î", '"').replace("„", '"').replace("“", '"')
    s = re.sub(r"[«»\"'()\[\].,:;—–\-*]", " ", s)
    return re.sub(r"\s+", " ", s).strip()


# Слова, по которым видно, что это не название предмета.
NOT_ITEM = ("улучшения ", "улучшение ", "бросок ", "броски", "итог", "результат",
            "уровень", "ранг ", "примеры", "название", "сотворение", "поддержание")


def pack_items():
    out = []
    for f in ("05-items.json", "14-storm-items.json", "12-tau-items.json"):
        p = os.path.join(PKG, f)
        if not os.path.exists(p):
            continue
        d = json.load(io.open(p, encoding="utf-8"))
        out += (d.get("items") or [])
        out += (d.get("augmetics") or [])
    return out


def book_text(fname):
    path = os.path.join(BOOKS, fname)
    if not os.path.exists(path):
        return None
    doc = pymupdf.open(path)
    text = "\n".join(doc[p].get_text() for p in range(doc.page_count))
    doc.close()
    return text


def table_names(text):
    """Названия предметов из таблиц: имя идёт перед словом класса."""
    text = text.replace("\n", " | ")
    text = re.sub(r"[ \t]+", " ", text)
    marks = [(m.start(), m.group(0)) for m in re.finditer(r"Таблица\s+[\d\-–]+\s+[^|]{0,80}", text)]
    found = collections.OrderedDict()
    for i, (pos, title) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else min(len(text), pos + 4000)
        block = text[pos:end]
        for cl in CLASSES:
            for m in re.finditer(r"([^|]{2,60}?)\s+\|\s*" + cl + r"\b", block):
                cand = norm(m.group(1))
                cand = re.sub(r"^.*?\b(таблица|iii|ii|iv|v)", "", cand).strip(" |")
                if len(cand) >= 4 and not re.search(r"^\d", cand) and cand not in found:
                    found[cand] = title.strip()
    return found


def main():
    items = pack_items()
    names = {norm(i.get("name", "")): i for i in items if i.get("name")}
    total_missing = 0
    for key, fname in PDFS.items():
        text = book_text(fname)
        if text is None:
            print(f"{key}: PDF не найден — {fname}")
            continue
        tnorm = norm(text)
        # 1. каждый предмет этой книги есть в её тексте
        src = {"core": "core", "stormstout": "stormstout", "darkmarkets": "darkmarkets",
               "abhuman": "abhuman", "tau": "tau"}[key]
        lost = []
        for it in items:
            if it.get("source") != src:
                continue
            n = norm(it.get("name", ""))
            if not n or n in tnorm:
                continue
            # В таблице название могло быть сокращено («Тяж. стаббер «Ортлак»»)
            # — сверяем по значащим словам: модель и тип оружия.
            words = [w for w in n.split() if len(w) > 3]
            if words and all(w in tnorm for w in words):
                continue
            lost.append(it.get("name", ""))
        # 2. каждое название из таблиц есть в паке
        missing = []
        for cand, title in table_names(text).items():
            if any(cand.startswith(w) for w in NOT_ITEM):
                continue
            if cand in names:
                continue
            if any(cand in n or (len(cand) >= 8 and n in cand) for n in names):
                continue
            missing.append((title, cand))
        print(f"== {key}: предметов пака — {sum(1 for i in items if i.get('source') == src)}, "
              f"не найдено в тексте книги — {len(lost)}, названий из таблиц без предмета — {len(missing)}")
        for n in lost:
            print(f"   нет в тексте: {n!r}")
        for title, cand in missing[:40]:
            print(f"   нет в паке: {cand!r}  ({title})")
        total_missing += len(lost) + len(missing)
    print(f"\nитого расхождений: {total_missing}")
    return 0 if total_missing == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
