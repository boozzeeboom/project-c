# Инструкция по созданию материалов для облачных слоёв

**Важно:** Материалы должны быть созданы через Unity Editor UI.

---

## 📋 Шаг 1: Открыть Unity Editor

1. Откройте проект ProjectC в Unity Editor
2. Перейдите в папку `Assets/_Project/Materials/Clouds/` в Project окне

---

## 📋 Шаг 2: Создать Material_Cloud_Upper.mat

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Material**
2. Назвать файл: `Material_Cloud_Upper`
3. Выбрать созданный материал и настроить в Inspector:

### Настройки Material_Cloud_Upper:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Shader** | ProjectC/CloudGhibli (если доступен) | Или Universal Render Pipeline/Unlit |
| **_BaseColor** | R: 0.96, G: 0.94, B: 0.91, A: 0.4 | Цвет #f5f0e8, прозрачность 40% |
| **_RimColor** | R: 1.0, G: 0.83, B: 0.65, A: 0.6 | Золотистый rim glow |
| **_RimPower** | 2.0 | Сила rim glow |
| **_Softness** | 0.3 | Мягкость краёв |
| **_NoiseTex** | ProceduralNoiseGenerator.texture1 | Процедурная текстура |
| **_NoiseTex2** | ProceduralNoiseGenerator.texture2 | Вторая процедурная текстура |
| **_NoiseScale** | 1.0 | Масштаб шума |
| **_AlphaBase** | 0.4 | Базовая прозрачность |
| **_VertexDisplacement** | 3.0 | Вершинное смещение |

---

## 📋 Шаг 3: Создать Material_Cloud_Middle.mat

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Material**
2. Назвать файл: `Material_Cloud_Middle`
3. Выбрать созданный материал и настроить в Inspector:

### Настройки Material_Cloud_Middle:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Shader** | ProjectC/CloudGhibli (если доступен) | Или Universal Render Pipeline/Unlit |
| **_BaseColor** | R: 0.83, G: 0.82, B: 0.78, A: 0.6 | Цвет #d4d0c8, прозрачность 60% |
| **_RimColor** | R: 1.0, G: 0.83, B: 0.65, A: 0.6 | Золотистый rim glow |
| **_RimPower** | 2.0 | Сила rim glow |
| **_Softness** | 0.4 | Мягкость краёв |
| **_NoiseTex** | ProceduralNoiseGenerator.texture1 | Процедурная текстура |
| **_NoiseTex2** | ProceduralNoiseGenerator.texture2 | Вторая процедурная текстура |
| **_NoiseScale** | 1.0 | Масштаб шума |
| **_AlphaBase** | 0.6 | Базовая прозрачность |
| **_VertexDisplacement** | 3.0 | Вершинное смещение |

---

## 📋 Шаг 4: Создать Material_Cloud_Lower.mat

1. В Project окне: **Правой кнопкой** в папке `Clouds` → **Create** → **Material**
2. Назвать файл: `Material_Cloud_Lower`
3. Выбрать созданный материал и настроить в Inspector:

### Настройки Material_Cloud_Lower:

| Параметр | Значение | Описание |
|----------|----------|----------|
| **Shader** | ProjectC/CloudGhibli (если доступен) | Или Universal Render Pipeline/Unlit |
| **_BaseColor** | R: 0.54, G: 0.54, B: 0.54, A: 0.8 | Цвет #8a8a8a, прозрачность 80% |
| **_RimColor** | R: 1.0, G: 0.83, B: 0.65, A: 0.6 | Золотистый rim glow |
| **_RimPower** | 2.0 | Сила rim glow |
| **_Softness** | 0.5 | Мягкость краёв |
| **_NoiseTex** | ProceduralNoiseGenerator.texture1 | Процедурная текстура |
| **_NoiseTex2** | ProceduralNoiseGenerator.texture2 | Вторая процедурная текстура |
| **_NoiseScale** | 1.0 | Масштаб шума |
| **_AlphaBase** | 0.8 | Базовая прозрачность |
| **_VertexDisplacement** | 3.0 | Вершинное смещение |

---

## 📋 Шаг 5: Проверка

После создания всех 3 материалов, в папке `Assets/_Project/Materials/Clouds/` должны быть:

- ✅ `Material_Cloud_Upper.mat`
- ✅ `Material_Cloud_Middle.mat`
- ✅ `Material_Cloud_Lower.mat`

**Проверка в Inspector:**
- Каждый материал имеет правильный цвет и настройки
- Shader установлен на ProjectC/CloudGhibli (или fallback URP Unlit)

---

## ⚠️ КРИТИЧНО

1. **НЕ создавать материалы через C# скрипты** — только через Unity Editor UI
2. **Цвета** должны соответствовать таблице в SESSION_B3_Prompt.md
3. **CloudGhibli shader** — предпочтительный выбор, если доступен
4. **Fallback shader** — Universal Render Pipeline/Unlit

---

**После создания:** назначить эти материалы в соответствующие CloudLayerConfig ассеты.
