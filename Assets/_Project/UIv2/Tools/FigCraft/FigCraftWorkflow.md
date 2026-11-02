# FigCraft → Unity Workflow (UIv2)

## 1. Экспорт токенов (переменных) из FigCraft

В FigCraft (CLI или MCP):
```
variable.export { format: "css", path: "tokens.css" }
```
Или в приложении: Export → Variables → CSS.

Скопируйте `tokens.css` в:
```
Assets/_Project/UIv2/Tools/FigCraft/Import/tokens.css
```
Затем в Unity: **Project C → UIv2 → Import FigCraft Tokens (CSS)**.

Импортёр парсит `:root { --name: value; }` и `[data-*="mode"]` блоки, генерирует `DesignTokens.uss`.

## 2. Экспорт ассетов (иконки, спрайты)

В FigCraft:
```
file.exportLayers { assets: true, dir: "exported_assets" }
```
Или Export → Layers → PNG/SVG.

Скопируйте экспортированные файлы в:
```
Assets/_Project/UIv2/DesignSystem/Icons/
```
Затем создайте Sprite Atlas (Assets → Create → 2D → Sprite Atlas) и добавьте папку Icons.

## 3. Перенос стилей (Dev Mode)

1. В FigCraft включите Dev Mode (Shift+D).
2. Выделите элемент → Inspect → Properties (размеры, отступы, цвета, шрифты).
3. Перенесите значения в USS (через UI Builder или вручную).
4. Используйте `inspect.code { language: "css" }` для получения CSS-референса.

## 4. MCP-интеграция (опционально)

Запустите FigCraft с control port:
```
figcraft --control 47800
```
Затем подключите MCP:
```
figcraft-cli mcp --attach 47800
```
Агенты могут управлять FigCraft: создавать/редактировать дизайны, экспортировать токены и ассеты.

## 5. CLI-автоматизация

```
# Рендер страницы
figcraft-cli render design.figcraft -o preview.png --scale 2

# Экспорт
figcraft-cli export design.figcraft -o out/ --format svg

# Список команд
figcraft-cli commands --filter export

# Запуск скрипта команд
figcraft-cli run build-ui.json --out result.figcraft
```

## 6. Ограничения (FigCraft alpha)

- .fig импорт — best effort.
- Нет нативного Unity/UXML/USS экспорта — ручной/полуручной перенос.
- Нет shared libraries — компоненты дублируются между файлами.
- Возможны rough edges в UI/рендере.

## 7. Figma Free (резерв)

Если FigCraft не покрывает нужды:
- Figma Free + drafts (неограниченные черновики).
- Официальный UI Toolkit Connector (опционально).
- Vendored fallback: FigmaToUnity (open source).