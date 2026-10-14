# Совместимость

## Harmony-патчи мода

| Метод | Тип | Зачем |
|---|---|---|
| `Pawn.Kill` | Prefix+Postfix | клятва: убийство; «не спас» |
| `Thing.TakeDamage` | Prefix+Postfix | клятва: увечье; кто последним ранил |
| `Recipe_RemoveBodyPart.ApplyThoughts` | Postfix | клятва: изъятие здорового органа |
| `WorkGiver_Warden_DoExecution.JobOnThing` | Postfix | на смене не казнит |
| `Bill_Medical.PawnAllowedToStartAnew` | Postfix | на смене не делает вредных операций |
| `Pawn.ThreatDisabled` | Postfix | крик: враг не выбирает медика целью |
| `Verb.TryStartCastOn(LocalTargetInfo, LocalTargetInfo, …)` | Prefix | крик: страховка от выстрела (в т.ч. по клетке рядом), подавление автоатак медика, вероломство |
| `EquipmentUtility.CanEquip(…, out string, bool)` | Postfix | кризис веры, только взрослые |
| `TendUtility.DoTend` | Postfix | спасённые, мысль пациента, гуманитарка |
| `Pawn.ExitMap` | Prefix | гуманитарка |
| `FloatMenuOptionProvider_RescuePawn.GetSingleOptionFor` | Postfix | сортировка |
| `Ability.StartCooldown` | Prefix | перезарядки от комплекта и звания |

XML-патчи: `StatDef MoveSpeed/parts` (+ `StatPart_ParamedicCarrying`), `nullifyingHediffs` у
`WitnessedDeathAlly`, `WitnessedDeathNonAlly`, `ObservedLayingCorpse`.

Патчи ставятся по одному: сбой одного пишет ошибку в лог, остальные работают.

## Проверено (2026-10-12, RimWorld 1.6.4871, все DLC)

| Набор | Автотест | Бой: безоружный медик под крестом, 3 пирата с винтовками, 3000 тиков |
|---|---|---|
| Harmony + DutyOptim 0.3.2-beta | PASS ×2 | 26–39 выстрелов, 0 прицельных попаданий (1 шальная пуля в соседа) |
| + CAI 5000 (`krkr.rule56`, мастерская 3673768803) | PASS ×4 | 42–62 выстрела, 0 прицельных попаданий |

Сейв после автотеста грузится без ошибок (`-dutyloadsave`). Отчёт DutyOptim `-dutypatchreport` (с CAI):
пересечений наших патчей с чужими — **нет**.

Найдено и исправлено в ходе проверок: подавляющий огонь CAI по клетке (не по пешке) обходил защиту —
теперь уважающий враг не стреляет и по клетке рядом с медиком. Кровожадные пираты стреляют по медику — так задумано.

### Почему крик работает с CAI 5000 и DutyOptim
- CAI 5000 частично выбирает цели сам (`ThingComp_CombatAI.TryRangedReaction` пишет `mindState.enemyTarget`
  напрямую, мимо `ThreatDisabled`). Поэтому защита раз в 30 тиков сбрасывает у врагов цель/прицел/работу на
  медика, а префикс `Verb.TryStartCastOn` не даёт начать выстрел.
- DutyOptim `AttackTargetCacheModule` кэширует `BestAttackTarget`, но фильтр `ThreatDisabled` срабатывает
  внутри поиска, и бой это подтвердил.

## Не поддерживается
- Чужие расы (HAR) — форма нарисована под тела людей (Male/Female/Thin/Hulk/Fat).

## Combat Extended (CE) и CE Armory — полностью поддерживается

### Версии и даты проверки
- **RimWorld 1.6.4871 rev590** (все DLC: Royalty, Ideology, Biotech, Anomaly, Odyssey)
- **Combat Extended 16.7.3.0** (Steam workshop 2890901044)
- **Combat Extended Armory** (Steam workshop 2911110083)
- **Harmony 2.2.2** (Steam workshop 2009463077)
- Дата полного прогона: **2026-10-14**

### Что работает
| Режим модов | Автотест (`-pmselftest`) | Live fire | Ошибки лога (Paramedic) |
|---|---|---|---|
| Vanilla | PASS | 24–48 выстрелов, 0 прицельных попаданий | 0 |
| CE only | PASS | 39–116 CE projectiles, 0 прицельных попаданий | 1* |
| CE + Armory | PASS | 42–170 CE projectiles, 0 прицельных попаданий | 1* |

\* Единственная ошибка — `Failed to resolve text. Trying again with English` (ArgumentNullException в Core RimWorld, не Paramedic).

### Что патчится для CE
**XML (Patches/PM_CE.xml, внутри `PatchOperationConditional` по стату `Bulk`):**
- Все 8 предметов формы получают `Bulk` + `WornBulk` → CE авто-патчер (ApparelAutoPatcher) их пропускает, броня не переписывается.
- Броня: `PM_Apparel_Vest` (Sharp 3.6 / Blunt 5.0), `PM_Apparel_Pants` (Blunt 0.18), `PM_Apparel_Jacket` (Sharp 0.5 / Blunt 0.75), `PM_Apparel_Beret` (Sharp 0.04 / Blunt 0.07), `PM_Apparel_Helmet` (Sharp 4 / Blunt 6).
- `PM_Apparel_Belt` получает `equippedStatOffsets.CarryBulk = 6`.
- `PM_AdrenalineSyringe` получает `Bulk = 0.05`.

**C# (через рефлексию, без ссылок на CE в csproj):**
- `CompatCE.Active` — кэшируемая детекция CE по `packageId` (`ceteam.combatextended`) и сборке `CombatExtended`.
- `CompatCE.GetCELauncher` / `GetCEInstigator` / `GetCEIntendedTarget` — чтение полей `ProjectileCE.launcher` и `ProjectileCE.intendedTarget`.
- `CompatCE.IsCEVerb` — детект `Verb_ShootCE` / `Verb_LaunchProjectileCE`.
- `CompatCE.IsSuppressed` — проверка `CompSuppressable.IsSuppressed`.
- Патчи на `Thing.TakeDamage`, `Pawn.Kill`, `Verb.TryStartCastOn`, `Pawn.ThreatDisabled` используют эти хуки для корректной работы клятвы, крика и перфидии под CE.

### Проверено (2026-10-14)
- Клятва: маим → убийство → животное → кризис веры (3 нарушения) — всё засчитывается корректно.
- Крик «Не стрелять!»: защита работает против CE-стрельбы (`Verb_ShootCE`, `Verb_LaunchProjectileCE`), перфидия фиксируется.
- Live fire: 3 пирата с CE Assault Rifle + ammo (556x45 FMJ), 3000 тиков ультрафаст — медик не получает прицельных попаданий.
- Перекрёстная загрузка сейвов: vanilla → CE+Armory (OK), CE+Armory → CE+Armory (OK).
- Harmony-отчёт (`-dutypatchreport`): наши 4 патча на горячих путях (`ThreatDisabled`, `TryStartCastOn`, `TakeDamage`, `Kill`) — приоритеты 400 (postfix/prefix) и -100 (TryStartCastOn prefix), конфликтов с CE-патчами нет.

### Известные нюансы
- CE-патч формы применяется через `PatchOperationConditional` (проверка стата `Bulk`), а не `FindMod` — работает независимо от регистра packageId.
- `ExecutionCut` в автотесте для гарантированного убийства; дополнительно тестируется `Bullet` без брони на голове для покрытия CE damage path.
- Единственная ошибка в логе — не связанная с модом `ArgumentNullException` в текстовой системе Core.
