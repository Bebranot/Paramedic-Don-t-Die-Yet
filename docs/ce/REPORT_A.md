# Отчёт по треку A — XML-данные и метаданные (CE-совместимость)

**Ветка:** `ce/xml` (worktree: `C:\ai-rimworld-wt\CE_A`)  
**Дата:** 2026-10-13  
**База:** `ce/audit` (AUDIT.md, AUDIT_ADDENDUM.md)

---

## Сделано

### 1. `About/About.xml`
- Убран `<incompatibleWith><li>CETeam.CombatExtended</li></incompatibleWith>` — блокировал совместную загрузку с CE.
- В `<loadAfter>` добавлены:
  - `CETeam.CombatExtended`
  - `CETeam.CombatExtendedArmory`
  (только порядок загрузки, зависимости необязательные).
- Описание мод обновлено: «Requires Harmony. Supports Combat Extended (optional). …»

### 2. `Patches/PM_CE.xml` (новый)
Все операции внутри `PatchOperationFindMod("Combat Extended")` — без CE патч не выполняется.

| Вещь (defName) | Операции | Было (ваниль) | Стало (CE) | Обоснование (кратко) |
|---|---|---|---|---|
| `PM_Apparel_Shirt` | Add Bulk/WornBulk | — | Bulk 1, WornBulk 0.5 | Лёгкая одежда, без брони. CE даёт лёгкой одежде Bulk ~1-2. У нас — ниже среднего. |
| `PM_Apparel_Pants` | Replace Blunt 0.05→0.18; Add Bulk/WornBulk | Sharp 0.05, Blunt 0.05 | Sharp 0.05, Blunt 0.18, Bulk 1, WornBulk 0.5 | Sharp оставлен (почти 0). Blunt поднят до «пола» CE-пресета PantsClothing (0.18 при ванильном пороге 0.10). |
| `PM_Apparel_Vest` | Replace Sharp 0.45→3.6, Blunt 0.15→5.0; Add Bulk/WornBulk | Sharp 0.45, Blunt 0.15, Heat 0.15 | Sharp 3.6, Blunt 5.0, Heat 0.15, Bulk 2.5, WornBulk 1.5 | Лёгкий баллистический жилет ≈ 45% от ванильного FlakVest (CE FlakVest со сталью: 8 sharp / 12 blunt мм). 8×0.45=3.6, 12×0.42≈5.0. Bulk половина от CE FlakVest (5/3). |
| `PM_Apparel_Jacket` | Replace Sharp 0.40→0.5, Blunt 0.15→0.75; Add Bulk/WornBulk | Sharp 0.40, Blunt 0.15, Heat 0.55 | Sharp 0.5, Blunt 0.75, Heat 0.55, Bulk 4, WornBulk 1 | Лёгкая Shell-одежда по кривой CE LeatherJacket/ShellClothing. Heat оставлен (куртка огнестойкая). Bulk как у лёгкой куртки CE. |
| `PM_Apparel_Beret` | Replace Sharp 0.05→0.04, Blunt 0.03→0.07; Add Bulk/WornBulk | Sharp 0.05, Blunt 0.03 | Sharp 0.04, Blunt 0.07, Bulk 1, WornBulk 0.5 | Декоративная шапка по пресету CE Hat. |
| `PM_Apparel_Helmet` | Replace Sharp 0.55→4, Blunt 0.20→6; Add Bulk/WornBulk | Sharp 0.55, Blunt 0.20, Heat 0.25 | Sharp 4, Blunt 6, Heat 0.25, Bulk 3, WornBulk 1 | ≈ простой шлем CE (SimpleHelmet со сталью = 4/6). Наши ванильные чуть выше, округляем вниз — не усиливаем аналог. Bulk как у CE SimpleHelmet. |
| `PM_Apparel_Goggles` | Add Bulk/WornBulk | — | Bulk 0.3, WornBulk 0.1 | Аксессуар на глаза, брони нет. Минимальный Bulk. |
| `PM_Apparel_Belt` | Add Bulk/WornBulk; Add CarryBulk 6 | — | Bulk 1, WornBulk 0.5, CarryBulk 6 | Медицинская разгрузка. CarryBulk 6 — с запасом под 6 слотов кармана, заметно меньше CE-разгрузок (TacVest 30). |
| `PM_AdrenalineSyringe` | Add Bulk | — | Bulk 0.05 | По массе (0.05) — как мелкие препараты CE (Yayo/Flake 0.05). |

**Важное примечание про авто-патчер CE:** CE имеет `ApparelAutoPatcher`, который пропускает вещи, у которых в `statBases` уже есть `Bulk` или `WornBulk`. Мы добавляем их всем 8 предметам формы — поэтому CE не перезаписывает наши значения, и числа остаются детерминированными (иначе, напр., куртка попала бы под пресет `ArmorVestJacket` и стала тяжёлой бронёй 4/3 с Bulk 5).

`ArmorRating_Heat` оставлены как есть (0.15 / 0.55 / 0.25): CE не масштабирует этот стат, единицы совпадают с ванилью.

### 3. CE Armory
Аудит показал: конфликтов по слотам/категориям/defName нет. CE Armory только добавляет свои предметы, не патчит ванильные вещи глобально. Отдельный блок `PatchOperationFindMod("Combat Extended Armory")` **не добавлен** — не выдумываем конфликтов, где их нет.

### 4. `tools/ce_xpath_check.py` (новый)
Проверяет, что все `xpath` из `PM_CE.xml` находят узлы в наших `Defs`. Использует `ElementTree.findall`, нормализует пути (убирает ведущий `Defs/`, так как корень проверяющего дерева — уже `Defs`).

**Результат:** ошибок 0. Все 29 xpath находятся.

### 5. `tools/validate_xml.py`
Существующий валидатор проходит: `XML: 20 файлов, ошибок: 0`.

---

## Таблица «вещь → было → стало» (подробная)

| Вещь | Ванильные статы (statBases) | CE-статы после патча (statBases) | Примечания |
|---|---|---|---|
| **PM_Apparel_Shirt** | WorkToMake 3000, MaxHP 100, Mass 0.3, Flammability 1.0, Insulation_Cold 3, Insulation_Heat 2, EquipDelay 1.5 | + Bulk 1, WornBulk 0.5 | Брони нет и не будет |
| **PM_Apparel_Pants** | WorkToMake 3500, MaxHP 120, Mass 0.6, Flammability 1.0, Sharp 0.05, Blunt 0.05, Insulation_Cold 3, Insulation_Heat 1, EquipDelay 2 | Sharp 0.05, Blunt **0.18**, + Bulk 1, WornBulk 0.5 | Blunt поднят до порога CE PantsClothing |
| **PM_Apparel_Vest** | WorkToMake 9000, MaxHP 160, Mass 2.5, Flammability 0.6, Sharp 0.45, Blunt 0.15, Heat 0.15, Insulation_Cold 2, EquipDelay 3 | Sharp **3.6**, Blunt **5.0**, Heat 0.15, + Bulk 2.5, WornBulk 1.5 | ~45% от CE FlakVest (8/12 sharp/blunt) |
| **PM_Apparel_Jacket** | WorkToMake 16000, MaxHP 220, Mass 3, Flammability 0.2, Sharp 0.40, Blunt 0.15, Heat 0.55, Insulation_Cold 14, Insulation_Heat 6, EquipDelay 3 | Sharp **0.5**, Blunt **0.75**, Heat 0.55, + Bulk 4, WornBulk 1 | Лёгкая Shell по кривой CE |
| **PM_Apparel_Beret** | WorkToMake 1500, MaxHP 80, Mass 0.1, Sharp 0.05, Blunt 0.03, Insulation_Cold 1, EquipDelay 0.8 | Sharp **0.04**, Blunt **0.07**, + Bulk 1, WornBulk 0.5 | Пресет CE Hat |
| **PM_Apparel_Helmet** | WorkToMake 7000, MaxHP 140, Mass 1.5, Flammability 0.4, Sharp 0.55, Blunt 0.20, Heat 0.25, Insulation_Cold 2, EquipDelay 1.5 | Sharp **4**, Blunt **6**, Heat 0.25, + Bulk 3, WornBulk 1 | ≈ CE SimpleHelmet (сталь 4/6) |
| **PM_Apparel_Goggles** | WorkToMake 12000, MaxHP 60, Mass 0.3, EquipDelay 1 | + Bulk 0.3, WornBulk 0.1 | Минимальный Bulk |
| **PM_Apparel_Belt** | WorkToMake 4000, MaxHP 100, Mass 1, EquipDelay 2 | + Bulk 1, WornBulk 0.5, equippedStatOffsets: **CarryBulk 6** | Медицинский риг, CarryBulk под 6 слотов кармана |
| **PM_AdrenalineSyringe** | MaxHP 50, WorkToMake 1200, Mass 0.05, Flammability 0.5, DeteriorationRate 2 | + Bulk 0.05 | Как мелкие препараты CE |

---

## Допущения и решения

1. **Единицы брони CE:** `ArmorRating_Sharp` — мм RHA, `ArmorRating_Blunt` — МПа. Ванильные доли (0.45) в CE = «почти ноль». Переводили через пропорцию к ванильному аналогу в CE (FlakVest, SimpleHelmet и т.д.), сохраняя «долю» от аналога.
2. **Форма — мундир, а не броня:** Не усиливаем до уровня CE-брони того же тира. Цель — лёгкая защита, как в ванили.
3. **Bulk/WornBulk:** Подобраны так, чтобы медик не упирался в лимит переноски, но CE не патчил вещи повторно (авто-патчер пропускает вещи с уже заданным Bulk).
4. **Heat:** Оставлен без изменений — CE его не масштабирует.
5. **CarryWeight на поясе:** Не добавляли. CE-разгрузки его не используют (только у силовой брони). CarryBulk 6 достаточно для 6 медикаментов кармана.
6. **CE Armory:** Конфликта нет — отдельный патч не нужен.
7. **Безопасность без CE:** Все изменения внутри `PatchOperationFindMod("Combat Extended")` — в ванили ничего не делают. Повторная загрузка безопасна (операции идемпотентны: Replace на то же значение, Add с уже существующими узлами — RimWorld игнорирует дубликаты при повторном применении).

---

## Риски

| Риск | Вероятность | Митигация |
|---|---|---|
| Авто-патчер CE всё-таки что-то перезапишет (race condition порядка патчей) | Низкая | Добавлены Bulk/WornBulk — патчер их видит и пропускает вещь. loadAfter CE поставлен. |
| CarryBulk 6 на поясе мало/много для реального инвентаря CE | Низкая | CE-разгрузки дают 20–30; 6 — только под карман. При перегрузе поведение проверяет трек B. |
| CE Armory добавит обязательные поля для наших слотов в будущем | Низкая | Сейчас конфликтов нет. Если появятся — допишем в отдельный патч. |

---

## Что проверить в игре (по чек-листу)

- [ ] Загрузить мод **без CE** — всё работает, ошибок в логе нет.
- [ ] Загрузить мод **с CE** — форма получает Bulk/WornBulk/брони из PM_CE.xml (проверить через Debug → Inspect).
- [ ] Проверить, что авто-патчер CE не меняет нашу броню (значения остаются теми, что в патче).
- [ ] Проверить носимость: надеть полный комплект, убедиться, что Bulk не перегружает пешку (лимит ~50-60 у солдата).
- [ ] Карман медика: 3/6 слотов учитываются в Bulk — нет зацикливания подбора/скидывания (трек B).
- [ ] Сейв без CE → загрузка с CE: форма не ломается, статы применяются.
- [ ] Сейв с CE → загрузка без CE: CE-статы игнорируются, ванильные статы работают.

---

## Файлы изменены/добавлены (в worktree `ce/xml`)

- `About/About.xml` — убран incompatibleWith, добавлены loadAfter CE/CE Armory, обновлено описание.
- `Patches/PM_CE.xml` — **новый**, основной CE-патч (29 операций).
- `tools/ce_xpath_check.py` — **новый**, проверка xpath (0 ошибок).
- `tools/validate_xml.py` — без изменений, проходит.

---

## Команды проверки

```bash
# XML валидация
python tools\validate_xml.py

# xpath проверка
python tools\ce_xpath_check.py

# Сборка (требует Harmony в NuGet)
dotnet build Source\Paramedic.csproj -c Release
```

*Примечание: `dotnet build` на момент отчёта падает на отсутствии Harmony в пакетах — это предусловие репозитория, не связанное с изменениями трека A.*