"""Статические проверки CE-патча (Patches/PM_CE.xml).

Что делает:
  1. Объединяет все наши Defs (Defs/**/*.xml) в одно дерево <Defs> и проверяет,
     что каждый xpath из Patches/PM_CE.xml находит узел. Ловит опечатки в
     defName/путях до запуска игры.
  2. Проверяет, что всем 8 вещам формы патч добавляет и Bulk, и WornBulk —
     именно это заставляет авто-патчер CE (ApparelAutoPatcher) пропустить наши
     вещи и не переписать броню по своим пресетам.
  3. Если указан путь к CE, проверяет, что CE-статы, которые мы задаём
     (Bulk/WornBulk/CarryBulk), действительно определены в Defs CE.

Запуск:
    python tools/ce_xpath_check.py [корень_мода] [корень_CE]

Без второго аргумента путь к CE берётся из стандартной папки Steam Workshop;
если её нет — проверка CE-статов просто пропускается.
"""
import os
import sys
import xml.etree.ElementTree as ET

# Статы, которых нет в ванили: их определяет только CE (проверяем по Defs CE).
CE_ONLY_STATS = ("Bulk", "WornBulk", "CarryBulk")

# Все 8 вещей формы: каждой нужен Bulk+WornBulk, чтобы авто-патчер CE её пропустил.
APPAREL_DEFNAMES = (
    "PM_Apparel_Shirt",
    "PM_Apparel_Pants",
    "PM_Apparel_Vest",
    "PM_Apparel_Jacket",
    "PM_Apparel_Beret",
    "PM_Apparel_Helmet",
    "PM_Apparel_Goggles",
    "PM_Apparel_Belt",
)

DEFAULT_CE_ROOT = os.path.join(
    r"C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2890901044"
)


def merge_defs(defs_dir):
    """Собирает все <Defs> в один корень (как это делает игра перед патчингом)."""
    root = ET.Element("Defs")
    files = 0
    for dirpath, _, filenames in os.walk(defs_dir):
        for name in sorted(filenames):
            if not name.lower().endswith(".xml"):
                continue
            path = os.path.join(dirpath, name)
            try:
                tree = ET.parse(path)
            except ET.ParseError as e:
                print(f"ОШИБКА XML {path}: {e}")
                continue
            files += 1
            for child in tree.getroot():
                root.append(child)
    return root, files


def collect_operations(patch_path):
    """Возвращает [(class, xpath), ...] из патча."""
    tree = ET.parse(patch_path)
    ops = []
    for el in tree.getroot().iter():
        cls = el.get("Class", "")
        if not cls.startswith("PatchOperation"):
            continue
        xpath = el.findtext("xpath")
        if xpath:
            ops.append((cls, xpath.strip()))
    return ops


def eval_xpath(defs_root, xpath):
    """Оценивает xpath относительно <Defs>. Возвращает список узлов или None,
    если синтаксис не поддержан ElementTree."""
    rel = xpath
    if xpath == "Defs":
        rel = "."
    elif xpath.startswith("Defs/"):
        rel = xpath[len("Defs/"):]
    try:
        return defs_root.findall(rel)
    except SyntaxError:
        return None


def ce_stat_defnames(ce_root):
    names = set()
    for dirpath, _, filenames in os.walk(os.path.join(ce_root, "Defs")):
        for name in filenames:
            if not name.lower().endswith(".xml"):
                continue
            try:
                tree = ET.parse(os.path.join(dirpath, name))
            except ET.ParseError:
                continue
            for el in tree.getroot().iter("StatDef"):
                dn = el.findtext("defName")
                if dn:
                    names.add(dn.strip())
    return names


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    mod_root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..")
    ce_root = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_CE_ROOT

    patch_path = os.path.join(mod_root, "Patches", "PM_CE.xml")
    defs_dir = os.path.join(mod_root, "Defs")

    if not os.path.isfile(patch_path):
        print(f"Нет файла патча: {patch_path}")
        return 1

    defs_root, files = merge_defs(defs_dir)
    ops = collect_operations(patch_path)
    print(f"Defs: {files} файлов, операций в PM_CE.xml: {len(ops)}")

    problems = 0

    # 1. Каждый xpath должен находить узел.
    for cls, xpath in ops:
        found = eval_xpath(defs_root, xpath)
        if found is None:
            print(f"  SKIP (сложный xpath): {xpath}")
        elif len(found) == 0:
            problems += 1
            print(f"  НЕ НАЙДЕН узел: {xpath}  [{cls}]")

    # 2. Каждой вещи формы — Bulk и WornBulk.
    patch_tree = ET.parse(patch_path)
    per_def = {}
    for el in patch_tree.getroot().iter():
        if not el.get("Class", "").startswith("PatchOperation"):
            continue
        xpath = (el.findtext("xpath") or "")
        tags = set()
        for v in el.iter("value"):
            for d in v.iter():
                if d is not v:
                    tags.add(d.tag)
        for defname in APPAREL_DEFNAMES:
            if f'defName="{defname}"' in xpath:
                per_def.setdefault(defname, set()).update(tags)
    for defname in APPAREL_DEFNAMES:
        have = per_def.get(defname, set())
        if not {"Bulk", "WornBulk"} <= have:
            problems += 1
            print(f"  {defname}: нет Bulk/WornBulk (есть: {sorted(have)})")
    if problems == 0:
        print("  Все xpath находят узел; у всех 8 вещей формы есть Bulk+WornBulk.")

    # 3. CE-статы должны быть определены в CE.
    if ce_root and os.path.isdir(ce_root):
        stats = ce_stat_defnames(ce_root)
        missing = [s for s in CE_ONLY_STATS if s not in stats]
        if missing:
            problems += 1
            print(f"  CE не определяет статы: {missing}")
        else:
            print(f"  CE-статы определены: {', '.join(CE_ONLY_STATS)}")
    else:
        print(f"  CE не найден ({ce_root}) — проверка статов пропущена.")

    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
