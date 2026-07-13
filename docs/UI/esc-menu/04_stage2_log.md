# Этап 2 — Лог реализации (2026-07-13)

> **Статус:** ✅ Завершён

---

## Созданные файлы

| Файл | Описание |
|---|---|
| `Scripts/Core/SettingsManager.cs` | Статический Singleton: PlayerPrefs, C# events, `ApplyAll()` |
| `Scripts/UI/EscMenu/SettingsWidgets.cs` | Фабрика: `CreateSlider/Toggle/Dropdown/SectionHeader` |
| `Resources/UI/EscMenuSettingsStyles.uss` | Стили для виджетов: slider, toggle, dropdown, section-header |

## Изменённые файлы

| Файл | Изменение |
|---|---|
| `Scripts/Core/NetworkManagerController.cs` | `SettingsManager.ApplyAll()` при старте (после CreateUIManager) |
| `Scripts/UI/EscMenu/EscMenuWindow.cs` | `escSettingsUss` field + Resources.Load fallback в EnsureBuilt |

## SettingsManager API

```csharp
// Свойства (readonly, через сеттеры)
SettingsManager.MouseSensitivity   // float, 0.1-10
SettingsManager.InvertY            // bool
SettingsManager.MasterVolume       // float, 0-1 (→ AudioListener.volume)
SettingsManager.Subtitles          // bool
SettingsManager.QualityLevel       // int (→ QualitySettings)
SettingsManager.Fullscreen         // bool (→ Screen.fullScreenMode)
SettingsManager.VSync              // bool (→ QualitySettings.vSyncCount)
SettingsManager.AntiAliasing       // int (→ QualitySettings.antiAliasing)

// Сеттеры (сохраняют в PlayerPrefs + применяют сразу)
SetMouseSensitivity(float)
SetInvertY(bool)
SetMasterVolume(float)
SetSubtitles(bool)
SetQualityLevel(int)
SetFullscreen(bool)
SetVSync(bool)
SetAntiAliasing(int)

// События
OnMouseSensitivityChanged, OnInvertYChanged, OnMasterVolumeChanged, OnSubtitlesChanged

// Сохранение/загрузка
Load() / Save() / ApplyAll()
```

## SettingsWidgets API

```csharp
CreateSlider(label, min, max, initial, onChange)    → VisualElement (row с лейблом, слайдером, значением)
CreateToggle(label, initial, onChange)               → VisualElement (row с лейблом и Toggle)
CreateDropdown(label, choices, selected, onChange)   → VisualElement (row с лейблом и CustomDropdown)
CreateSectionHeader(title)                           → Label (заголовок секции)
```

Все виджеты используют USS-классы: `esc-setting-row`, `esc-setting-label`, `esc-setting-slider`, `esc-setting-toggle`, `esc-setting-dropdown`.

Dropdown использует существующий `CustomDropdown` (из CharacterWindow) — полностью стилизуемый VisualElement вместо Unity DropdownField.
