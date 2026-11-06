# Выпуск

## Чек-лист перед загрузкой
1. Версия: `About/About.xml` → `<modVersion>`, `Source/Directory.Build.props` → `<Version>`, запись в `CHANGELOG.md`.
2. Текстуры: `python tools/gen_textures.py`. Основы лежат в `tools/_vanilla`; `tools/extract_vanilla.py` читает ресурсы Unity и работает только с игрой 1.6.
3. Автотест: `powershell -ExecutionPolicy Bypass -File tools\run_selftest.ps1` → `PASS`, в логе нет ошибок мода.
4. Сейв: `RimWorld.exe -dutyloadsave=ParamedicSelfTest` (нужен DutyOptim) → 0 ошибок в `DutyOptimLoadSave.txt`.
5. Совместимость (по желанию): то же с CAI 5000 (`krkr.rule56`) в списке модов.
6. Снимки: `RimWorld.exe -quicktest -pmshot` → обновить `docs/ingame_preview.png`.
7. Коммит + тег `vX.Y.Z`.

## Чистая сборка
Steam загружает папку мода целиком, поэтому исходники, tools и docs туда класть не нужно:
```
powershell -ExecutionPolicy Bypass -File tools\make_release.ps1
```
→ `..\Paramedic-release` (About, 1.6, 1.7, LoadFolders.xml, Defs, Languages, Patches, Textures, LICENSE). Файлы `.dds` не копируются.

## Загрузка в Steam Workshop
Пуш в GitHub Мастерскую не обновляет: новая версия попадает туда только после загрузки из игры.

1. Заменить ссылку `RimWorld/Mods/Paramedic` на папку чистой сборки (или скопировать её туда).
2. В игре включить режим разработчика (Настройки → Режим разработчика).
3. Главное меню → Моды → выбрать мод → «Загрузить в Мастерскую» (Upload).
4. Игра создаст `About/PublishedFileId.txt` — скопировать его в репозиторий и закоммитить: дальнейшие
   загрузки обновят тот же предмет.
5. На странице мастерской: описание (EN/RU из About), превью (`About/Preview.png`), теги, видимость.

## Перед публикацией проверить
- Красного креста нет нигде (эмблема — зелёный крест, см. QUESTIONS #21).
- В текстурах нет чужих модов (с 2026-10-12 — только ванильные основы и своё).
- Ключи автотестов (`-pmselftest`, `-pmshot`) в обычной игре ничего не делают.


## Обложка
`python tools/gen_preview.py --assets cover/public` (персонажи), затем в `cover/`: `npm i`,
`npx remotion still Cover ../About/Preview.png` и `npx remotion still CoverNoShout ../docs/Preview_noshout.png`;
пересжать PNG без потерь (Steam: до 1 МБ). Правка вживую: `npx remotion studio`.