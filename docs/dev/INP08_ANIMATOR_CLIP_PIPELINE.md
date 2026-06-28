# T-INP-08 — Skill Animation via AnimatorOverrideController (data-driven, designer-friendly)

> **Статус:** Design finalized, pending implementation
> **Подсистема:** Character Progression → Skills → AOE formulas + animation
> **Цель:** Дизайнер перетаскивает `AnimationClip` в поле `SkillNodeConfig.attackClip` → клип проигрывается
> из idle/locomotion на NetworkPlayer. **Без правки Animator Controller per skill.**

---

## Проблема (фиксируем что было)

`SkillNodeConfig.attackAnimationTrigger` — поле `string` хранит имя Animator-параметра.
`SkillInputService.TryActivate` делает `_animator.SetTrigger(trigger)`.

**Не работает out-of-the-box** потому что:
- В Animator Controller игрока нет параметра с этим именем → Unity warning, анимация не играет.
- Даже если параметр есть — нужно вручную добавлять State + Transition в Animator на каждый скилл.

Дизайнер не может «просто перетащить .fbx» — нужно ещё и Animator Controller патчить.

## Решение: AnimatorOverrideController + one shared "Skill" state

Подход:
1. **Один раз** (при инициализации префаба) добавляем в Animator Controller игрока:
   - Параметр: `Trigger SkillPlay`.
   - State: `Skill` (Motion = любой placeholder-клип, его всё равно override'ит код).
   - Transition `AnyState → Skill`: Has Exit Time = OFF, Condition = `SkillPlay`.
   - Transition `Skill → Locomotion`: Has Exit Time = ON (по концу клипа), Duration = 0.2с (плавный crossfade).
2. **В SkillNodeConfig** добавляем поле `AnimationClip attackClip` — дизайнер тащит .fbx/anim напрямую.
3. **На TryActivate** код создаёт `AnimatorOverrideController`, подменяет Motion в состоянии `Skill` на нужный клип, ставит `SkillPlay` триггер. Animator проигрывает нужный клип. По окончании — сам возвращается в Locomotion (через Exit Time).
4. **На Animation Event `OnAttackImpact`** (60% клипа) — дёргаем RPC на сервер.

## Дизайн-решения (зафиксировано с юзером)

| # | Вопрос | Решение |
|---|---|---|
| 1 | Lower body во время Skill | **Фулл-стоп locomotion** (всё тело играет Skill-клип) |
| 2 | Blend обратно в Locomotion | **Плавный crossfade** через Transition Duration = 0.2с |
| 3 | Модификатор скорости | Поле `attackClipSpeed` в SO (default 1.0, range 0.5–2.0) |
| 4 | Timing RPC | **Animation Event `OnAttackImpact` на 60% клипа** (RPC уходит в момент удара) |
| 5 | Cancel | **Ждём окончания** текущего Skill, потом играем следующий |
| 6 | Event не найден | **Fallback на немедленный RPC** + warning в Console |

## Что НЕ делаем

- ❌ НЕ переписываем контракт `attackAnimationTrigger` (string) — он остаётся как legacy/escape hatch.
- ❌ НЕ создаём новый Animator Controller — патчим существующий (на префабе NetworkPlayer) минимально (1 state, 1 param, 2 transitions).
- ❌ НЕ пишем свой PlayableGraph — `AnimatorOverrideController` это built-in Unity механизм.
- ❌ НЕ трогаем NPC-анимации (отдельный Animator Controller, отдельный пайплайн).

## Файлы

### EDIT
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` (+15 строк):
  - `public AnimationClip attackClip;` (прямая ссылка)
  - `public float attackClipSpeed = 1.0f;` (модификатор)
  - Сохранить legacy `attackAnimationTrigger` как `string` (override path: если attackClip == null, используем trigger).
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` (+25 строк):
  - В `InitializeSkillInputService` подписаться на Animation Event `OnAttackImpact` через `AnimationClipPlayable` НЕТ — через `AnimationEvent`-callback на клипе (Unity автоматически вызывает метод с именем Event'а на скрипте Animator'а).
  - Реализовать `public void OnAttackImpact()` — вызывает `SkillInputService.Instance.TryActivate(SkillInputSlot.Primary)` или передаёт event обратно в сервис.
  - В `_animator = GetComponentInChildren<Animator>()` — убедиться что компонент есть.

### NEW
- `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs` (~120 строк, runtime):
  - Компонент на NetworkPlayer (AddComponent в `InitializeSkillInputService`).
  - State machine: `Idle → Casting → Cooldown → Idle`.
  - API: `Play(SkillNodeConfig skill, Vector3 origin)` — создаёт/кеширует `AnimatorOverrideController`, подменяет Motion, выставляет `SkillPlay` trigger.
  - Кеширует override'ы по `(skill.attackClip.GetInstanceID())` чтобы не создавать на каждый каст (GC reduction).
  - В `Update`: если Animator в состоянии `Skill` и текущий normalizedTime >= 1.0 → переход в Cooldown state, через `_animationCooldown` (защита от re-trigger).
  - Public event `OnAttackImpact(SkillNodeConfig skill)` — вызывается из NetworkPlayer.OnAttackImpact (Animation Event).

### EDIT (префаб)
- `Assets/_Project/Prefabs/NetworkPlayer.prefab` (через MCP `manage_gameobject` НЕ работает для Animator Controller — нужно вручную в Editor, либо через MCP `execute_code` + `UnityEditor.Animations.AnimatorController` API):
  - Добавить параметр `SkillPlay` (Trigger).
  - Добавить state `Skill` с placeholder motion (любой короткий клип).
  - Transition AnyState → Skill (Condition: SkillPlay, HasExitTime: OFF).
  - Transition Skill → Locomotion (HasExitTime: ON, Duration: 0.2).
  - На placeholder motion добавить Animation Event `OnAttackImpact` на 60% (для тестового клипа; для реальных клипов дизайнер добавляет сам).

### NEW (опционально, для тестов)
- `Assets/_Project/Editor/Skills/AnimationEventHint.cs` (~30 строк, Editor-only):
  - `[InitializeOnLoad]` — логирует warning если у `SkillNodeConfig.attackClip` нет Animation Event `OnAttackImpact`.
  - Не блокер, а подсказка дизайнеру.

## Verification plan

1. `refresh_unity` → 0 errors.
2. NetworkPlayer.prefab: Animator Controller имеет параметр `SkillPlay` + state `Skill` (через MCP read на префабе или Editor).
3. Поставить `attackClip = HumanM@CombatDeath01` на `Skill_Combat_BasicStrike.asset`.
4. Запустить Play Mode → StartHost → нажать Ctrl+ЛКМ → должна проиграться анимация смерти (фулл-стоп locomotion).
5. Через 0.2с после конца клипа — плавный возврат в Locomotion.
6. С 60% клипа (или сразу, если event нет) — RPC уходит на сервер.
7. Спам нажатий → второй Skill не начинается, пока не закончится первый (Cancel = wait).
8. Console: `[SkillAnimationPlayer] Playing combat_basic_spinstrike with clip=HumanM@CombatDeath01 speed=1.0`.
9. Если attackClip=null → fallback на legacy `attackAnimationTrigger` путь, Console warning.

## Open questions

1. Префаб `NetworkPlayer.prefab` правка Animator Controller — делать через MCP или документировать инструкцию для ручной правки в Editor? (Зависит от того, есть ли у MCP тулинг для AnimatorController API. Если нет — документируем.)
2. Animation Event `OnAttackImpact` — должен ли работать для **всех** атакующих клипов, или только для тех, что помечены определённым тегом в импортере? (Пока — все, дизайнер добавляет Event на клип вручную в Animation window.)

## Связь с предыдущими тикетами

- **T-INP-04 (Animation hooks)**: введён `attackAnimationTrigger` string — этот тикет расширяет его до `AnimationClip` reference, оставляя string как fallback.
- **T-INP-06 (AOE debug)**: orthogonal — AOE-визуализация работает независимо от того, какой animation trigger/clip используется.
