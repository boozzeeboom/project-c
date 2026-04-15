# Changelog: Trade Debug Tools — 16.04.2026

## Созданные файлы

### 1. `Assets/_Project/Trade/Scripts/TradeDebugTools.cs`
**Назначение:** Принудительный UI склада клиента для диагностики

**Функции:**
- Всегда отображается в правой части экрана
- Не зависит от TradeUI
- Обновляется каждые 0.5 сек
- F3 = toggle visibility

**UI отображает:**
- Кредиты игрока (из PlayerDataStore)
- Вес/объём склада
- Список товаров
- Статус компонентов (NetworkPlayer, Storage, PlayerDataStore, TradeMarketServer)

**Методы:**
- `ForceRefresh()` — принудительное обновление из PlayerDataStore
- `LogState()` — вывод состояния в лог

---

## Изменённые файлы

### 1. `Assets/_Project/Scripts/Player/NetworkPlayer.cs`
**Изменения:**
- Добавлен `using ProjectC.UI;`
- Добавлен вызов `TradeDebugTools.Instance.ForceRefresh()` в `TradeResultClientRpc`

### 2. `docs/bugs/BUG_CLIENT_TRADE_RPC_DIAGNOSTIC_2026-04-16.md`
**Назначение:** Документация всех попыток исправления

---

## Что проверять на клиенте

1. **Включён ли TradeDebugTools?**
   - Должен быть добавлен в сцену (или на NetworkManagerController)
   - Справа на экране должен появиться синий/белый UI "СКЛАД КЛИЕНТА"

2. **Нажата ли F3?**
   - Если UI не виден, нажмите F3

3. **Проверить логи при торговле:**
   ```
   [NetworkPlayer] TradeResultClientRpc: targetClientId=X, localClientId=Y
   [NetworkPlayer] Вызываю OnTradeResult для клиента X
   [TradeDebugTools] ForceRefresh: обновляю из PlayerDataStore
   ```

4. **Проверить UI:**
   - Кредиты должны обновиться
   - Товары должны появиться в списке

---

## Root Cause подозрение

RPC доходит до клиента (сервер отправляет), но либо:
1. `TradeResultClientRpc` не вызывается на клиенте (проблема маршрутизации)
2. `TradeUI.Instance == null` на клиенте
3. Данные не синхронизируются между сервером и клиентом

TradeDebugTools позволяет определить, где именно проблема.