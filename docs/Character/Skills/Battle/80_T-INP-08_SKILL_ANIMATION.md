# T-INP-08: Skill Animation via AnimatorOverrideController

> **Подсистема:** Skills → Animation → SkillAnimationPlayer
> **Период:** 2026-06-28/29
> **Статус:** ✅ Завершено (Pass 4)
> **Коммит:** `2e71c30`
> **Исправленные файлы:**
> - `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs` (v2, +120 строк)
> - `Assets/_Project/Scripts/Skills/SkillInputService.cs` (+3 строки)
> - `Assets/_Project/Animations/Combat/Male/HumanM_Model@Standing Melee Attack 360 Low.fbx` (force-reimport)

---

## TL;DR

Cистема для проигрывания кастомных AnimationClip из `SkillNodeConfig.attackClip` через Animator state "Skill" с AnimatorOverrideController. Три клика в игре → анимация скилла (2.5с) → RPC на 60% → Idle.

---

## 1. Архитектура

```
SkillInputService.TryActivate(slot)
  │
  ├─ clip-path: SkillAnimationPlayer.Play(skillConfig, slot)
  │    ├─ GetOrCreateOverride(clip) → AnimatorOverrideController
  │    ├─ _animator.runtimeAnimatorController = overrideController
  │    ├─ Deferred SetTrigger("SkillPlay") в LateUpdate
  │    └─ Update(): watchdog + impact (60%) + restore
  │
  └─ FireImpactRpc() → TryActivate(slot, skipAnimation: true)
       └─ RPC only (без SetTrigger)
```

## 2. Найденные проблемы и фиксы

### P1: `AnimatorOverrideController.this[string]` в Unity 6 работает только по имени клипа

**Симптом:** custom clip не проигрывается, играется дефолтная атака
**Корень:** `overrideCtrl["Skill"] = clip` тихо игнорируется — в Unity 6 `this[string]` ищет **только по имени AnimationClip**, имена состояний не работают
**Фикс:** `overrideCtrl["HumanM@Attack1H01_L"] = clip` (оригинальный клип из Skill state)
**Поле:** `_defaultSkillClipName` с автодетектом в Editor (`AutoDetectDefaultSkillClip()`)

### P2: SetTrigger теряется при смене runtimeAnimatorController

**Симптом:** триггер "SkillPlay" не доходит до Animator
**Корень:** `SetTrigger()` сразу после подмены контроллера — контроллер ещё не инициализирован
**Фикс:** отложенный вызов в `LateUpdate()` через флаг `_triggerScheduled`

### P3: Дефолтный удар после скилла

**Симптом:** после окончания skill анимации сразу играется дефолтная атака
**Корень:** `FireImpactRpc()` → `TryActivate(slot, skipAnimation: true)` проходил в legacy-путь и вызывал `_animator.SetTrigger("Attack")`. Триггер висел в Animator, срабатывал после `Restore()`
**Фикс:** guard `!skipAnimation` на legacy SetTrigger

### P4: FBX импортирован как Generic, AnimatorController — Humanoid

**Симптом:** сломанная анимация (руки прижаты, кручение не туда, уход под пол)
**Корень:** Generic-кости (`Rig/B-root/B-hips/...`) насильно применялись к Humanoid-аватару
**Фикс:** переключение `ModelImporter.animationType = Human` + force-reimport

### P5: applyRootMotion был false

**Симптом:** персонаж не вращается во время 360-атаки, нижняя часть "зафиксирована"
**Корень:** `_animator.applyRootMotion` был `false` по дефолту в инспекторе
**Фикс:** `_animator.applyRootMotion = true` на время каста (восстанавливается в Restore)

### P6: Position guard — защита от ухода Y

**Симптом:** Root Motion Y + CharacterController gravity уводили персонажа вниз
**Фикс:** `LateUpdate()` — если `transform.position.y < _castStartY`, возвращаем на `_castStartY`

---

## 3. Ключевые классы

| Класс | Файл | Роль |
|---|---|---|
| `SkillAnimationPlayer` | `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs` | Проигрывание AnimationClip из SkillNodeConfig.attackClip |
| `SkillInputService` | `Assets/_Project/Scripts/Skills/SkillInputService.cs` | Точка входа: нажатие → RPC + анимация |
| `SkillNodeConfig` | `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` | SO с attackClip, isActive, aoeFormula |
| `SkillAnimationEventPassthrough` | `Assets/_Project/Scripts/Skills/SkillAnimationEventPassthrough.cs` | Прокси Animation Events из child Animator в root SkillAnimationPlayer |
| `PlayerAnimation.controller` | `Assets/_Project/Animations/PlayerAnimation.controller` | Core AnimatorController: 14 states, 12 params, state "Skill" |

---

## 4. Требования к Animator Controller

- state "Skill" с motion placeholder (любой clip)
- AnyState → Skill по trigger "SkillPlay"
- Skill → Idle по exit (0.95, 0.2s duration)
- Trigger "SkillPlay" объявлен

---

## 5. Требования к SkillNodeConfig.attackClip

| Параметр | Требование | Проверка |
|---|---|---|
| Animation Type | **Humanoid** (не Generic) | `ModelImporter.animationType == Human` |
| Avatar | Создан из этого же FBX или совместимый | `avatar.isValid && avatar.isHuman` |
| Root Motion | Желательно включён (для вращения/движения) | Position guard защищает Y |
| Длина | Любая (watchdog считает clip.length / speed + buffer) | `_restoreTimeBuffer = 0.5s` |
| Animation Events | Опциональны (OnSkillAnimationEnd, OnAttackImpact) | Без них — работа через normalizedTime (60%) |

### Импорт нового FBX для skill-анимации

1. Выделить FBX в Project Window
2. Inspector → Rig → Animation Type = **Humanoid**
3. Avatar Definition = **Create From This Model**
4. **Apply**
5. После Apply — проверить Avatar Mapping (должны быть зелёные человечки на всех обязательных костях)

---

## 6. Watchdog и тайминги

| Механизм | Когда срабатывает | Что делает |
|---|---|---|
| Watchdog | `elapsed >= _castMaxDuration` | Принудительный Restore() (safety net) |
| Exit transition | `IsInTransition && nextState != Skill` | Restore() — анимация закончена |
| NormalizedTime >= 1 | `si.normalizedTime >= 1.0f` | Restore() (редкий случай без transition) |
| Impact timing | `normalizedTime >= 0.6f` | FireImpactRpc() |
| Animation Event (OnSkillAnimationEnd) | Из клипа | Restore() (более точный, но не обязательный) |
| Animation Event (OnAttackImpact) | Из клипа | FireImpactRpc() (более точный impact timing) |

---

## 7. Комментарий про FBX-импорт

**Важно:** `.meta` файл FBX был УЖЕ настроен на Humanoid (`animationType: 3`). 
Проблема была в **stale Library** — Unity закешировала Generic-импорт при первой загрузке
и не перечитывала .meta. Force-reimport через `ModelImporter.SaveAndReimport()`
решил проблему.

**Если после клонирования проекта или сброса Library анимация скилла сломается:**
1. Выделить `.fbx` → Inspector → Rig → убедиться Animation Type = Humanoid
2. Если нет — переключить и Apply
3. Если да — нажать кнопку **Apply** (форсирует реимпорт)

---

## 8. Future work

- Анимация с кораблём (ship combat) — отдельный AnimatorController
- Blend между skill-анимацией и locomotion (upper-body layer)
- Multiple skill-состояний в AnimatorController (Skill1, Skill2, ...) вместо одного override
- Animation Events в SO (не в FBX) — дизайнер задаёт тайминги в SkillNodeConfig
