# Трек B — C#: боевая и медицинская логика под CE. Ветка/worktree: `ce/code`

Создай worktree из `ce/audit`: `git worktree add ..\ai-rimworld-wt\CE_B -b ce/code ce/audit`.
Прочитай `docs/ce/00_RULES.md`, `docs/ce/AUDIT.md` и (приоритетнее AUDIT) `docs/ce/AUDIT_ADDENDUM.md` (лежит НЕ закоммиченным в `C:\ai-rimworld-paramedic\docs\ce\`).

**Твои файлы:** `Source/*.cs` КРОМЕ `SelfTest.cs` и `ShotTest.cs`; новый `Source/CompatCE.cs`.
**НЕ трогай:** `Defs/**`, `Patches/**`, `About/**`, `tools/**`.

Задачи:
1. `Source/CompatCE.cs`: `static class CompatCE` с флагом `Active` (`ModsConfig.IsActive("CETeam.CombatExtended")`) и кэшируемыми рефлексивными поисками типов/методов CE, которые мягко проваливаются в лог, а не падают.
2. Пройди каждый наш Harmony-патч (таблица в `docs/COMPAT.md`) и по `AUDIT.md` реши, работает ли он с CE как есть. Найденные проблемы исправь (или добавь отдельный патч под CE-метод: `Prepare()` возвращает false без CE). Особенно:
   - крик (`Shout.cs`): `ThreatDisabled` + `Verb.TryStartCastOn` против `Verb_ShootCE`, подавление CE (`CompSuppressable`), сброс цели врага — должно работать как с CAI;
   - клятва (`OathUtility.cs`): виновник ранения/убийства от CE-снарядов, взрывов, ударов; шальная пуля по союзнику не должна давать ложное увечье;
   - карман и переноска (`Gear.cs`, `ParamedicData.cs`, `StatPart_ParamedicCarrying`): не конфликтуют с лимитом Bulk/Weight CE;
   - `Ability.StartCooldown`, `TendUtility.DoTend` — совместимость с медикаментами CE.
3. Новые настройки только если без них никак; новые ключи в EN/RU Keyed добавляй в конец существующих файлов (минимальный diff, чтобы не конфликтовать с треком A).
4. При активном CE один раз при старте пиши в лог `[Paramedic] Combat Extended detected: …`; сбой любого CE-хука — понятная ошибка, не краш.
5. `dotnet build` чистый, без новых warnings по твоим файлам.

Отчёт: `docs/ce/REPORT_B.md` — что изменено и почему; список мест, где CE-поведение нельзя проверить без запуска игры.
