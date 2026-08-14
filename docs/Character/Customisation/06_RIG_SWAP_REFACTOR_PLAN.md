# План рефакторинга: замена модели тела целиком (humanoid)

> **Подсистема:** Character Customisation
> **Статус:** ✅ реализовано (2026-08-14) — см. `07_RIG_SWAP_IMPLEMENTATION.md`
> **Связанный тикет:** T-CUS-03 (CharacterCustomisationApplier)

## 1. Цель

Сделать так, чтобы в `CharacterCustomisationApplier` можно было назначать **любой humanoid FBX
целиком** (модель + скелет + аватар), а не только меш с идентичным порядком костей.
Убрать хрупкую зависимость от порядка/количества костей, которая ломается при FBX-раундтрипе
через Blender.

## 2. Причина (кратко)

Сейчас `ApplyBodyType` делает только `_bodyRenderer.sharedMesh = targetMesh`, оставляя старые
`SkinnedMeshRenderer.bones[]` и `Animator.avatar`. Это работает **только** если у нового меша
порядок и количество костей совпадают со старым (как у `HumanM` ↔ `HumanF` из одного пака).

Blender-раундтрип (`HumanM_Model.fbx` → Blender → `testing.fbx`) меняет скин-данные:
- порядок костей в `bones[]` другой;
- количество костей влияния 52 → 51;
- появляется лишний root-узел `Human_DummyModel_M`.

Имена костей (`B-hips`, `B-thigh.L`, …) при этом сохраняются, но текущий код на имена не опирается.

**Решение:** подменять модель целиком и ставить новый `avatar` на `Visual_Model.Animator`.
Раз всё Humanoid — анимации ретаргетятся сами, а экипировка резолвится через
`Animator.GetBoneTransform(HumanBodyBones.X)` независимо от имён/порядка костей.

## 3. Текущее состояние (зафиксировано)

Префаб `Assets/_Project/Prefabs/NetworkPlayer.prefab`:

```
NetworkPlayer [NetworkObject, NetworkTransform, NetworkPlayer, CharacterController,
               PlayerInputReader, CharacterEquipmentVisualApplier,
               CharacterCustomisationApplier, PlayerRespawnTracker]
  Visual_Model [Animator]
    HumanM_BodyMesh [SkinnedMeshRenderer]
    Rig
      B-root
        B-hips / B-spine / B-chest / B-neck / B-head / ... / B-thigh.L / ...
        B-spineProxy
```

Сериализованные поля `CharacterCustomisationApplier` на префабе:

| Поле | Значение |
|---|---|
| `_visualRoot` | `Visual_Model` (Transform) |
| `_animator` | `Visual_Model` (Animator) |
| `_bodyRenderer` | `null` (автонайдено в рантайме) |
| `_maleMesh` | `HumanM_BodyMesh` (Mesh) |
| `_femaleMesh` | `HumanF_BodyMesh` (Mesh) |
| `_maleController` | `PlayerAnimation_Default` (AnimatorOverrideController) |
| `_femaleController` | `PlayerAnimation_Female` (AnimatorOverrideController) |
| `_maleMaterial` | `Lit` (Material) |
| `_femaleMaterial` | `Lit` (Material) |

## 4. Изменения

### 4.1 `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs`

- Удалить поля `Mesh _maleMesh`, `Mesh _femaleMesh`.
- Добавить поля `GameObject _maleModel`, `GameObject _femaleModel`.
- Переписать `ApplyBodyType`.

```csharp
[Header("Body models (L1)")]
[Tooltip("Модель целиком (FBX/prefab) для Male. Назначить HumanM_Model.fbx.")]
[SerializeField] private GameObject _maleModel;
[Tooltip("Модель целиком (FBX/prefab) для Female. Назначить HumanF_Model.fbx.")]
[SerializeField] private GameObject _femaleModel;
```

```csharp
private void ApplyBodyType(CharacterBodyType bodyType)
{
    GameObject targetModel = bodyType == CharacterBodyType.Female ? _femaleModel : _maleModel;
    RuntimeAnimatorController targetCtrl = bodyType == CharacterBodyType.Female ? _femaleController : _maleController;
    Material targetMaterial = bodyType == CharacterBodyType.Female ? _femaleMaterial : _maleMaterial;

    if (targetModel == null)
    {
        if (_logWarnings) Debug.LogWarning($"[CharacterCustomisationApplier] {bodyType} model not assigned in Inspector.", this);
        return;
    }
    if (_visualRoot == null)
    {
        if (_logWarnings) Debug.LogWarning("[CharacterCustomisationApplier] _visualRoot not assigned.", this);
        return;
    }

    // 1. Уничтожить текущее тело (SMR + Rig). Animator на Visual_Model остаётся.
    for (int i = _visualRoot.childCount - 1; i >= 0; i--)
    {
        var child = _visualRoot.GetChild(i).gameObject;
        if (Application.isPlaying) Destroy(child);
        else DestroyImmediate(child);
    }

    // 2. Инстанциировать новую модель под Visual_Model.
    GameObject instance = Instantiate(targetModel, _visualRoot, worldPositionStays: false);
    instance.name = targetModel.name;

    // 3. Забрать avatar у вложенного Animator модели и убрать вложенный Animator.
    Avatar newAvatar = null;
    var nested = instance.GetComponentInChildren<Animator>(true);
    if (nested != null)
    {
        newAvatar = nested.avatar;
        if (Application.isPlaying) Destroy(nested);
        else DestroyImmediate(nested);
    }

    // 4. Переключить avatar + controller на главном Animator (Visual_Model).
    if (_animator != null)
    {
        if (newAvatar != null) _animator.avatar = newAvatar;
        if (targetCtrl != null) _animator.runtimeAnimatorController = targetCtrl;
        foreach (var p in _animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger)
                _animator.ResetTrigger(p.nameHash);
        }
    }

    // 5. Перекэшировать renderer + материал.
    _bodyRenderer = _visualRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
    if (targetMaterial != null && _bodyRenderer != null)
        _bodyRenderer.sharedMaterial = targetMaterial;

    // 6. Экипировка была прицеплена к старым костям → переприменить.
    var equip = GetComponent<CharacterEquipmentVisualApplier>();
    if (equip != null) equip.Reapply();

    // 7. Переутвердить пропорции (scale на Visual_Model).
    if (_hasSnapshot)
        ApplyProportions(_currentSnapshot.heightScale, _currentSnapshot.widthScale);

    if (Debug.isDebugBuild)
        Debug.Log($"[CharacterCustomisationApplier] Applied bodyType={bodyType} (model='{targetModel.name}').", this);
}
```

### 4.2 `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs`

Добавить публичный метод полной перепривязки (после смены скелета старые визуалы уже уничтожены
вместе с костями, а diff по `_currentItems` увидел бы «изменений нет»):

```csharp
/// <summary>
/// Полная перепривязка после смены модели тела: сбрасывает кэш слотов
/// и заново спавнит визуалы на костях нового скелета.
/// </summary>
public void Reapply()
{
    DestroyAllVisuals();
    _currentItems.Clear();
    if (_clientState != null && _clientState.CurrentSnapshot.HasValue)
        OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
}
```

### 4.3 `Assets/_Project/Prefabs/NetworkPlayer.prefab`

- Назначить `_maleModel` = `Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx` (GameObject).
- Назначить `_femaleModel` = `Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx` (GameObject).
- Поля `_maleMesh`/`_femaleMesh` исчезают после правки кода.
- `_maleController`/`_femaleController`/`_maleMaterial`/`_femaleMaterial` — без изменений.

### 4.4 Temp editor-скрипты

`Assets/_Editor_Temp/AssignFemaleMesh.cs` и `Assets/_Editor_Temp/RevertFemaleMesh.cs` ссылаются
на serialized-поле `_femaleMesh` (удалено). Это одноразовые temp-скрипты — удалить их либо
обновить на `_femaleModel`.

## 5. Верификация (чек-лист)

- [ ] M↔F swap: меш + скелет + анимации корректны (нет «взрыва» скина).
- [ ] Экипировка перевешивается на новые кости после смены bodyType.
- [ ] Пропорции (`localScale` на `Visual_Model`) работают после swap.
- [ ] Подстановка `testing.fbx` в `_maleModel` (или `_femaleModel`) даёт корректную анимацию —
      это и есть проверка humanoid-retarget на произвольной модели.
- [ ] `SkillAnimationPlayer` продолжает кастить скиллы после swap.
- [ ] Нет ошибок/ворнингов в Console при старте и при переключении пола.

## 6. Rollback

Git revert до коммита с этим изменением. Старые поля `_maleMesh/_femaleMesh` и
`sharedMesh`-логика восстанавливаются. Префаб/контроллеры/материалы не меняются
(кроме переименования полей).

## 7. Blender workflow (после рефакторинга)

Порядок костей больше не важен. Рекомендации при экспорте модели из Blender в Unity:

- Не переименовывать кости (иначе потребуется вручную `Configure Avatar` в Unity).
- В Unity убедиться, что у модели `Rig = Humanoid` и Avatar валиден
  (`Inspector → Rig → Configure…` — все человеческие кости зелёные).
- Лишний root-узел (`Human_DummyModel_M`) не мешает — humanoid-маппинг резолвится по аватару.

## 8. Вне скоупа

- Сведение Male/Female анимаций в один контроллер (ретаргет и так работает).
- Автоматический `Configure Avatar` для моделей с нестандартным неймингом костей.
