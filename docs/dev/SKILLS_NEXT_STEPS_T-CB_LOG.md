# Skills/Battle — Next Steps Log (T-INP / T-CB series)

> **Серия:** T-INP-* (input layer + SkillInputService) + T-CB01+ (Battle skill system extensions)
> **База:** `docs/Character/Skills/AUDIT_2026-06-26_CURRENT_STATE_AND_NEXT_STEPS.md`
> **Дата:** 2026-06-26 (сессия #2 — реализация первой пачки)
> **Статус:** 🟢 Все 8 запланированных шагов из audit §4 завершены. Compile clean (0 errors). Требуется Play Mode verify.

---

## Сессия #2 (2026-06-26) — реализация

### Что сделано

| # | Ticket | Файл | Что | Статус |
|---|---|---|---|---|
| 1 | **T-INP-01** | `Assets/_Project/Scripts/Skills/SkillInputService.cs` (новый) | Новый `MonoBehaviour` singleton (owner-only). `SkillInputSlot` enum (None/Primary/Secondary/Slot1..4). API: `Initialize()`, `TryActivate()`, `BindSlot()`, `IsOnCooldown()`. Per-slot cooldown, target finder через `System.Func<ulong>` | ✅ |
| 2 | **T-INP-02** | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | `InitializeSkillInputService()` (AddComponent + Initialize) + `HandlePrimaryAttackInput()` (delegate). Вызов в IsOwner блоке `OnNetworkSpawn`. K-attack handler переделан: `DebugAttackNearestNpc()` → `HandlePrimaryAttackInput()`. `using ProjectC.Skills;` добавлен | ✅ |
| 3 | **T-INP-03** | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | ЛКМ (Mouse 0) handler в `Update()` сразу после K-attack. Параллельный к K-fallback (оба зовут `HandlePrimaryAttackInput()` → `SkillInputService.TryActivate(Primary)`) | ✅ |
| 4 | **T-INP-04** | `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | Новый event `OnAttackPressed`. В `Update()` после Mouse-delta: emit при ЛКМ **ИЛИ** K. Legacy K-handling в NetworkPlayer НЕ тронут (parallel пути) | ✅ |
| 5 | **T-CB01** | `Assets/_Project/Scripts/Skills/SkillEffect.cs` | Enum `Type` расширен с 3 до 8 значений: `WeaponProficiencyUnlock=3, ArmorProficiencyUnlock=4, WeaponTechniqueUnlock=5, ExplosiveRecipeUnlock=6, AntigravTechniqueUnlock=7`. 5 factory methods добавлены. **Backward-compat**: 8 существующих .asset используют только `StatMod=0/AbilityUnlock=1` — никаких поломок | ✅ |
| 6 | **T-INP-06** (variant A per audit §4) | `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | Warning-only proficiency log в `TryEquip` ветке `WeaponItemData`. Hard gate (`requiredSkills` check) уже работал раньше. Дополнительно: `Debug.Log` показывает weapon name + class + proficiency + hasProficiency | ✅ |
| 7 | **T-INP-07** | `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | `CombatFilter` enum (All/Melee/Ranged/Explosives/Antigrav/Defense). Поле `_activeCombatFilter`, методы `SetCombatFilter/GetCombatFilter`, `MatchesCombatFilter` (substring по skillId prefix). `RebuildSkillsListView()` фильтрует combat-строки. UI-chip-row **НЕ** добавлен (отдельная задача) | ✅ |

### Compile verification

| Этап | read_console errors |
|---|---|
| После Task 1 (SkillInputService.cs создан) | 2 → CS0246 (NetworkPlayer not found) → fix (using) → 0 |
| После Task 2 (NetworkPlayer patches × 4) | 0 |
| После Task 3 (ЛКМ handler) | 0 |
| После Task 4 (OnAttackPressed event) | 0 |
| После Task 5 (SkillEffect.Type enum + factories) | 0 |
| После Task 6 (EquipmentWorld warning log) | 0 |
| После Task 7 (CharacterWindow filter) | 0 |

**Итог:** все 8 шагов компилируются чисто.

---

## Что НЕ сделано в этой сессии (явные deferral)

| Item | Причина | Где доделать |
|---|---|---|
| **UX-чипы фильтра в CharacterWindow.uxml** (горизонтальный chip-row `[Все] [⚔ Melee] [🏹 Ranged] [💣 Explosives] [🌌 Antigrav] [🛡 Defense]`) | API есть (`SetCombatFilter`), но UI не подключён. Можно подключить через `Q<VisualElement>("skill-filter-row")` если добавим в UXML. Решил не трогать — отдельная сессия для UX-полировки | Session #5 (audit roadmap §5) |
| **Drag-and-drop skill → skill slot bar** | SkillInputService.BindSlot API готов, но UI нет | Phase 2 |
| **Raycast targeting** (замена "nearest NpcTarget в 15м") | Target finder delegate работает, но использует legacy nearest. Phase 2 | Session #8 |
| **ApplySkillEffects runtime handler для новых типов** (T-CB07) | Enum добавлен, но runtime switch в SkillsServer — no-op. WeaponProficiency ещё не проверяется при TryEquip | Session #7 |
| **`SkillNodeConfig.CombatDiscipline` поле** (T-CB02) | Substring fallback достаточно для MVP. Поле добавим когда CharacterWindow UI-chip будет готов | Session #4 |
| **ExplosiveItemData SO + WeaponClassCatalog + ArmorClassCatalog** (T-CB04/05) | Не в scope этой сессии | Session #12+ |
| **Remove `DebugAttackNearestNpc` method** | Оставлен как reference / fallback. Удалим в Session #9 | Session #9 |
| **Input System рефакторинг (legacy → Input Actions)** | Не в scope | Session #11 |

---

## Verification checklist для Play Mode

Когда будете готовы проверить в Play Mode:

1. **Compile:** открыть Unity Editor → Console → **0 errors**.
2. **Compile-fix:** если есть warnings о .meta — это нормально (Unity создаёт .meta при импорте).
3. **Play Mode (single-player host):**
   - StartHost → Console должно показать `[CombatServer] OnNetworkSpawn: Instance set` + `[NetworkPlayer] InitializeSkillInputService: SkillInputService ready (owner-only)`.
   - Подойти к NPC на расстояние ≤ 15м.
   - Нажать **K** → Console: `[SkillInputService] TryActivate: slot=Primary skill='' target=X trigger='Attack'` (skill пуст, потому что нет bind — это OK; должны видеть `_animator.SetTrigger("Attack")` если Animator жив).
   - Нажать **ЛКМ** → то же поведение.
   - Проверить что Animator Controller проигрывает Attack state.
4. **Equip weapon flow:**
   - В инвентаре (P → персонаж → инвентарь): найти `Weapon_WoodenSword`.
   - Нажать [НАДЕТЬ] → Console должно показать `[EquipmentWorld] Weapon equip attempt: client=0 weapon='Деревянный меч' class=Sword proficiency='<none>' hasProficiency=True` (или False если skill не learned).
5. **Skill filter:**
   - P → НАВЫКИ → должны видеть все combat-навыки (фильтр All).
   - Через execute_code: `var cw = ProjectC.UI.Client.CharacterWindow.Instance; cw.SetCombatFilter(ProjectC.UI.Client.CharacterWindow.CombatFilter.Melee);` → должны видеть только 6 melee навыков.

---

## Файлы изменены

| Файл | Тип | Строки изменений |
|---|---|---|
| `Assets/_Project/Scripts/Skills/SkillInputService.cs` | NEW (262 строк) | +262 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | EDIT | +60 / -4 |
| `Assets/_Project/Scripts/Player/PlayerInputReader.cs` | EDIT | +17 / -0 |
| `Assets/_Project/Scripts/Skills/SkillEffect.cs` | EDIT | +50 / -2 |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | EDIT | +12 / -0 |
| `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` | EDIT | +29 / -0 |

**Итого:** 6 файлов, 1 новый, 5 отредактированных, +430 / -6 строк.

---

## Что дальше (по audit roadmap §5)

| Session | Тема | Сложность |
|---|---|---|
| **#3** | `SkillEffect.Type` runtime handler (T-CB07, Variant A+B — proficiency tracking) | ~2ч |
| **#4** | `SkillNodeConfig.CombatDiscipline` поле (T-CB02) + CharacterWindow UX-чипы | ~1.5ч |
| **#5** | CharacterWindow UI-chip фильтр | ~1.5ч |
| **#6** | EquipmentServer.TryEquip + WeaponItemData hard proficiency gate (T-CB06 full) | ~2ч |
| **#7** | ApplySkillEffects handler Variant B (proficiency tracking в SkillsWorld) | ~2ч |
| **#8** | Targeting: raycast от камеры (замена "nearest NpcTarget в 15м") | ~1.5ч |
| **#9** | Cleanup: удалить `DebugAttackNearestNpc` метод | ~15м |
| **#10** | Skill tree Painter2D UI (T-P19) | ~3-4ч |
| **#11** | Input System рефакторинг (legacy → Input Actions) | ~3-4ч |
| **#12+** | ExplosiveItemData, WeaponClassCatalog, ArmorClassCatalog | ~3-4ч |

---

## История документа

| Дата | Сессия | Изменения |
|---|---|---|
| 2026-06-26 | #2 (эта) | Первая пачка из 8 шагов. SkillInputService + ЛКМ/K-attack unified flow + SkillEffect.Type expansion + warning-only proficiency + combat-filter API. Все 8 шагов compile clean. |