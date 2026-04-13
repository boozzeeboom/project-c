# Инструкция по созданию CloudLayerConfig ассетов

**Важно:** ScriptableObject ассеты должны быть созданы через Unity Editor UI.

---

## 📋 Шаг 1: Открыть Unity Editor

1. Откройте проект ProjectC в Unity Editor
2. Перейдите в папку `Assets/_Project/Data/Clouds/` в Project окне

---

## 📋 Шаг 2: Создать CloudLayerConfig_Upper.asset

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Project C** → **Cloud Layer Config**
2. Назвать файл: `CloudLayerConfig_Upper`
3. Выбрать созданный ассет и заполнить параметры в Inspector:

### Параметры Upper слоя:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Layer Type** | Upper | Перистые облака |
| **Min Height** | 7000 | Минимальная высота (метры) |
| **Max Height** | 9000 | Максимальная высота (метры) |
| **Density** | 0.3 | Редкие облака |
| **Cloud Size** | 150 | Размер облака (метры) |
| **Size Variation** | 2.0 | Вариативность размера |
| **Move Speed** | 0.5 | Медленное движение |
| **Move Direction** | (1, 0, 0) | Движение по X |
| **Animate Morph** | ✅ true | Анимировать форму |
| **Morph Speed** | 0.3 | Медленная анимация |
| **Cloud Material** | Material_Cloud_Upper | См. шаг создания материалов |
| **Use 2D Planes** | ✅ true | Перистые как плоскости |

---

## 📋 Шаг 3: Создать CloudLayerConfig_Middle.asset

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Project C** → **Cloud Layer Config**
2. Назвать файл: `CloudLayerConfig_Middle`
3. Выбрать созданный ассет и заполнить параметры в Inspector:

### Параметры Middle слоя:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Layer Type** | Middle | Высоко-кучевые облака |
| **Min Height** | 4000 | Минимальная высота (метры) |
| **Max Height** | 7000 | Максимальная высота (метры) |
| **Density** | 0.6 | Средние облака |
| **Cloud Size** | 100 | Размер облака (метры) |
| **Size Variation** | 2.0 | Вариативность размера |
| **Move Speed** | 1.0 | Среднее движение |
| **Move Direction** | (1, 0, 0) | Движение по X |
| **Animate Morph** | ✅ true | Анимировать форму |
| **Morph Speed** | 0.5 | Средняя анимация |
| **Cloud Material** | Material_Cloud_Middle | См. шаг создания материалов |
| **Use 2D Planes** | ❌ false | Объёмные сферы |

---

## 📋 Шаг 4: Создать CloudLayerConfig_Lower.asset

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Project C** → **Cloud Layer Config**
2. Назвать файл: `CloudLayerConfig_Lower`
3. Выбрать созданный ассет и заполнить параметры в Inspector:

### Параметры Lower слоя:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Layer Type** | Lower | Слоистые облака |
| **Min Height** | 1500 | Минимальная высота (метры) |
| **Max Height** | 4000 | Максимальная высота (метры) |
| **Density** | 0.8 | Плотные облака |
| **Cloud Size** | 80 | Размер облака (метры) |
| **Size Variation** | 1.5 | Меньшая вариативность |
| **Move Speed** | 2.0 | Быстрое движение |
| **Move Direction** | (1, 0, 0) | Движение по X |
| **Animate Morph** | ❌ false | Без анимации формы |
| **Morph Speed** | 0.5 | Не используется |
| **Cloud Material** | Material_Cloud_Lower | См. шаг создания материалов |
| **Use 2D Planes** | ❌ false | Объёмные сферы |

---

## 📋 Шаг 5: Проверка

После создания всех 3 ассетов, в папке `Assets/_Project/Data/Clouds/` должны быть:

- ✅ `CloudLayerConfig_Upper.asset`
- ✅ `CloudLayerConfig_Middle.asset`
- ✅ `CloudLayerConfig_Lower.asset`

**Проверка в Inspector:**
- Каждый ассет должен иметь правильные параметры по таблице выше
- **Все высоты указаны в МЕТРАХ (не scaled units!)**

---

## ⚠️ КРИТИЧНО

1. **НЕ использовать scaled units (Y=15-90)** — только метры (Y=1500-9000)
2. **Cloud Material** нужно назначить после создания материалов (см. инструкцию материалов)
3. **Upper слой** использует 2D Planes (перистые облака), остальные — сферы

---

**После создания:** назначить эти 3 ассета на CloudSystem в Inspector.
