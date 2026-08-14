# Iterations — Character Customisation

## Итерация от 2026-08-14 (whole-model swap)

**Задача:** Перейти с mesh-swap на whole-model swap — модель M/Ж меняется целиком (SMR + кости + Animator + avatar), чтобы Blender-раундтрип (`testing.fbx`) не «рвал» меш.

**Коммит:** _(фиксируется git-commit)_

**Изменения:**
- `Assets/_Project/Prefabs/NetworkPlayer.prefab` — реструктуризация: стабильный пустой `Visual_Model` (scale-root) + дочерний `Body` (nested HumanM_Model с Animator+avatar+SMR+Rig); `CharacterCustomisationApplier._visualRoot` перевязан на новый `Visual_Model`.
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — `ApplyBodyType` переписан на whole-model swap (`Instantiate(targetModel, _visualRoot)` → прямые ссылки на новый Animator/SMR → `SetActive(false)`+`Destroy` старого body → `BodySwapped`); добавлены `public event System.Action<Animator> BodySwapped`, runtime-поле `_currentBody`, переписан `AutoFindRefs`.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — подписка на `BodySwapped` → `_animator = newAnimator`; guard `if (_animator == null)` в `OnNetworkSpawn` (respawn race).
- `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` — подписка на `BodySwapped` → `_animator = newAnimator; Reapply()`.
- `Assets/_Project/Scripts/Skills/SkillAnimationPlayer.cs` — подписка на `BodySwapped` → `_animator = newAnimator; _originalController = null; _overrideCache.Clear()`.
- `docs/Character/Customisation/08_WHOLE_MODEL_SWAP.md` — документация + Blender export checklist (создан).

**Вне коммита (gitignored `*.prefab`):** `NetworkPlayer.prefab` реструктурирован локально.

## Итерация от 2026-08-14 (фикс)

**Задача:** Исправление бага «всегда Ж модель и анимации» при переключении М↔Ж в CustomisationWindow.

**Коммит:** `a0a84bce9a9464a81d83ca0b98f87fa7533d706b` — T-CUS-03: исправление переключения М/Ж модели и анимаций (mesh-swap)

**Изменения:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — `ApplyBodyType` переписан на mesh/avatar/controller swap (без destroy+instantiate и без лишнего уровня иерархии); убран залипающий guard по протухшему `_bodyRenderer`, добавлен защитный перекэш в `OnCustomisationUpdated`.
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `HideGhostVisualIfNeeded()` скрывает `Visual_Model` у scene-placed `PlayerSpawner` (убирал второй дефолтный мужской меш поверх женского).

## Итерация от 2026-08-14

**Задача:** Рефакторинг смены модели тела — замена модели целиком (модель + avatar) вместо `sharedMesh`.

**Коммит:** `6399cab3014bcb388030c001041ba10a12a1c476` — T-CUS-03: рефакторинг смены модели тела — модель целиком + avatar

**Изменения:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — `_maleModel`/`_femaleModel` (GameObject) вместо `_maleMesh`/`_femaleMesh`; `ApplyBodyType` переписан (модель целиком + avatar на `Visual_Model.Animator`).
- `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` — добавлен `public void Reapply()`.
- `docs/Character/Customisation/06_RIG_SWAP_REFACTOR_PLAN.md` — план (создан).
- `docs/Character/Customisation/07_RIG_SWAP_IMPLEMENTATION.md` — детальная реализация (создан).
- `docs/Character/Customisation/CHANGELOG.md` — обновлён.

**Вне коммита (gitignored `*.prefab`):** `Assets/_Project/Prefabs/NetworkPlayer.prefab`
перепровязан локально (`_maleModel` = HumanM_Model.fbx, `_femaleModel` = HumanF_Model.fbx).
Файл не попадает в git из-за правила `.gitignore:149 *.prefab`.
