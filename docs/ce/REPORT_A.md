# REPORT_A — Трек A: XML-данные и метаданные (CE / CE Armory)

**Ветка/worktree:** `ce/xml` — `C:\ai-rimworld-wt\CE_A`
**Основа:** `ce/audit` (`docs/ce/AUDIT.md`), с приоритетом `docs/ce/AUDIT_ADDENDUM.md` и `docs/ce/00_RULES.md`.
**Целевые версии CE:** Combat Extended `16.7.3.0`, CE Armory `16.2.1.0` (`...\294100\2890901044`, `...\294100\2911110083`).

## 1. Что сделано

| Файл | Изменение |
|---|---|
| `About/About.xml` | Убран `incompatibleWith CETeam.CombatExtended`; в `loadAfter` добавлены `CETeam.CombatExtended` и `CETeam.CombatExtendedArmory`; строка описания «Not compatible with Combat Extended» → «Supports Combat Extended (optional)». Версия (`modVersion`) не менялась. |
| `Patches/PM_CE.xml` | **Новый.** Все операции — внутри одного `PatchOperationFindMod` (имя мода `Combat Extended`). Без CE файл не делает ничего. |
| `tools/ce_xpath_check.py` | **Новый.** Статическая проверка: все `xpath` из `PM_CE.xml` находят узел; у всех 8 вещей формы есть `Bulk`+`WornBulk`; CE-статы `Bulk/WornBulk/CarryBulk` определены в Defs CE. |
| `docs/ce/ce_xpath_check_output.txt` | Вывод проверки (см. §4). |

CE Armory: `AUDIT.md` п.2 показал отсутствие конфликтов по `defName`/слотам — **отдельный блок `PatchOperationFindMod` для Armory не делался** (правило «не выдумывай»).

Source (`**/*.cs`) не трогался — это трек B. `Assemblies/Paramedic.dll` не коммитился.

## 2. Таблица «вещь → было (ваниль) → стало (CE)»

`sharp`/`blunt` — в единицах CE (мм RHA / МПа). `heat` — в ванильных единицах (CE этот стат не масштабирует).

| Вещь (`defName`) | Слой | Было sharp/blunt/heat | Стало sharp/blunt/heat | Bulk / WornBulk | offsets |
|---|---|---|---|---|---|
| `PM_Apparel_Shirt` | OnSkin | — / — / — | без брони | 1 / 0.5 | — |
| `PM_Apparel_Pants` | OnSkin | 0.05 / 0.05 / — | **0.05 / 0.18** / — | 1 / 0.5 | — |
| `PM_Apparel_Vest` | Middle | 0.45 / 0.15 / 0.15 | **3.6 / 5.0** / 0.15 | 2.5 / 1.5 | — |
| `PM_Apparel_Jacket` | Shell | 0.40 / 0.15 / 0.55 | **0.5 / 0.75** / 0.55 | 4 / 1 | — |
| `PM_Apparel_Beret` | Overhead | 0.05 / 0.03 / — | **0.04 / 0.07** / — | 1 / 0.5 | — |
| `PM_Apparel_Helmet` | Overhead | 0.55 / 0.20 / 0.25 | **4 / 6** / 0.25 | 3 / 1 | — |
| `PM_Apparel_Goggles` | EyeCover | — / — / — | без брони | 0.3 / 0.1 | — |
| `PM_Apparel_Belt` | Belt | — / — / — | без брони | 1 / 0.5 | **CarryBulk 6** |
| `PM_AdrenalineSyringe` | предмет | — | — | **Bulk 0.05** | — |

## 3. Обоснование чисел (с источниками в файлах CE)

CE задаёт броню двумя способами: фиксированные `ArmorRating_Sharp/Blunt` (мм/МПа) и/или `StuffEffectMultiplierArmor`, который умножается на «силу» материала. Наша форма делается **без материала** (цвет зашит в текстуру), поэтому `StuffEffectMultiplierArmor` не работает — значения ставим прямо в `statBases`.

- **Жилет = 45% от ванильного FlakVest.** Ваниль `Apparel_FlakVest`: sharp `1.00`, blunt `0.36` (`Core\...\Apparel_Various.xml:728-729`). В CE он же со сталью = `8 sharp / 12 blunt`: `StuffEffectMultiplierArmor 8` (`Patches\Core\ThingDefs_Misc\Apparel_Various.xml:211-218`) × сила стали sharp `1.0` / blunt `1.5`. Наш жилет `0.45/0.15` = 45% / 42% → **3.6 / 5.0**. Bulk = половина CE FlakVest (5/3) → 2.5/1.5.
- **Каска ≈ простой шлем.** CE `Apparel_SimpleHelmet`: `StuffEffectMultiplierArmor 4`, `Bulk 3`, `WornBulk 1` (`Apparel_Hats.xml:54-61`) → со сталью **4 / 6**. Наши ванильные `0.55/0.20` чуть выше простого шлема (`0.5`), но округляем вниз — не усиливаем аналог; Bulk берём ровно как у SimpleHelmet (3/1).
- **Куртка** — по кривой CE для лёгкой Shell-одежды (пресет `LeatherJacket`/`ShellClothing`, `Defs\ApparelAutoPatcherPresets\TorsoSets.xml:45-101`): sharp `(0.20,0.08)-(0.45,0.60)` при 0.40 → `0.50`; blunt `(0.08,0.14)-(0.16,0.85)` при 0.15 → `0.76 ≈ 0.75`.
- **Штаны** — «пол» пресета `PantsClothing` (`TorsoSets.xml:160-184`): sharp `(0.10,0.05)` → `0.05`; blunt `(0.10,0.18)` → `0.18`. Наши 0.05/0.05 остаются «почти ничем», но не нулём.
- **Берет** — пресет `Hat` (`Helmets.xml:83-109`): sharp `(0,0)-(0.35,0.25)` при 0.05 → `0.04`; blunt `(0,0)-(0.35,0.5)` → `0.07`.
- **Разгрузка** — `CarryBulk 6`: с запасом под 6 слотов медикаментов кармана; CE-разгрузки дают больше (TacVest 30, детский рюкзак 20, tribal backpack 15 — `Defs\ThingDefs_Misc\Apparel_Carrying.xml`). `CarryWeight` не даём: у CE-разгрузок его нет (только у силовой брони). Bulk 1/0.5 — лёгкий поясной риг.
- **Шприц** — `Bulk 0.05`, как у мелких мед-доз CE: таблетка `0.01`, Yayo/Flake `0.05`, Go-juice `0.2` (`Patches\Core\Drugs\Drugs.xml`). Масса шприца 0.05 → 0.05.
- **Heat не трогаем** (`0.15/0.55/0.25`): CE не масштабирует `ArmorRating_Heat` — в CE он остаётся в ванильных единицах (ср. CE `AdvancedHelmet` heat `0.54` = ванильные 0.54, `Apparel_Hats.xml:72-78`).

## 4. Проверки

```
python tools\validate_xml.py
```
→ `XML: 20 файлов, ошибок: 0`

```
python tools\ce_xpath_check.py
```
→ (полный вывод — `docs/ce/ce_xpath_check_output.txt`)
```
Defs: 7 файлов, операций в PM_CE.xml: 19
  Все xpath находят узел; у всех 8 вещей формы есть Bulk+WornBulk.
  CE-статы определены: Bulk, WornBulk, CarryBulk
```

Сборку C# (`dotnet build`) не запускал — трек A не меняет `Source/**`.
Игру не запускал (правило 3).

## 5. Допущения и замечания

1. **AUDIT_ADDENDUM п.1 опирается на неверное число ванильного флак-жилета.** В аддендуме: «наш жилет 0.45 vs ваниль flak 0.5+ → около 70–90% аналога». Реальный ванильный `FlakVest` sharp = **1.00** (не 0.5). Наш жилет — это 45% флак-жилета, поэтому в CE он = 45% CE-аналога (3.6/5.0), а не 70–90% (иначе жилет стал бы почти как полноценный CE armor vest — «не усиливать»). Обоснование — §3.
2. **Форма не из материала** → `StuffEffectMultiplierArmor` бесполезен, броня задаётся фиксированными `ArmorRating_*`. `PartialArmorExt` (дифференциация защиты по частям) **не добавлял**: наши аналоги — лёгкие пресеты CE (`LightArmorVest`, `ShellClothing`, `LeatherJacket`), у которых `partialStats` нет (в отличие от тяжёлых `FlakVest`/`ArmorVestJacket`). Аддендум называет `PartialArmorExt` условно («если так принято у аналогов») — для наших лёгких аналогов не принято.
3. **Авто-патчер CE выключается наличием `Bulk`/`WornBulk`.** По исходникам CE (`CombatExtended/Compatibility/ApparelAutoPatcher.cs`, GitHub `CombatExtended-Continued/CombatExtended`) патчер пропускает одежду, у которой в `statBases` уже есть `Bulk` или `WornBulk`; в воркшоп-папке CE исходников нет, в этой сессии не перепроверял. Смысл: мы добавляем Bulk всем 8 вещам → патчер их не трогает. Без этого CE переписал бы часть вещей по своим пресетам: **куртка** (0.40) попала бы под `ArmorVestJacket` и стала бы тяжёлой бронёй (~4/3, Bulk 5/Worn 5), **берет** — под `Hat`, **шлем** — под `LightHelmet` (sharp 4 / blunt 6.4). Жилет/штаны/рубашка/очки/пояс в ванильных значениях в диапазоны пресетов не попадают (у жилета 0.45 — «щель» между `MiddleClothing` 0.08–0.28 и `LightArmorVest` 0.55–0.99).
4. **Bulk лёгкие** — суммарно WornBulk всей формы ≈ 4.3 (шлем 1 + куртка 1 + жилет 1.5 + …) при базовом `CarryBulk` 20 (`Defs\Stats\Stats_Pawns_Inventory.xml`), плюс риг даёт +6. Медик не упрётся в лимит переноски.
5. **Повторная загрузка безопасна:** `PatchOperationAdd/Replace` идемпотентны на один прогон, дефы при перезагрузке читаются с нуля; без CE — файл не применяется.
6. Медицину/лечение и C#-логику не трогал (треки B/C).

## 6. Сделано / не сделано / риски / что проверить в игре

**Сделано:** `About.xml` (метаданные), `Patches/PM_CE.xml` (броня/Bulk/CarryBulk для 8 вещей + Bulk шприца), `tools/ce_xpath_check.py` + прогон, этот отчёт.

**Не сделано (сознательно):** блок для CE Armory (конфликтов нет), `PartialArmorExt`, изменения `Source/**`, `Languages/**` (новых CE-ключей для игрока не требуется).

**Риски:**
- Числа брони — оценочные (перевод «доли от аналога»); баланс в бою не проверен — это Фаза 2/трек C.
- Наличие `Bulk` как ключа отключения авто-патчера CE — по исходникам CE, локально не перепроверено.

**Что проверить в игре (при CE):**
1. В логе при загрузке нет красного про наши дефы; жилет `3.6/5.0`, шлем `4/6`, куртка `0.5/0.75` видны в инспекторе вещи.
2. Медик в полной форме не упирается в Bulk-лимит; риг даёт +6 CarryBulk.
3. Шприц адреналина укладывается в инвентарь с Bulk 0.05; бонусы `MedicalTendQuality/Speed` не сломаны.
4. Без CE мод грузится и ведёт себя как раньше (форма без Bulk; ванильная броня).

## 7. Инцидент: параллельная запись в этот worktree

Во время работы `Patches/PM_CE.xml` и `tools/ce_xpath_check.py` в `CE_A` были перезаписаны другим процессом/сессией (черновик со старыми ванильными числами жилета/шлема, `PatchOperationAdd` дублировал уже существующий `ArmorRating_Heat`, и `xpath` на несуществующий `PM_Apparel_Belt/equippedStatOffsets`). По решению владельца файлы приведены к `AUDIT_ADDENDUM`. **Если с веткой `ce/xml` работает ещё одна сессия — нужно согласовать владение файлами**, иначе правки будут перетираться.
