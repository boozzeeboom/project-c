## Итерация от 2026-07-30 (#2)

**Задача:** Configurable cooldown per skill — замена хардкода 0.5f
**Коммит:** `6f871e7` — T-SKILL-06: configurable cooldown per skill
**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — поле `cooldownSeconds` (float, 0.5f default, Range 0.1–30)
- `Assets/_Project/Scripts/Skills/SkillInputService.cs` — замена `0.5f` на `skillConfig.cooldownSeconds` / `skillConfig?.cooldownSeconds ?? 0.5f`
- `Assets/_Project/Editor/SkillNodeConfigEditor.cs` — PropertyField для `_cooldownSeconds` в секции Active vs Passive

---

## Итерация от 2026-07-30

**Задача:** Bugfix: custom editor discipline switching broke after adding Bows/Crossbows subtypes
**Коммит:** `a79ea91` — T-RTC-R5-fix: fix custom editor discipline switching + stale subtype arrays
**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — OnValidate: AutoSetDisciplineFromPrefix только при discipline=None
- `Assets/_Project/Editor/SkillNodeConfigEditor.cs` — SubtypesRanged + Bows/Crossbows; секция Bows/Crossbows; сброс subtype при смене discipline
- `Assets/_Project/Resources/Skills/Skill_Ranged_BasicBow.asset` — subtype None→Bows + новые поля
- `Assets/_Project/Resources/Skills/Skill_Ranged_CrossbowMastery.asset` — новые поля rangedMaxRange/rangedHitChance
