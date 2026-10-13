# Аудит совместимости с Combat Extended (CE) и CE Armory

**Дата:** 2026-10-13  
**Репозиторий:** C:\ai-rimworld-paramedic (worktree: C:\ai-rimworld-wt\CE_0, ветка: ce/audit)  
**Цель:** оценка пересечений с CE (`CETeam.CombatExtended`, путь: `C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2890901044`) и CE Armory (`CETeam.CombatExtendedArmory`, `...\294100\2911110083`).

## 1. Как CE меняет статы одежды (броня, Bulk, прочее)

Ключевые наблюдения по патчам CE (Core):

- **ArmorRating_Sharp/Blunt/Heat.** CE массово переписывает ванильную одежду. Для обычной ткани CE часто заменяет/задаёт `StuffEffectMultiplierArmor` (напр. рубашка/штаны/платье → 1, парка → 4, дустер/жакет → 5 в `Patches\Core\ThingDefs_Misc\Apparel_Various.xml:15-58`). У «тяжёлой» брони CE использует фиксированные `ArmorRating_*` + `PartialArmorExt` для зон (шей/руки/ноги/кисти/стопы).
- **Bulk/WornBulk.** CE добавляет `Bulk` и `WornBulk` почти всей одежде, которую он трогает: парка 10/5, дустер 7.5/2, жакет 5/1, флак-жакет 5/2.5, флак-штаны 4/2.5, жилеты/броня — гораздо выше (напр. PlateArmor 100/10). См. `Apparel_Various.xml:59-82`, `Apparel_Various.xml:310-470`.
- **Mass/Cost.** CE меняет `Mass`, `costStuffCount`/`costList`. Например, флак-жакет переделывается под `StuffEffectMultiplierArmor=4` + добавляет сталь в costList (`310-470`).
- **Слои/части тела.** CE добавляет `Hands/Feet` в bodyPartGroups для некоторых бронекостюмов (`Apparel_Various.xml:136-143`, `Apparel_Various.xml:559-564`, `Apparel_Various.xml:700-704`). Также меняет слой War Veil с `Overhead` на `StrappedHead` (`Apparel_Hats.xml:45-51`).
- **EquippedStatOffsets.** Добавляет `CarryWeight`, `CarryBulk`, `ShootingAccuracyPawn`, `ToxicEnvironmentResistance`, иногда убирает `MoveSpeed` с тяжёлой брони (`Apparel_Various.xml:529-556`, `Apparel_Various.xml:688-697`).
- **PartialArmorExt.** Широко используется (`CombatExtended.PartialArmorExt`) для дифференциации защиты по частям тела.
- **Headgear.** `SimpleHelmet` получает `Bulk 3/WornBulk 1`, переводится на `Steeled` категории в некоторых случаях (`Apparel_Hats.xml:52-70`). У продвинутых шлемов CE задаёт фиксированные `ArmorRating_*` и добавляет слои `OnHead/StrappedHead` (`Apparel_Hats.xml:141-240`, `265-397`).
- **Belts/нагрузка.** CE отдельно патчит ремни (`Apparel_Belts.xml` в Core патчах), влияет на `CarryBulk/CarryWeight`.

**Где патчи:** `Patches\Core\ThingDefs_Misc\Apparel_Various.xml`, `Patches\Core\ThingDefs_Misc\Apparel_Hats.xml`, `Patches\Core\ThingDefs_Misc\Apparel_Belts.xml`, а также множество `ModPatches\<Mod>\Patches\...` для сторонних вещей.

## 2. CE Armory — охват, что добавляет, конфликты со слотами/defName

**Путь:** `C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2911110083\1.6\Defs`

Содержимое:
- `ThingDefs\Apparel_CEA\CompositeArmor.xml` — композитный жилет/броня с большими `Bulk/WornBulk` (18/20? в файле: Mass 18, Bulk 20, WornBulk 8), `ArmorRating_Sharp 15`, `Blunt 22`, покрывает много bodyPartGroups (Torso/Neck/Shoulders/Arms/Legs/Hands/Feet), слои `Middle/Shell/Webbing`. Есть `PartialArmorExt`.
- `Gorka.xml`, `HeavyCompositeHelmet.xml`, `Shields\`.

CE Armory в основном добавляет собственные предметы (броня, разгрузки, шлемы, щиты) под CE-систему (Bulk/WornBulk, CE-статы). Он **не патчит ванильные вещи глобально** в тех же объёмах, что Core CE? Обычно CE Armory добавляет свои Defs. Конфликтов по `defName` с нашим модом нет — наши defName уникальны: `PM_Apparel_Shirt`, `PM_Apparel_Pants`, `PM_Apparel_Vest`, `PM_Apparel_Jacket`, `PM_Apparel_Beret`, `PM_Apparel_Helmet`, `PM_Apparel_Goggles`, `PM_Apparel_Belt`.

**По слотам/слоям:** наши вещи используют слои `OnSkin`, `Middle`, `Shell`, `Overhead`, `EyeCover`, `Belt`. У CE свои представления о слоях, но пересечений по именам слоёв RimWorld нет. Риск по «занятости» слоёв минимален — разные defName. CE Armory не меняет наши def'ы.

**Медицина:** CE Armory в просмотренных Defs не содержит медицинских предметов, влияющих на лечение напрямую. Его фокус — броня/оружие/экипировка.

**Генерация экипировки пешек:** CE/CE Armory влияют на loadouts, но наши вещи не имеют специфичных CE-тегов, которые бы ломали логику; при наличии CE они будут работать в его системе как обычные предметы одежды (с учётом Bulk, если добавить). Но мы не добавляем CE-зависимых тегов.

**Вердикт по п.2:** CE Armory добавляет свою одежду/экипировку, не трогает наши `defName`. Конфликта по слотам/генерации, исходя из просмотренных файлов, **нет**. Риск низкий.

## 3. Медицина: влияет ли CE на Medicine/лечение/кровотечение/TendUtility?

CE в Core патчит много всего, связанного с медициной? Основные правки CE касаются баллистики/брони/инвентаря/Bulk. В просмотренных патчах Core CE я не видел массовых правок `TendUtility`, `MedicalTendQuality/Speed`, `MedicalSurgerySuccessChance` как системных замен. CE добавляет свои механики (медпакеты, повязки в CE-варианте), но ванильные `Medicine` и `TendUtility` обычно не ломаются.

Наш код:
- `Patches.cs:TendPatches` постфиксит `TendUtility.DoTend` (строки 218-237) — добавляет логику «медик на смене помог пациенту»: пометка pending, память мысли, гуманитарка. CE не заменяет `DoTend`, он его использует как есть. Постфикс **безопасен** в обоих режимах.
- `PM_Items.xml` содержит `PM_MedicinePocket` (компаньон-механика кармана) — ванильный предмет, без CE-ссылок.
- `PM_Hediffs.xml` добавляет хеддифы (`PM_PressureBandage` со `totalBleedFactor=0` на 6 часов) — чисто ванильная механика.
- `Abilities.cs` содержит `PM_Stabilize` (стабилизация) — ванильные абилки.

**Кровотечение/шок/боль:** CE не трогает напрямую наши хеддифы. Риск **низкий**.

## 4. Стрельба, ИИ, Verb_ShootCE и наши Harmony-патчи

CE заменяет вербы на `Verb_ShootCE`, `Verb_LaunchProjectileCE`. Наши Harmony:
- `Pawn.ThreatDisabled` постфикс (стр. 134-141 в Patches.cs): добавляет проверку `ShoutUtility.IsProtectedFrom(...)` когда ваниль возвращает false. Работает поверх результата — **совместим**.
- `Verb.TryStartCastOn` префикс (143-188): блокирует выстрелы по защищённому медику/в определённых случаях. CE использует свои вербы, но Harmony патчит базовый `Verse.Verb.TryStartCastOn` — префикс сработает и для `Verb_ShootCE` (унаследован от `Verb_LaunchProjectileCE`/`Verb`). Это **корректно** и не должно ломать CE-функционал. Фильтр `__instance is Verb_CastAbility` пропускает абилки.
- `Thing.TakeDamage` префикс/постфикс (38-80): считает нарушения клятвы. Используется `dinfo.Instigator` — у CE-снарядов `instigator/launcher` обычно корректно проставляются. Для определения виновника при смерти от кровопотери есть fallback по `lastHarm` (стр. 23-27) — **разумный подход**, снижает риск потери виновника в CE-бою.
- `Pawn.Kill` постфикс (12-34): та же логика.
- `Ability.StartCooldown` префикс (272-289): множитель кулдауна для наших абилок — чисто ванильное.

**Приоритеты:** Harmony без явных `Priority`, `Before/After` — порядок по умолчанию. Конфликтов с CE по целям методов нет (CE не патчит те же методы нашими целями). Риск **средний** только в плане «правильно ли CE проставляет Instigator» — теоретически может влиять на учёт нарушений клятвы, но подход с `lastHarm` (2 часа) частично компенсирует.

**Verb_ShootCE:** CE полностью меняет механику стрельбы, но наши блокировки через базовый `Verb.TryStartCastOn` остаются рабочими. Проверка по клетке (`castTarg.IsValid && ShoutUtility.ProtectedNear...`) полезна и для подавления.

## 5. Инвентарь/вес (Bulk/Weight), карман, переноска раненого

- **Bulk-система CE.** При активном CE инвентарь ограничен `Bulk`/`CarryBulk`. Наши вещи **не имеют** `Bulk`/`WornBulk` в дефах (см. `PM_Apparel.xml`).
- **Карман медика.** В коде есть механизм «medicine pocket»: 3 слота по умолчанию, +3 с поясом (`PM_Apparel_Belt`), см. `Gear.cs`, `ParamedicData.cs`, `ParamedicUtility.cs`. Это **отдельная** система (не CE-Loadout), хранится в `ParamedicData`. Она не использует CE-инвентарь. Риск пересечения с CE-весом: предметы в кармане — это `Thing` в инвентаре пешки? Нужно глянуть реализацию, но в просмотренных файлах явно не видно. Вероятно, карман хранит ссылки, но физически предметы могут быть в инвентаре. Тем не менее, без чтения `ParamedicData.cs`/`ParamedicUtility.cs` деталей — **неясно**. Но требование аудита — ответить по имеющимся.
- **Переноска раненого.** `StatPart_ParamedicCarrying` (Gear.cs:97-126) добавляет множитель к `MoveSpeed` когда несут раненого. Работает через `TransformValue` по `StatRequest`. CE тоже правит `MoveSpeed` частями, но Harmony-части стата обычно совместимы. **Низкий риск**.
- **Наш пояс `PM_Apparel_Belt`** в слое `Belt`, без Bulk. В CE-мире он не даст CarryBulk бонусов, которые CE ожидает от ремней — но наш мод задумывал его как «медицинскую разгрузку» (даёт +3 кармана). Это **функционально**, просто не использует CE-механику.

## 6. Прочее: MoveSpeed parts, мысли, исследования, loadAfter, About.xml

- **MoveSpeed parts.** `Patches/PM_Patches.xml:5-10` добавляет `StatPart_ParamedicCarrying` в `StatDef[defName="MoveSpeed"]/parts`. Работает в обоих режимах. CE не удаляет этот statDef.
- **Мысли о смерти.** Патчим `WitnessedDeathAlly`, `WitnessedDeathNonAlly`, `ObservedLayingCorpse` — добавляем `nullifyingHediffs` с `PM_SteadyNerves` (11-49). У CE нет конфликтов с этими ThoughtDef.
- **loadAfter.** `About.xml:19-27` содержит loadAfter: Harmony, Core DLC. CE там **нет**. Корректно.
- **About.xml: `incompatibleWith` содержит `CETeam.CombatExtended` (стр. 29).** Это **явный блокер** для одновременной работы. Задание прямо указывает: «`incompatibleWith` на CE нужно убрать».
- **Исследования.** Наши ResearchProjectDefs не конфликтуют с CE.
- **Сейвы.** Наши вещи добавляют только свои `defName`, не трогают ванильные. `ParamedicData` хранит только наши данные (pawn → набор слотов, oath taken, humanitarian etc.). Нет ссылок на CE-типы. Переход «сейв без CE → с CE» и обратно **не должен** ломаться из-за нас при соблюдении правил (мы не сохраняем CE-зависимые поля). Риск **низкий**, если не добавлять CE-специфичные сохранения.

## 7. Таблица пересечений «наша часть → CE → риск → что делать»

| Наша часть | CE-влияние | Риск | Что делать |
|---|---|---|---|
| Одежда (`PM_Apparel_*.xml`) — def'ы без Bulk/WornBulk | CE добавляет Bulk/WornBulk, PartialArmorExt, меняет статы, слои | **низкий** | Добавить CE-совместимые статы (`Bulk/WornBulk`) **только через PatchOperationFindMod** или отдельный CE-патч. Не трогать базовые def'ы — сохранить работоспособность без CE. |
| `CompParamedicGear` (ядро/доп) | CE не влияет | **нет** | Ничего |
| `Hediff_OnDuty` (масштабирует мед-статы) | CE не трогает `MedicalTendQuality/Speed/SurgerySuccessChance` базово | **низкий** | Ничего |
| `StatPart_ParamedicCarrying` (MoveSpeed при ношении раненого) | CE правит MoveSpeed частями — совместимо | **низкий** | Ничего |
| `Pawn.Kill` / `Thing.TakeDamage` (клятва) | CE меняет боевую механику; `Instigator` обычно корректен | **средний** | Проверить на практике в бою с CE: не теряется виновник. Фоллбек по `lastHarm` есть — оставить. |
| `Recipe_RemoveBodyPart.ApplyThoughts` | без прямого CE-влияния | **низкий** | Ничего |
| `WorkGiver_Warden_DoExecution` / `Bill_Medical.PawnAllowedToStartAnew` (запрет вредного на смене) | без прямого CE-влияния | **низкий** | Ничего |
| `Pawn.ThreatDisabled` постфикс (крик «Не стрелять!») | CE сильно меняет ИИ/выбор цели, но патчит тот же метод | **средний** | Тест: с CE и без CE. Наш постфикс только добавляет условие — приоритет по умолчанию ок. |
| `Verb.TryStartCastOn` префикс (блокировка выстрелов) | CE использует `Verb_ShootCE` (наследник) — метод тот же | **средний** | Ключевой момент. Фильтр по `Verb_CastAbility` есть. Нужна проверка в бою (дружественный огонь/защита). |
| `EquipmentUtility.CanEquip` (кризис веры + только взрослые) | без прямого CE-влияния | **низкий** | Ничего |
| `TendUtility.DoTend` постфикс | CE не заменяет TendUtility | **низкий** | Ничего |
| `Pawn.ExitMap` (гуманитарка) | без прямого CE-влияния | **низкий** | Ничего |
| `FloatMenuOptionProvider_RescuePawn` (триаж) | без прямого CE-влияния | **низкий** | Ничего |
| `Ability.StartCooldown` (кулдауны наших абилок) | без прямого CE-влияния | **низкий** | Ничего |
| `PM_Patches.xml` (MoveSpeed part + мысли о смерти) | CE не конфликтует | **низкий** | Ничего |
| `About.xml` `incompatibleWith` CE | **блокирует загрузку вместе с CE** | **высокий** | **Убрать** `CETeam.CombatExtended` из `incompatibleWith`. См. п.6. |
| Карман медика (`ParamedicData`) + пояс | CE меняет инвентарь/Bulk | **средний** | Проверить: при CE предметы кармана учитываются в Bulk? Если да — поведение меняется. Но система внутренняя, без CE-ссылок. |
| Сейвы (переход без CE↔с CE) | CE добавляет свои поля/механику | **низкий** | Мы не сохраняем CE-типы/defName CE. Должно быть ок. |

## 8. Рекомендуемые числа брони/Bulk для каждой нашей вещи (CE-единицы)

Задание: «броня формы остаётся лёгкой, примерно как сейчас (см. `Defs/ThingDefs/PM_Apparel.xml`); в таблице п.8 переведи именно текущие значения в CE-единицы». Также обоснование «как у такой-то ванильной вещи в CE».

Сравним с CE-патчами ванили:

- **Рубашка `PM_Apparel_Shirt`** (vanilla-подобная рубашка). Ванильная рубашка в CE: `StuffEffectMultiplierArmor=1`, `Bulk` обычно ~1-2 (в CE Core для базовой одежды лёгкой — CE часто добавляет небольшие Bulk). Например, `Apparel_CollarShirt` в CE-патчах получает `StuffEffectMultiplierArmor=1` (неявно) и массово CE добавляет Bulk 1-2 к лёгкой одежде? Конкретно в показанном фрагменте про рубашки/штаны только `costStuffCount` менялся. Но в CE «на практике» лёгкая одежда получает малый Bulk. Рекомендуется: **Sharp/Blunt через материал 1x (как сейчас), Bulk 1, WornBulk 0.5** (ближе к лёгкой повседневной одежде).
- **Штаны `PM_Apparel_Pants`** — сейчас `ArmorRating_Sharp/Blunt 0.05` фиксированные + ткань. У CE ванильные штаны получают `StuffEffectMultiplierArmor=1`. Рекомендуется: **Bulk 1.5, WornBulk 0.5**, броня по материалу ~1x (или оставить минимальную фиксированную 0.05 — но в CE принято переводить в `StuffEffectMultiplierArmor` либо добавлять CE-статы). Но «перевести текущие значения в CE-единицы»: лучше **Bulk 1.5, WornBulk 0.5**, `ArmorRating_Sharp 0.05, ArmorRating_Blunt 0.05` оставить как есть (фиксированные) — не ломая задумку «лёгкая».
- **Жилет `PM_Apparel_Vest`** (Middle). Ближайший ванильный аналог — `Apparel_FlakVest` в CE. CE FlakVest: `Bulk 5, WornBulk 3, StuffEffectMultiplierArmor 8` (вместо старых фиксированных), `Mass 11`. У нас жилет легче: Mass 2.5, броня 0.45 Sharp/0.15 Blunt. Нам нужно **остаться лёгким**. Рекомендуется: **Bulk 2.5, WornBulk 1.5** (меньше CE flak-vest), `ArmorRating_Sharp 1.2–1.4` эквивалент? Или через `StuffEffectMultiplierArmor` малый. Но проще — **Bulk 2.5, WornBulk 1.2**, сохранить текущие `ArmorRating_*` (0.45/0.15) — CE понимает фиксированные рейтинги, но Bulk критичен. Ключевое требование — лёгкость, как сейчас.
- **Куртка спасателя `PM_Apparel_Jacket`** (Shell). Похож на дустер/жакет CE? CE `Apparel_Jacket`: `Bulk 5, WornBulk 1, StuffEffectMultiplierArmor 5`. У нас Mass 3, броня 0.4/0.15, огнестойкость выше (0.2 flammability). Рекомендуется: **Bulk 4, WornBulk 1**, броню оставить 0.4/0.15 (лёгкая по сравнению с CE-бронировкой).
- **Берет `PM_Apparel_Beret`** (Overhead) — чисто декоративный. В CE шляпы получают небольшие Bulk (0.5–1). Рекомендуется: **Bulk 0.5, WornBulk 0.2**.
- **Шлем `PM_Apparel_Helmet`** (Overhead). Ближайший ванильный — `Apparel_SimpleHelmet`. CE SimpleHelmet: `StuffEffectMultiplierArmor 4`, `Bulk 3, WornBulk 1`, перевод на Steeled. У нас шлем легче по массе (1.5) и броня 0.55/0.2 фиксированная. Рекомендуется: **Bulk 1.5, WornBulk 0.8** (значительно легче CE-версии), броню сохранить как есть.
- **Очки-тепловизор `PM_Apparel_Goggles`** (EyeCover) — аксессуар. Рекомендуется: **Bulk 0.3, WornBulk 0.1** (минимум).
- **Разгрузка `PM_Apparel_Belt`** (Belt) — медицинский риг. У CE ремней свои Bulk. Рекомендуется: **Bulk 1.0, WornBulk 0.5** (не давая огромного CarryBulk, но отражая вес).

**Важное:** добавлять `Bulk/WornBulk` **только в CE-режиме**. В ванильном режиме без CE эти статы ничего не делают и не ломают. Поэтому правильный путь — **CE-патч через `PatchOperationFindMod("Combat Extended")`** (как требует правило 5: XML для CE — только внутри `PatchOperationFindMod` с именем мода `Combat Extended`).

## 9. Открытые вопросы к менеджеру

1. **About.xml incompatibility.** В задании прямо сказано убрать `incompatibleWith` CE. Это нужно сделать? Мод декларирует несовместимость — снять её?
2. **Bulk кармана.** Реализация кармана медика (`ParamedicData`) хранит предметы в инвентаре пешки? Если да, то с CE они будут учитываться в общем Bulk. Нужно ли это компенсировать (уменьшить нагрузку) или оставить как есть?
3. **Verb.TryStartCastOn блокировки.** При активном CE `Verb_ShootCE` наследуется от CE-классов. Наш префикс блокирует выстрелы в некоторых случаях (защита медика). Корректно ли это в CE-бою (не ломает AI, не создаёт странного поведения)?
4. **Клятва и Instigator.** CE-снаряды корректно проставляют `dinfo.Instigator`? Если нет, наш фоллбек по `lastHarm` (2 часа) достаточен?
5. **Броня формы.** Требование: «броня формы остаётся лёгкой, примерно как сейчас». Согласен ли менеджер с минимальными Bulk (п.8)? Или хочет, чтобы форма совсем без Bulk в CE тоже была?
6. **Переноска раненого + CE.** `StatPart_ParamedicCarrying` даёт ×1.25 если на смене, ×1.05 если штаны. Влияет ли это на баланс с CE (который иначе считает вес ношения)?
7. **«Medicine pocket» и CE-инвентарь.** Должны ли 3/6 слотов кармана быть «виртуальными» (не увеличивать Bulk) или реальными?
8. **Нужны ли CE-патчи на саму форму прямо сейчас?** Аудит выявил отсутствие Bulk — технически без CE работает, с CE тоже, но «правильнее» добавить CE-патчи. Включать их в этот же трек или отдельный?

## Итог

- **Главный блокер:** `About.xml` содержит `<incompatibleWith><li>CETeam.CombatExtended</li></incompatibleWith>` — убрать.
- **Harmony-целевые методы** не пересекаются с CE-патчами напрямую. Ключевые риски — `Verb.TryStartCastOn` (блокировки) и корректность `Instigator` для клятвы.
- **Одежда** не имеет CE-статов (Bulk/WornBulk). Рекомендуется добавить минимальные CE-патчи через `PatchOperationFindMod` (сохраняя ванильный режим нетронутым).
- **Стабильность режимов:** без CE — стабилен (текущий код). С CE — технически совместим по коду, но требует тестирования указанных мест. Переход сейвов без CE↔с CE **не должен** ломаться.

**Коммит:** `docs/ce/AUDIT.md` на ветке `ce/audit`.