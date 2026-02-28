// Assets/Scripts/Dungeon/SwitchSequencePuzzle.cs
// =================================================
// Validates that switches are pulled in the correct order.
// Resets all switches on wrong order with visual feedback.

using System.Collections.Generic;
using UnityEngine;

namespace FantasyGame.Dungeon
{
    public class SwitchSequencePuzzle : MonoBehaviour
    {
        public int[] CorrectOrder;       // e.g. {2, 0, 1} for "III - I - II"
        public PuzzleDoor LinkedDoor;
        public PuzzleSwitch[] Switches;

        private List<int> _currentSequence = new List<int>();
        private bool _solved;
        private float _resetTimer;
        private bool _resetting;

        public bool IsSolved => _solved;

        public void Init(int[] correctOrder, PuzzleDoor door, PuzzleSwitch[] switches)
        {
            CorrectOrder = correctOrder;
            LinkedDoor = door;
            Switches = switches;
        }

        public void ReportSwitchPressed(int switchIndex)
        {
            if (_solved || _resetting) return;

            _currentSequence.Add(switchIndex);

            // Check if current sequence matches the prefix of correct order
            for (int i = 0; i < _currentSequence.Count; i++)
            {
                if (i >= CorrectOrder.Length || _currentSequence[i] != CorrectOrder[i])
                {
                    // Wrong order — reset
                    Debug.Log($"[SwitchSequence] Wrong order! Expected {CorrectOrder[i]}, got {_currentSequence[i]}. Resetting...");
                    StartReset();
                    return;
                }
            }

            // Check if sequence is complete
            if (_currentSequence.Count == CorrectOrder.Length)
            {
                _solved = true;
                if (LinkedDoor != null)
                {
                    LinkedDoor.Open();
                    LinkedDoor.Lock();
                }
                Debug.Log("[SwitchSequence] Puzzle solved!");
            }
            else
            {
                Debug.Log($"[SwitchSequence] Correct so far: {_currentSequence.Count}/{CorrectOrder.Length}");
            }
        }

        private void StartReset()
        {
            _resetting = true;
            _resetTimer = 0.8f; // Brief delay before reset

            // Flash all switches red
            foreach (var sw in Switches)
            {
                if (sw != null) sw.FlashError();
            }
        }

        private void Update()
        {
            if (_resetting)
            {
                _resetTimer -= Time.deltaTime;
                if (_resetTimer <= 0f)
                {
                    _resetting = false;
                    _currentSequence.Clear();

                    // Reset all switches
                    foreach (var sw in Switches)
                    {
                        if (sw != null) sw.ResetSwitch();
                    }
                    Debug.Log("[SwitchSequence] Switches reset. Try again!");
                }
            }
        }
    }
}
