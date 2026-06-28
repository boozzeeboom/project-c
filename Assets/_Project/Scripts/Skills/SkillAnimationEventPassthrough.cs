// Project C: Skills/Battle — T-INP-08
// SkillAnimationEventPassthrough: tiny компонент, вешается на GameObject с Animator (child mesh).
// Animation Event "OnAttackImpact" / "OnSkillAnimationEnd" из AnimationClip вызываются Unity
// через SendMessage на GameObject с Animator. Этот компонент пробрасывает вызов в SkillAnimationPlayer
// на родительском NetworkPlayer.
//
// Design: docs/dev/INP08_ANIMATOR_CLIP_PIPELINE.md

using UnityEngine;
using ProjectC.Player;  // NetworkPlayer

namespace ProjectC.Skills
{
    /// <summary>
    /// T-INP-08: passthrough для Animation Events. Вешается на GameObject с Animator.
    /// </summary>
    public class SkillAnimationEventPassthrough : MonoBehaviour
    {
        private NetworkPlayer _target;

        public void SetTarget(NetworkPlayer target)
        {
            _target = target;
        }

        // Animation Event handler — Unity вызывает этот метод по имени, заданному в Animation window.
        // Имя в клипе должно быть "OnAttackImpact" (case-sensitive).
        public void OnAttackImpact()
        {
            if (_target == null) return;
            var animPlayer = _target.GetComponent<SkillAnimationPlayer>();
            if (animPlayer != null) animPlayer.OnAttackImpact();
        }

        // Animation Event handler — вызывается в конце клипа (если дизайнер добавил event).
        public void OnSkillAnimationEnd()
        {
            if (_target == null) return;
            var animPlayer = _target.GetComponent<SkillAnimationPlayer>();
            if (animPlayer != null) animPlayer.OnSkillAnimationEnd();
        }
    }
}
