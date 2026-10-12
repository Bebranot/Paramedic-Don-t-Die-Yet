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
- Combat Extended — несовместим (другая модель брони/стрельбы).
- Чужие расы (HAR) — форма нарисована под тела людей (Male/Female/Thin/Hulk/Fat).
