// Assets/Scripts/Dungeon/PuzzleSwitch.cs
// ========================================
// Wall-mounted lever switch. Extends Interactable for E-key interaction.
// Can link to a PuzzleDoor directly or feed into a SwitchSequencePuzzle.

using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class PuzzleSwitch : Interaction.Interactable
    {
        public PuzzleDoor LinkedDoor;
        public SwitchSequencePuzzle SequencePuzzle;
        public int SwitchIndex;

        private bool _isOn;
        private Transform _leverArm;
        private Material _offMat;
        private Material _onMat;

        public bool IsOn => _isOn;

        public void Init(Transform leverArm, PuzzleDoor door = null,
            SwitchSequencePuzzle puzzle = null, int switchIndex = 0)
        {
            _leverArm = leverArm;
            LinkedDoor = door;
            SequencePuzzle = puzzle;
            SwitchIndex = switchIndex;

            PromptText = "Pull Switch";
            InteractRange = 2.5f;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _offMat = new Material(shader) { color = new Color(0.6f, 0.55f, 0.5f) };
            _offMat.SetFloat("_Smoothness", 0.4f);
            _onMat = new Material(shader) { color = new Color(0.3f, 0.7f, 0.3f) };
            _onMat.SetFloat("_Smoothness", 0.4f);

            if (_leverArm != null)
            {
                var renderer = _leverArm.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = _offMat;
            }
        }

        protected override void OnInteract()
        {
            _isOn = !_isOn;

            // Animate lever
            if (_leverArm != null)
            {
                _leverArm.localRotation = _isOn
                    ? Quaternion.Euler(45f, 0, 0)
                    : Quaternion.Euler(-45f, 0, 0);

                var renderer = _leverArm.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = _isOn ? _onMat : _offMat;
            }

            // Direct door link
            if (LinkedDoor != null)
            {
                if (_isOn)
                    LinkedDoor.Open();
                else
                    LinkedDoor.Close();
            }

            // Sequence puzzle link
            if (SequencePuzzle != null)
            {
                SequencePuzzle.ReportSwitchPressed(SwitchIndex);
            }

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayItemPickup();

            Debug.Log($"[PuzzleSwitch] Toggled {gameObject.name}: {(_isOn ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Reset switch to off state (used by sequence puzzle on wrong order).
        /// </summary>
        public void ResetSwitch()
        {
            _isOn = false;
            if (_leverArm != null)
            {
                _leverArm.localRotation = Quaternion.Euler(-45f, 0, 0);
                var renderer = _leverArm.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = _offMat;
            }
        }

        /// <summary>
        /// Set the lever material to a flash color (for wrong sequence feedback).
        /// </summary>
        public void FlashError()
        {
            if (_leverArm != null)
            {
                var renderer = _leverArm.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    renderer.material = new Material(shader) { color = new Color(0.9f, 0.2f, 0.2f) };
                }
            }
        }
    }
}
