# Общие правила для всех сессий (вставляется в начало каждого промпта)

Проект: RimWorld 1.6 мод «Paramedic: Don't Die Yet!» (`C:\ai-rimworld-paramedic`, C# + Harmony + XML). Прочитай `docs/DESIGN.md`, `docs/COMPAT.md`, `docs/TESTING.md`, `Patches/PM_Patches.xml`, `About/About.xml` до начала работы.

Цель: мод должен работать с **Combat Extended** (CE, `CETeam.CombatExtended`) и **Combat Extended Armory** (CE Armory, `CETeam.CombatExtendedArmory`) и при этом **продолжать работать без них** (ваниль не ломаем). Оба мода подписаны и лежат здесь:
- CE: `C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2890901044` (Defs, ModPatches, Assemblies, папки DLC)
- CE Armory: `...\294100\2911110083` (папка `1.6\Defs`)
- Ваниль для справки: `tools\_vanilla` (не в git); DLL игры — `RimWorld\RimWorldWin64_Data\Managed`.
CE читай только как справку; кусками из его кода не копируй.

Правила работы:
1. Работай ТОЛЬКО в своей git-ветке/worktree (указано в промпте) и только со своими файлами («Твои файлы»). Если нужно изменить чужой файл — не меняй, запиши просьбу в `docs/ce/HANDOFF_<трек>.md`.
2. Никогда не коммить `Assemblies/Paramedic.dll` (бинарник, конфликтует): не добавляй его в `git add`, после сборки `git checkout -- Assemblies/Paramedic.dll`.
3. Игру НЕ запускай, если в промпте не сказано иначе (одновременно возможен один процесс; ею управляет менеджер).
4. Текст для игрока: EN в `Languages/English/Keyed`, RU в `Languages/Russian`. UTF-8, комментарии по-русски, как в соседнем коде. Стиль и плотность комментариев — как вокруг.
5. CE-зависимость в C# — только через рефлексию/`ModsConfig.IsActive("CETeam.CombatExtended")`; **ссылки на сборку CE в csproj быть не должно**. XML для CE — только внутри `PatchOperationFindMod` (имя мода `Combat Extended`) или с `MayRequire="CETeam.CombatExtended"`.
6. **Стабильность в обоих режимах — главное требование.** Мод должен одинаково стабильно работать и БЕЗ CE, и С CE (и с CE+CE Armory): без ошибок/красного в логе, без сломанных сейвов. Сейвы без CE → с CE и обратно не должны ломаться из-за нас (не добавляй CE-defName'ы в сохраняемые данные, не меняй defName'ы существующих вещей/хедиффов). Любой CE-код неактивен без CE и не ссылается на его типы напрямую. Проверяй оба режима.
7. Проверки: `dotnet build Source\Paramedic.csproj -c Release` и `python tools\validate_xml.py` проходят.
8. В конце: коммит(ы) на своей ветке с понятным сообщением на русском и краткий отчёт (сделано / не сделано / риски / что проверить в игре) в `docs/ce/REPORT_<трек>.md`.
