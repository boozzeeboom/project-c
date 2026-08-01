## Итерация от 2026-07-16

**Задача:** Доработать блок социальных навыков в CharacterWindow — добавить кнопку «ИЗУЧИТЬ НАВЫК», открывающую окно графа социальных навыков (упрощённая версия SkillTreeWindow без слотов биндов).
**Коммит:** `4c9b7246` — T-SOC-01: SocialSkillTreeWindow — окно графа социальных навыков
**Изменения:**
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` — кнопка `open-social-skill-tree-btn` в social-col
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` — метод `InitOpenSocialSkillTreeButton()`
- `Assets/_Project/Resources/UI/SocialSkillTreeWindow.uxml` — создан (упрощённый макет без slot-overview/bind)
- `Assets/_Project/Resources/UI/SocialSkillTreeWindow.uss` — создан (стили)
- `Assets/_Project/Scripts/Skills/UI/SocialSkillTreeWindow.cs` — создан (singleton, фильтр SkillCategory.Social)
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — `CreateSocialSkillTreeWindow()` auto-spawn
- `Assets/_Project/Scripts/UI/UIManager.cs` — проверка `SocialSkillTreeWindow` в `IsAnyExternalWindowOpen()`
- `docs/Character/Social-skills/01_IMPLEMENTATION.md` — документация
