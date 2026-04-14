#!/bin/bash
# Cline Stop hook: Session cleanup and summary
# Авто-обновляет session-recovery.md

DATE=$(date '+%Y-%m-%d %H:%M')
RECOVERY_FILE=".cline/session-recovery.md"

echo "=== Session Complete ==="
echo "Time: $DATE"

# Определяем что изменилось
STAGED=$(git diff --name-only --staged 2>/dev/null | head -20)
CHANGED=$(git diff --name-only 2>/dev/null | head -20)
UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null | head -10)

# Собираем summary
SESSION_SUMMARY=""
if [ -n "$STAGED" ]; then
    SESSION_SUMMARY="${SESSION_SUMMARY}\n- Зафиксировано: $(echo "$STAGED" | wc -l) файлов"
fi
if [ -n "$CHANGED" ]; then
    SESSION_SUMMARY="${SESSION_SUMMARY}\n- Изменено (unstaged): $(echo "$CHANGED" | wc -l) файлов"
fi

# Анализируем что сделано по файлам
if echo "$STAGED $CHANGED" | grep -q "TradeUI\|ContractBoardUI\|UIManager\|UIFactory\|UITheme"; then
    SESSION_SUMMARY="${SESSION_SUMMARY}\n- UI система изменена"
fi
if echo "$STAGED $CHANGED" | grep -q "ShipController\|ShipModule\|ShipFuel"; then
    SESSION_SUMMARY="${SESSION_SUMMARY}\n- Система кораблей изменена"
fi
if echo "$STAGED $CHANGED" | grep -q "NetworkPlayer\|NetworkManager\|FloatingOrigin"; then
    SESSION_SUMMARY="${SESSION_SUMMARY}\n- Сетевая система изменена"
fi

# Следующие шаги (по умолчанию)
NEXT_STEPS="- Продолжить с текущей задачи\n- Проверить git status перед началом"

# Сохраняем в recovery файл
cat > "$RECOVERY_FILE" << RECOVERY_EOF
# Session Recovery — Project C

**Авто-обновляется хуками. НЕ редактировать вручную.**

---

## 📍 Текущее Состояние

**Ветка:** $(git rev-parse --abbrev-ref HEAD 2>/dev/null)
**Последнее обновление:** $DATE

**Этап:** Этап 2.x — Визуальный прототип с сетью
**Активный спринт:** Sprint 4 (Polish) — в ожидании

---

## ✅ Последние Достижения

$SESSION_SUMMARY

---

## 🔴 Критичные Правила (НЕ нарушать)

| Правило | Описание |
|---------|---------|
| **URP** | ❌ НЕ создавать URP ассеты через C# → ТОЛЬКО Editor UI |
| **.meta** | ❌ НЕ трогать .meta файлы |
| **Масштаб ×5** | Скриптовые объекты создавать в 5 раз меньше → умножать размеры ×5 |
| **Координаты ×50** | XZ координаты городов ×50 (радиус мира ~350,000 units) |

---

## 🔴 Известные Проблемы (приоритет)

| Приоритет | Проблема | Файл | Статус |
|-----------|----------|------|--------|
| P0 | PlayerPrefs для данных игрока | PlayerDataStore | Заменить на БД |
| P0 | AltitudeUI HUD не отображается | AltitudeUI.cs | Требует @unity-ui-specialist |
| P0 | ScriptableObject state теряется | LocationMarket | Разделить Config + State |
| P1 | Нет проверки позиции в RPC | TradeMarketServer | Добавить locationId check |

---

## 📋 TODO для Следующей Сессии

$NEXT_STEPS

---

## 📂 Изменённые Файлы (эта сессия)

### Staged (зафиксировано):
$(echo "$STAGED" | sed 's/^/  - /' || echo "  Нет")

### Changed (изменено):
$(echo "$CHANGED" | sed 's/^/  - /' || echo "  Нет")

### Untracked (новые):
$(echo "$UNTRACKED" | sed 's/^/  - /' || echo "  Нет")

---

## 🔗 Быстрые Ссылки

| Контекст | Файл |
|----------|------|
| Полный контекст | \`docs/QWEN_CONTEXT.md\` (910 строк) |
| Сессия 2 кораблей | \`docs/world/LargeScaleMMO/SESSION_2026-04-14.md\` |
| UI система | \`docs/QWEN-UI-AGENTIC-SUMMARY.md\` |
| Торговля | \`docs/TRADE_SYSTEM_RAG.md\` |

---

**Версия:** 1.0 | **Обновлено:** $DATE
RECOVERY_EOF

echo ""
echo "✅ Recovery сохранён в $RECOVERY_FILE"
echo ""
echo "==================================="

exit 0
