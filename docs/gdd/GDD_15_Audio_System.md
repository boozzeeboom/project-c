# GDD-15: Audio System — Project C: The Clouds

| Параметр | Значение |
|---|---|
| **Проект** | Project C: The Clouds |
| **Версия** | 1.0 |
| **Статус** | [🔴 Запланировано] |
| **Дата** | 06.04.2026 |
| **Автор** | GDD Team |
| **Обновлено** | 06.04.2026 |

---

## Контекст

**2090 год.** Горные вершины пробиваются сквозь облака. Корабли на антигравитации парят между парящими островами. Sci-Fi + Ghibli стиль. Сетевой мультиплеер (Unity 6, NGO).

Аудио-система **ещё не реализована**. Все элементы в этом документе — **[🔴 Запланировано]**.

---

## Секция 1: Audio Architecture

### 1.1 AudioMixer Structure

Проект использует Unity AudioMixer с иерархической группой:

```
Master (группа по умолчанию)
├── SFX
│   ├── SFX_Steps
│   ├── SFX_Ship
│   ├── SFX_UI
│   ├── SFX_Pickup
│   └── SFX_Chest
├── Music
│   ├── Music_Explore
│   ├── Music_Fly
│   └── Music_Danger
├── Ambient
│   ├── Ambient_Wind
│   ├── Ambient_Clouds
│   └── Ambient_City
└── UI
    └── UI_Click
```

### 1.2 AudioMixer Groups — спецификация

| Группа | Назначение | Exposure (dB) | Snapshot |
|---|---|---|---|
| **Master** | Главный выход, общий контроль громкости | 0 | _MasterMute_ |
| **SFX** | Все звуковые эффекты игры | 0 | _SFXMute_ |
| **SFX_Steps** | Шаги по разным поверхностям | -6 | — |
| **SFX_Ship** | Двигатель, лопасти, буст корабля | -3 | — |
| **SFX_UI** | Клики, hover, подтверждение меню | -12 | — |
| **SFX_Pickup** | Подбор ресурсов, предметов | -6 | — |
| **SFX_Chest** | Открытие/закрытие сундуков | -6 | — |
| **Music** | Фоновая музыка | 0 | _MusicMute_ |
| **Music_Explore** | Исследование мира | -6 | — |
| **Music_Fly** | Полёт на корабле | -3 | — |
| **Music_Danger** | Опасные зоны (Завеса, шторм) | -3 | — |
| **Ambient** | Окружающие звуки мира | 0 | _AmbientMute_ |
| **Ambient_Wind** | Ветер на высотах | -9 | — |
| **Ambient_Clouds** | Звуки внутри облаков | -12 | — |
| **Ambient_City** | Города на вершинах | -9 | — |
| **UI** | Звуки интерфейса | 0 | _UIMute_ |

### 1.3 AudioMixer Parameters (Exposed)

| Параметр | Группа | Диапазон | Назначение |
|---|---|---|---|
| `MasterVolume` | Master | -80..0 dB | Общая громкость |
| `SFXVolume` | SFX | -80..0 dB | Громкость эффектов |
| `MusicVolume` | Music | -80..0 dB | Громкость музыки |
| `AmbientVolume` | Ambient | -80..0 dB | Громкость окружения |
| `UIVolume` | UI | -80..0 dB | Громкость интерфейса |
| `StepsVolume` | SFX_Steps | -80..0 dB | Громкость шагов |
| `ShipVolume` | SFX_Ship | -80..0 dB | Громкость корабля |

### 1.4 AudioMixer Snapshots

| Snapshot | Состояние | Триггер |
|---|---|---|
| _MasterMute_ | Master = -80 dB | Кнопка Mute |
| _SFXMute_ | SFX = -80 dB | Настройки → SFX Off |
| _MusicMute_ | Music = -80 dB | Настройки → Music Off |
| _AmbientMute_ | Ambient = -80 dB | Настройки → Ambient Off |
| _UIMute_ | UI = -80 dB | Настройки → UI Off |
| _DangerMode_ | Music_Danger = 0 dB, Music_Explore = -24 dB | Вход в опасную зону |
| _CinematicMode_ | SFX = -12 dB, Music = -3 dB, Ambient = -6 dB | Катсцена |

### 1.5 Script Architecture

| Скрипт | Назначение |
|---|---|
| `AudioManager.cs` | Singleton. Инициализация AudioMixer, глобальные методы PlaySFX/PlayMusic |
| `AudioSettings.cs` | Привязка к UI слайдерам, сохранение/загрузка в PlayerPrefs |
| `SFXPlayer.cs` | Воспроизведение 3D/2D звуков по запросу |
| `MusicManager.cs` | Кроссфейд между музыкальными состояниями |
| `AmbientController.cs` | Управление ambient-звуками по зонам |
| `FootstepAudio.cs` | Raycast surface detection → воспроизведение шагов |
| `ShipAudioController.cs` | Звуки корабля (двигатель, буст, лопасти) |

---

## Секция 2: Sound Effects

### 2.1 Footstep Sounds (Шаги)

| Поверхность | Файл | Pitch Range | Volume |
|---|---|---|---|
| Камень/скала | `SFX_Footstep_Stone_01.wav` | 0.9–1.1 | -6 dB |
| Металл (палуба) | `SFX_Footstep_Metal_01.wav` | 0.9–1.1 | -4 dB |
| Дерево (помост) | `SFX_Footstep_Wood_01.wav` | 0.9–1.1 | -6 dB |
| Трава | `SFX_Footstep_Grass_01.wav` | 0.85–1.15 | -8 dB |
| Вода/лужа | `SFX_Footstep_Water_01.wav` | 0.9–1.1 | -6 dB |

- **4 вариации** на каждую поверхность (01–04) для рандомизации
- **Pitch randomization**: ±10% для естественности
- **Trigger**: OnCollisionEnter + проверка surface tag

### 2.2 Ship Sounds (Корабль)

| Звук | Файл | Тип | Loop |
|---|---|---|---|
| Двигатель (холостой) | `SFX_ShipEngine_Idle.wav` | Loop | ✅ |
| Двигатель (полёт) | `SFX_ShipEngine_Fly.wav` | Loop | ✅ |
| Лопасти антигравитации | `SFX_ShipBlades_Hum.wav` | Loop | ✅ |
| Буст/ускорение | `SFX_ShipBoost_Activate.wav` | One-shot | ❌ |
| Буст (цикл) | `SFX_ShipBoost_Loop.wav` | Loop | ✅ |
| Буст (завершение) | `SFX_ShipBoost_End.wav` | One-shot | ❌ |
| Посадка | `SFX_ShipLand.wav` | One-shot | ❌ |
| Взлёт | `SFX_ShipTakeoff.wav` | One-shot | ❌ |
| Столкновение | `SFX_ShipImpact_Hit.wav` | One-shot | ❌ |

### 2.3 UI Sounds

| Действие | Файл | Тип |
|---|---|---|
| Клик кнопки | `SFX_UI_Click_01.wav` | One-shot |
| Hover | `SFX_UI_Hover.wav` | One-shot |
| Подтверждение | `SFX_UI_Confirm.wav` | One-shot |
| Отмена/назад | `SFX_UI_Cancel.wav` | One-shot |
| Открытие меню | `SFX_UI_Open.wav` | One-shot |
| Закрытие меню | `SFX_UI_Close.wav` | One-shot |
| Ошибка | `SFX_UI_Error.wav` | One-shot |

### 2.4 Pickup Sounds (Подбор ресурсов)

| Действие | Файл | Тип |
|---|---|---|
| Подбор ресурса (малый) | `SFX_Pickup_Small.wav` | One-shot |
| Подбор ресурса (большой) | `SFX_Pickup_Large.wav` | One-shot |
| Ресурс собран (комбо) | `SFX_Pickup_Combo.wav` | One-shot |
| Инвентарь полон | `SFX_Pickup_Full.wav` | One-shot |

### 2.5 Chest Sounds (Сундуки)

| Действие | Файл | Тип |
|---|---|---|
| Открытие сундука | `SFX_Chest_Open.wav` | One-shot |
| Закрытие сундука | `SFX_Chest_Close.wav` | One-shot |
| Редкий предмет | `SFX_Chest_Rare.wav` | One-shot |
| Сундук заблокирован | `SFX_Chest_Locked.wav` | One-shot |

---

## Секция 3: Ambient Sounds

### 3.1 Ambient Zones

| Зона | Файл | Loop | Priority |
|---|---|---|---|
| Ветер (открытое небо) | `AMB_Wind_Open.wav` | ✅ | 1 |
| Ветер (ущелье) | `AMB_Wind_Canyon.wav` | ✅ | 1 |
| Облака (лёгкие) | `AMB_Clouds_Light.wav` | ✅ | 2 |
| Облака (плотные) | `AMB_Clouds_Heavy.wav` | ✅ | 2 |
| Город (далёкий) | `AMB_City_Distant.wav` | ✅ | 3 |
| Город (близкий) | `AMB_City_Near.wav` | ✅ | 3 |
| Завеса (future) | `AMB_Veil_Mystery.wav` | ✅ | 4 |
| Шторм (future) | `AMB_Storm.wav` | ✅ | 4 |

### 3.2 Zone Transition Logic

| Переход | Crossfade Duration | Trigger |
|---|---|---|
| Ветер → Облака | 3.0 сек | OnTriggerEnter (Cloud Zone) |
| Облака → Город | 2.0 сек | OnTriggerEnter (City Zone) |
| Город → Завеса | 4.0 сек | OnTriggerEnter (Veil Zone) [future] |
| Любая → Шторм | 1.5 сек | Weather System [future] |

### 3.3 Ambient Layering

```
Master Ambient Volume
├── Wind (постоянный слой, всегда активен)
├── Clouds (активен при входе в облака)
├── City (активен при приближении к городу)
└── Special (Завеса, шторм — по событию)
```

Каждый слой управляется через **AudioMixer group volume**. Переходы через **AudioMixerSnapshot**.

---

## Секция 4: Music System

### 4.1 Music States

| Состояние | Файл | BPM | Mood |
|---|---|---|---|
| Exploration | `MUS_Explore_01.ogg` | 90 | Спокойный, исследовательский |
| Flight | `MUS_Fly_01.ogg` | 120 | Энергичный, полёт |
| Danger | `MUS_Danger_01.ogg` | 140 | Напряжённый, опасный |

### 4.2 Crossfade Transitions

| From → To | Fade Out | Fade In | Duration |
|---|---|---|---|
| Explore → Fly | -∞ dB | 0 dB | 2.0 сек |
| Fly → Explore | -∞ dB | 0 dB | 3.0 сек |
| Explore → Danger | -∞ dB | 0 dB | 1.0 сек |
| Fly → Danger | -∞ dB | 0 dB | 1.0 сек |
| Danger → Explore | -∞ dB | 0 dB | 4.0 сек |
| Danger → Fly | -∞ dB | 0 dB | 3.0 сек |

### 4.3 Music Manager — логика

```
Player State → Music State Mapping:
├── На земле, без угрозы → Explore
├── В полёте (корабль активен) → Fly
├── В зоне Завесы/шторма → Danger
├── В бою (future) → Danger
└── Катсцена → Mute all, play cinematic track
```

### 4.4 Music Tracks — вариации

| Состояние | Треки | Loop |
|---|---|---|
| Explore | `MUS_Explore_01.ogg` – `MUS_Explore_03.ogg` | ✅ |
| Fly | `MUS_Fly_01.ogg` – `MUS_Fly_02.ogg` | ✅ |
| Danger | `MUS_Danger_01.ogg` – `MUS_Danger_02.ogg` | ✅ |

- **Рандомизация**: при повторном входе в состояние — случайный трек
- **Избегание повторов**: не повторять последний сыгранный трек

---

## Секция 5: Positional Audio

### 5.1 3D Sound Sources

| Источник | Spatial Blend | Rolloff | Max Distance | Min Distance |
|---|---|---|---|---|
| Корабль (двигатель) | 1.0 (3D) | Logarithmic | 80 м | 5 м |
| Корабль (буст) | 1.0 (3D) | Logarithmic | 100 м | 5 м |
| NPC голоса (future) | 1.0 (3D) | Logarithmic | 30 м | 3 м |
| Сундук (открытие) | 1.0 (3D) | Logarithmic | 40 м | 5 м |
| Событие мира | 1.0 (3D) | Logarithmic | 60 м | 5 м |

### 5.2 Audio Source Settings

| Параметр | Значение | Примечание |
|---|---|---|
| **Spatial Blend** | 3D = 1.0 | Позиционный звук |
| **Rolloff Mode** | Custom (Logarithmic) | Реалистичное затухание |
| **Spread** | 0 | Направленный источник |
| **Doppler Level** | 0.5 | Лёгкий доплер-эффект |
| **Priority** | 128 (низкий) / 64 (высокий) | Приоритет при лимите voices |

### 5.3 Voice Limiting

| Платформа | Max Voices | Стратегия |
|---|---|---|
| PC | 64 | Приоритет по距离, отключение далёких |
| Mobile (future) | 32 | Агрессивный culling, LOD звука |

---

## Секция 6: Network Audio Sync

### 6.1 Audio Authority

| Тип звука | Authority | Видимость |
|---|---|---|
| **Позиционные (3D)** | Server → All Clients | Все слышат |
| **Локальные (2D)** | Client Owner | Только владелец |
| **UI** | Client Owner | Только владелец |
| **Ambient** | Client Owner (по зоне) | Только владелец |
| **Music** | Client Owner (по состоянию) | Только владелец |

### 6.2 NetworkAudioSync — архитектура

| Компонент | Назначение |
|---|---|
| `NetworkAudioSync.cs` | NetworkBehaviour. Синхронизация AudioSource параметров |
| `ServerRpc PlaySoundAtPosition` | Сервер вызывает RPC → все клиенты воспроизводят |
| `ClientRpc BroadcastSound` | Сервер рассылает звук всем клиентам |

### 6.3 Sync Rules

| Правило | Описание |
|---|---|
| **Host authoritative** | Только сервер решает, какие 3D звуки воспроизводить |
| **Position sync** | Позиция AudioSource синхронизируется через NetworkTransform |
| **Lag compensation** | Звук начинается с задержкой ≤100 мс (приемлемо) |
| **Client prediction** | Локальные звуки (шаги, UI) — без ожидания сервера |
| **Bandwidth limit** | Не более 5 NetworkAudioSync RPC/сек на клиента |

### 6.4 Пример: звук корабля в сети

```
Client A включает буст:
1. Локально: SFX_ShipBoost_Activate.wav → сразу (ClientRpc не нужен)
2. ServerRpc: PlayShipSound(boost, position)
3. Server → ClientRpc: AllClients.PlaySoundAtPosition(boost, position)
4. Client B, C, D: слышат буст с позиции корабля A
5. Client A: слышит локально + не дублирует из RPC
```

---

## Секция 7: Audio Settings

### 7.1 UI Elements

| Элемент | Тип | Диапазон | Сохранение |
|---|---|---|---|
| **Master Volume** | Slider | 0–100% | PlayerPrefs |
| **SFX Volume** | Slider | 0–100% | PlayerPrefs |
| **Music Volume** | Slider | 0–100% | PlayerPrefs |
| **Ambient Volume** | Slider | 0–100% | PlayerPrefs |
| **UI Volume** | Slider | 0–100% | PlayerPrefs |
| **Master Mute** | Toggle | On/Off | — |
| **Reset to Default** | Button | — | — |

### 7.2 PlayerPrefs Keys

| Ключ | Тип | Значение по умолчанию |
|---|---|---|
| `Audio_MasterVolume` | float | 1.0 |
| `Audio_SFXVolume` | float | 1.0 |
| `Audio_MusicVolume` | float | 0.8 |
| `Audio_AmbientVolume` | float | 0.9 |
| `Audio_UIVolume` | float | 0.7 |

### 7.3 Settings Flow

```
Player изменяет slider →
  AudioSettings.cs →
    AudioMixer.SetFloat(exposedParam, dbValue) →
      PlayerPrefs.SetFloat(key, value) →
        PlayerPrefs.Save()
```

### 7.4 dB Conversion

| Slider Value | dB Value | Формула |
|---|---|---|
| 0% | -80 dB | Полная тишина |
| 10% | -20 dB | `20 * Mathf.Log10(Mathf.Max(value, 0.0001f))` |
| 50% | -6 dB | `20 * Mathf.Log10(0.5)` |
| 100% | 0 dB | Максимум |

---

## Секция 8: Asset Requirements

### 8.1 Format Specifications

| Параметр | Требование |
|---|---|
| **Формат** | WAV (SFX), OGG (Music) |
| **Sample Rate** | 44.1 kHz |
| **Bit Depth** | 16-bit |
| **Каналы** | Mono (SFX), Stereo (Music/Ambient) |
| **Loop Points** | Указать в метаданных или в Unity Inspector |
| **Max Duration (SFX)** | ≤5 сек (one-shot), ≤30 сек (loop) |
| **Max Duration (Music)** | 60–180 сек |

### 8.2 Naming Convention

| Префикс | Категория | Пример |
|---|---|---|
| `SFX_` | Звуковые эффекты | `SFX_Footstep_Stone_01.wav` |
| `SFX_Ship` | Звуки корабля | `SFX_ShipEngine_Fly.wav` |
| `SFX_UI` | Звуки интерфейса | `SFX_UI_Click_01.wav` |
| `SFX_Pickup` | Подбор ресурсов | `SFX_Pickup_Small.wav` |
| `SFX_Chest` | Сундуки | `SFX_Chest_Open.wav` |
| `MUS_` | Музыка | `MUS_Explore_01.ogg` |
| `AMB_` | Ambient | `AMB_Wind_Open.wav` |

### 8.3 Folder Structure

```
Assets/Audio/
├── SFX/
│   ├── Steps/
│   │   ├── SFX_Footstep_Stone_01.wav
│   │   ├── SFX_Footstep_Stone_02.wav
│   │   ├── SFX_Footstep_Stone_03.wav
│   │   ├── SFX_Footstep_Stone_04.wav
│   │   ├── SFX_Footstep_Metal_01.wav
│   │   └── ...
│   ├── Ship/
│   │   ├── SFX_ShipEngine_Idle.wav
│   │   ├── SFX_ShipEngine_Fly.wav
│   │   ├── SFX_ShipBlades_Hum.wav
│   │   ├── SFX_ShipBoost_Activate.wav
│   │   └── ...
│   ├── UI/
│   │   ├── SFX_UI_Click_01.wav
│   │   ├── SFX_UI_Hover.wav
│   │   └── ...
│   ├── Pickup/
│   │   ├── SFX_Pickup_Small.wav
│   │   └── ...
│   └── Chest/
│       ├── SFX_Chest_Open.wav
│       └── ...
├── Music/
│   ├── MUS_Explore_01.ogg
│   ├── MUS_Explore_02.ogg
│   ├── MUS_Fly_01.ogg
│   └── MUS_Danger_01.ogg
├── Ambient/
│   ├── AMB_Wind_Open.wav
│   ├── AMB_Clouds_Light.wav
│   └── AMB_City_Distant.wav
├── Mixers/
│   └── MasterMixer.mixer
└── Scripts/
    ├── AudioManager.cs
    ├── AudioSettings.cs
    ├── MusicManager.cs
    ├── AmbientController.cs
    ├── FootstepAudio.cs
    ├── ShipAudioController.cs
    ├── SFXPlayer.cs
    └── NetworkAudioSync.cs
```

### 8.4 Asset Checklist

| Категория | Кол-во | Статус |
|---|---|---|
| Footstep (5 поверхностей × 4 вариации) | 20 файлов | [🔴 Запланировано] |
| Ship Sounds | 9 файлов | [🔴 Запланировано] |
| UI Sounds | 7 файлов | [🔴 Запланировано] |
| Pickup Sounds | 4 файла | [🔴 Запланировано] |
| Chest Sounds | 4 файла | [🔴 Запланировано] |
| Ambient Sounds | 8 файлов | [🔴 Запланировано] |
| Music Tracks (Explore ×3, Fly ×2, Danger ×2) | 7 файлов | [🔴 Запланировано] |
| **Итого** | **59 файлов** | **[🔴 Запланировано]** |

---

## Секция 9: Implementation Plan

### Приоритет 1: AudioMixer + Settings (Неделя 1)

| Задача | Описание | Статус |
|---|---|---|
| Создать MasterMixer | Настроить группы, expose parameters, snapshots | [🔴] |
| Реализовать AudioSettings UI | Слайдеры, сохранение в PlayerPrefs | [🔴] |
| AudioManager singleton | Базовый менеджер для PlaySFX/PlayMusic | [🔴] |
| Настроить Audio Source prefabs | 2D/3D пресеты с правильными настройками | [🔴] |

### Приоритет 2: SFX + Footsteps (Неделя 2)

| Задача | Описание | Статус |
|---|---|---|
| FootstepAudio.cs | Surface detection, pitch randomization | [🔴] |
| ShipAudioController.cs | Двигатель, буст, посадка/взлёт | [🔴] |
| UI Sounds integration | Привязка к кнопкам меню | [🔴] |
| Pickup/Chest SFX | Подбор ресурсов, сундуки | [🔴] |
| Загрузить все SFX ассеты | Импортировать, настроить Import Settings | [🔴] |

### Приоритет 3: Music + Ambient (Неделя 3)

| Задача | Описание | Статус |
|---|---|---|
| MusicManager.cs | Crossfade transitions, state machine | [🔴] |
| AmbientController.cs | Zone detection, layering | [🔴] |
| Настроить триггеры зон | Collider-триггеры для Cloud, City, Veil | [🔴] |
| Загрузить Music/Ambient ассеты | Импортировать, настроить loop points | [🔴] |

### Приоритет 4: Network + Polish (Неделя 4)

| Задача | Описание | Статус |
|---|---|---|
| NetworkAudioSync.cs | Server RPC, Client RPC, sync rules | [🔴] |
| Positional audio tuning | Rolloff curves, doppler, priority | [🔴] |
| Voice limiting | Max voices, culling strategy | [🔴] |
| Integration testing | Multiplayer audio sync, edge cases | [🔴] |
| Final mix pass | Баланс громкости всех групп | [🔴] |

---

## Секция 10: Acceptance Criteria

### 10.1 Functional Requirements

| # | Критерий | Проверка | Статус |
|---|---|---|---|
| F1 | AudioMixer имеет все 4 основные группы (Master, SFX, Music, Ambient, UI) | Inspector → AudioMixer | [🔴] |
| F2 | Слайдеры настроек меняют громкость в реальном времени | Изменить slider → проверить громкость | [🔴] |
| F3 | Настройки сохраняются между сессиями | Закрыть/открыть игру → проверить | [🔴] |
| F4 | Шаги меняют звук в зависимости от поверхности | Пройти по Stone/Metal/Wood → разные звуки | [🔴] |
| F5 | Корабль издаёт звуки двигателя/буста | Включить буст → слышно всем клиентам | [🔴] |
| F6 | Music crossfade при смене состояния | Explore → Fly → проверить плавность | [🔴] |
| F7 | Ambient меняется по зонам | Войти в облака → услышать AMB_Clouds | [🔴] |
| F8 | 3D звуки затухают с расстоянием | Отойти от источника → тише | [🔴] |
| F9 | Позиционные звуки синхронизированы в сети | Client A включает буст → Client B слышит | [🔴] |
| F10 | UI звуки только для локального клиента | Client A кликает → Client B не слышит | [🔴] |

### 10.2 Performance Requirements

| # | Критерий | Метрика | Статус |
|---|---|---|---|
| P1 | CPU Audio | < 2% CPU на audio processing | [🔴] |
| P2 | Max Voices | Не более 64 одновременных (PC) | [🔴] |
| P3 | Memory | Все аудио загружаются через AudioImporter (не в память) | [🔴] |
| P4 | Network Bandwidth | < 5 Audio RPC/сек на клиента | [🔴] |
| P5 | Load Time | AudioMixer инициализация < 1 сек | [🔴] |

### 10.3 Quality Requirements

| # | Критерий | Метрика | Статус |
|---|---|---|---|
| Q1 | Нет клипов/артефактов | Прослушать все звуки | [🔴] |
| Q2 | Crossfade плавный | Без рывков, пауз | [🔴] |
| Q3 | Volume баланс | Все группы слышимы при 100% Master | [🔴] |
| Q4 | Нет дублирования | Один звук = один воспроизведение | [🔴] |
| Q5 | Mute работает | Все группы мутятся корректно | [🔴] |

---

## Revision History

| Версия | Дата | Автор | Изменения |
|---|---|---|---|
| 1.0 | 06.04.2026 | GDD Team | Первоначальная версия |

---

*Документ: GDD-15 | Project C: The Clouds | Все элементы [🔴 Запланировано]*
