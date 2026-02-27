// Assets/Scripts/Enemies/LootBob.cs
// ==================================
// Simple bobbing + rotation animation for dropped loot.

using UnityEngine;

namespace FantasyGame.Enemies
{
    public class LootBob : MonoBehaviour
    {
        private Vector3 _startPos;
        private float _offset;

        private const float BOB_HEIGHT = 0.15f;
        private const float BOB_SPEED = 2f;
        private const float SPIN_SPEED = 90f;

        private void Start()
        {
            _startPos = transform.position;
            _offset = Random.value * Mathf.PI * 2f; // Randomize phase
        }

        private void Update()
        {
            // Bob up and down
            float y = Mathf.Sin((Time.time + _offset) * BOB_SPEED) * BOB_HEIGHT;
            transform.position = _startPos + new Vector3(0, y, 0);

            // Spin slowly
            transform.Rotate(Vector3.up, SPIN_SPEED * Time.deltaTime, Space.World);
        }
    }
}
