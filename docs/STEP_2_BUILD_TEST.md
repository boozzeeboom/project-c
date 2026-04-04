# Шаг 2: Тест Host + Client через билд

**Дата:** 4 апреля 2026 г.

---

## 📋 Инструкция

### Действие 1: Сборка клиента

1. В Unity (где запущен Host): **File → Build Settings**
2. **Platform:** PC, Mac & Linux Standalone (если не выбрана — выбери и нажми Switch Platform)
3. **Scenes In Build:** убедись что `ProjectC_1` в списке
   - Если нет — открой сцену и нажми **Add Open Scenes**
4. Нажми **Build**
5. Создай папку: `C:\UNITY_PROJECTS\ProjectC_client\Builds\`
6. Сохрани как: `ProjectC_Client.exe`
7. Дождись сборки

### Действие 2: Запуск клиента

1. В Unity **останови Play Mode** (выйди из Play)
2. Снова нажми **Play** → **Start Host** (запусти сервер)
3. Запусти `C:\UNITY_PROJECTS\ProjectC_client\Builds\ProjectC_Client.exe`
4. В окне билда:
   - IP: `127.0.0.1`
   - Порт: `7777`
   - Нажми **Connect Client**

### Действие 3: Проверка

**В консоли Unity (хост):**
```
[Player] Удалённый игрок spawned. OwnerClientId: 1
```

**В Hierarchy (хост):**
- Два объекта `NetworkPlayer(clone)`

**Тест синхронизации:**
1. Двигай персонажа в Unity (хост) → видно в билде
2. Двигай персонажа в билде → видно в Unity (хост)

---

## ❌ Если не работает

| Проблема | Решение |
|----------|---------|
| Клиент не подключается | Убедись что Host запущен в Unity |
| Firewall блокирует | Разреши Unity в брандмауэре Windows |
| Ошибка в билде | Проверь что NetworkManager.prefab и NetworkPlayer.prefab на месте |
| Нет NetworkPlayer(clone) в хосте | Проверь PlayerPrefab в NetworkManager |
