# Port Station Creator (FARMS_PREFAB)

> Автоматизация создания портовых станций «Средняя» в сцене мира.

## Назначение

Создаёт полную заготовку портовой станции с одним вызовом.  
Дизайнеру больше не нужно вручную копировать и переименовывать десятки объектов —  
скрипт делает всё сам, с правильными ID и ссылками.

## Как использовать

1. **Tools → ProjectC → Create Port Station…**
2. Заполнить поля:
   - **Namespace / ID** — уникальный ID локации (напр. `Primium_farm_1_1`)
   - **Display Name** — читаемое имя (напр. `Ферма Примума 1_1`)
   - **Кол-во pads** — от 1 до 20
   - **Расстояние между pads** — шаг по X (по умолчанию 10)
   - **Parent** — опционально, куда положить в иерархии
3. Нажать **«Создать»**

## Что создаётся

### ScriptableObject-ы

| Ассет | Путь | Поля |
|---|---|---|
| `DockStation_{ID}.asset` | `Assets/_Project/Docking/Resources/Data/` | stationId, locationId, displayName, maxConcurrentLandings=10, landingWindow=300s |
| `MarketConfig_{ID}.asset` | `Assets/_Project/Trade/Data/Markets/` | Копия `MarketConfig_Primium_farm_0_0` с новыми locationId и displayName |

### Иерархия GameObject

```
{DisplayName}                              [Plane + NetworkObject + OuterCommZone + DockStationController]
├── Dockings/                              
│   └── RepairManager                      [Capsule + RepairManager + StationRootReference]
├── Pad_01 .. Pad_NN                       [BoxCollider(trigger) + DockingPadTriggerBox + PadTriggerReference + DockPadVisualMarker]
│   ├── Pad_0X (TMP label)
│   └── Empty_visual
└── Market_zone_{ID}/
    └── Npc_peacfull_market_zone           [из префаба Npc_peacfull_market_zone Variant.prefab]
        └── Visual (Animator + риг)
```

## Ключевые связи компонентов

| Компонент | Находится на | Как находит DockStationController |
|---|---|---|
| `StationRootReference` | `RepairManager` | `transform.root.GetComponent<>()` → корень |
| `DockingPadTriggerBox` | `Pad_XX` | `GetComponentInParent<>()` → корень |
| `OuterCommZone` | корень | `GetComponentInParent<>()` → self |

## Shared assets (НЕ создаются заново)

| Ассет | Назначение |
|---|---|
| `ModuleShopDatabase.asset` | Каталог модулей для RepairManager |
| `Npc_Goblin.asset` | Данные NPC (NpcAttacker/NpcTarget) |
| `Npc_peacfull_market_zone Variant.prefab` | Префаб NPC рынка |

## ID-схема

При namespace = `Primium_farm_1_1`:

- **DockStationDef.stationId** = `DockStation_Primium_farm_1_1`
- **DockStationDef.locationId** = `PRIMIUM_FARM_1_1`
- **MarketConfig.locationId** = `PRIMIUM_FARM_1_1` (нормализован в UpperCase)
- **OuterCommZone.stationId** = `DockStation_Primium_farm_1_1`
- **Pad.padId** = `Pad_01` … `Pad_NN`

> ⚠️ locationId в DockStationDefinition и MarketConfig **совпадают** (синхронизированы).

## Эталонная заготовка

Сцена: `WorldScene_0_0`  
Объект: `WorldRoot_0_0/Primum_farms/Средняя 0_0`

Это эталон, с которого скрипт берёт структуру и значения по умолчанию.

## Скрипт

`Assets/_Project/Scripts/Editor/PortStationCreator.cs`
