# 08 — Whole-Model Swap (замена M/Ж модели целиком)

> **Тикет:** T-CUS-03
> **Дата:** 2026-08-14
> **Файлы:** `CharacterCustomisationApplier.cs`, `NetworkPlayer.cs`, `CharacterEquipmentVisualApplier.cs`, `SkillAnimationPlayer.cs`, `NetworkPlayer.prefab`

## 1. Проблема

Предыдущая версия `ApplyBodyType` меняла только `SkinnedMeshRenderer.sharedMesh` + `avatar` +
`AnimatorController`, оставляя старые кости скелета (`Visual_Model`).

Это работает, пока скелет M и F идентичен по порядку/количеству костей. Но FBX, прошедший
Blender-раундтрип (`Assets/Generated_Models/Testing blender/testing.fbx`), имеет **другой**
порядок костей влияния и их число (52 → 51, `hasExtraRoot: 1`, лишний root-узел
`Human_DummyModel_M`). `SkinnedMeshRenderer.bones[]` при этом остаётся от старой модели,
и скиннинг «рвёт меш на куски».

**Ключевой факт:** humanoid-ретаргет покрывает **анимации**, но **не пересчитывает**
`SkinnedMeshRenderer.bones[]`. Поэтому менять только `sharedMesh` между разными скелетами нельзя.

## 2. Решение

Модель тела меняется **целиком** — как единый юнит:
`SkinnedMeshRenderer + кости (Rig) + Animator + avatar`.

Порядок/количество костей FBX больше не важен: каждая модель приходит со **своим** скелетом
и **своим** avatar, а humanoid (`Animator.GetBoneTransform(HumanBodyBones.X)`) резолвит кости
по аватару.

## 3. Целевая структура префаба

```text
NetworkPlayer (root)
  Visual_Model   — СТАБИЛЬНЫЙ пустой root, держит localScale (пропорции рост/полнота)
    Body         — swappable модель (nested HumanM_Model.fbx): Animator + avatar + controller + SMR + Rig
```

- `Visual_Model` **не уничтожается** при смене пола — `localScale` пропорций переживает swap.
- `Body` полностью заменяется при смене `bodyType`.
- Имя `Visual_Model` сохранено специально: `NetworkPlayer.HideGhostVisualIfNeeded()` ищет его через `transform.Find("Visual_Model")`.

## 4. ApplyBodyType (новый контракт)

```
Instantiate(targetModel, _visualRoot)          // новое тело целиком
  → name = "Body"
  → прямые ссылки newAnimator / newSMR с НОВОГО инстанса
  → newAnimator.runtimeAnimatorController = targetCtrl
  → newSMR.sharedMaterial = targetMaterial
  → SetActive(false) старого body + Destroy(oldBody)   // deferred
  → _currentBody/_animator/_bodyRenderer = новые
  → ResetTrigger по новым параметрам
  → BodySwapped?.Invoke(newAnimator)
```

**Правила:**
- Новые ссылки берутся **напрямую с нового инстанса**, НЕ через `_visualRoot.GetComponentInChildren(...)`
  (старый body ещё жив до конца кадра из-за deferred `Destroy`).
- Старый body гасится сразу (`SetActive(false)`) **до** `Destroy`, чтобы не было кадра с двумя телами.

## 5. Событие BodySwapped

`public event System.Action<Animator> BodySwapped;` — вызывается после замены тела.
Передаёт **новый** Animator (не через поиск по корню).

Подписчики (все на том же GameObject, подписываются в `Awake` — до `OnEnable` applier'а,
чтобы не потерять событие при respawn race):

| Подписчик | Реакция |
|---|---|
| `NetworkPlayer` | `_animator = newAnimator` |
| `CharacterEquipmentVisualApplier` | `_animator = newAnimator; Reapply()` |
| `SkillAnimationPlayer` | `_animator = newAnimator; _originalController = null; _overrideCache.Clear()` |

`NetworkPlayer.OnNetworkSpawn` теперь делает `if (_animator == null) _animator = FindFirstValidAnimator()`
(не перезатирает Animator, полученный от `BodySwapped` при respawn race).

## 6. Верификация (playtest — выполняет пользователь)

1. Подставить `Assets/Generated_Models/Testing blender/testing.fbx` в `_maleModel` префаба →
   меш **не** рвётся на куски.
2. M ↔ F в обе стороны (через CustomisationWindow) — тело/анимации меняются корректно.
3. Экипировка перепривязывается на кости нового скелета (`Reapply`).
4. Скиллы (`SkillAnimationPlayer`) работают после смены пола — override-контроллер не залипает.

## 7. Blender export checklist (для новых моделей)

1. **Humanoid rig обязателен** — Unity должен построить Avatar:
   `Model` → `Rig` → `Animation Type = Humanoid`, `Avatar Definition = Create From This Model`.
   Ключевые кости (Hips/Spine/Chest/…) смаплены зелёным.
2. **Animator должен быть на FBX-root** (Unity добавляет его автоматически при avatarSetup=1).
   Лишний `Dummy`-root внутри допустим, но Animator на корне — надёжнее.
3. **Трансформ корня** — position (0,0,0), rotation identity, scale (1,1,1); модель стоит
   «ногами в 0», forward совпадает с forward префаба.
4. **Scale в Blender** — применять (`Ctrl+A → Scale`) перед экспортом, чтобы не было
   неявного масштаба на root.
5. **Экспорт FBX** — root = identity; `Bake Animation` не требуется (анимации не импортируем).
6. **Имена костей** — не критичны (humanoid резолвит по аватару), но осмысленные имена
   упрощают диагностику.
7. **Материал** — если у модели свой материал, оставить `_maleMaterial`/`_femaleMaterial`
   пустым (null): `ApplyBodyType` сохранит материал модели; иначе задаст явный.
