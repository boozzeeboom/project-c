# Шаг: Сетевой ShipController и посадка/выход (F)

**Дата:** 5 апреля 2026 г.

---

## 📋 Архитектура

```
Клиент нажимает F:
  1. SubmitSwitchRpc() → все узнают о переключении
  2. Локально: переключить управление, камеру, видимость

Клиент в корабле (WASD + Q/E + Shift):
  1. ShipController шлёт ввод на сервер через ServerRpc
  2. Сервер двигает Rigidbody корабля
  3. NetworkTransform синхронизирует позицию всем
```

## 🔧 Изменения

### NetworkPlayer.cs
- Добавить поле `NetworkVariable<ShipController> currentShip`
- Добавить обработку F (SubmitSwitchRpc через SendTo.Everyone)
- Методы: TryBoardShip(), Disembark()
- При посадке: скрыть игрока, камера на корабль, включить ShipController
- При выходе: показать игрока, камера на игрока

### ShipController.cs
- Добавить ServerRpc для ввода: `SubmitShipInputRpc(thrust, yaw, pitch, vertical, boost)`
- В FixedUpdate: только сервер применяет силы к Rigidbody
- Клиенты только читают (NetworkTransform)

---

## ⚠️ Важно

- Каждый корабль на сцене должен иметь `NetworkTransform` + `NetworkObject`
- Корабли должны быть зарегистрированы в DefaultNetworkPrefabs (если спавнятся динамически)
- Если корабли стоят на сцене с самого начала — достаточно NetworkTransform на них
