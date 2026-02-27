// Assets/Scripts/Interaction/DialogueNPC.cs
// ============================================
// NPC that shows dialogue when interacted with.
// Can give quests and have branching dialogue lines.

using UnityEngine;
using UnityEngine.InputSystem;
using FantasyGame.RPG;

namespace FantasyGame.Interaction
{
    [System.Serializable]
    public class DialogueLine
    {
        public string Speaker;
        public string Text;

        public DialogueLine(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    public class DialogueNPC : Interactable
    {
        public string NPCName = "Villager";
        private DialogueLine[] _dialogueLines;
        private DialogueLine[] _questCompleteLines;
        private int _currentLine = -1;
        private bool _inDialogue;
        private float _bobOffset;
        private string _questId;
        private bool _questGiven;
        private bool _questComplete;

        // Floating name tag
        private bool _showNameTag = true;

        public void Init(string name, DialogueLine[] lines, DialogueLine[] questCompleteLines = null, string questId = "")
        {
            NPCName = name;
            _dialogueLines = lines;
            _questCompleteLines = questCompleteLines;
            _questId = questId;
            PromptText = $"Talk to {name}";
            InteractRange = 3f;
            _bobOffset = Random.value * Mathf.PI * 2f;
        }

        protected override void OnInteract()
        {
            if (_inDialogue)
            {
                AdvanceDialogue();
            }
            else
            {
                StartDialogue();
            }
        }

        protected override void Update()
        {
            // Override base: allow E to advance dialogue even when "used"
            FindPlayerForDialogue();

            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerInRange = dist <= InteractRange;

            // Close dialogue if player walks away
            if (_inDialogue && dist > InteractRange * 1.5f)
            {
                EndDialogue();
            }

            if (_playerInRange)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    OnInteract();
                }
            }

            // Gentle idle bob
            float bob = Mathf.Sin((Time.time + _bobOffset) * 1.5f) * 0.02f;
            Vector3 pos = transform.position;
            // Don't bob — just face player when in range
            if (_playerInRange && _player != null)
            {
                Vector3 lookDir = _player.position - transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(lookDir),
                        3f * Time.deltaTime
                    );
                }
            }
        }

        private void StartDialogue()
        {
            // Check if quest is complete
            if (!string.IsNullOrEmpty(_questId) && _questGiven && !_questComplete)
            {
                var questMgr = FindAnyObjectByType<QuestManager>();
                if (questMgr != null && questMgr.IsQuestComplete(_questId))
                {
                    _questComplete = true;
                    questMgr.TurnInQuest(_questId);
                }
            }

            _inDialogue = true;
            _currentLine = 0;

            if (Audio.SoundManager.Instance != null)
                Audio.SoundManager.Instance.PlayDialogueOpen();

            Debug.Log($"[DialogueNPC] Talking to {NPCName}");
        }

        private void AdvanceDialogue()
        {
            DialogueLine[] lines = GetCurrentLines();
            _currentLine++;
            if (_currentLine >= lines.Length)
            {
                EndDialogue();
            }
        }

        private void EndDialogue()
        {
            _inDialogue = false;
            _currentLine = -1;

            // Give quest after first conversation if not already given
            if (!string.IsNullOrEmpty(_questId) && !_questGiven)
            {
                _questGiven = true;
                var questMgr = FindAnyObjectByType<QuestManager>();
                if (questMgr != null)
                {
                    questMgr.ActivateQuest(_questId);
                    Debug.Log($"[DialogueNPC] Quest '{_questId}' given by {NPCName}");
                }
            }
        }

        private DialogueLine[] GetCurrentLines()
        {
            if (_questComplete && _questCompleteLines != null && _questCompleteLines.Length > 0)
                return _questCompleteLines;
            return _dialogueLines;
        }

        // Dialogue box UI
        protected override void OnGUI()
        {
            if (_inDialogue)
            {
                DrawDialogueBox();
                return; // Don't show interact prompt during dialogue
            }

            // Show interact prompt via base
            if (!_playerInRange) return;

            var cam = UnityEngine.Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (screenPos.z < 0) return;

            float scale = Screen.height / 1080f;

            // Name tag
            if (_showNameTag)
            {
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(14 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                nameStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
                GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y - 25, 200, 20), NPCName, nameStyle);
            }

            // Interact prompt
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(1f, 0.9f, 0.6f);
            GUI.Label(new Rect(screenPos.x - 100, Screen.height - screenPos.y, 200, 30), $"[E] {PromptText}", style);
        }

        private void DrawDialogueBox()
        {
            DialogueLine[] lines = GetCurrentLines();
            if (_currentLine < 0 || _currentLine >= lines.Length) return;

            var line = lines[_currentLine];
            float scale = Screen.height / 1080f;

            // Dialogue box at bottom of screen
            float boxW = Mathf.Min(700f * scale, Screen.width - 40f);
            float boxH = 120f * scale;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = Screen.height - boxH - 20f * scale;

            // Background
            var whiteTex = Texture2D.whiteTexture;
            GUI.color = new Color(0.08f, 0.06f, 0.12f, 0.9f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), whiteTex);
            // Border
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.8f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 2), whiteTex);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2, boxW, 2), whiteTex);
            GUI.DrawTexture(new Rect(boxX, boxY, 2, boxH), whiteTex);
            GUI.DrawTexture(new Rect(boxX + boxW - 2, boxY, 2, boxH), whiteTex);
            GUI.color = Color.white;

            // Speaker name
            var speakerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            speakerStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
            GUI.Label(new Rect(boxX + 15f * scale, boxY + 10f * scale, boxW - 30f * scale, 30f * scale),
                line.Speaker, speakerStyle);

            // Text
            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            textStyle.normal.textColor = new Color(0.9f, 0.88f, 0.82f);
            GUI.Label(new Rect(boxX + 15f * scale, boxY + 35f * scale, boxW - 30f * scale, boxH - 55f * scale),
                line.Text, textStyle);

            // Continue prompt
            bool isLast = _currentLine >= lines.Length - 1;
            var continueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12 * scale),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.LowerRight
            };
            continueStyle.normal.textColor = new Color(0.7f, 0.65f, 0.55f);
            GUI.Label(new Rect(boxX, boxY + boxH - 25f * scale, boxW - 15f * scale, 20f * scale),
                isLast ? "[E] Close" : "[E] Continue", continueStyle);
        }

        // Uses base class _player and _playerInRange (protected)
        private void FindPlayerForDialogue()
        {
            FindPlayer(); // Base class method
        }
    }
}
