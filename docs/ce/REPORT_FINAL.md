# ФИНАЛЬНЫЙ ОТЧЁТ ФАЗЫ 2 — ИНТЕГРАЦИЯ CE (включая Фазы 2б и 2в)

**Ветка:** ce/integrate  
**Дата:** 2026-10-14

## Краткий итог
Выполнена интеграция треков A/B/C. Исправлены все FAIL автотестов на ванили, CE и CE+Armory.
Пройдены проверки: `dotnet build Source\Paramedic.csproj -c Release` — PASS (0 ошибок, 0 предупреждений), `python tools\validate_xml.py` — PASS (20 файлов), `python tools\ce_xpath_check.py` — PASS (все 19 xpath найдены, всем 8 вещам формы добавлены Bulk+WornBulk, CE-статы Bulk/WornBulk/CarryBulk определены).

## Таблица результатов прогонов

| Режим | PASS/FAIL | logerrors | Дата прогона | Файл лога |
|---|---|---|---|---|
| vanilla | PASS | 0 | 2026-10-14 14:38 | `docs/ce/runs/vanilla_for_crossload.txt` |
| CE only | PASS | 1* | 2026-10-14 14:35 | `docs/ce/runs/ce_only_20261014.txt` |
| CE+Armory | PASS | 1* | 2026-10-14 14:53 | `docs/ce/runs/ce_final_comment_check.txt` |
| Cross-load vanilla → CE+Armory | PASS | 1* | 2026-10-14 14:42 | `docs/ce/runs/ce_crossload_vanilla_to_ce.txt` |

\* Единственная ошибка — `Failed to resolve text. Trying again with English` (ArgumentNullException в Core RimWorld, не Paramedic).

## Исправления Фазы 2б (доведение до зелёного)

### 1. PM_CE.xml — PatchOperationConditional вместо PatchOperationFindMod
**Проблема:** `PatchOperationFindMod` не срабатывал, потому что в runtime все `packageId` приводятся к нижнему регистру (`ceteam.combatextended`), а патч проверял `CETeam.CombatExtended`. CE-патч не применялся → 8 предметов формы не получали `Bulk/WornBulk` → 2 FAIL в самотесте.

**Решение:** Заменён на `PatchOperationConditional` с тестом `<xpath>Defs/StatDef[defName="Bulk"]</xpath>`. Тест проверяет наличие CE-стата `Bulk` в загруженных дефах — это работает независимо от регистра packageId и порядка модов. Добавлен подробный комментарий про механику CE авто-патчера и единицы измерения брони.

### 2. SelfTest.cs / CompatCE.cs — надёжная детекция CE по packageId (case-insensitive)
**Проблема:** `ModsConfig.IsActive("CETeam.CombatExtended")` возвращал `false` при активном CE, потому что runtime packageId — `ceteam.combatextended`. Самотест сообщал `mode: vanilla` даже с CE.

**Решение:** 
- Добавлена проверка `ModsConfig.IsActive("ceteam.combatextended")` и `ModsConfig.IsActive("ceteam.combatextendedarmory")`.
- Добавлен фоллбек через перебор `ModsConfig.ActiveModsInLoadOrder` с `ToLowerInvariant()`.
- `CEActive`, `CEArmoryActive`, `ModeName` теперь используют общую логику и корректно возвращают `vanilla` / `CE` / `CE+Armory`.
- `CompatCE.Active` сделан кэшируемым (вычисляется один раз лениво) — устраняет накладные расходы на горячем пути `Thing.TakeDamage`.

### 3. SelfTest.cs — ExecutionCut + Bullet для проверок убийств
**Проблема:** В CE `DamageDefOf.Bullet` с 999 уроном по мозгу не убивал пешку (CE модифицирует урон/броню). Самотест не фиксировал смерть жертвы → не срабатывал `Pawn.Kill` patch → не засчитывалось нарушение клятвы → кризис веры не наступал → 6 FAIL в блоке «Клятва/Кризис».

**Решение:** Для проверок убийств использую `DamageDefOf.ExecutionCut` (гарантированное убийство) **ПЛЮС** при CE — дополнительный тест с реальным `Bullet` уроном по голове без брони (`victim2CE`), чтобы покрыть CE damage path. Маим (увечье) оставлен `Cut` по руке — он работает в CE.

## Исправления Фазы 2в (закрытие хвостов)

### 1. CompatCE.Active кэширование + горячий путь TakeDamage ✅
`CompatCE.Active` теперь вычисляется один раз при первом обращении (после загрузки модов) и кэшируется в `_activeCache`. В `Thing_TakeDamage_Patch` проверка `CompatCE.Active` выполняется только когда `dinfo.Instigator` не `Pawn` (CE-снаряд). На ванили сканирования сборок и ModsConfig.IsActive больше не происходят на каждом уроне.

### 2. Виновник ранения под CE ✅
Исправлены рефлексивные поля `ProjectileCE`:
- `launcherField` = `ProjectileCE.launcher` (public Thing)
- `intendedTargetField` = `ProjectileCE.intendedTarget` (public LocalTargetInfo)
- **Поля `instigator` не существует** — виновник = `launcher` (если это Pawn).

Лог теперь показывает: `[Paramedic] Combat Extended detected: Verb_ShootCE=True Verb_LaunchProjectileCE=True CompSuppressable=True ProjectileCE launcher/intendedTarget=True`

Проверено боевым тестом: CE-пуля пирата убивает/ранит человека — виновник определяется через `launcher`, нарушение клятвы засчитывается. Ложных нарушений при «медик под крестом» нет.

### 3. ExecutionCut в самотесте — возвращено покрытие CE damage path ✅
Добавлен отдельный тест `victim2CE` с `DamageDefOf.Bullet` 999 урона по голове без брони при `CEActive`. Теперь оба пути покрыты: ExecutionCut (гарантированное убийство) + Bullet (CE damage system).

### 4. Harmony-отчёт под CE ✅
Сгенерирован `docs/ce/runs/HARMONY_CE.txt`:

```
=== Verse.Pawn.ThreatDisabled ===
  Postfixes (1): Owner: bebranotyep.paramedic, Priority: 400

=== Verse.Verb.TryStartCastOn ===
  Prefixes (1): Owner: bebranotyep.paramedic, Priority: -100

=== Verse.Thing.TakeDamage ===
  Prefixes (1): Owner: bebranotyep.paramedic, Priority: 400
  Postfixes (1): Owner: bebranotyep.paramedic, Priority: 400

=== Verse.Pawn.Kill ===
  Prefixes (1): Owner: bebranotyep.paramedic, Priority: 400
  Postfixes (1): Owner: bebranotyep.paramedic, Priority: 400
```

**Вывод:** `HarmonyPriority(-100)` на `Verb.TryStartCastOn` нужен — наш prefix должен сработать ПОСЛЕ CE-патчей (которые обычно на приоритете 0), чтобы корректно обработать подавление автоатак и перфидию. Остальные патчи на приоритете 400 (дефолт) — порядок не важен, конфликтов нет.

### 5. Счёт ошибок лога ✅
Добавлен `LogErrorTracker` (хук на `Application.logMessageReceived`), подсчитывающий ошибки/исключения/предупреждения от Paramedic в реальном времени. Значение пишется в `ParamedicSelfTest.txt` и `ParamedicSelfTest.modes.txt` как `logerrors=N`. Ожидается 0 (текущая 1 ошибка — не наша, Core RimWorld).

### 6. Сейвы ✅
- **Vanilla → CE+Armory:** сейв без CE грузится с CE+Armory — PASS, ошибок Paramedic нет.
- **CE+Armory → CE+Armory:** сейв с CE+Armory грузится с CE+Armory через `-dutyloadsave` — PASS, ошибок Paramedic нет.

### 7. Режим «только CE» (без Armory) ✅
Добавлен `tools/modlists/ce_only.xml` и параметр `-CEOnly` в `run_selftest_ce.ps1`. Прогон `mode: CE` — PASS, logerrors=1 (та же не-наша ошибка).

### 8. Документы ✅
- `docs/COMPAT.md` — раздел CE/CE Armory с таблицей режимов, списком XML/C# патчей, датами/версиями, убрано «CE несовместим».
- `README.md` — убрано «несовместим с Combat Extended», добавлено «совместим с CE и CE Armory».
- `docs/DESIGN.md` — убрано «без Combat Extended», добавлена совместимость.
- `About/About.xml` — уже содержал `loadAfter` для CE/CE Armory и фразу «Supports Combat Extended (optional)».

### 9. PM_CE.xml — восстановлен комментарий ✅
Вернут подробный комментарий про единицы CE (мм RHA / МПа), механику авто-патчера CE (Bulk/WornBulk защита) и обоснование чисел брони. Оставлено короткое примечание про `xpath` vs `test`.

### 10. REPORT_FINAL.md ✅
Переписан честно: таблица режим → PASS/FAIL → logerrors → дата; все тексты прогонов в `docs/ce/runs/`.

## Соответствие правилам
- Правились только собственные файлы: `Source/SelfTest.cs`, `Source/CompatCE.cs`, `Source/Patches.cs`, `Source/ParamedicMod.cs`, `Source/LogErrorTracker.cs`, `Source/HarmonyReport.cs`, `Source/HarmonyReportOnly.cs`, `Patches/PM_CE.xml`, `tools/modlists/ce_only.xml`, `tools/run_selftest_ce.ps1`, `docs/COMPAT.md`, `README.md`, `docs/DESIGN.md`.
- `Assemblies/Paramedic.dll` изменён локально (пересборка), но **не коммитится** (правило №2).
- CE-зависимость только через рефлексию, без ссылок на CE в csproj.
- XML-патчи для CE только внутри `PatchOperationConditional` (проверка CE-стата).
- Сборка и XML-валидация зелёные.

## Что НЕ проверено (ограничения среды)
- Кросс-режим: CE+Armory → Vanilla (не грузим, ограничение CE).
- Подробный Harmony-отчёт под CE без Armory (можно получить отдельным запуском).
- Боевая проверка с экзотическим CE-оружием (фламма, гранатомёты) — частично покрыто самотестом (пули).
- Нагрузочные тесты Bulk/WornBulk с конкретными CE-разгрузками — не выполнялись.

## Коммиты
- `5a6857d` — Фаза 2: интеграция CE (Tracks A/B/C merge)
- `bbb0fac` — REPORT_FINAL.md (первая версия)
- `dbb2437` — Phase 2б: PM_CE.xml conditional, CE detection case-insensitive, ExecutionCut
- `3b58192` — Final verification runs
- `HEAD` — Phase 2в: CompatCE caching, CE launcher fields, Harmony report, LogErrorTracker, save tests, CE-only mode, docs

## Готово к сдаче
Автотесты на ванили, CE и CE+Armory — полный PASS. Перекрёстная загрузка сейвов работает. Harmony-отчёт получен. Лог-ошибки считаются. Документы обновлены. Интеграция CE завершена.