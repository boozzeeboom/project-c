// Project C: Character Progression — T-P12
// SkillsConfig: SO для global skill system config. Design: docs/Character/06_SKILL_TREE.md §2.3.

using System;
using UnityEngine;

namespace ProjectC.Skills
{
    [CreateAssetMenu(fileName = "SkillsConfig", menuName = "Project C/Skills/Skills Config", order = 14)]
    public class SkillsConfig : ScriptableObject
    {
        [Header("Default starting skills (auto-granted on connect)")]
        [Tooltip("Q3.2: по решению пользователя — ПУСТОЙ массив. Игрок учит все skills с нуля сам. " +
                 "Если designer захочет starter skills — добавит в .asset (через Inspector или MCP).")]
        public SkillNodeConfig[] defaultSkills = Array.Empty<SkillNodeConfig>();

        [Header("Rate limit (T-P13)")]
        [Tooltip("Max learn/forget requests per second per client. Anti-spam для RequestLearnSkillRpc / RequestForgetSkillRpc.")]
        [SerializeField, Min(1)] private int _maxOpsPerSec = 5;
        public int MaxOpsPerSec => _maxOpsPerSec;

        [Header("Resources path")]
        [Tooltip("Папка в Resources/ для Resources.LoadAll<SkillNodeConfig>. По умолчанию 'Skills'.")]
        [SerializeField] private string _skillsResourcesPath = "Skills";
        public string SkillsResourcesPath => _skillsResourcesPath;
    }
}
