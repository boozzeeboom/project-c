# T-INP-06 — AOE Debug Visualization (Gizmos + Debug.AOE runtime toggle)

> **Статус:** Design (plan to implement in next pass)
> **Подсистема:** Character Progression → Skills → AOE formulas
> **Цель:** Визуализировать AOE-форму каждого навыка (Cone/Sphere/Line/Box) прямо в Play Mode и в Scene view, чтобы:
> 1. Дизайнер видел глазами, какой реальный размер/форма у его навыка.
> 2. Программисты могли подгонять параметры под VFX и анимации.
> 3. Не угадывать «почему AOE не попадает» — сразу видно.

---

## Проблема

Сейчас AOE-формула и её параметры (`aoeFormula`, `aoeSize`, `aoeConeAngleDeg`, `aoeWidth`) существуют только как числа в `SkillNodeConfig.asset`. Дизайнер не видит форму в Play Mode; программист подбирает aoeSize наугад. Результат — «круговой удар радиусом 15 попадает одному», хотя на самом деле формула корректная, просто `aoeSize=1` в ассете.

## Решение (2 части)

### Часть 1. Gizmos в SkillNodeConfig (Editor-only, Scene view)

Каждый `SkillNodeConfig` (ScriptableObject) получает `OnDrawGizmos()` (через `[ExecuteAlways]` НЕ нужен — SO сами рисуют gizmos через `OnDrawGizmos`-callback, **но только если SO выбран в Project view**).

**Реализация:** static helper `SkillAoeGizmos.Draw(SkillNodeConfig skill, Vector3 origin, Vector3 forward)`, который по `aoeFormula` рисует:
- **Cone** — wire-cone с дугой (`Handles.DrawWireArc` нельзя из runtime — используем `Gizmos.DrawLine` + пары линий по периметру, 24 сегмента).
- **Sphere** — `Gizmos.DrawWireSphere(origin, aoeSize)`.
- **Line** — `Gizmos.DrawLine(origin, origin + forward*aoeSize)` + 2 пары линий по ширине.
- **Box** — `Gizmos.DrawWireCube(center, halfExtents)`.
- **SingleTarget** — не рисуем (или маленькая сфера-маркер).

Цвет — полупрозрачный жёлтый/зелёный, разный для разных формул (для читаемости).

**Использование в Editor:**
- Когда ScriptableObject выбран в Project — `OnDrawGizmos` (через `[DrawGizmo]` attribute) рисует preview в позиции (0,0,0) с forward=Vector3.forward. Дизайнер крутит Scene view и видит форму.
- Когда активный NetworkPlayer имеет привязанный skill к слоту — рисуем preview в позиции игрока через `SkillInputService.OnDrawGizmos`.

### Часть 2. Runtime toggle: SkillAoeDebugVisualizer (Play Mode)

Небольшой MonoBehaviour `SkillAoeDebugVisualizer`, который:
- Подписывается на SkillInputService.
- На каждый успешный `TryActivate(slot)` (или нажатие клавиши активации) рисует форму на **время каста** (например, 0.5с) в позиции атакующего, используя `LineRenderer` или `Debug.DrawLine` (если `Application.isPlaying`).
- По таймеру очищает.

**Альтернатива проще:** при скилл-касте логировать в Console:
```
[SkillAoeDebug] SpinStrike at (40000,2503,40060) formula=Sphere radius=1m forward=(0,0,1)
[SkillAoeDebug] Sphere hits: 4 targets (goblin@1.3m, goblin@2.0m, goblin@2.1m, goblin@2.2m)
```

Это **минимум**, с чего стоит начать.

## Что НЕ делаем (явно)

- ❌ Не делаем runtime LineRenderer overlay (overkill для debug).
- ❌ Не трогаем существующий `TargetingService` / `CombatServer.ResolveSkillCast` — они работают корректно.
- ❌ Не создаём `.asmdef`, `.meta` для нового файла — Unity создаст сам.

## Файлы

- **NEW** `Assets/_Project/Scripts/Skills/Debug/SkillAoeGizmos.cs` (~80 строк, Editor-only)
  - Static helper + `[DrawGizmo(GizmoType.Selected)]` для `SkillNodeConfig` (рисует preview в центре (0,0,0)).
  - Public method `DrawAoeGizmos(Vector3 origin, Vector3 forward, SkillNodeConfig skill, Color color)`.
- **NEW** `Assets/_Project/Scripts/Skills/Debug/SkillAoeDebugVisualizer.cs` (~40 строк, runtime)
  - MonoBehaviour. На каждом TryActivate логирует в Console позицию, форму, радиус, найденные targets.
  - Подписка на SkillInputService: можно сделать через static event `OnSkillActivated(slot, skillId, origin, aoeFormula, aoeSize)`.
- **EDIT** `Assets/_Project/Scripts/Skills/SkillInputService.cs` (~+10 строк)
  - После успешного TryActivate в `#if UNITY_EDITOR` секции вызывать `SkillAoeDebugVisualizer.OnSkillActivated(...)`.

## Verification plan

1. `refresh_unity` → 0 errors.
2. Выбрать в Project view `Skill_Combat_BasicStrike 1.asset` — в Scene view виден wire-sphere.
3. Запустить Play Mode → StartHost → нажать Ctrl+ЛКМ → Console: `[SkillAoeDebug] SpinStrike formula=Sphere radius=1m forward=...`.
4. Поставить `aoeSize=15` в инспекторе → повторить → Console покажет radius=15.

## Open questions (зафиксировать перед кодом)

1. **Вопрос:** Рисовать gizmos только когда SO выбран в Project, или ВСЕГДА для всех SO (превью сцены)?
   **Ответ:** Selected only (по умолчанию). Чтобы не загромождать Scene view 30 сферами от всех .asset в проекте.
2. **Вопрос:** Использовать `Gizmos` или `Handles` (Editor-only)?
   **Ответ:** `Gizmos` — проще, доступны из runtime скриптов под `#if UNITY_EDITOR`. `Handles` требуют Editor assembly.
3. **Вопрос:** Нужен ли toggle «show AOE always» для дизайнера?
   **Ответ:** Пока нет. Если понадобится — простой `SkillAoeDebugSettings` SO с bool `showAllAoeAlways` + `SkillAoeDebugVisualizer` подпишется и будет рисовать.
