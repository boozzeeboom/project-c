# M15 — Toast Notifications (Quest events)

> **Дата:** 2026-06-09
> **Сессия:** M15 (T-Q23, T-Q24, T-Q25)
> **Roadmap:** расширяет `08_ROADMAP.md` §8.5
> **Статус:** 📋 DESIGN — реализация после подтверждения
> **Зависимости:** M13 ✅, T-Q22 ✅

---

## 1. Проблема (audit 2026-06-09)

**Текущее состояние:** Игрок **не видит** feedback на действия квестов. Все изменения silent:
- Accept quest → quest появляется в P-табе, но игрок не знает что квест "взят"
- Pickup 3 руды → quest в P-табе обновляется, но нет визуального сигнала "собрал 1/3"
- Stage transition → quest переходит в следующий stage silent
- Rewards (credits/rep/attitude) → Console лог есть, но игрок не видит
- Discover quest (auto через trigger) → quest в "Найденных" без уведомления

**Существующие сигналы (insufficient):**
- `SetMessage` в CharacterWindow — локальное сообщение в табе
- QuestTracker HUD — только для tracked quest
- Console logs — игрок не смотрит Console

**Жалоба пользователя (2026-06-09):** "когда встал в зону с квестом: нет в найденных в P-квесты" (resolved trigger), но feedback loop всё ещё слабый.

## 2. Что в скоупе M15

### 2.1 Backend
- **Reuse existing infrastructure:** `DialogActionResultDto` (type, success, resultData) уже отправляется client-side через `OnDialogActionResultReceived` event.
- **New DTO поле (optional):** `int intParam` — для delta values (credits delta, rep delta, attitude delta). Сейчас не сериализуется.

### 2.2 New Files
- `Assets/_Project/UI/Toast/ToastUI.cs` — singleton MonoBehaviour с UIDocument
- `Assets/_Project/UI/Toast/ToastUI.uxml` — пустой root container (clone per toast)
- `Assets/_Project/UI/Toast/ToastUI.uss` — стили (position top-right, fade, colors)
- `Assets/_Project/UI/Toast/ToastService.cs` — static API `Show(message, kind)`
- `Assets/_Project/UI/Toast/ToastKind.cs` — enum: Info, Success, Warning, Error

### 2.3 Modified Files
- `Assets/_Project/Quests/Dto/DialogStepDto.cs` — add `int intParam` к `DialogActionResultDto` для delta display
- `Assets/_Project/Quests/Network/QuestServer.cs` — pass `action.intParam` to `SendDialogActionResultToClient` для GiveCredits/AddReputation/AddNpcAttitude/GiveItem/TakeItem
- `Assets/_Project/Quests/Client/QuestClientState.cs` — подписаться на `OnDialogActionResultReceived` и route to ToastService
- `Assets/_Project/Quests/Client/QuestClientState.cs` — подписаться на `OnSnapshotUpdated` → если new quest discovered, toast "Квест найден"
- `Assets/_Project/Scenes/BootstrapScene.unity` — добавить `[ToastService]` GameObject (NOT remove existing)

## 3. Сценарий верификации

### Test 1: Pickup feedback (T-Q23)
- Подобрать `[Pickup_CopperOre_1]`
- Через 5 сек tick → Console: `[ToastService] Show: 📦 +1 Медная руда` → toast appears top-right
- Подобрать ещё 2 → ещё 2 toasts

### Test 2: Quest accept + rewards (T-Q24)
- Войти в `TriggerZone_StageIntro` → toast: "✨ Найден квест: Демо: stage с onEnter"
- Принять quest → toast: "📜 Квест принят" + "💚 Mira +5" (AddNpcAttitude from onEnter)
- E → Mira → quest complete → toast: "✅ Демо: stage с onEnter ВЫПОЛНЕН" + "💰 +10 CR"

### Test 3: Error toast (T-Q25)
- (NoOp — errors уже fire через SendDialogActionResultToClient success=false. Verify toast появляется для failed actions.)

## 4. Не в скоупе M15

- Toast localization — только русский
- Toast queue priority — FIFO (новые вытесняют старые)
- Custom toast sounds — silent
- Persistent toast history — нет
- Per-quest toast settings — нет

## 5. Файлы

**New files (additive):**
- `Assets/_Project/UI/Toast/ToastUI.cs`
- `Assets/_Project/UI/Toast/ToastService.cs`
- `Assets/_Project/UI/Toast/ToastKind.cs`
- `Assets/_Project/UI/Toast/ToastUI.uxml`
- `Assets/_Project/UI/Toast/ToastUI.uss`
- `Assets/_Project/UI/Toast/ToastUI.uxml.meta` (auto-generated)

**Modified files (additive):**
- `DialogStepDto.cs` — add intParam
- `QuestServer.cs` — pass intParam to SendDialogActionResultToClient
- `QuestClientState.cs` — subscribe OnDialogActionResultReceived
- `BootstrapScene.unity` — add [ToastService] GameObject

## 6. Критерии готовности

- [ ] Toast overlay работает (top-right, fade in/out)
- [ ] `ToastService.Show("msg", ToastKind.Success)` API
- [ ] Pickup → "📦 +1 ItemName" toast
- [ ] Quest accept → "📜 Квест принят: name" toast
- [ ] Quest complete → "✅ name ВЫПОЛНЕН" toast
- [ ] GiveCredits → "💰 +N CR" toast
- [ ] AddReputation → "📈 FactionName +N" toast
- [ ] AddNpcAttitude → "💚 NpcName +N" toast
- [ ] Auto-discover quest → "✨ Найден квест: name" toast
- [ ] 0 compile errors

## 7. Effort

~3 ч (M15 medium):
- T-Q23 (ToastUI + Service): 1.5 ч
- T-Q24 (QuestClientState routing): 1 ч
- T-Q25 (Action result routing + DTO intParam): 0.5 ч
