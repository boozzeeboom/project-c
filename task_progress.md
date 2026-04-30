# NetworkManagerController Fix Plan

## Проблема

`StartHost()` использует reflection для создания `NetworkConfig`, но:
- В NGO 2.x внутреннее поле `m_NetworkConfig` не находится через `GetField`
- Reflection approach не работает

## Root Cause

1. **Reflection failure**: `GetField("m_NetworkConfig")` возвращает `null`
2. **BootstrapSceneGenerator использует SerializedObject** (строки 156-158) - это Editor-time подход
3. **Runtime approach**: NGO сам инициализирует NetworkConfig из транспорта

## Решение

1. Убрать reflection для NetworkConfig
2. Использовать `UnityTransport.SetConnectionData()` для настройки
3. Просто вызвать `networkManager.StartHost()` - NGO инициализирует конфиг сам
4. Проверить что transport назначен правильно

## Файлы для изменения

- [ ] `NetworkManagerController.cs` - StartHost(), StartServer(), StartHostCoroutine()
- [ ] Добавить диагностику состояния NM