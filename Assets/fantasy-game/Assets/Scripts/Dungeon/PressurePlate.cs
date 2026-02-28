// Assets/Scripts/Dungeon/PressurePlate.cs
// ==========================================
// Floor pressure plate that detects player (CharacterController) and
// PushableBlock. Opens a linked PuzzleDoor when pressed.
// Uses a tall invisible trigger box so the CharacterController capsule
// reliably enters it when walking over the plate.

using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class PressurePlate : MonoBehaviour
    {
        public PuzzleDoor LinkedDoor;
        public bool StayDown; // If true, plate locks down once pressed (single use)

        private bool _isPressed;
        private bool _lockedDown;
        private Renderer _plateVisual;
        private Material _unpressedMat;
        private Material _pressedMat;
        private int _occupantCount; // Track multiple objects on plate
        private GameObject _visualGo;

        public bool IsPressed => _isPressed;

        public void Init(PuzzleDoor door, bool stayDown = false)
        {
            LinkedDoor = door;
            StayDown = stayDown;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _unpressedMat = new Material(shader) { color = new Color(0.5f, 0.5f, 0.5f) };
            _unpressedMat.SetFloat("_Smoothness", 0.3f);
            _pressedMat = new Material(shader) { color = new Color(0.3f, 0.8f, 0.3f) };
            _pressedMat.SetFloat("_Smoothness", 0.3f);

            // The main GameObject's collider from CreatePrimitive is the visual plate.
            // We need to:
            // 1) Keep the visual renderer but remove its collider
            // 2) Add a tall trigger box so CharacterController enters it
            // 3) Add a kinematic Rigidbody so trigger events fire reliably

            _plateVisual = GetComponent<Renderer>();
            if (_plateVisual != null)
                _plateVisual.material = _unpressedMat;

            // Remove the original collider from the visual (it's too thin for triggers)
            var origCol = GetComponent<BoxCollider>();
            if (origCol != null)
                Destroy(origCol);

            // Add a tall trigger box — 2m wide, 1.5m tall so CC capsule can't miss it
            var triggerCol = gameObject.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            // Scale is (2, 0.1, 2) so local extents are (1, 0.05, 1)
            // We want the trigger to extend upward ~1.5m from the plate surface
            // In local space: center at (0, 7.5, 0) with size (1, 15, 1)
            // because localScale.y = 0.1, so 15 * 0.1 = 1.5m world height
            triggerCol.center = new Vector3(0, 7.5f, 0);
            triggerCol.size = new Vector3(1f, 15f, 1f);

            // Kinematic rigidbody ensures OnTrigger events fire with CharacterController
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_lockedDown) return;

            // Detect player (CharacterController) or PushableBlock
            if (other.GetComponent<CharacterController>() != null ||
                other.GetComponent<PushableBlock>() != null)
            {
                _occupantCount++;
                if (!_isPressed)
                {
                    Press();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_lockedDown) return;

            if (other.GetComponent<CharacterController>() != null ||
                other.GetComponent<PushableBlock>() != null)
            {
                _occupantCount = Mathf.Max(0, _occupantCount - 1);
                if (_occupantCount == 0 && _isPressed && !StayDown)
                {
                    Release();
                }
            }
        }

        private void Press()
        {
            _isPressed = true;
            if (_plateVisual != null)
                _plateVisual.material = _pressedMat;

            // Sink the plate slightly
            var pos = transform.position;
            pos.y -= 0.05f;
            transform.position = pos;

            if (LinkedDoor != null)
                LinkedDoor.Open();

            if (StayDown)
            {
                _lockedDown = true;
                if (LinkedDoor != null)
                    LinkedDoor.Lock();
            }

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayItemPickup();

            Debug.Log($"[PressurePlate] Pressed: {gameObject.name}");
        }

        private void Release()
        {
            _isPressed = false;
            if (_plateVisual != null)
                _plateVisual.material = _unpressedMat;

            // Raise plate back up
            var pos = transform.position;
            pos.y += 0.05f;
            transform.position = pos;

            if (LinkedDoor != null)
                LinkedDoor.Close();

            Debug.Log($"[PressurePlate] Released: {gameObject.name}");
        }
    }
}
