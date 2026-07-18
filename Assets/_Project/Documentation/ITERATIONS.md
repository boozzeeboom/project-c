# Итерации разработки

## Итерация от 2025-01-21

**Задача:** Чистка старых editor-скриптов и унификация меню Tools/Project C
**Коммит:** `957d27e31d6e74259dec0b132d2a6d82327bea51` — T-EDITOR01: чистка старых editor-скриптов и унификация меню Tools/Project C
**Изменения:**
- Удалено 53 editor-скрипта (старые генераторы сцен, миграции, отладочные setup-ы)
- Унифицированы MenuItem: все Tools/ProjectC/* → Tools/Project C/*
- Cloud Generator перенесён в Tools/Project C/Cloud Generator
- Оставлены: ShipPresetCreator, PortStationCreator, Quests CSV/Editor, Items CSV/Editor, CloudGenerator
