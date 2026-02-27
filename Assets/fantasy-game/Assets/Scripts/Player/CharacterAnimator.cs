// Assets/Scripts/Player/CharacterAnimator.cs
// =============================================
// Drives Legacy Animation crossfades based on ThirdPersonController
// and MeleeCombat state.

using UnityEngine;
using FantasyGame.Combat;

namespace FantasyGame.Player
{
    public class CharacterAnimator : MonoBehaviour
    {
        private const string CLIP_IDLE = "Idle";
        private const string CLIP_WALK = "Walk";
        private const string CLIP_RUN  = "Run";
        private const string CLIP_JUMP = "Jump";
        private const string CLIP_ATTACK = "Attack";

        private const float CROSSFADE_TIME = 0.15f;

        private Animation _animation;
        private ThirdPersonController _controller;
        private MeleeCombat _combat;
        private string _currentClip;

        public void Init(Animation anim, ThirdPersonController controller)
        {
            _animation = anim;
            _controller = controller;

            // Log all available clips so we know what GLTFast gave us
            Debug.Log("[CharacterAnimator] Available animation clips:");
            foreach (AnimationState state in _animation)
            {
                Debug.Log($"  Clip: '{state.name}' length={state.length:F2}s wrapMode={state.wrapMode}");
            }
            Debug.Log($"[CharacterAnimator] Total clip count: {_animation.GetClipCount()}");

            ConfigureClips();

            // Try to play Idle; if it doesn't exist, play whatever is first
            if (_animation.GetClip(CLIP_IDLE) != null)
            {
                _currentClip = CLIP_IDLE;
                _animation.Play(CLIP_IDLE);
                Debug.Log("[CharacterAnimator] Playing Idle clip.");
            }
            else
            {
                foreach (AnimationState state in _animation)
                {
                    _currentClip = state.name;
                    _animation.Play(state.name);
                    Debug.Log($"[CharacterAnimator] 'Idle' not found. Playing first clip: '{state.name}'");
                    break;
                }
            }
        }

        /// <summary>
        /// Called after MeleeCombat is attached so we can read attack state.
        /// </summary>
        public void SetCombat(MeleeCombat combat)
        {
            _combat = combat;
        }

        private void ConfigureClips()
        {
            SetWrapMode(CLIP_IDLE, WrapMode.Loop);
            SetWrapMode(CLIP_WALK, WrapMode.Loop);
            SetWrapMode(CLIP_RUN,  WrapMode.Loop);
            SetWrapMode(CLIP_JUMP, WrapMode.Once);
            SetWrapMode(CLIP_ATTACK, WrapMode.Once);
        }

        private void SetWrapMode(string clipName, WrapMode mode)
        {
            var clip = _animation.GetClip(clipName);
            if (clip != null)
                clip.wrapMode = mode;
            else
                Debug.LogWarning($"[CharacterAnimator] Clip '{clipName}' not found in Animation component.");
        }

        private void Update()
        {
            if (_animation == null || _controller == null)
                return;

            string desired = DetermineClip();

            if (desired == _currentClip)
                return;

            _animation.CrossFade(desired, CROSSFADE_TIME);
            _currentClip = desired;
        }

        private string DetermineClip()
        {
            // Attack takes highest priority
            if (_combat != null && _combat.IsAttacking)
                return CLIP_ATTACK;

            // Jump
            if (_controller.IsJumping && !_controller.IsGrounded)
                return CLIP_JUMP;

            float speed = _controller.CurrentSpeed;

            if (speed > 6f)
                return CLIP_RUN;
            if (speed > 0.1f)
                return CLIP_WALK;

            return CLIP_IDLE;
        }
    }
}
