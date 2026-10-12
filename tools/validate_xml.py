"""Проверка, что все XML мода (About, Defs, Patches, Languages) корректны.

Битый XML роняет загрузку, и RimWorld сбрасывает список модов — запускать перед каждым запуском игры.

    python tools/validate_xml.py [корень_мода]
"""
import os
import sys
import xml.etree.ElementTree as ET

FOLDERS = ["About", "Defs", "Patches", "Languages"]


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
    bad = 0
    count = 0
    for folder in FOLDERS:
        for dirpath, _, filenames in os.walk(os.path.join(root, folder)):
            for name in filenames:
                if not name.lower().endswith(".xml"):
                    continue
                path = os.path.join(dirpath, name)
                count += 1
                try:
                    ET.parse(path)
                except ET.ParseError as e:
                    bad += 1
                    print(f"ОШИБКА {os.path.relpath(path, root)}: {e}")
    print(f"XML: {count} файлов, ошибок: {bad}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
