# Трек C — тестовая обвязка под CE. Ветка/worktree: `ce/test`

Создай worktree из `ce/audit`: `git worktree add ..\ai-rimworld-wt\CE_C -b ce/test ce/audit`.
Прочитай `docs/ce/00_RULES.md`, `docs/ce/AUDIT_ADDENDUM.md` (в `C:\ai-rimworld-paramedic\docs\ce\`, не закоммичен), `docs/TESTING.md`, `Source/SelfTest.cs`, `Source/ShotTest.cs`, `tools/run_selftest.ps1`. Посмотри, как устроены прогоны совместимости в `C:\ai-rimworld-compat` и `C:\RimWorldCompatRuns` (набор модов, `ModsConfig.xml`, отчёты) и используй тот же подход.

**Твои файлы:** `Source/SelfTest.cs`, `Source/ShotTest.cs`, `tools/**` (новые `tools/run_selftest_ce.ps1`, `tools/modlists/ce.xml` и т.п.), `docs/TESTING.md`.
**НЕ трогай:** остальные `Source/*.cs`, XML мода.

Задачи:
1. Набор модов для CE-прогона, порядок: Harmony → Core/DLC → CE (`CETeam.CombatExtended`) → CE Armory (`CETeam.CombatExtendedArmory`) → Paramedic. `tools/run_selftest_ce.ps1` — как `run_selftest.ps1`, но: (а) подменяет `ModsConfig.xml` на CE-набор с бэкапом в `C:\RimWorldConfigBackup` и ОБЯЗАТЕЛЬНО возвращает оригинал в `finally`; (б) разворачивает мод в `...\RimWorld\Mods\Paramedic` так же, как существующий скрипт; (в) печатает ошибки лога, отфильтровав шум CE.
2. Адаптируй `SelfTest.cs` к CE: статы по `GetValueUnfinalized`; у пиратов в бою должны быть CE-оружие и **патроны** (иначе не стреляют и боевой тест ложно зелёный); тест «безоружный медик под крестом среди 3 пиратов» считает и CE-снаряды (`ProjectileCE`), и требует число выстрелов > 0 (иначе тест недействителен). Новые пункты: форма надевается без нарушения слотов; у вещей формы заданы Bulk/WornBulk; карман не превышает лимит; шприц и повязка влезают в Bulk-лимит.
3. Определяй CE через `ModsConfig.IsActive`; без CE тест ведёт себя как раньше (регрессия на ваниль остаётся).
3a. Стабильность в обоих режимах — отдельный критерий: отчёт теста должен явно печатать режим («vanilla» / «CE» / «CE+Armory»), число ошибок лога, и итог PASS/FAIL по каждому режиму. Добавь проверку «сейв, созданный без CE, не даёт ошибок при загрузке с CE» там, где это возможно проверить скриптом (или опиши ручной шаг в отчёте).
4. Скрипт только собирает и готовит. Запуск игры делает менеджер; в отчёте дай точную команду.
5. В `docs/TESTING.md` — раздел «Прогон с Combat Extended».

Отчёт: `docs/ce/REPORT_C.md`.
