# Трек C — тестовая обвязка под CE: отчёт

Ветка `ce/test`, worktree `C:\ai-rimworld-wt\CE_C` (от `ce/audit`, `2c64b00`). Дата: 2026-10-13.
Задача: `docs/ce/04_TRACK_C_TEST.md`. Отчёт положен сюда, как указано в задаче (`REPORT_C.md`).

## Что сделано

### `Source/SelfTest.cs`
1. **Снаряды CE.** Новый патч `SelfTest_CEProjectileImpact_Patch` — `CombatExtended.ProjectileCE.Impact(Thing)`
   (`BulletCE` зовёт `base.Impact`, поэтому одного патча хватает на все пули). Считает попадания в защищённого
   медика: прицельные/разрешённые/шальные, как ванильный патч `Bullet.Impact`. Доступ к типам CE — **только рефлексией**
   (`AccessTools.TypeByName`), ссылки на сборку CE в csproj нет.
2. **Реальные выстрелы CE.** `SelfTest_CEShot_Patch` — `Verb_LaunchProjectileCE.SpawnProjectile()`, считает созданные
   снаряды враждебных стрелков. Новый пункт `CE: live fire happened` (выстрелов > 0): без него «медик не ранен» при CE
   ничего не доказывает.
3. **Пираты с патронами.** `ArmRaider` при CE: `CompAmmoUser.ResetAmmoCount(null)` (полный магазин) + запас патронов
   (`CurAmmoSet.ammoTypes[].ammo`) в инвентарь, в лог — `def/mag/ammo`. Пункт `CE: raiders have CE weapons + ammo`.
4. **Новые пункты формы** (`CheckCEBulk`): `CE: kit apparel has Bulk`, `CE: kit apparel has WornBulk`,
   `CE: 6 medicine + syringe fit CarryBulk`. Проверяется **наличие** стата в `def.statBases`, а не значение: у CE
   `Bulk` имеет `defaultBaseValue=1`, и проверка значения была бы ложно-зелёной. Без CE (нет StatDef) пункты пропускаются.
5. **Бой считает и CE-попадания**, а `EndLiveFire` печатает `CE projectiles fired`/`CE aimed/allowed/stray`.
6. **Отчёт (п. 3a):** шапка печатает `mode: vanilla|CE|CE+Armory` и `log errors (approx, so far): N`; строка
   `mode=… selftest=PASS|FAIL logerrors=N` дописывается в `ParamedicSelfTest.modes.txt` — итоги по режимам копятся в одном файле.
7. Vanilla-поведение не менялось: всё CE-специфичное под `CEActive` (или под наличием типов CE). Статы уже считались
   по `GetValueUnfinalized` — оставлено как есть.

### `Source/ShotTest.cs`
Одна строка: в лог пишется режим (`shot mode: vanilla|CE|CE+Armory`). На снимки CE не влияет (Bulk/броня — только статы),
менять больше нечего.

### `tools/modlists/ce.xml` (новый)
Порядок: Harmony → Core/DLC (5) → `CETeam.CombatExtended` → `CETeam.CombatExtendedArmory` → `bebranotyep.paramedic`.

### `tools/run_selftest_ce.ps1` (новый)
- **по умолчанию готовит, но не запускает**: сборка → `validate_xml.py` → разворачивает мод
  (`tools/make_release.ps1 -Out ...\RimWorld\Mods\Paramedic`) → бэкап `ModsConfig.xml` в `C:\RimWorldConfigBackup`
  (`ModsConfig.original.xml` + копия со меткой времени, файл состояния) → пишет CE-набор, сохраняя `<version>` и
  `<knownExpansions>` → печатает **точную команду запуска**, ошибки лога (шум CE отфильтрован) и напоминание про `-Restore`;
- **`-Run`** — весь цикл сам: готовит, запускает игру, ждёт `ParamedicSelfTest.txt`, печатает режим/итог/ошибки лога и
  вердикт загрузки сейва, затем **в `finally` всегда возвращает оригинальный `ModsConfig.xml`**;
- **`-Restore`** — вернуть оригинал; **`-NoDeploy`**, **`-NoLoadSave`**, **`-VanillaSave <файл>`**, **`-Force`**;
- если предыдущий CE-конфиг остался на месте (нет `-Restore`), следующий запуск сначала возвращает оригинал — потерять его нельзя;
- проверка загрузки сейва (п. 3a): `RimWorldWin64.exe -dutyloadsave=<сейв>`, парсится вердикт DutyOptim
  («ошибок (с начала сессии) N»; кириллица ловится `\uXXXX`-регэкспом, сам скрипт остаётся ASCII-only);
  `-VanillaSave` копирует ванильный сейв **до** прогона, поэтому проверка «сохранён без CE → грузится с CE» возможна скриптом;
- **страж:** если `About/About.xml` содержит `CETeam.CombatExtended` в `<incompatibleWith>`, скрипт отказывается
  готовить прогон (иначе игра загрузит один мод и тест молча проверит ваниль). Обход — `-Force`.

### `docs/TESTING.md`
Раздел «Прогон с Combat Extended» с точной командой запуска, описанием ключей и ожидаемых пунктов.

## Проверено (факты)

| Проверка | Результат |
| --- | --- |
| `dotnet build Source\Paramedic.csproj -c Release -p:HarmonyDir=…\2009463077\1.6\Assemblies` | Ошибок 0, предупреждений 0 |
| `python tools\validate_xml.py` | «XML: 19 файлов, ошибок: 0» |
| `run_selftest_ce.ps1 -Force -NoDeploy` | CE-набор записан, оригинал сохранён в `C:\RimWorldConfigBackup` |
| `run_selftest_ce.ps1 -Restore` | `ModsConfig.xml` побайтово вернулся (SHA256 `9A70CD04…CD74D` совпал с копией до опыта) |
| `run_selftest_ce.ps1 -NoDeploy` без `-Force` | отказ с объяснением, код возврата 1 |

**Игру я не запускал** (правило: запуск делает менеджер), поэтому CE-ветки автотеста проверены компиляцией и логикой,
но не рантаймом.

## Что не сделано (не мои файлы)

1. **`About/About.xml`, стр. 29:** `<li>CETeam.CombatExtended</li>` в `<incompatibleWith>` — прямой блокер CE (трек A).
   Пока он там, CE-прогон невозможен.
2. **CE-патч формы** (`Bulk`/`WornBulk` в `PM_Apparel.xml`) — трек A. Значит пункты `CE: kit apparel has Bulk/WornBulk`
   **сейчас заведомо FAIL**; после трека A должны стать OK.
3. **Карман и CE-Bulk.** Пункт «6 медикаментов + шприц влезают в `CarryBulk`» считает номинальную выкладку против
   `CarryBulk` медика. Учитывает ли CE медицину из ванильного `inventoryStock` в своём Bulk — вопрос к рантайму
   (карман — внутренняя система мода, см. аудит, п. «Карман медика + пояс»).

## Точная команда запуска (игру запускает менеджер)

Готовность (сборка, deploy, подмена конфига, печать команды):

```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1
```

Запуск игры:

```
"C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe" -quicktest -pmselftest
```

Возврат конфига:

```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 -Restore
```

Всё сразу, включая проверку сейва и авто-возврат конфига:

```
powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 -Run
```

## Что проверить в игре (вручную)

1. В `ParamedicSelfTest.txt` строка `mode:` — `CE` или `CE+Armory`. Если `vanilla` — CE не загрузился (см. блокер №1).
2. Есть строки `CE: raiders have CE weapons + ammo` и `CE: live fire happened` (в конце `CE projectiles fired: N`, N > 0).
   Нет пункта `CE:` при `mode: CE` — значит патчи CE не наложились (имена типов CE изменились): смотри `Player.log` на
   `[Paramedic] patch SelfTest_CEProjectileImpact_Patch failed`.
3. `no aimed hits on medic` — OK, при этом в логе считаются и CE-попадания (aimed/allowed/stray).
4. После трека A: `CE: kit apparel has Bulk`, `CE: kit apparel has WornBulk`, `CE: 6 medicine + syringe fit CarryBulk` — OK.
5. Загрузка сейва: `-Run` печатает вердикт DutyOptim («ошибок 0»). Кросс-режим: сначала обычный
   `tools\run_selftest.ps1` (сейв без CE), затем
   `tools\run_selftest_ce.ps1 -Run -VanillaSave "<…>\Saves\ParamedicSelfTest.rws"`.
6. Регрессия ванили: прогнать `tools\run_selftest.ps1` — отчёт не должен отличаться от прежнего (`mode: vanilla`, без строк `CE:`).
7. Строка `protection lost at +Nt` (если есть) — когда крест перестал защищать, полезно при разборе боя.

## Риски

- **API CE по рефлексии.** Проверено по установленной CE 16.7.3.0: есть `ProjectileCE.Impact(Thing)`,
  `Verb_LaunchProjectileCE.SpawnProjectile()`, `CompAmmoUser.ResetAmmoCount/CurAmmoSet/CurMagCount/HasAndUsesAmmoOrMagazine`.
  При смене имён `Prepare()`/`TargetMethod()` отдадут null, `PatchAllSafe` залогирует сбой одного патча, остальные
  продолжат работать — счётчики CE просто исчезнут из отчёта (это заметно: пункты `CE:` пропадут).
- **Сборка.** Обычный `dotnet build Source\Paramedic.csproj -c Release` падает и на `main`, и на `ce/test`:
  `Directory.Build.props` ждёт `…\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll`, а мод Harmony
  теперь ставит версионные папки (`1.4`…`1.7`). Это дрейф среды, не следствие трека C (воспроизведено на `main`, 32 ошибки
  CS0246). Обход: `-p:HarmonyDir=…\2009463077\1.6\Assemblies`; скрипт находит папку сам по версии игры и экспортирует
  `HarmonyDir`, который MSBuild подхватывает как свойство. Править `Directory.Build.props` — не мои файлы, решать трекам A/B.
- **Правка настоящего `ModsConfig.xml`.** Мера защиты: бэкап `ModsConfig.original.xml`, `finally` в `-Run`, `-Restore`,
  авто-возврат, если предыдущий CE-конфиг не убрали.
- `GetStatValueUnfinalized` и `GetStatValue(CarryBulk)` в самотесте — стандартный обход кэша статов в одном тике.

## Файлы трека

`Source/SelfTest.cs`, `Source/ShotTest.cs`, `tools/run_selftest_ce.ps1`, `tools/modlists/ce.xml`,
`docs/TESTING.md`, `docs/ce/REPORT_C.md`.
