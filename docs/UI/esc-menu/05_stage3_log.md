# Этап 3 — Лог реализации (2026-07-13)

> **Статус:** 3a ✅, 3b ✅, 3c ✅, 3d ⏳ placeholder

---

## 3a. GraphicsSettingsSection

| Настройка | Виджет | API |
|---|---|---|
| Качество | Dropdown (Low/Med/High/Ultra) | `QualitySettings.SetQualityLevel()` |
| Разрешение | Dropdown (`Screen.resolutions`) | `Screen.SetResolution()` |
| Полный экран | Toggle | `Screen.fullScreenMode` |
| VSync | Toggle | `QualitySettings.vSyncCount` |
| Сглаживание | Dropdown (Off/2x/4x/8x) | `QualitySettings.antiAliasing` |

Файл: `Assets/_Project/Scripts/UI/EscMenu/GraphicsSettingsSection.cs`

## 3b. AudioSettingsSection

| Настройка | Виджет | API |
|---|---|---|
| Общая громкость | Slider 0-100% | `AudioListener.volume` |
| Музыка/Эффекты/Голос/UI | Slider (placeholder) | Требуется AudioMixer |

Файл: `Assets/_Project/Scripts/UI/EscMenu/AudioSettingsSection.cs`

## 3c. GameplaySettingsSection

| Настройка | Виджет | Значения |
|---|---|---|
| Чувств. мыши | Slider | 0.1–10.0 |
| Инвертировать Y | Toggle | On/Off |
| Субтитры | Toggle | On/Off |
| Язык | — | DEFERRED |

Файл: `Assets/_Project/Scripts/UI/EscMenu/GameplaySettingsSection.cs`

## 3d. KeybindingsWindow как sub-page

⏳ **Placeholder.** Будет реализован отдельно — требует модификации KeybindingsWindow для работы внутри EscMenu.

---

## Все секции подключены

В `EscMenuWindow.NavigateToSettingsMenu()` теперь 4 кнопки, 3 из которых ведут на реальные страницы:

```
НАСТРОЙКИ → подменю:
  ├── Управление → placeholder (3d)
  ├── Графика    → GraphicsSettingsSection.Create()
  ├── Звук       → AudioSettingsSection.Create()
  └── Геймплей   → GameplaySettingsSection.Create()
```
