# Performance Guardrails — UI (Project C)

Бюджеты и правила, которым должен следовать любой UI-код. Нарушения логируются `UIPerformanceMonitor` в dev-билдах и блокируют CI.

## 1. Бюджеты

| Метрика | Бюджет | Проверка |
|---|---|---|
| Draw calls chrome (HUD+Modal+Tooltip+Overlay) | ≤4 | Frame Debugger smoke в CI |
| UI CPU per frame | ≤1.0 ms (mid-range PC, 1080p) | Profiler marker `UIFrame.Update` |
| Открытие модала (transition in) | ≤16 ms, без хитча | Profiler |
| Закрытие модала | ≤16 ms | Profiler |
| Inventory scroll (10k items) | ≥60 FPS | PlayMode perf test |
| Chat append (1k lines) | ≤0.5 ms | PlayMode perf test |
| Market list (5k rows) scroll | ≥60 FPS | PlayMode perf test |
| Память UI-ассетов (runtime) | ≤120 MB | Memory Profiler snapshot в CI |
| World-space nameplates одновременно | ≤50 | Runtime cap + distance LOD |
| Текстуры distinct в одном батче | ≤8 | Frame Debugger |
| Вложенные маски | ≤7 уровней | Code review |
| Семейства шрифтов | ≤4 | Code review |

## 2. Правила (MUST)

1. **Виртуализация списков:** используй `ListView`/`TreeView`, не `ScrollView` из тысяч элементов. `fixedItemHeight` где возможно.
2. **Пулинг виджетов:** не Destroy, а Release в пул. Reparent + disable (disable first, then reparent).
3. **Скрытые панели:** `DisplayStyle.None` (останавливает layout+render), не `opacity:0`.
4. **Анимация:** только `translate/scale/rotate`; никогда `width/height/top/left`. Не переключать USS-классы на больших иерархиях во время анимации — inline style.
5. **Binding:** change-tracking (`INotifyBindablePropertyChanged`/`IDataSourceViewHashProvider`); для high-freq — прямая запись + троттлинг (≤10 Hz текст, per-frame только transforms).
6. **No per-frame Update в UI:** драйвить от событий. Если Update нужен — без аллокаций, без строк.
7. **No string concat в `.text` в hot paths:** `StringBuilder` или форматирование.
8. **No `Resources.Load` в runtime hot paths:** кэшировать ссылки.
9. **No `Clear()+CloneTree()` в EnsureBuilt.**
10. **Маски:** прямоугольные (shader-based) по умолчанию; круглые/complex — только где нужно.
11. **Текстуры:** Sprite Atlas для статики; ≤8 distinct на батч.
12. **World-space UI:** cap + distance LOD (hide beyond N м, simplify beyond M).
13. **DPI:** масштабировать по `Screen.dpi`/reference resolution (1920×1080 base); тестировать 100/125/150/200%.
14. **UI не подчиняется render scale 3D:** не даунскейлить UI вместе с 3D.

## 3. Анти-паттерны (запрещены)

- WebView для core UI.
- `OnGUI` для player UI.
- `Resources.Load<StyleSheet>` fallback.
- `Clear()+CloneTree()` в EnsureBuilt.
- Per-frame `MarkDirtyRepaint` без необходимости.
- Глубокие иерархии (>10 уровней) в UXML.
- Inline styles для темизации (вместо USS/tokens).
- Уничтожение виджетов вместо пулинга в hot paths.

## 4. Инструменты профилирования

- Unity Profiler (CPU, Memory, Rendering).
- Frame Debugger (draw calls, батчи).
- Memory Profiler (снимки).
- `UIPerformanceMonitor` (dev) — логирует нарушения бюджетов в консоль.
- PlayMode perf tests (Unity Test Framework) — автоматические бенчмарки.

## 5. Чеклист перед мерджем UI-PR

- [ ] Draw calls ≤ бюджет (Frame Debugger).
- [ ] Нет per-frame аллокаций (Profiler).
- [ ] Пулинг для повторяющихся виджетов.
- [ ] Виртуализация для списков >50 элементов.
- [ ] Скрытые панели — `DisplayStyle.None`.
- [ ] Локализация: +40% expansion протестирован.
- [ ] A11y: focus ring видим, keyboard nav работает.
- [ ] High-contrast theme протестирован.
- [ ] Motion-reduction уважается.

---

*См. также `04_IMPLEMENTATION_PLAN.md` §4 (бюджеты и guardrails в CI).*