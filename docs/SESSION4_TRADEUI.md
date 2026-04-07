# Сессия 4: TradeUI — Интерфейс торговли

**Статус:** ЗАВЕРШЕНО
**Дата:** 7 апреля 2026 г.

## Что сделано

### Исправления
1. **TradeTrigger** — заменён `SendMessage` на прямые вызовы `TradeUI.OpenTrade()` / `TradeUI.CloseTrade()`
2. **PlayerTradeStorage.Load()** — исправлен путь `Resources.Load` (создана папка `Resources/Trade/` и скопирована `TradeItemDatabase.asset`)
3. **ShipController <-> CargoSystem** — `TradeSceneSetup` автоматически добавляет `CargoSystem` на все корабли в сцене

### Новые файлы
| Файл | Описание |
|------|----------|
| `TradeSceneSetup.cs` | Runtime-скрипт: автоматически настраивает все компоненты торговли при `Awake()` |
| `Editor/TradeSceneSetupTool.cs` | Editor-скрипт: меню `Tools > Project C > Setup Trade Scene` для настройки сцены |

### Архитектура (рынок-склад-корабль)
```
LocationMarket (ScriptableObject)
       |
       v
  TradeUI (runtime Canvas)
       |
       v
PlayerTradeStorage (склад игрока, credits)
       |
       v
  CargoSystem (трюм корабля)
```

## Как настроить в Unity

### Способ 1: Автоматический (рекомендуется)
1. Открой сцену `ProjectC_1.unity`
2. В меню: **Tools > Project C > Setup Trade Scene**
3. Готово — все объекты созданы и связаны

### Способ 2: Ручной
1. Создай пустой GameObject → назови `TradeSceneSetup`
2. Добавь компонент `TradeSceneSetup`
3. Назначить поле `Market` → `Market_Primium_v01.asset`
4. Нажми Play — всё настроится автоматически

## Как тестировать

1. Открой сцену `ProjectC_1.unity`
2. Запусти (Play)
3. Подойди к жёлтой зоне TradeTrigger (Gizmos)
4. Нажми **E** — откроется TradeUI
5. **W/S** — изменить количество
6. **Up/Down** — выбрать товар
7. **Enter** — купить
8. **Shift+Enter** — продать
9. **Tab** — переключить вкладку Рынок/Склад
10. **L** — погрузить со склада на корабль
11. **U** — разгрузить с корабля на склад
12. **Esc** — закрыть

## Проверка CargoSystem
- CargoSystem автоматически добавлен на все ShipController в сцене
- При покупке товар попадает в PlayerTradeStorage (склад)
- При нажатии L товар перемещается со склада в трюм корабля (CargoSystem)
- Вес/объём/слоты отображаются в TradeUI
- Скорость корабля снижается от груза (GetSpeedPenalty)

## Известные ограничения
- Локальная торговля (без ServerRpc — будет в Сессии 5)
- Без иконок товаров (текстовые placeholder)
- Без анимаций
