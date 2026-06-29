// Project C: Skills/Battle — T-INP-08
// SkillAnimationEventPassthrough: вешается на GameObject с Animator.
// Animation Events "OnAttackImpact" / "OnSkillAnimationEnd" из клипа вызываются Unity через SendMessage
// на GameObject с Animator. Этот компонент форвардит в SkillAnimationPlayer.

using UnityEngine;
using ProjectC.Player;

namespace ProjectC.Skills
{
    public class SkillAnimationEventPassthrough : MonoBehaviour
    {
        private NetworkPlayer _target;
        public void SetTarget(NetworkPlayer target) { _target = target; }

        public void OnAttackImpact()
        {
            if (_target == null) return;
            var p = _target.GetComponent<SkillAnimationPlayer>();
            if (p != null) p.OnAttackImpact();
        }

        public void OnSkillAnimationEnd()
        {
            if (_target == null) return;
            var p = _target.GetComponent<SkillAnimationPlayer>();
            if (p != null) p.OnSkillAnimationEnd();
        }
    }
}