# MetaRequirement — Testing Guide

**Документ:** пошаговые тест-сценарии для MetaRequirement (включая тест LockBox).
**Дата:** 2026-06-06

---

## 0. Предусловия (Pre-conditions)

- [ ] Unity 6000.4.1f1 открыт, проект загружен
- [ ] `BootstrapScene` открыта (или это текущая сцена)
- [ ] Скомпилировано без ошибок (Console → 0 errors)
- [ ] В `BootstrapScene` есть:
  - `[NetworkManager]` (NetworkManager + UnityTransport + NetworkManagerController)
  - `[MetaRequirementRegistry]` (NetworkObject + MetaRequirementRegistry)
  - `[MetaRequirementToast]` (UIDocument + MetaRequirementPanelSettings)
- [ ] В `WorldScene_0_0` (если тестируем LockBox):
  - `[MetaRequirement_Test]` parent с 3 Pickup-ключами (Blue/Red/Green) и 3 LockBox-блоками
- [ ] ItemData ключей лежат в `Resources/Items/` (НЕ в подпапке)

---

## 1. Базовый smoke test (без LockBox)

**Цель:** убедиться что MetaRequirement-инфраструктура работает (registry, client state, toast).

### 1.1 Запуск
1. Editor → `BootstrapScene` → Play
2. UI кнопка **StartHost** (или горячая клавиша если настроена)
3. В Console ждать:
   ```
   [MetaRequirementRegistry] OnNetworkSpawn. IsServer=True, existing requirements=0
   ```
4. Никаких NRE, никаких ошибок

**Ожидаемое:** clean start, registry зарегистрирован.

### 1.2 Проверка auto-spawn MetaRequirementClientState
1. После StartHost → Hierarchy → должна быть root GameObject `[MetaRequirementClientState]`
2. Кликнуть на него → Inspector → должен быть component `MetaRequirementClientState` (enabled)
3. В Console не должно быть warning'ов про "DontDestroyOnLoad failed"

**Ожидаемое:** singleton создан и переживает сцену.

### 1.3 Проверка загрузки WorldScene_0_0
1. После StartHost (через ~1-2 сек) WorldScene_0_0 загрузится автоматически (ClientSceneLoader)
2. В Console:
   ```
   [ScenePlacedObjectSpawner] Scene (0,0): spawned=N, already=M, failed=0
   [MetaRequirementRegistry] Registered requirement: netId=42, displayName='Синий Сундук', ...
   [MetaRequirementRegistry] Registered requirement: netId=43, displayName='Красный Сундук', ...
   [MetaRequirementRegistry] Registered requirement: netId=44, displayName='Зелёный Сундук', ...
   [MetaRequirementRegistry] PushRequirementsToClient: pushed 3 requirement(s).
   ```
3. N должен включать LockBox'ы (3 шт), плюс корабли с `ShipKeyBinding` алиасом (3 шт)

**Ожидаемое:** 6+ requirements зарегистрированы, push на host.

---

## 2. LockBox тест (с ключом → успех)

**Цель:** убедиться что с подобранным ключом LockBox проигрывает анимацию.

### 2.1 Setup
1. Player спавнится в BootstrapScene (host = client 0)
2. WorldScene_0_0 загружена, LockBox'ы на позициях ~(40050, 2502, 39990) ± 6 м по X

### 2.2 Шаги
1. WASD → подойти к `[Key_Blue_Pickup]` (маленькая синяя сфера, должна "висеть" с пульсацией)
2. E (подобрать):
   - Console: `[InventoryServer] ... picked up ID=N (Equipment)`
   - Console: `[PickupItem] <Name> успешно подобран`
   - Визуально: сфера пропадает
3. TAB-колесо: в секторе Equipment должна появиться запись "Ключ: Синий Замок"
4. WASD → подойти к `[LockBox_Blue]` (большой синий куб)
5. E (открыть):
   - Console: `[MetaRequirementRegistry] CanUse: client=0, obj=42, allowed=True`
   - Console: `[MetaRequirementClientState] Use allowed: netId=42, reason=''`
   - **Визуально:** LockBox увеличивается в 1.2 раза + emissive flash (ярко-синий) → возвращается к base
   - Toast НЕ показывается (allowed=true)

**Ожидаемое:** анимация проиграна, нет deny.

### 2.3 Повторный E (через 0.5+ сек)
1. После анимации (через 0.6 сек) снова E
2. Должна проиграться та же анимация (re-open)

**Ожидаемое:** повторная анимация (reopenCooldown = 0.5s, animDuration = 0.6s).

---

## 3. LockBox тест (без ключа → отказ)

**Цель:** убедиться что без ключа LockBox не открывается + показывается toast.

### 3.1 Setup
1. Player спавнится
2. Ключи **НЕ подобраны** (например, перезапустил Editor)

### 3.2 Шаги
1. WASD → подойти к `[LockBox_Blue]` (не подбирая ключ!)
2. E (попытка открыть):
   - Console: `[MetaRequirementRegistry] CanUse: client=0, obj=42, allowed=False, reason='Нужен предмет: Ключ: Синий Замок'`
   - Console: `[MetaRequirementClientState] Access denied: netId=42, reason='Нужен предмет: Ключ: Синий Замок'`
   - **Визуально:** внизу экрана появляется toast «Нужен предмет: Ключ: Синий Замок» на 2.5 сек
   - **LockBox НЕ анимируется** (OnAccessAllowed не дёргается)

**Ожидаемое:** toast виден, нет анимации.

### 3.3 Race: двойной E
1. E дважды быстро (в пределах 1.5 сек) на тот же LockBox
2. Должен быть **только один** RPC → один toast (race protection в NetworkPlayer)
3. Console: только один `[MetaRequirementRegistry] CanUse: ...`

**Ожидаемое:** один запрос, один toast.

---

## 4. Кросс-ключ тест (Cross-key test)

**Цель:** убедиться что неправильный ключ не открывает другой блок.

### 4.1 Шаги
1. Подобрать `[Key_Blue_Pickup]` (синий)
2. E на `[LockBox_Red]` (красный) — **не должен** открыться
3. Toast: «Нужен предмет: Ключ: Красный Замок»
4. E на `[LockBox_Blue]` (синий) — **должен** открыться
5. Анимация проигрывается

**Ожидаемое:** логика работает per-key, не "любой ключ = любой блок".

---

## 5. Regression: Ship Key (legacy)

**Цель:** убедиться что старые корабли с `ShipKeyBinding` (алиас) **продолжают работать** после миграции.

### 5.1 Шаги
1. Подобрать `[KeyRod_ShipLight]` (жёлтый ключ от Light-корабля, в `[Ship_Key_Container]`)
2. F на `Ship_Light` — **должен** сесть за штурвал
3. Console:
   - `[ShipKeyServer-ALIAS] CanBoard: client=0, ship=N, allowed=True` (старый протокол)
   - **или** `[MetaRequirementRegistry] CanUse: client=0, obj=N, allowed=True` (новый протокол, зависит от приоритета)
4. F повторно → выйти

**Ожидаемое:** корабли работают как раньше. Алиасы прозрачны.

### 5.2 Без ключа
1. Перезапустить Editor (без ключа)
2. F на `Ship_Heavy` (без KeyRod_ShipHeavy)
3. Toast «Нет ключа корабля (Корабль Heavy)» (старый формат)
4. Не садится

**Ожидаемое:** старая логика работает.

---

## 6. Multiple LockBox тест

**Цель:** убедиться что несколько разных блоков работают независимо.

### 6.1 Шаги
1. Подобрать все 3 ключа (Blue, Red, Green)
2. E на Blue → анимация
3. E на Red → анимация
4. E на Green → анимация
5. Все три одновременно открыты (визуально — все три мигнули)

**Ожидаемое:** каждое взаимодействие независимо, нет глобального state.

---

## 7. Скриншот-тест (visual verification)

**Цель:** убедиться что toast визуально корректен (не закрывает HUD, читаемый).

### 7.1 Шаги
1. Player подходит к LockBox без ключа
2. E → toast появляется внизу экрана
3. **Сделать скриншот** (юзер сам)
4. Проверить:
   - Toast **виден** (не прозрачный)
   - Toast **по центру** внизу
   - Текст **читаемый** (контрастный фон)
   - Toast **не перекрывает** важный HUD (TAB-колесо, чат, миникарта)
   - Toast **исчезает** через 2.5 сек

**Ожидаемое:** toast не блокирует UI, читаем.

---

## 8. Тест-чеклист (для Verify)

- [ ] **Setup** работает: `StartHost` → registry, client state, toast инициализированы
- [ ] **LockBox with key**: подобрать ключ → E на блок → анимация scale + emission
- [ ] **LockBox without key**: E без ключа → toast с reason "Нужен предмет: ..."
- [ ] **Cross-key**: Blue ключ НЕ открывает Red блок (toast показывает нужен правильный)
- [ ] **Cooldown**: rapid E (двойное нажатие) → только один RPC
- [ ] **Regression ships**: F на Ship_Light с ключом → садится
- [ ] **Regression ships**: F на Ship_Heavy без ключа → toast старого формата
- [ ] **Multi-LockBox**: 3 блока независимо работают
- [ ] **Animation re-trigger**: через 0.5+ сек повторный E → анимация проигрывается заново
- [ ] **Scene transition** (если тестируется): при стриминге сцен `[MetaRequirement_Test]` остаётся
- [ ] **Console clean**: 0 errors во время тестов

---

## 9. Частые проблемы и диагностика

| Симптом | Диагностика | Фикс |
|---|---|---|
| Toast не появляется | Проверить `[MetaRequirementToast]` в BootstrapScene, `PanelSettings` назначен | Добавить/переподключить |
| Анимация не играет | Проверить `LockBox` component на GameObject, baseColor задан | Добавить/исправить |
| `allowed=False` сразу | Проверить `_requiredItems[]` в Inspector (size, itemData) | Заполнить |
| `allowed=True` без ключа | `_requiredItems[]` пуст, `_logic=All` (trivially true) | Заполнить массив |
| `OnNetworkSpawn not called` | Нет `NetworkObject` на GameObject | Add Component → Network Object |
| `MetaRequirementRegistry.Instance == null` | Не спавнится scene-placed NetworkObject (см. `docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md`) | Проверить `ScenePlacedObjectSpawner` в Console |
| `Resources/Items` warning | ItemData в подпапке (R2-SHIP-KEY-001) | Переместить в корень `Resources/Items/` |
| ItemData не найден | Не `RegisterItem`'нут, или Resources.LoadAll не нашёл | Проверить путь, см. KNOWN_ISSUES |

---

## 10. Автоматизация (будущее)

Эти тесты пока делаются вручную. В Phase 10+ можно:
- Написать EditMode/PlayMode unit-тесты (NUnit + Unity Test Framework)
- Сценарии: "дано 2 keys в инвентаре, 1 нужен → CanPlayerUse=true"
- Сетевые тесты: "хост + 1 клиент, клиент шлёт RPC → сервер отвечает"

**⚠️ Не делаем сейчас** (по AGENTS.md юзер сам тестирует, нет CI/CD).

---

## 11. Что делать если тест провалился

1. **Скриншот** (юзер делает сам, не Mavis)
2. **Полный Console output** (включая warnings)
3. **Шаги repro** (что нажал, что ожидал, что получил)
4. **Прислать мне** в чат

Я починю в следующей сессии.
