# Трек A — XML-данные и метаданные. Ветка/worktree: `ce/xml`

Создай worktree из `ce/audit` (там уже `AUDIT.md`): `git worktree add ..\ai-rimworld-wt\CE_A -b ce/xml ce/audit`.
Прочитай `docs/ce/00_RULES.md`, `docs/ce/AUDIT.md` и (приоритетнее AUDIT) `docs/ce/AUDIT_ADDENDUM.md` (лежит НЕ закоммиченным в `C:\ai-rimworld-paramedic\docs\ce\`).

**Твои файлы:** `Patches/PM_CE.xml` (новый), при необходимости новые `Defs/**/PM_*CE*.xml`, `About/About.xml`, новые CE-ключи в `Languages/**`, `tools/ce_xpath_check.py` (новый), `tools/validate_xml.py` (если надо поддержать новые файлы).
**НЕ трогай:** `Source/**`, остальные `docs/**` (кроме своего REPORT).

Задачи:
1. `About/About.xml`: убрать `incompatibleWith CETeam.CombatExtended`; в `loadAfter` добавить `CETeam.CombatExtended` и `CETeam.CombatExtendedArmory` (необязательные, только порядок загрузки). Версию не менять.
2. `Patches/PM_CE.xml`: все операции внутри `PatchOperationFindMod` (имя `Combat Extended`). Для каждой из 8 вещей формы (`PM_Apparel_*` в `Defs/ThingDefs/PM_Apparel.xml`) привести статы к CE: убрать/пересчитать ванильные `ArmorRating_*` в единицы CE, добавить `Bulk`, `WornBulk`; для головных и разгрузочных — как у ванильных аналогов в CE; для медицинской разгрузки — осмысленные `CarryBulk`/`CarryWeight` offsets как у CE-разгрузок. Числа — по `AUDIT_ADDENDUM.md` п.1 (раздел 8 AUDIT.md для брони НЕ использовать). РЕШЕНИЕ ВЛАДЕЛЬЦА: броня остаётся лёгкой, примерно как сейчас в ванили (жилет sharp 0.45/blunt 0.15, шлем 0.55/0.20, куртка 0.40/0.15, берет и штаны ≈0.05, рубашка/очки/пояс — 0). Перенеси эти значения в CE-единицы по соотношению к ванильным аналогам в CE (жилет ≈ лёгкий жилет, шлем ≈ простой шлем и т.п.), не усиливай и не обнуляй. Форма — мундир, а не броня: не должна стать лучше CE-аналогов того же уровня. Bulk/WornBulk — лёгкие, чтобы медик не упирался в лимит переноски.
3. Шприц адреналина (`Defs/ThingDefs/PM_Items.xml`) — `Bulk` как у ближайших медпредметов CE. Бонусы `MedicalTendQuality/Speed` не ломать.
4. CE Armory: только если аудит показал конфликт слотов/категорий или обязательные поля — отдельный блок `PatchOperationFindMod` «Combat Extended Armory». Нет конфликтов — не выдумывай.
5. Всё безопасно без CE (ничего не делает) и при повторной загрузке.
6. `python tools\validate_xml.py`. Напиши `tools\ce_xpath_check.py`: читает наши Defs и CE-патчи/Defs и сообщает, какие xpath не нашли узел. Прогони, приложи вывод в отчёт.

Отчёт: `docs/ce/REPORT_A.md` — таблица «вещь → было → стало», допущения.
