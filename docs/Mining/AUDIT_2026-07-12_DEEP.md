# Mining System — Deep Architecture & Integration Audit v2

> **Дата:** 2026-07-12
> **Тип:** Глубокий research-driven аудит по запросу пользователя
> **Охват:** 5 .cs-файлов, 7 интеграционных файлов, 3 SO-ассета, вся документация
> **Предыдущий аудит:** `AUDIT_2026-07-11.md` — учтён, findings перепроверены и дополнены

---

## 0. Executive Summary

Система **технически работоспособна**: сбор ресурсов идёт, XP начисляется, UI-тост показывает прогресс. Однако выявлено **3 архитектурных расхождения с Crafting-паттерном** (WorldEventBus XP, отсутствие префаба, orphaned event), **2 XP-бага** (hardcoded multiplier + quantity не умножается в dead-коде), **1 системный баг** (нет cleanup при disconnect) и **1 copy-paste-баг в StatsServer**, затрагивающий все 9 WorldEventBus-подписок.

**Готовность к расширению предыдущим аудитом: 7/10. Фактически: 5/10** — система работает в вакууме, но интеграция с квестами/ачивками/аудио сломана (отсутствие WorldEventBus), а добавление нового нода требует ручной работы (нет префаба).

---

## 1. Моя методология

Проанализированы все файлы из `Assets/_Project/Scripts/ResourceNode/` (5 файлов, ~1620 LOC) + интеграции:

| Файл | LOC | Роль | Моя оценка |
|------|-----|------|------------|
| `ResourceNodeConfig.cs` | 182 | SO-конфиг | ✅ Чисто, но typo "Lambering" |
| `ResourceNode.cs` | 575 | NetworkBehaviour + state machine | ✅ Хорошо, но Cooldown state не используется |
| `GatheringServer.cs` | 418 | Server RPC hub + tick | ✅ Функционально, но XP-путь неконсистентен |
| `GatheringClientState.cs` | 213 | Client singleton + timeout | ✅ Чисто |
| `GatheringToastController.cs` | 232 | UI Toolkit toast | ✅ Работает, но programmatic UI (не UXML) |

**Интеграции (проверены все):**
- `Core/InteractableManager.cs` — ✅ Register/Unregister/FindNearest
- `Core/NetworkManagerController.cs` — ✅ CreateGatheringClientState()
- `Core/WorldEvent.cs` — ⚠️ MiningCompletedEvent определён, но orphaned
- `Player/NetworkPlayer.cs` — ✅ F-key priority + gather animation (T-G09)
- `Stats/StatsServer.cs` — ⚠️ Подписка есть, вызова нет (dead code + copy-paste XP bug)
- `Stats/ExperienceConfig.cs` — ⚠️ XP не используется в активном пути (hardcoded 1.0)
- `Stats/StatsConfig.cs` — ⚠️ Дублирует ExperienceConfig
- `Stats/StatSourceMapConfig.cs` — ✅ Mining → Strength (STR)

---

## 2. Findings — Мои новые находки

### 🔴 CRITICAL 1: XP Quantity не умножается в StatsServer WorldEventBus-обработчиках

**Где:** `StatsServer.cs:260-266` (dead code OnMiningCompleted)

```csharp
private void OnMiningCompleted(MiningCompletedEvent ev)
{
    // ...
    float xp = _expConfig != null ? _expConfig.GetBaseXp(XpSource.Mining) : 0f * ev.Quantity;
    ApplyXp(ev.PlayerId, stat, xp, $"Mining ×{ev.Quantity}");
}
```

**Проблема:** `GetBaseXp(XpSource.Mining)` возвращает XP **за единицу** (1 шт). Но результат **не умножается на `ev.Quantity`**. Если активировать WorldEventBus-путь, XP будет всегда = 1 за сбор, независимо от того, сколько собрано.

**Масштаб:** ⚠️ **Это copy-paste баг во ВСЕХ 8 WorldEventBus-обработчиках StatsServer!**
- `OnMiningCompleted` — строка 264: `_expConfig.GetBaseXp(XpSource.Mining)` — нет `* ev.Quantity`
- `OnCraftingCompleted` — строка 272: `_expConfig.GetBaseXp(XpSource.Crafting)` — нет `* ev.Quantity`
- `OnExchangeCompleted` — строка 280: `_expConfig.GetBaseXp(XpSource.Exchange)` — нет `* ev.Quantity`
- Аналогично Market, Quest, Dialog, Pilot, Jump — все без `* ev.Quantity`

**Последствие:** Если кто-то решит опубликовать любое из этих событий через WorldEventBus, XP будет начисляться неверно (всегда 1 ед., независимо от количества).

---

### 🔴 CRITICAL 2: Активный XP-путь использует hardcoded multiplier 1.0f вместо конфига

**Где:** `GatheringServer.cs:168`

```csharp
ss.ApplyXp(clientId, statType, (float)result.Quantity * 1.0f, $"Mining ×{result.Quantity} {result.ItemName}");
```

**Проблема:** `* 1.0f` — hardcoded. Должно быть `* _expConfig.GetBaseXp(XpSource.Mining)` или аналогично. Конфиг `ExperienceConfig` содержит `_miningXpPerItem` (сейчас = 1), но он **никогда не читается** в активном коде.

**Последствие:** Если designer поменяет `_miningXpPerItem` в `ExperienceConfig_Default.asset` на 2.5 — XP не изменится. Придётся править код.

---

### 🔴 CRITICAL 3: Нет cleanup при дисконнекте игрока

**Где:** `GatheringServer.cs` — отсутствует подписка на `OnClientDisconnectedCallback`

**Проблема:** Если игрок отключается mid-gather:
1. `_activeGathers[clientId]` остаётся висеть — никогда не удалится
2. `ResourceNode._replicatedState` остаётся в `Occupied` навсегда
3. Узел становится вечно занятым для других игроков

**Фикс:** Добавить в `GatheringServer.OnNetworkSpawn`:
```csharp
NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
```
И реализовать `OnClientDisconnected` → `CancelGather()` для всех `_activeGathers[clientId]`.

---

### 🟡 MEDIUM 4: StatsConfig дублирует ExperienceConfig

**Где:** `Stats/StatsConfig.cs` и `Stats/ExperienceConfig.cs`

**Проблема:** Оба содержат поле `_miningXpPerItem` (и другие XpSource-мультипликаторы). `StatsServer` использует `_expConfig` (ExperienceConfig), поле `_sourceMapConfig` (StatSourceMapConfig) и поле `_statsConfig` (StatsConfig).

**StatsConfig** содержит:
- `_miningXpPerItem = 1f` (строка 28)
- Метод `GetBaseXp(XpSource)` (строка 153)
- Stat-маппинг (Mining → Strength, строка 157)

**ExperienceConfig** содержит:
- `_miningXpPerItem = 1f` (строка 16)
- Метод `GetBaseXp(XpSource)` (строка 82)
- Stat-маппинг (Mining → Strength, строка 139)

**Кто что использует:**
- `StatsServer` использует `_expConfig` (ExperienceConfig) для получения XpSource→XP
- `StatsServer` использует `_sourceMapConfig` (StatSourceMapConfig) для XpSource→StatType
- `StatsConfig` — похоже, **не используется напрямую** в StatsServer

**Рекомендация:** Удалить дублирующиеся поля из StatsConfig, оставив только ExperienceConfig как единый источник XP-параметров. Проверить, не ссылается ли кто-то ещё на StatsConfig.

---

### 🟡 MEDIUM 5: XP Quantity прогрессивный, но недокументированный

**Где:** `ResourceNode.cs:345-347`

```csharp
_currentHarvests++;
_totalHarvestedThisLife++;
int currentQty = _currentHarvests;  // 1, 2, 3, ... MaxHarvests
```

**Проблема:** Quantity в `GatherResult` — это **порядковый номер сбора** с данного узла (1-й сбор = 1, 2-й = 2, 3-й = 3). XP начисляется как `currentQty * 1.0f`. То есть:
- 1-й сбор с нода: 1 XP
- 2-й сбор: 2 XP
- ...
- 5-й (последний перед cooldown): 5 XP

Это **прогрессивная XP** — не bugs, но undocumented behavior. Ни один документ не описывает эту механику.

---

### 🟡 MEDIUM 6: Typo "Lambering" — закреплён в коде

**Где:** `ResourceNodeConfig.cs:40`, `NetworkPlayer.cs:1973`, `PlayerAnimation.controller`

**Проблема:** Enum value называется `Lambering` вместо `Lumbering`. Это уже production — все ссылки используют это имя. Менять рискованно (сломает SO-ассеты). Рекомендую задокументировать как intentional.

---

### 🟡 MEDIUM 7: Два XP Config .asset с разными значениями

Нашёл два .asset файла с XP-настройками:
- `Resources/Stats/ExperienceConfig_Default.asset` — `_miningXpPerItem: 1`
- `Resources/Stats/StatsConfig_Default.asset` — `_miningXpPerItem: 1`

Пока значения совпадают (1:1), но если поменять только один — рассинхронизация.

---

### 🟢 MINOR 8: Programmatic UI вместо UXML

`GatheringToastController.cs` создаёт UI через C# code (`new VisualElement {}`, `new Label {}`, `new ProgressBar {}`). Это противоречит UI Toolkit-практике проекта (использовать UXML + USS). Для тоста это допустимо (мало элементов), но стоит отметить.

---

### 🟢 MINOR 9: GatherResult.NetworkSerialize — несериализованные поля

Проверил `GatherResult.NetworkSerialize` — сериализует:
- ✅ `code`
- ✅ `progress`
- ✅ `quantity`
- ✅ `isDepleted`
- ✅ `itemName` (с null-guard)
- ✅ `reason` (с null-guard)

Всё корректно. Null-string guard на строках 379-384 — молодец.

---

## 3. Сравнение с предыдущим аудитом (AUDIT_2026-07-11.md)

### Что я подтверждаю

| Finding (предыдущий аудит) | Мой вердикт | Комментарий |
|---------------------------|-------------|-------------|
| 2. CRITICAL: Dual XP path | ✅ Подтверждаю | Добавил что XP **ещё и отличается** (hardcoded vs configurable) |
| 3. MEDIUM: Нет префаба ResourceNode | ✅ Подтверждаю | `query_project_assets("ResourceNode t:Prefab")` → 0 |
| 4. MEDIUM: MiningCompletedEvent orphaned | ✅ Подтверждаю | Нигде не публикуется |
| 5. MEDIUM: Lambering без тестового нода | ✅ Подтверждаю | Нет `ResourceNode_Tree.asset` |
| 6. MEDIUM: Документация устарела | ✅ Подтверждаю | TODO ниже |
| 7. MINOR: disconnect handler нет | ✅ Подтверждаю | Повышаю до CRITICAL — баг блокирует узел навсегда |
| 8. MINOR: Tooltip GatherType устарел | ✅ Подтверждаю | Строка 51 в ResourceNodeConfig.cs |
| 9. MINOR: `_debugMode = true` по умолчанию | ✅ Подтверждаю | GatheringServer.cs:55 |
| 10. MINOR: ResourceNodeState.Cooldown не используется | ✅ Подтверждаю | Depleted → Idle напрямую |
| 11. MINOR: Fallback GatherPulseLoop dead code | ✅ Подтверждаю | `_gatherScaleAmplitude = 0f` по умолчанию |
| 12. MINOR: 3D-модели — кубы | ✅ Подтверждаю | Placeholder |
| 13. MINOR: CopperVein требует "ShipLight" | ✅ Подтверждаю | Placeholder |

### Что я добавил нового

| # | Новая находка | Severity | Почему пропущено в предыдущем аудите |
|---|--------------|----------|---------------------------------------|
| 1 | **XP Quantity не умножается в WorldEventBus-обработчиках StatsServer** | 🔴 CRITICAL | Аудит смотрел Mining-систему изолированно, не проверяя copy-paste баги в связанных системах |
| 2 | **Активный XP-путь использует hardcoded 1.0f вместо конфига** | 🔴 CRITICAL | Аудит не заглянул в ExperienceConfig |
| 3 | **Нет cleanup при дисконнекте** | 🔴 CRITICAL | Был помечен как Low, я поднял до CRITICAL (блокирует узел навсегда) |
| 4 | **StatsConfig дублирует ExperienceConfig** | 🟡 MEDIUM | Не кросс-проверяли Stats-систему |
| 5 | **XP Quantity прогрессивный (1, 2, 3...)** | 🟡 MEDIUM | Не исследовали поведение Quantity |
| 6 | **Copy-paste баг во всех 8 WorldEventBus-обработчиках StatsServer** | 🔴 CRITICAL | Не смотрели на все обработчики сразу |
| 7 | **Два XP Config .asset** | 🟡 MEDIUM | Не проверяли assets |

---

## 4. Финальный приоритизированный action plan

### 🔴 Немедленно (блокирует работу/расширение)

| # | Действие | Файлы | Трудозатраты |
|---|----------|-------|-------------|
| A | **Добавить disconnect handler** в GatheringServer | `GatheringServer.cs` | 15 мин |
| B | **Унифицировать XP-путь**: опубликовать MiningCompletedEvent через WorldEventBus + убрать прямой вызов StatsServer | `GatheringServer.cs`, `StatsServer.cs` | 30 мин |
| C | **Починить XP Quantity в StatsServer** — добавить `* ev.Quantity` во все 8 обработчиков | `StatsServer.cs` | 15 мин |
| D | **Заменить hardcoded 1.0f** на configurable множитель из ExperienceConfig | `GatheringServer.cs` | 15 мин |

### 🟡 Ближайшее время (перед расширением)

| # | Действие | Файлы | Трудозатраты |
|---|----------|-------|-------------|
| E | **Создать префаб `ResourceNode_Default.prefab`** | `Assets/_Project/Prefabs/` | 30 мин + установка в сцене |
| F | **Добавить `ResourceNode_Tree.asset`** с GatherType.Lambering | `Resources/ResourceNodes/` | 10 мин |
| G | **Удалить StatsConfig** или явно разделить ответственность с ExperienceConfig | `Stats/` | 1 час + проверка references |
| H | **Обновить документацию** под реальное состояние | `docs/Mining/*.md` | 1 час |

### 🟢 Nice-to-have

| # | Действие |
|---|----------|
| I | Выключить `_debugMode` default → false |
| J | Обновить tooltip на GatherType (убрать "T-G08: only enum") |
| K | Реализовать ResourceNodeState.Cooldown или удалить из enum |
| L | Создать 3D-модели для IronVein, CopperVein, PlantHerb |
| M | Создать `Item_Tool_Pickaxe.asset` для CopperVein |
| N | Создать UXML для GatheringToastController вместо programmatic UI |

---

## 5. Архитектурные риски (long-term)

### 5.1 Отсутствие WorldEventBus для Mining

Если квестовая система (или ачивки, или аудио, или HUD-нотификации) захочет реагировать на завершение сбора — придётся либо:
- Подписываться напрямую на `GatheringClientState` (клиентский код, не подходит для серверных квестов)
- Писать новый Event + публикацию

**Рекомендация:** Унифицировать с Crafting-паттерном **сейчас**, пока система не разрослась.

### 5.2 Связанность ResourceNode с InventoryWorld

В `ResourceNode.CompleteGather()` прямой вызов `InventoryWorld.Instance.AddItemDirect()`. Это:
- Нарушает single-responsibility (ResourceNode не должен знать про InventoryWorld)
- Создаёт циклическую зависимость (ResourceNode → InventoryWorld → ?)

**Альтернатива:** Делегировать выдачу предметов через GatheringServer (уже есть ссылка на `_activeGathers`).

### 5.3 Связанность с InteractableManager через OnTriggerEnter

Использование `OnTriggerEnter/Exit` для регистрации в InteractableManager — стандартный паттерн в проекте. Работает, но:
- Каждый ResourceNode должен иметь BoxCollider (isTrigger)
- Trigger size не конфигурируется отдельно (используется _gatherRange только для distance check на сервере)

---

## 6. Appendix: Inventory всех файлов системы

```
# Core files (5)
Assets/_Project/Scripts/ResourceNode/
├── ResourceNodeConfig.cs          (182 LOC) — ✅ OK, typo Lambering
├── ResourceNode.cs                (575 LOC) — ✅ OK, Disconnect не обработан
├── GatheringServer.cs             (418 LOC) — ❌ Disconnect, hardcoded XP, no WorldEventBus
├── GatheringClientState.cs        (213 LOC) — ✅ OK
└── GatheringToastController.cs    (232 LOC) — ✅ OK, programmatic UI

# ScriptableObject assets (3)
Assets/_Project/Resources/ResourceNodes/
├── ResourceNode_IronVein.asset    — GatherType=Mining, tool=None, 3s/5harv/60s
├── ResourceNode_CopperVein.asset  — GatherType=Mining, tool=ShipLight, 3s/5harv/45s
└── ResourceNode_PlantHerb.asset   — GatherType=Gathering, tool=None, 1.5s/3harv/30s

# Интеграционные точки (7 файлов)
├── Core/InteractableManager.cs    — +_resourceNodes list, +3 methods (✅)
├── Core/NetworkManagerController.cs — +CreateGatheringClientState() (✅)
├── Core/WorldEvent.cs             — +MiningCompletedEvent (⚠️ orphaned)
├── Player/NetworkPlayer.cs        — +TryGatherNearestNode, +gather animation (✅)
├── Stats/StatsServer.cs           — +OnMiningCompleted handler (⚠️ dead + copy-paste bug)
├── Stats/ExperienceConfig.cs      — _miningXpPerItem (⚠️ не используется в активном коде)
└── Stats/StatSourceMapConfig.cs   — Mining → STR (✅)

# Prefabs (0 ❌)
# ResourceNode_Default.prefab — НЕ СОЗДАН

# Scenes (2)
├── Scenes/BootstrapScene.unity    — [GatheringServer] GO + [GatheringToast] GO
└── Scenes/World/WorldScene_0_0.unity — 3 raw ResourceNode GO (не prefab instances)

# Documentation (6)
├── docs/Mining/00_OVERVIEW.md     — ⚠️ устарела (WorldEventBus описано, но не реализовано)
├── docs/Mining/10_DESIGN.md       — ✅ актуальна
├── docs/Mining/20_IMPLEMENTATION_PLAN.md — ⚠️ T-G07 "Scene placement + префабы" → DONE, но префаб не создан
├── docs/Mining/ROADMAP.md         — ⚠️ все тикеты помечены ✅, но часть не выполнена
├── docs/Mining/99_CHANGELOG.md    — ✅ OK
└── docs/Mining/AUDIT_2026-07-11.md — ✅ предыдущий аудит (учтён)
```

---

## 7. Итоговая оценка

| Критерий | Оценка | Комментарий |
|----------|--------|-------------|
| Core-функциональность | ✅ 9/10 | Сбор работает, прерывания, cooldown, UI |
| Архитектурная консистентность | ❌ 4/10 | WorldEventBus vs прямой вызов — разрыв с Crafting |
| Обработка edge cases | ❌ 3/10 | Disconnect → вечно занятый узел |
| XP-система | ❌ 2/10 | Hardcoded multiplier, dead code с copy-paste bug |
| Расширяемость | ❌ 5/10 | Нет префаба, нет WorldEventBus → квесты не подписать |
| Документация | ⚠️ 6/10 | Устарела, но есть |

**Общая готовность к production: 5/10**

Система выглядит рабочей на поверхности, но под капотом — **3 CRITICAL бага**, которые проявятся при первом же расширении (квесты, дисконнект, смена XP-баланса).

---

## 8. Краткий чеклист для пользователя (что проверить в Play Mode)

1. ✅ Собрать IronVein → тост показывает прогресс → предмет в инвентаре → XP начислена
2. ✅ Узел уходит в Depleted → через 60s возвращается в Idle
3. ✅ Нажать F на чужом узле во время сбора → "Ресурс сейчас недоступен"
4. ❌ **Отключить клиента во время сбора** → проверить что узел вернулся в Idle (сейчас не вернётся!)
5. ❌ **Поменять _miningXpPerItem в ExperienceConfig.asset на 2.5** → XP должно стать Quantity * 2.5 (сейчас Quantity * 1.0)
6. ✅ Проверить что Lambering-нода нет (должен быть создан)

---

*Составлено Mavis (Project C: The Clouds Agent) на основе анализа кода, документации и аудита от 2026-07-11.*
