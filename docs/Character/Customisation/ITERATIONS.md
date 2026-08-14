# Iterations — Character Customisation

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
