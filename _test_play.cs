var sb = new System.Text.StringBuilder();
try
{
    // 1) Найти spinstrike
    var spinstrike = UnityEngine.Resources.LoadAll<ProjectC.Skills.SkillNodeConfig>("Skills");
    ProjectC.Skills.SkillNodeConfig spinstrikeCfg = null;
    foreach (var s in spinstrike) if (s != null && s.skillId == "combat_basic_spinstrike") { spinstrikeCfg = s; break; }
    if (spinstrikeCfg == null) { sb.AppendLine("spinstrike not in Resources"); return sb.ToString(); }
    sb.AppendLine("spinstrike.attackClip = " + (spinstrikeCfg.attackClip != null ? spinstrikeCfg.attackClip.name : "null"));

    // 2) Запустить Play Mode
    if (!UnityEngine.Application.isPlaying)
    {
        UnityEditor.EditorApplication.EnterPlaymode();
        sb.AppendLine("EnterPlaymode requested");
    }
    else
    {
        sb.AppendLine("Already in Play Mode");
    }
} catch (System.Exception ex) { sb.AppendLine("THROWN: " + ex.GetBaseException().Message); }
return sb.ToString();
