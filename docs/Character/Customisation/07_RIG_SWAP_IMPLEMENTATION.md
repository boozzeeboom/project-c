# Реализация: замена модели тела целиком (humanoid)

> **Подсистема:** Character Customisation
> **Дата:** 2026-08-14
> **Статус:** ✅ реализовано (код + префаб), компиляция без ошибок
> **Тикет:** T-CUS-03
> **План:** `06_RIG_SWAP_REFACTOR_PLAN.md`

## 1. Что сделано

`CharacterCustomisationApplier` больше не подменяет `SkinnedMeshRenderer.sharedMesh`
(что требовало идентичного порядка костей), а **инстанциирует модель тела целиком**
(SMR + скелет) и переключает `avatar` на `Visual_Model.Animator`.

За счёт humanoid-ретаргета:
- анимации (через `AnimatorOverrideController`) работают на любом humanoid-скелете;
- экипировка резолвится через `Animator.GetBoneTransform(HumanBodyBones.X)` независимо
  от имён/порядка костей;
- FBX-раундтрип через Blender (перестановка костей, лишний root-узел, 52→51 кость влияния)
  больше не ломает скин.

## 2. Изменённые файлы

### 2.1 `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs`

**Убрано:**
```csharp
[SerializeField] private Mesh _maleMesh;
[SerializeField] private Mesh _femaleMesh;
```

**Добавлено:**
```csharp
[Header("Body models (L1)")]
[Tooltip("Модель целиком (FBX/prefab) для Male. Назначить HumanM_Model.fbx.")]
[SerializeField] private GameObject _maleModel;
[Tooltip("Модель целиком (FBX/prefab) для Female. Назначить HumanF_Model.fbx.")]
[SerializeField] private GameObject _femaleModel;
```

**Переписан `ApplyBodyType`:**

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
        // Reset trigger-ы чтобы не было залипших state-ов после смены controller-а.
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

    // 6. Экипировка была прицеплена к старым костям → переприменить на новые.
    var equip = GetComponent<CharacterEquipmentVisualApplier>();
    if (equip != null) equip.Reapply();

    if (Debug.isDebugBuild)
    {
        Debug.Log($"[CharacterCustomisationApplier] Applied bodyType={bodyType} " +
                  $"(model='{targetModel.name}', " +
                  $"mat='{(targetMaterial != null ? targetMaterial.name : "null")}', " +
                  $"ctrl='{(targetCtrl != null ? targetCtrl.name : "null")}').", this);
    }
}
```

### 2.2 `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs`

Добавлен публичный метод перепривязки (после смены скелета старые визуалы уже уничтожены
вместе с костями, а diff по `_currentItems` увидел бы «изменений нет»):

```csharp
/// <summary>
/// Полная перепривязка после смены модели тела: сбрасывает кэш слотов
/// и заново спавнит визуалы на костях нового скелета.
/// Вызывается из CharacterCustomisationApplier.ApplyBodyType после смены bodyType.
/// </summary>
public void Reapply()
{
    DestroyAllVisuals();
    _currentItems.Clear();
    if (_clientState != null && _clientState.CurrentSnapshot.HasValue)
    {
        OnEquipmentUpdated(_clientState.CurrentSnapshot.Value);
    }
}
```

### 2.3 `Assets/_Project/Prefabs/NetworkPlayer.prefab`

Через editor-скрипт (`PrefabUtility.LoadPrefabContents` + `SerializedObject`) назначено:

| Поле | Значение |
|---|---|
| `_maleModel` | `Assets/Kevin Iglesias/Human Animations/Models/HumanM_Model.fbx` (GameObject) |
| `_femaleModel` | `Assets/Kevin Iglesias/Human Animations/Models/HumanF_Model.fbx` (GameObject) |

Старые serialized-поля `_maleMesh`/`_femaleMesh` ушли вместе с кодом (проверено:
`FindProperty("_maleMesh")` больше не находит поле). Остальные поля
(`_maleController`, `_femaleController`, `_maleMaterial`, `_femaleMaterial`) не менялись.

## 3. Отклонения от плана

- **Убран шаг 7 плана** («переутвердить пропорции»). `localScale` живёт на `Visual_Model`,
  который мы НЕ уничтожаем (удаляются только его дети), поэтому scale сохраняется
  автоматически. Повторное применение с использованием `_currentSnapshot` было бы
  некорректным (в момент вызова `ApplyBodyType` снапшот ещё старый) — шаг удалён.

## 4. Статус верификации

- ✅ Компиляция без ошибок (`check_compile_errors` → No compile errors).
- ✅ Префаб перепровязан, значения прочитаны обратно и подтверждены.
- ⏳ Runtime-проверка (playtest, скриншоты) — **за пользователем** (правило проекта:
  playtest/скриншоты делает только пользователь).

Чек-лист для playtest-проверки:
- [ ] M↔F swap: меш + скелет + анимации без «взрыва» скина.
- [ ] Экипировка перевешивается на новые кости после смены bodyType.
- [ ] Пропорции (`localScale`) работают после swap.
- [ ] Подстановка `testing.fbx` в `_maleModel`/`_femaleModel` даёт корректную анимацию
      (проверка humanoid-retarget на произвольной модели).
- [ ] `SkillAnimationPlayer` продолжает кастить скиллы после swap.

## 5. Известные устаревшие артефакты

`Assets/_Editor_Temp/AssignFemaleMesh.cs` и `Assets/_Editor_Temp/RevertFemaleMesh.cs`
ссылкуются на удалённое serialized-поле `_femaleMesh` через `SerializedObject.FindProperty`.
Они одноразовые (temp) и больше не работают по назначению. Не удалялись автоматически —
можно удалить вручную при следующей чистке `_Editor_Temp`.

## 6. Rollback

`git revert` коммита с этим изменением. Код возвращается к `sharedMesh`-логике, а поля
`_maleMesh`/`_femaleMesh` восстанавливаются. Префаб/контроллеры/материалы при этом
остаются в прежнем виде (менялись только имена serialized-полей).
