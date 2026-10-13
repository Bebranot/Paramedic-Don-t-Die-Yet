# Трек B — отчёт по C# совместимости с Combat Extended

## Что сделано

### 1. Новый файл: `Source/CompatCE.cs`
Создан класс `CompatCE` — мягкий слой совместимости с CE только через рефлексию:
- `Active` — проверка `ModsConfig.IsActive("CETeam.CombatExtended")`
- `Init()` — вызывается из `ParamedicMod` конструктора, кэширует типы/методы CE (`Verb_ShootCE`, `Verb_LaunchProjectileCE`, `CompSuppressable`, `ProjectileCE` с полями `launcher`/`instigator`, `CE_WeaponInfo.GetWeaponInfo`)
- Публичные безопасные методы:
  - `IsCEVerb(Verb)` — true если verb является CE-стрельбой
  - `IsSuppressed(Thing)` — true если на цели есть `CompSuppressable` и она подавлена
  - `GetCEInstigator(Thing)` / `GetCELauncher(Thing)` — извлечение виновника/пусковой установки из CE-снаряда
  - `TryInvokeCE_TryStartCastOn` — вызов CE-версии TryStartCastOn через рефлексию (не используется напрямую, задел)
  - `HasCEWeaponInfo(ThingDef)` — проверка наличия CE-инфо об оружии
  - `IsCEDamageInfo(DamageInfo)` — эвристика: урон от CE-снаряда
- Все ошибки рефлексии пишутся в лог (`Log.Warning/Error`), не роняют игру.
- При старте с активным CE пишется в лог: `[Paramedic] Combat Extended detected: Verb_ShootCE=True Verb_LaunchProjectileCE=True CompSuppressable=True ProjectileCE launcher/instigator=True`

### 2. Обновлён `Source/ParamedicMod.cs`
- Добавлен вызов `CompatCE.Init()` после применения патчей.

### 3. Исправлены Harmony-патчи в `Source/Patches.cs`

#### Крик «Не стрелять!» — `Verb_TryStartCastOn_Patch`
- Добавлен `Prepare()` (всегда true) и `Priority = -100` при активном CE — чтобы наш префикс шёл после возможных CE-префиксов.
- В `Prefix`: ранний возврат `true`, если кастер подавлен (`CompatCE.IsSuppressed(caster)`) — подавляемый враг и так не стреляет.
- Остальная логика (блок выстрела по клетке рядом, вероломство медика, страховка врага) сохранена.

#### Клятва: убийство — `Pawn_Kill_Patch`
- В `Postfix`: если `dinfo.Instigator` не Pawn, пробуем получить виновника через `CompatCE.GetCEInstigator` / `GetCELauncher` (поля `instigator`/`launcher` у `ProjectileCE`).
- Это покрывает смерть от CE-снарядов, где инстигатором может быть снаряд, а не пешка.

#### Клятва: увечье — `Thing_TakeDamage_Patch`
- В `Prefix` и `Postfix`: аналогично — если `dinfo.Instigator` не Pawn, пробуем достать атакующего через `CompatCE.GetCEInstigator` / `GetCELauncher`.
- Запись в `lastHarm` и вызов `OathUtility.Notify_Violation` теперь работают с CE-снарядами.

#### Остальные патчи
- `Pawn_ThreatDisabled_Patch`, `EquipmentUtility_CanEquip_Patch`, `TendPatches`, `Pawn_ExitMap_Patch`, `RescueOption_Triage_Patch`, `Ability_StartCooldown_Patch` — работают с CE как есть (нет прямых конфликтов с типами CE).

### 4. Проверки сборки
- `dotnet build Source\Paramedic.csproj -c Release` — **PASS** (0 warnings, 0 errors) в worktree `ce/code`.
- `python tools\validate_xml.py` — **PASS** (19 файлов, 0 ошибок).

---

## Риски / что проверить в игре (требует запуска с CE)

| Область | Риск | Как проверить |
|---------|------|---------------|
| **Крик: ThreatDisabled** | CE может иметь свою логику выбора цели, минующую `ThreatDisabled` (как CAI 5000). | Спавнить врагов с CE-оружием, включить крик — враги не должны выбирать медика целью. |
| **Крик: Verb.TryStartCastOn** | CE `Verb_ShootCE.TryStartCastOn` может вернуть `false` раньше нашего префикса (priority). | Наблюдать за логами `SelfTestComponent.BlockedShots` — должны расти при стрельбе по защищённому медику. |
| **Крик: подавление (CompSuppressable)** | Если враг подавлен CE, он не стреляет — наш код это уважает (ранний возврат). | Проверить, что подавленные враги не получают фолс-позитивов «заблокирован выстрел». |
| **Клятва: убийство/увечье** | CE-снаряды (`ProjectileCE`) имеют `instigator`/`launcher` — мы их читаем. Нужно убедиться, что поля заполнены корректно для всех типов урона (пули, осколки, фламера,近战). | Ранить/убить пешку CE-оружием от имени парамедика — должно сработать нарушение клятвы. |
| **Клятва: «шальная пуля»** | Ванильный код считает увечье по `MissingPartsCommonAncestors` + `HediffComp_GetsPermanent`. CE может добавлять свои перманентные хедиффы. | Проверить, что увечье от CE-раны (например, перелом от тяжелого калибра) засчитывается. |
| **Карман/инвентарь (SyncPocket)** | CE учитывает вес/объём предметов в инвентаре (Bulk). Наш «карман» только меняет `inventoryStock.count` (желаемое кол-во). При перегрузке по Bulk пешка может зациклиться на подборе/сбросе. | Нагрузить медика Bulk до лимита, включить смену — следить за зацикливанием работ `PickUp`/`Drop`. |
| **Переноска (StatPart_ParamedicCarrying)** | CE меняет `MoveSpeed` части; наш `StatPart` умножает значение. Должно работать аддитивно. | Проверить скорость переноски раненого на смене / в штанах — бонус должен применяться. |
| **Медикаменты (Stabilize, Adrenaline)** | Наши способности ищут `IsMedicine` / `PM_AdrenalineSyringe` в инвентаре. CE-медицина тоже `IsMedicine=true`. | Использовать стабилизацию/адреналин с CE-медикаментами — расход должен работать. |
| **Совместимость сейвов** | Никаких CE-defName в сохраняемых данных не добавляли. Переход ваниль↔CE не должен ломать сейвы. | Загрузить ванильный сейв с модом → включить CE → загрузить снова (менеджер проверит). |

---

## Не сделано / заведомо не проверяемо без игры

1. **Harmony patch report под CE** — `DutyOptim -dutypatchreport` или флаг `SelfTest` требуют запуска игры с загруженным CE. Менеджер запустит и даст отчёт.
2. **XPath проверка CE-патчей** — `tools\ce_xpath_check.py` (трек A) проверит, что все XML-патчи находят узлы.
3. **Боевая проверка (фаза 2 / трек C)** — 3000 тиков боя безоружного медика под крестом с 3 пиратами (CE-оружие).
4. **Проверка подавления** — `CompSuppressable` поведение в динамике (накопление подавления, сброс).
5. **CE Armory** — специфичные вещи/амуниция (проверка `HasCEWeaponInfo` на их ThingDef'ах).

---

## Файлы в коммите
- `Source/CompatCE.cs` — новый
- `Source/ParamedicMod.cs` — вызов `CompatCE.Init()`
- `Source/Patches.cs` — CE-совместимость в 3 патчах (Shout, Kill, TakeDamage)

Коммит: `ce/code` — «Трек B: C# совместимость с CE (CompatCE, клятва, крик)»