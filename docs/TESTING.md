# Тесты и графика

## Автотест в игре
```
powershell -ExecutionPolicy Bypass -File tools\run_selftest.ps1
```
Собирает DLL, проверяет XML, запускает игру с `-quicktest -pmselftest` (игра должна быть закрыта),
печатает `ParamedicSelfTest.txt` (рядом с `Player.log`) и ошибки лога. Игра закрывается сама (`-pmstay` — не закрывать).

Проверяется (~45 пунктов): включение роли только ядром; стадии комплекта; способности; карман 3/6;
рост качества лечения и падение точности; стабилизация (повязка, кровь 0, расход медикамента, мысль пациента);
адреналин и запрет второго укола; крик (ThreatDisabled, механоиды не слушают, сброс цели); подавление
автоатаки под крестом; вероломство при атаке по приказу; увечье/убийство (стадии мысли), животное — не нарушение;
кризис веры (снятие ядра, запрет надеть, возврат кармана); сохранение перезарядки; подсчёт спасённых;
**бой** — безоружный медик под крестом среди трёх штурмующих пиратов (без кровожадных, с винтовками)
3000 тиков на макс. скорости: 0 прицельных попаданий от уважающих, нет вероломства; шальные пули считаются отдельно.
Статы проверяются по `GetValueUnfinalized` (обычный `GetStatValue` в том же тике может отдать кэш сборки).
В конце пишется сейв `ParamedicSelfTest`.

## Проверка сейва
`RimWorld.exe -dutyloadsave=ParamedicSelfTest` (инструмент DutyOptim) → `DutyOptimLoadSave.txt`:
должно быть «ошибок 0». Последний прогон (2026-10-12): 0 ошибок.

## Прогон с Combat Extended
```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1
```
Собирает DLL, проверяет XML, разворачивает мод в `...\RimWorld\Mods\Paramedic`, подменяет `ModsConfig.xml`
набором `tools/modlists/ce.xml` (Harmony → Core/DLC → CE → CE Armory → Paramedic) с бэкапом в
`C:\RimWorldConfigBackup`, печатает ошибки лога с отфильтрованным шумом CE и **точную команду запуска**.
Игру запускает менеджер:
```
"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe" -quicktest -pmselftest
```
Отчёт `ParamedicSelfTest.txt` печатает `mode: CE`/`CE+Armory`, число ошибок лога и PASS/FAIL; итоги по
режимам копятся в `ParamedicSelfTest.modes.txt`. После прогона верни конфиг:
```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 -Restore
```
Весь цикл одним запуском (сборка → конфиг → игра → проверка загрузки сейва → возврат конфига в `finally`):
```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 -Run
```
`-Run` грузит сейв с CE (`-dutyloadsave`) и печатает вердикт DutyOptim; `-VanillaSave <файл>` проверяет
«сейв, сделанный без CE, грузится с CE» на копии ванильного сейва. Ещё ключи: `-NoDeploy`, `-NoLoadSave`, `-Force`.

В CE-режиме дополнительно проверяется: у пиратов CE-оружие **и патроны** (иначе не стреляют); в бою реально
были снаряды (`ProjectileCE`, выстрелов > 0, иначе тест недействителен); форма надевается без конфликта слотов;
у вещей формы заданы `Bulk`/`WornBulk`; карман не превышает лимит; 6 медикаментов + шприц влезают в `CarryBulk`.

Пока `About/About.xml` содержит `CETeam.CombatExtended` в `<incompatibleWith>`, скрипт отказывается
готовить CE-прогон: игра загрузит только один из модов, и тест молча проверит не то. Убирает строку трек A.
Пункты отчёта `CE: kit apparel has Bulk/WornBulk` заведомо FAIL, пока CE-патч формы (трек A) не добавлен.

## Снимки формы
`RimWorld.exe -quicktest -pmshot` → `ParamedicShot_Юг/Восток/Север.png` рядом с `Player.log`:
пять пешек (все типы тел) в форме, три ракурса. Последний результат — [ingame_preview.png](ingame_preview.png).

## Текстуры
```
python tools/extract_vanilla.py   # один раз; нужен pip install UnityPy, пишет в tools/_vanilla (не в git)
python tools/gen_textures.py      # все текстуры, иконки, About/Preview.png и About/ModIcon.png (~30 с)
```
Раскладка (для ручной перерисовки — те же имена):
- `Textures/Things/Paramedic/Apparel/<Вещь>/PM_<Вещь>.png` — иконка на земле (128);
- на теле: `PM_<Вещь>_<Male|Female|Thin|Hulk|Fat>_<south|east|north>.png` для Shirt, Vest, Jacket, Belt (256);
- головные: `PM_<Beret|Helmet|Goggles>_<south|east|north>.png` (256); у штанов только иконка (на теле не рисуются);
- `Textures/Things/Paramedic/Item/PM_AdrenalineSyringe.png`, `Textures/UI/Abilities/PM_*.png`.
West рисуется зеркалом east.
