# ФИНАЛЬНЫЙ ОТЧЁТ ФАЗЫ 2 — ИНТЕГРАЦИЯ CE (включая Фазу 2б — доведение до зелёного)

**Ветка:** ce/integrate  
**Дата:** 2026-10-14

## Краткий итог
Выполнена интеграция треков A/B/C. Исправлены все FAIL автотестов на ванили и CE+Armory.
Пройдены проверки: `dotnet build Source\Paramedic.csproj -c Release` — PASS (0 ошибок, 0 предупреждений), `python tools\validate_xml.py` — PASS (20 файлов), `python tools\ce_xpath_check.py` — PASS (все 19 xpath PM_CE.xml найдены, всем 8 вещам формы добавлены Bulk+WornBulk, CE-статы Bulk/WornBulk/CarryBulk определены).

## Исправления Фазы 2б (доведение до зелёного)

### 1. PM_CE.xml — PatchOperationConditional вместо PatchOperationFindMod
**Проблема:** `PatchOperationFindMod` не срабатывал, потому что в runtime все `packageId` приводятся к нижнему регистру (`ceteam.combatextended`), а патч проверял `CETeam.CombatExtended`. CE-патч не применялся → 8 предметов формы не получали `Bulk/WornBulk` → 2 FAIL в самотесте.

**Решение:** Заменил на `PatchOperationConditional` с тестом `<xpath>Defs/StatDef[defName="Bulk"]</xpath>`. Тест проверяет наличие CE-стата `Bulk` в загруженных дефах — это работает независимо от регистра packageId и порядка модов.

### 2. SelfTest.cs / CompatCE.cs — надёжная детекция CE по packageId (case-insensitive)
**Проблема:** `ModsConfig.IsActive("CETeam.CombatExtended")` возвращал `false` при активном CE, потому что runtime packageId — `ceteam.combatextended`. Самотест сообщал `mode: vanilla` даже с CE, а CE-специфичные проверки (снаряды, Bulk) работали некорректно.

**Решение:** 
- Добавлена проверка `ModsConfig.IsActive("ceteam.combatextended")` и `ModsConfig.IsActive("ceteam.combatextendedarmory")`.
- Добавлен фоллбек через перебор `ModsConfig.ActiveModsInLoadOrder` с `ToLowerInvariant()`.
- `CEActive`, `CEArmoryActive`, `ModeName` теперь используют общую логику и корректно возвращают `vanilla` / `CE` / `CE+Armory`.

### 3. SelfTest.cs — ExecutionCut вместо Bullet для проверок убийств
**Проблема:** В CE `DamageDefOf.Bullet` с 999 уроном по мозгу не убивал пешку (CE модифицирует урон/броню). Самотест не фиксировал смерть жертвы → не срабатывал `Pawn.Kill` patch → не засчитывалось нарушение клятвы → кризис веры не наступал → 6 FAIL в блоке «Клятва/Кризис».

**Решение:** Для проверок убийств (жертва2, жертва3, животное) использую `DamageDefOf.ExecutionCut` — ванильный дамаг для мгновенного казни, который CE не снижает. Маим (увечье) оставлен `Cut` по руке — он работает в CE.

## Результаты финальных прогонов

### Ваниль (чистый ModsConfig без CE)
```
Paramedic self-test: PASS
mode: vanilla
```
Все ~45 проверок — OK. CE-проверки корректно пропущены (статы Bulk/WornBulk не найдены). Бой идёт с ванильными `Verb_MeleeAttackDamage`.

### CE+Armory (ModsConfig с CE + CE Armory)
```
Paramedic self-test: PASS
mode: CE+Armory
```
Все проверки — OK:
- `CE: kit apparel has Bulk  [8 defs]` — OK
- `CE: kit apparel has WornBulk  [8 defs]` — OK
- `CE: 6 medicine + syringe fit CarryBulk  [6.05 / 56]` — OK
- Блок «Клятва/Кризис»: маим, убийство, животное, кризис веры — все OK
- Live fire: `CE: raiders have CE weapons + ammo  [3/3]`, `CE: live fire happened  [CE projectiles=42]`, `no aimed hits on medic` — OK
- CE-глаголы: `Verb_MeleeAttackCE`, `Verb_LaunchProjectileCE`, `ProjectileCE` — детектятся через рефлексию

### Player.log (CE run)
```
[Paramedic] Combat Extended detected: Verb_ShootCE=True Verb_LaunchProjectileCE=True CompSuppressable=True ProjectileCE launcher/instigator=False
[Paramedic] self-test written to ...
```
Нет ошибок патчинга, нет NRE.

## Соответствие правилам
- Правились только собственные файлы: `Source/SelfTest.cs`, `Source/CompatCE.cs`, `Patches/PM_CE.xml`.
- `Assemblies/Paramedic.dll` изменён локально (пересборка), но **не коммитится** (правило №2).
- CE-зависимость только через рефлексию, без ссылок на CE в csproj.
- XML-патчи для CE только внутри `PatchOperationConditional` (проверка CE-стата).
- Сборка и XML-валидация зелёные.

## Что НЕ проверено (ограничения среды)
- Кросс-режим: сейв без CE → загрузка с CE и обратно (логика перехода не менялась).
- Подробный Harmony-отчёт под CE (`-dutypatchreport`) — требует отдельного запуска.
- Боевая проверка с экзотическим CE-оружием (фламма, гранатомёты) — частично покрыто самотестом (пули).
- Нагрузочные тесты Bulk/WornBulk с конкретными CE-разгрузками — не выполнялись.

## Коммиты
- `5a6857d` — Integration fixes: Track B + CE detection + patch + selftest
- `bbb0fac` — REPORT_FINAL.md
- Текущий коммит (HEAD) — Phase 2б fixes: PM_CE.xml conditional, CE detection case-insensitive, ExecutionCut in selftest

## Готово к сдаче
Автотесты на ванили и CE+Armory — полный PASS. Критичные замечания трека B устранены, статические проверки пройдены. Интеграция CE завершена.