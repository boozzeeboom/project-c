# Шаг 1 (дополнение): Создание UI панели подключения

**Дата:** 4 апреля 2026 г.

---

## 📋 Создание панели NetworkUI в сцене

### Действие 1: Создать Canvas

1. В **Hierarchy** (если пусто — открой сцену `ProjectC_1`)
2. **Right-click → UI → Canvas**
3. Если Unity спросит добавить EventSystem — нажми **Yes**
4. Выбери `Canvas` → в **Inspector**:
   - **Render Mode:** `Screen Space - Overlay`
   - **Sort Order:** `1` (чтобы было поверх всего)

---

### Действие 2: Создать панель подключения

1. Выбери `Canvas` → **Right-click → UI → Panel**
2. Переименуй в **`ConnectionPanel`**
3. В **Inspector → Rect Transform**:
   - **Anchors:** Center-Middle (зажми Alt + кликни на центр)
   - **Width:** `300`, **Height:** `200`
   - **Pos X:** `0`, **Pos Y:** `0`

---

### Действие 3: Добавить заголовок

1. Выбери `ConnectionPanel` → **Right-click → UI → Text - TextMeshPro**
2. Переименуй в **`TitleText`**
3. В **Inspector**:
   - **Rect Transform:** Anchors: Top-Center, Width: `250`, Height: `40`, Pos Y: `70`
   - **Text:** `Project C — Сеть`
   - **Alignment:** Center
   - **Font Size:** `24`

*(Если спросит TMP Essentials — нажми Import)*

---

### Действие 4: Поле IP

1. `ConnectionPanel` → **Right-click → UI → Input Field - TextMeshPro**
2. Переименуй в **`ServerIpInput`**
3. **Rect Transform:** Anchors: Top-Center, Width: `200`, Height: `30`, Pos Y: `30`
4. Внутри найди `Text (TMP)` → **Text:** `127.0.0.1`
5. Внутри найди `Placeholder` → **Text:** `IP сервера`

---

### Действие 5: Поле порта

1. `ConnectionPanel` → **Right-click → UI → Input Field - TextMeshPro**
2. Переименуй в **`ServerPortInput`**
3. **Rect Transform:** Anchors: Top-Center, Width: `200`, Height: `30`, Pos Y: `-10`
4. Внутри `Text (TMP)` → **Text:** `7777`
5. Внутри `Placeholder` → **Text:** `Порт`

---

### Действие 6: Кнопка Host

1. `ConnectionPanel` → **Right-click → UI → Button - TextMeshPro**
2. Переименуй в **`StartHostButton`**
3. **Rect Transform:** Anchors: Bottom-Center, Width: `150`, Height: `35`, Pos Y: `-55`
4. Внутри `Text (TMP)` → **Text:** `Start Host`
5. Выбери кнопку → **Image Component → Color:** зелёный (для наглядности)

---

### Действие 7: Кнопка Client

1. `ConnectionPanel` → **Right-click → UI → Button - TextMeshPro**
2. Переименуй в **`StartClientButton`**
3. **Rect Transform:** Anchors: Bottom-Center, Width: `150`, Height: `35`, Pos Y: `-15`
4. Внутри `Text (TMP)` → **Text:** `Connect Client`
5. **Image Component → Color:** синий

---

### Действие 8: Текст статуса

1. `ConnectionPanel` → **Right-click → UI → Text - TextMeshPro**
2. Переименуй в **`StatusText`**
3. **Rect Transform:** Anchors: Bottom-Center, Width: `250`, Height: `25`, Pos Y: `-85`
4. **Text:** `Ожидание...`
5. **Alignment:** Center, **Font Size:** `14`

---

### Действие 9: Добавить скрипт NetworkUI

1. Выбери `Canvas` (или `ConnectionPanel`) → **Add Component**
2. Найди и добавь **`NetworkUI`** (скрипт)
3. В **Inspector** у компонента NetworkUI заполни поля:
   - **Start Host Button** → перетащи `StartHostButton`
   - **Start Client Button** → перетащи `StartClientButton`
   - **Server Ip Input** → перетащи `ServerIpInput`
   - **Server Port Input** → перетащи `ServerPortInput`
   - **Status Text** → перетащи `StatusText`
   - **Connection Panel** → перетащи `ConnectionPanel`

---

### Действие 10: Сохранить сцену

1. **Ctrl+S** или **File → Save**

---

## ✅ Проверка

1. **Play**
2. Панель должна быть видна на экране
3. Нажми **Start Host** → статус изменится на "Хост запущен"
4. В **Console** должно появиться: `[Player] Локальный игрок spawned`
5. В **Hierarchy** появится `NetworkPlayer(clone)`

---

## 📐 Итоговая структура в Hierarchy

```
Canvas
├── ConnectionPanel (Panel)
│   ├── TitleText (TextMeshPro)
│   ├── ServerIpInput (InputField TMP)
│   │   ├── Placeholder
│   │   └── Text
│   ├── ServerPortInput (InputField TMP)
│   │   ├── Placeholder
│   │   └── Text
│   ├── StartHostButton (Button TMP)
│   │   └── Text
│   ├── StartClientButton (Button TMP)
│   │   └── Text
│   └── StatusText (TextMeshPro)
└── NetworkUI (компонент на Canvas)
```

---

**После выполнения:** сообщи результат, закоммитим.
