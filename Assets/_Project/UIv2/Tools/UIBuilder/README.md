# Unity UI Builder Workflow (UIv2)

UI Builder — основной инструмент сборки UXML/USS в Unity.

## Workflow
1. Window → UI Toolkit → UI Builder.
2. Создайте UXML (File → New) или откройте существующий.
3. Drag-and-drop элементов, визуальное редактирование стилей.
4. Привязка USS: добавьте стили в StyleSheets.
5. Live preview: изменения отображаются мгновенно.
6. Сохранение: Ctrl+S → UXML/USS обновляются.

## Интеграция с Design System
- Импортируйте `DesignTokens.uss` в USS экрана: `@import url("../DesignSystem/DesignTokens.uss");`
- Используйте компоненты из `DesignSystem/Components/` как шаблоны (Template/Instance).
- Иконки: Sprite Atlas из `DesignSystem/Icons/`.

## Советы
- Не используйте inline styles для темизации — только USS-классы и токены.
- Проверяйте layout в разных разрешениях (1920×1080, 1280×720).
- Используйте UI Debugger (Window → UI Toolkit → Debugger) для диагностики.