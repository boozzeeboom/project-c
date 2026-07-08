## Итерация от 2026-07-30

**Задача:** Bugfix: custom editor discipline switching broke after adding Bows/Crossbows subtypes
**Коммит:** `a79ea91` — T-RTC-R5-fix: fix custom editor discipline switching + stale subtype arrays
**Изменения:**
- `Assets/_Project/Scripts/Skills/SkillNodeConfig.cs` — OnValidate: AutoSetDisciplineFromPrefix только при discipline=None
- `Assets/_Project/Editor/SkillNodeConfigEditor.cs` — SubtypesRanged + Bows/Crossbows; секция Bows/Crossbows; сброс subtype при смене discipline
- `Assets/_Project/Resources/Skills/Skill_Ranged_BasicBow.asset` — subtype None→Bows + новые поля
- `Assets/_Project/Resources/Skills/Skill_Ranged_CrossbowMastery.asset` — новые поля rangedMaxRange/rangedHitChance
