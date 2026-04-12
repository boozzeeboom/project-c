# Bug Report: Cascading Compilation Breakage from asmdef Introduction

**Session:** SESSION1 — ProjectC.Runtime asmdef
**Severity:** CRITICAL (57 compilation errors, project does not build)
**Status:** Open — requires remediation
**Date:** 11 апреля 2026
**Branch:** `qwen-gamestudio-agent-dev`
**Reporter:** QA Tester (@qa-tester)

---

## Summary

Добавление `ProjectC.Runtime` asmdef файла в `Assets/_Project/Scripts/` вызвало каскадные ошибки компиляции (57 штук). До этого проект компилировался без проблем — все скрипты находились в автоматически генерируемой assembly `Assembly-CSharp`.

После создания asmdef основные скрипты потеряли автоматические ссылки на Unity-пакеты и другие assembly проекта, которые Unity подключал к `Assembly-CSharp` по умолчанию.

---

## Root Cause

Когда Unity создаёт `Assembly-CSharp`, он **автоматически** подключает все установленные пакеты (InputSystem, TextMeshPro, Netcode и т.д.). При выделении кода в кастомную assembly (`ProjectC.Runtime`) эти ссылки **не наследуются** — их нужно явно прописать в `references` и `precompiledReferences` asmdef файла.

Текущий `ProjectC.asmdef` содержит только:
```json
"references": ["Unity.Netcode.Runtime"]
```

Это недостаточно. Скрипты внутри `Scripts/` используют InputSystem, TMPro, а также типы из других папок проекта (`Trade/`, `CargoSystem`).

---

## Error Breakdown

### 1. Missing: `Unity.InputSystem` (9 скриптов, ~15 ошибок)

Скрипты с `using UnityEngine.InputSystem;` не компилируются:

| Файл | Путь |
|---|---|
| ThirdPersonCamera.cs | `Assets/_Project/Scripts/Core/` |
| WorldCamera.cs | `Assets/_Project/Scripts/Core/` |
| ItemPickupSystem.cs | `Assets/_Project/Scripts/Player/` |
| NetworkPlayer.cs | `Assets/_Project/Scripts/Player/` |
| PlayerController.cs | `Assets/_Project/Scripts/Player/` |
| PlayerStateMachine.cs | `Assets/_Project/Scripts/Player/` |
| ControlHintsUI.cs | `Assets/_Project/Scripts/UI/` |
| InventoryUI.cs | `Assets/_Project/Scripts/UI/` |
| NetworkUI.cs | `Assets/_Project/Scripts/UI/` |
| UIManager.cs | `Assets/_Project/Scripts/UI/` |

### 2. Missing: `Unity.TextMeshPro` (8 скриптов, ~12 ошибок)

Скрипты с `using TMPro;` не компилируются:

| Файл | Путь |
|---|---|
| ThirdPersonCamera.cs | `Assets/_Project/Scripts/Core/` |
| WorldCamera.cs | `Assets/_Project/Scripts/Core/` |
| ConfirmationDialog.cs | `Assets/_Project/Scripts/UI/` |
| ControlHintsUI.cs | `Assets/_Project/Scripts/UI/` |
| NetworkUI.cs | `Assets/_Project/Scripts/UI/` |
| PeakNavigationUI.cs | `Assets/_Project/Scripts/UI/` |
| UIFactory.cs | `Assets/_Project/Scripts/UI/` |

### 3. Missing: Cross-assembly reference `ProjectC.Trade` (1 скрипт)

`NetworkPlayer.cs:6` — `using ProjectC.Trade;`

Папка `Assets/_Project/Trade/Scripts/` не имеет своего asmdef и остаётся в `Assembly-CSharp`. `ProjectC.Runtime` не ссылается на `Assembly-CSharp` (и не должна). Типы из Trade недоступны внутри ProjectC.Runtime.

### 4. Missing: Cross-namespace reference `ProjectC.Player.CargoSystem` (1 скрипт)

`ShipController.cs:71` — `[SerializeField] private ProjectC.Player.CargoSystem cargoSystem;`

`CargoSystem.cs` находится в `Assets/_Project/Trade/Scripts/` (вне `Scripts/`), namespace `ProjectC.Player`. Тип не виден из-за разрыва assembly boundaries.

### 5. Burst Compiler: Assembly resolution failed

```
Failed to resolve assembly: 'ProjectC.Tests, Version=0.0.0.0, Culture=neutral'
```

Burst compiler сканирует все assembly в поисках entry-points (Job-ов с `[BurstCompile]`). `ProjectC.Tests.asmdef` имеет `"autoReferenced": true`, но Burst не может его разрешить — вероятно из-за циклической зависимости или отсутствия самой assembly в домене компиляции.

---

## Dependency Graph (Current — Broken)

```
ProjectC.Runtime (asmdef)
  references: Unity.Netcode.Runtime
  MISSING: Unity.InputSystem
  MISSING: Unity.TextMeshPro
  MISSING: Assembly-CSharp (Trade/, CargoSystem)

ProjectC.Tests (asmdef)
  references: ProjectC.Runtime, Unity.Netcode.Runtime, UnityEngine.TestRunner
  PROBLEM: Burst cannot resolve this assembly

Assembly-CSharp (auto-generated, contains Trade/Scripts/ and other non-asmdef code)
  — cannot be referenced by ProjectC.Runtime (circular dependency risk)
```

---

## Recommended Fix

### Вариант A: Удалить asmdef (Recommended)

**Самое безопасное решение.** Для данного проекта asmdef не нужен.

1. **Удалить** `Assets/_Project/Scripts/ProjectC.asmdef`
2. **Удалить** `Assets/_Project/Tests/ProjectC.Tests.asmdef`
3. Скрипты вернутся в `Assembly-CSharp` — все зависимости заработают автоматически
4. Тесты оставить как Editor-only тесты внутри `Assembly-CSharp-Editor` или без отдельной assembly

**Плюсы:**
- Мгновенное восстановление компиляции
- Zero overhead на maintenance asmdef
- Для indie-проекта asmdef не даёт преимуществ

**Минусы:**
- Нет изоляции assembly (не критично для одного проекта)
- Чуть медленнее перекомпиляция (не заметно на этом масштабе)

### Вариант B: Добавить все зависимости в asmdef

Если asmdef принципиально нужен:

1. **Добавить в `ProjectC.asmdef` → `references`:**
   - `Unity.InputSystem`
   - `Unity.TextMeshPro`
   - `Assembly-CSharp` (если Trade/CargoSystem останутся там) — **НЕ РЕКОМЕНДУЕТСЯ** (circular dependency risk)

2. **ИЛИ** создать отдельные asmdef для Trade и других папок:
   - `Assets/_Project/Trade/ProjectC.Trade.asmdef`
   - Добавить ссылку на `ProjectC.Trade` в `ProjectC.Runtime`
   - Добавить ссылку на `ProjectC.Trade` в `ProjectC.Tests`

3. **Для Burst:** Убедиться что `ProjectC.Tests` компилируется до Burst pass, или добавить define constraint

4. **Для InputSystem / TMPro:** Добавить в `precompiledReferences`:
   ```json
   "precompiledReferences": [
       "Unity.InputSystem.dll",
       "Unity.TextMeshPro.dll"
   ]
   ```

**Плюсы:**
- Правильная архитектура assembly boundaries
- Быстрее инкрементальная компиляция на больших проектах

**Минусы:**
- Значительный overhead на поддержание
- Риск circular dependencies
- Требует создания asmdef для КАЖДОЙ логической группы

---

## Files Affected

| File | Action Needed |
|---|---|
| `Assets/_Project/Scripts/ProjectC.asmdef` | DELETE or fix references |
| `Assets/_Project/Tests/ProjectC.Tests.asmdef` | DELETE or fix Burst resolution |
| `Assets/_Project/Trade/Scripts/CargoSystem.cs` | Needs own asmdef if Variant B |
| `Assets/_Project/Trade/Scripts/*.cs` (Trade namespace) | Needs own asmdef if Variant B |

---

## Prevention Rules

1. **НЕ создавать asmdef для основных скриптов** проекта без полного маппинга всех зависимостей
2. **НЕ добавлять asmdef** если проект компилируется в Assembly-CSharp без проблем
3. **ПЕРЕД созданием asmdef** — запустить `grep -r "using " Scripts/` и собрать все внешние зависимости
4. **После добавления asmdef** — ОБЯЗАТЕЛЬНО проверить compilation в Unity Editor до отправки коммита
5. **Burst + custom asmdef** — тестировать отдельно, Burst имеет собственный assembly resolver

---

## Reproduction Steps

1. Убедиться что проект компилируется (без asmdef)
2. Создать `Assets/_Project/Scripts/ProjectC.asmdef` с references: `["Unity.Netcode.Runtime"]`
3. Создать `Assets/_Project/Tests/ProjectC.Tests.asmdef`
4. Вернуться в Unity Editor — Console покажет 57 ошибок
5. Попытка Build — fail

---

## Environment

- **Unity:** Unity 6 (URP)
- **OS:** Windows 11 (win32)
- **Branch:** `qwen-gamestudio-agent-dev`
- **Netcode:** Unity Netcode for GameObjects
- **Packages:** InputSystem, TextMeshPro, Burst
