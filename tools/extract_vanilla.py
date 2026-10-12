"""Достаёт из ресурсов игры ванильные текстуры-основы (силуэты одежды, тела, головы) в tools/_vanilla/.

Нужен UnityPy (pip install UnityPy). Папка _vanilla в git не попадает — в мод идут только
перерисованные текстуры (см. gen_textures.py).

    python tools/extract_vanilla.py
"""
import os
import sys

import UnityPy

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\resources.assets"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_vanilla")
WANT = ("ShirtBasic", "FlakJacket", "AdvancedHelmet", "Naked_", "Male_Average_Normal", "FlakPants", "Tuque")


def main():
    os.makedirs(OUT, exist_ok=True)
    env = UnityPy.load(GAME)
    n = 0
    for obj in env.objects:
        if obj.type.name != "Texture2D":
            continue
        data = obj.read()
        name = getattr(data, "m_Name", "")
        if name.startswith(WANT):
            data.image.save(os.path.join(OUT, name + ".png"))
            n += 1
    print(f"extracted {n} textures to {OUT}")


if __name__ == "__main__":
    sys.exit(main())
