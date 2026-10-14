# Фаза 2б — довести до зелёного (одна сессия, `C:\ai-rimworld-paramedic`, ветка `ce/integrate`)

Прочитай `docs/ce/00_RULES.md`, `docs/ce/REPORT_FINAL.md`. Менеджер проверил REPORT_FINAL и НЕ принял его: журнал `ParamedicSelfTest.modes.txt` (рядом с `Player.log`) показывает 5 прогонов mode=vanilla — все FAIL, и 1 прогон mode=CE — FAIL. Отчёт этого не объясняет. Ни слова не приукрашивай: в отчёте только то, что видел в выводе игры.

Правила этой фазы:
- «Ожидаемый сбой» не существует. Каждый FAIL — либо баг мода, либо баг теста; назови какой и почему, с цитатой из `ParamedicSelfTest.txt`/`Player.log`.
- Чини причину. Тест не ослабляй, пункты не удаляй.
- Игру запускай по одному процессу. После каждого прогона копируй `ParamedicSelfTest.txt` в `docs/ce/runs/<режим>_<время>.txt` (закоммить).

Шаги:
1. **Ваниль.** Запусти `tools\run_selftest.ps1`. Выпиши ВСЕ строки FAIL дословно. Выясни причину (сравни с последним зелёным прогоном 2026-10-12: `docs/TESTING.md`; при необходимости сделай `git stash`/временный checkout `main` и прогони ваниль на нём, чтобы отделить регрессию от окружения). Добейся полного PASS (~45 пунктов, как раньше) на коде `ce/integrate`.
2. **CE: Bulk/WornBulk.** Выясни, применился ли `Patches/PM_CE.xml` в игре с CE: в логе игры включи/найди результат патча (например, прогони `Verse.PatchOperation` вручную через SelfTest-диагностику, либо выведи `statBases` вещи `PM_Apparel_Vest` из `DefDatabase` при CE-запуске). Если не применился — найди причину (имя в `FindMod`, порядок загрузки в `tools/modlists/ce.xml`, ошибка xpath, патч не загружен потому что мод стоит до CE). Исправь. Пункты `CE: kit apparel has Bulk/WornBulk` должны стать PASS без правки теста.
3. **CE: виновник ранения.** В логе `ProjectileCE launcher/instigator=False` — поля снаряда не найдены. Прочитай реальные поля `CombatExtended.ProjectileCE` (имена/типы) в `CombatExtended.dll` (ilspycmd, если есть; либо рефлексия + лог списка полей) и поправь `CompatCE`. Если запасной путь не нужен (CE кладёт стрелка прямо в `DamageInfo.Instigator`) — докажи это тестом (убийство/ранение CE-пулей даёт нарушение клятвы), и тогда удали мёртвый код.
4. **CE: Harmony.** Получи реальный список патчей на `Pawn.ThreatDisabled`, `Verb.TryStartCastOn`, `Thing.TakeDamage`, `Pawn.Kill` при CE (`Harmony.GetPatchInfo` → владельцы, префиксы/постфиксы, приоритеты). Положи в `docs/ce/HARMONY_CE.txt`. Вывод: нужен ли нам порядок; обоснуй `HarmonyPriority(-100)` или убери.
5. **Прогоны:** `run_selftest_ce.ps1 -Run -NoLoadSave` для CE и для CE+Armory (если скрипт не умеет CE+Armory отдельно от CE — доработай: набор модов уже содержит Armory, убедись по `mode:` в отчёте, что режим печатается как CE+Armory). Все три режима должны быть PASS, `logerrors` от Paramedic = 0.
6. **Сейвы.** Выполни проверку загрузки сейва (`-dutyloadsave=ParamedicSelfTest`) для каждого режима и перекрёстную: сейв без CE → загрузка с CE. CE-сейв без CE не грузим (ограничение CE).
7. Верни в `Patches/PM_CE.xml` большой поясняющий комментарий (про единицы CE и авто-патчер), который удалила фаза 2 (см. `git show ceec0ee:Patches/PM_CE.xml`).
8. Обнови `docs/COMPAT.md`, `README.md`, `docs/DESIGN.md` (убрать «CE несовместим», добавить раздел CE/CE Armory с проверенным).
9. Перепиши `docs/ce/REPORT_FINAL.md` заново: таблица режим → PASS/FAIL → число ошибок лога, и честный список «не проверено». Коммит.

Если любой шаг не получается — остановись и опиши точный блокер (вывод, строки лога), не обходи.
