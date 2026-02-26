// Assets/Scripts/Player/CharacterAnimator.cs
// =============================================
// Drives Legacy Animation crossfades based on ThirdPersonController state.
// Attached at runtime by GltfBootstrap after GLTFast imports the model.

using UnityEngine;

namespace FantasyGame.Player
{
    public class CharacterAnimator : MonoBehaviour
    {
        private const string CLIP_IDLE = "Idle";
        private const string CLIP_WALK = "Walk";
        private const string CLIP_RUN  = "Run";
        private const string CLIP_JUMP = "Jump";

        private const float CROSSFADE_TIME = 0.15f;

        private Animation _animation;
        private ThirdPersonController _controller;
        private string _currentClip;

        public void Init(Animation anim, ThirdPersonController controller)
        {
            _animation = anim;
            _controller = controller;

            ConfigureClips();

            _currentClip = CLIP_IDLE;
            _animation.Play(CLIP_IDLE);
        }

        private void ConfigureClips()
        {
            SetWrapMode(CLIP_IDLE, WrapMode.Loop);
            SetWrapMode(CLIP_WALK, WrapMode.Loop);
            SetWrapMode(CLIP_RUN,  WrapMode.Loop);
            SetWrapMode(CLIP_JUMP, WrapMode.Once);
        }

        private void SetWrapMode(string clipName, WrapMode mode)
        {
            var clip = _animation.GetClip(clipName);
            if (clip != null)
                clip.wrapMode = mode;
        }

        private void Update()
        {
            if (_animation == null || _controller == null)
                return;

            string desired = DetermineClip();

            if (desired == _currentClip)
                return;

            // Don't interrupt a playing jump animation until it finishes
            if (_currentClip == CLIP_JUMP && _animation.IsPlaying(CLIP_JUMP))
                return;

            _animation.CrossFade(desired, CROSSFADE_TIME);
            _currentClip = desired;
        }

        private string DetermineClip()
        {
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
