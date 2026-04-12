# Сессия 4 — Баг: ModuleSlot не блокирует несовместимые типы через Inspector

**Дата:** 12 апреля 2026 | **Серьёзность:** Средняя | **Статус:** Открт

---

## Описание

На компоненте `ModuleSlot` можно перетащить **любой** модуль в поле `Installed Module` через Unity Inspector, независимо от `Slot Type`.

**Ожидание:** Propulsion-модуль (YAW_ENH) НЕ должен устанавливаться в Utility-слот.
**Факт:** Inspector позволяет перетащить — валидация не срабатывает.

---

## Причина

`installedModule` — публичное поле `public ShipModule installedModule;`. Unity Inspector напрямую записывает значение в обход метода `InstallModule()`, где находится валидация.

Метод `InstallModule()` вызывается только программно, но не при ручном перетаскивании в Inspector.

---

## Влияние

- **Тестирование:** Невозможно проверить блокировку несовместимых слотов через Inspector
- **Runtime:** Не влияет — установка через `ShipModuleManager.InstallModule()` вызывает `slot.InstallModule()` → `ValidateCompatibility()`
- **Editor:** Дизайнер может случайно установить несовместимый модуль

---

## Варианты Исправления

### Вариант A: Custom PropertyDrawer для ModuleSlot
Создать `ModuleSlotEditor.cs` с `OnInspectorGUI()`, который проверяет совместимость при изменении поля и выдаёт warning.

### Вариант B: OnValidate()
Добавить в `ModuleSlot.cs`:
```csharp
private void OnValidate() {
    if (installedModule != null && !ValidateCompatibility(installedModule)) {
        Debug.LogWarning($"[ModuleSlot] Incompatible module '{installedModule.moduleId}' for slot type '{slotType}'.");
    }
}
```

### Вариант C: Custom Attribute
Создать `[ValidateModuleSlot]` attribute и проверять через `ISerializationCallbackReceiver`.

**Рекомендация:** Вариант B — самый простой, работает в Editor, не требует дополнительных файлов.

---

## Воспроизведение

1. Выбрать корабль в сцене
2. Add Component → ModuleSlot
3. Slot Type: **Utility**
4. Перетащить `MODULE_YAW_ENH` (Propulsion) в поле Installed Module
5. **Ожидание:** ошибка/warning
6. **Факт:** модуль установлен без предупреждения

---

*Зафиксировано: 12 апреля 2026 | Сессия 4*
