// Assets/Scripts/Dungeon/PuzzleDoor.cs
// =======================================
// Sliding gate door for dungeon puzzles. Opens by sliding upward,
// closes by sliding back down. Collider blocks player when closed.

using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class PuzzleDoor : MonoBehaviour
    {
        private bool _isOpen;
        private bool _locked; // Once opened by certain puzzles, stays open
        private float _closedY;
        private float _openY;
        private float _targetY;
        private float _speed = 3f;
        private Renderer _renderer;
        private Material _closedMat;
        private Material _openMat;

        public bool IsOpen => _isOpen;

        /// <summary>
        /// Initialize the door. Call after setting transform.position.
        /// doorHeight is how tall the door slab is (used to calculate open position).
        /// </summary>
        public void Init(float doorHeight = 3.5f)
        {
            _closedY = transform.position.y;
            _openY = _closedY + doorHeight + 0.5f; // Slide up above doorway
            _targetY = _closedY;

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _closedMat = new Material(shader) { color = new Color(0.35f, 0.25f, 0.2f) };
                _closedMat.SetFloat("_Smoothness", 0.2f);
                _openMat = new Material(shader) { color = new Color(0.25f, 0.35f, 0.2f) };
                _openMat.SetFloat("_Smoothness", 0.2f);
                _renderer.material = _closedMat;
            }
        }

        public void Open()
        {
            _isOpen = true;
            _targetY = _openY;
            if (_renderer != null)
                _renderer.material = _openMat;

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayItemPickup();

            Debug.Log($"[PuzzleDoor] Opening: {gameObject.name}");
        }

        public void Close()
        {
            if (_locked) return;
            _isOpen = false;
            _targetY = _closedY;
            if (_renderer != null)
                _renderer.material = _closedMat;

            Debug.Log($"[PuzzleDoor] Closing: {gameObject.name}");
        }

        /// <summary>
        /// Lock the door in its current state (prevent closing).
        /// </summary>
        public void Lock()
        {
            _locked = true;
        }

        private void Update()
        {
            var pos = transform.position;
            if (Mathf.Abs(pos.y - _targetY) > 0.01f)
            {
                pos.y = Mathf.Lerp(pos.y, _targetY, _speed * Time.deltaTime);
                transform.position = pos;
            }
        }
    }
}
