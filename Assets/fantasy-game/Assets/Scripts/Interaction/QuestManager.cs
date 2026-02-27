// Assets/Scripts/Interaction/QuestManager.cs
// =============================================
// Simple quest system. Quests track kill counts and item collection.
// Rewards XP and items on completion.

using System.Collections.Generic;
using UnityEngine;
using FantasyGame.RPG;
using FantasyGame.Enemies;

namespace FantasyGame.Interaction
{
    public enum QuestType { KillEnemies, CollectItems }
    public enum QuestStatus { Inactive, Active, Complete, TurnedIn }

    [System.Serializable]
    public class Quest
    {
        public string Id;
        public string Title;
        public string Description;
        public QuestType Type;
        public string TargetId;        // Enemy name or item ID
        public int TargetCount;
        public int CurrentCount;
        public QuestStatus Status;
        public int XPReward;
        public string ItemReward;      // Item ID to give on completion
        public int ItemRewardCount;

        public bool IsComplete => CurrentCount >= TargetCount;
    }

    public class QuestManager : MonoBehaviour
    {
        private Dictionary<string, Quest> _quests = new Dictionary<string, Quest>();
        private PlayerStats _playerStats;
        private Texture2D _whiteTex;

        // Track for display
        private Quest _activeDisplayQuest;
        private float _questCompleteFlash;

        public void Init(PlayerStats stats)
        {
            _playerStats = stats;
            _whiteTex = Texture2D.whiteTexture;
            RegisterDefaultQuests();

            // Listen for enemy deaths
            // We'll poll in Update instead since enemies spawn dynamically
        }

        private void RegisterDefaultQuests()
        {
            AddQuest(new Quest
            {
                Id = "kill_slimes",
                Title = "Slime Cleanup",
                Description = "Defeat 5 Slimes to clear the area.",
                Type = QuestType.KillEnemies,
                TargetId = "Slime",
                TargetCount = 5,
                XPReward = 50,
                ItemReward = "potion_large",
                ItemRewardCount = 2
            });

            AddQuest(new Quest
            {
                Id = "kill_skeletons",
                Title = "Bone Collector",
                Description = "Defeat 3 Skeletons lurking in the highlands.",
                Type = QuestType.KillEnemies,
                TargetId = "Skeleton",
                TargetCount = 3,
                XPReward = 80,
                ItemReward = "sword_iron",
                ItemRewardCount = 1
            });

            AddQuest(new Quest
            {
                Id = "collect_pelts",
                Title = "Wolf Hunter",
                Description = "Collect 3 Wolf Pelts.",
                Type = QuestType.CollectItems,
                TargetId = "wolf_pelt",
                TargetCount = 3,
                XPReward = 100,
                ItemReward = "sword_magic",
                ItemRewardCount = 1
            });
        }

        private void AddQuest(Quest q)
        {
            q.Status = QuestStatus.Inactive;
            q.CurrentCount = 0;
            _quests[q.Id] = q;
        }

        public void ActivateQuest(string questId)
        {
            if (_quests.TryGetValue(questId, out var quest))
            {
                if (quest.Status == QuestStatus.Inactive)
                {
                    quest.Status = QuestStatus.Active;
                    _activeDisplayQuest = quest;
                    Debug.Log($"[QuestManager] Quest activated: {quest.Title}");
                }
            }
        }

        public bool IsQuestComplete(string questId)
        {
            if (_quests.TryGetValue(questId, out var quest))
                return quest.IsComplete && quest.Status == QuestStatus.Active;
            return false;
        }

        public void TurnInQuest(string questId)
        {
            if (!_quests.TryGetValue(questId, out var quest)) return;
            if (quest.Status != QuestStatus.Active || !quest.IsComplete) return;

            quest.Status = QuestStatus.TurnedIn;
            _questCompleteFlash = 2f;

            // Give rewards
            if (_playerStats != null)
                _playerStats.AddXP(quest.XPReward);

            if (!string.IsNullOrEmpty(quest.ItemReward))
            {
                var inventory = FindAnyObjectByType<InventoryComponent>();
                if (inventory != null)
                {
                    var item = ItemDatabase.Get(quest.ItemReward);
                    if (item != null)
                    {
                        inventory.Inventory.AddItem(item, quest.ItemRewardCount);
                        Debug.Log($"[QuestManager] Rewarded: {item.Name} x{quest.ItemRewardCount}");
                    }
                }
            }

            Debug.Log($"[QuestManager] Quest complete: {quest.Title} (+{quest.XPReward} XP)");

            // Show next active quest
            _activeDisplayQuest = null;
            foreach (var kvp in _quests)
            {
                if (kvp.Value.Status == QuestStatus.Active)
                {
                    _activeDisplayQuest = kvp.Value;
                    break;
                }
            }
        }

        public void ReportEnemyKill(string enemyName)
        {
            foreach (var kvp in _quests)
            {
                var q = kvp.Value;
                if (q.Status != QuestStatus.Active) continue;
                if (q.Type != QuestType.KillEnemies) continue;
                if (q.TargetId != enemyName) continue;

                q.CurrentCount++;
                Debug.Log($"[QuestManager] {q.Title}: {q.CurrentCount}/{q.TargetCount}");

                if (q.IsComplete && q.Status == QuestStatus.Active)
                {
                    q.Status = QuestStatus.Active; // Keep active until turned in
                    Debug.Log($"[QuestManager] Quest '{q.Title}' objectives complete! Return to NPC.");
                }
            }
        }

        public void ReportItemCollected(string itemId)
        {
            foreach (var kvp in _quests)
            {
                var q = kvp.Value;
                if (q.Status != QuestStatus.Active) continue;
                if (q.Type != QuestType.CollectItems) continue;
                if (q.TargetId != itemId) continue;

                // Count from inventory
                var inventory = FindAnyObjectByType<InventoryComponent>();
                if (inventory != null)
                {
                    q.CurrentCount = inventory.Inventory.GetItemCount(itemId);
                }
            }
        }

        private void Update()
        {
            if (_questCompleteFlash > 0)
                _questCompleteFlash -= Time.deltaTime;

            // Poll collection quests
            foreach (var kvp in _quests)
            {
                var q = kvp.Value;
                if (q.Status != QuestStatus.Active || q.Type != QuestType.CollectItems) continue;
                var inventory = FindAnyObjectByType<InventoryComponent>();
                if (inventory != null)
                {
                    q.CurrentCount = inventory.Inventory.GetItemCount(q.TargetId);
                }
            }
        }

        private void OnGUI()
        {
            if (_activeDisplayQuest == null) return;

            float scale = Screen.height / 1080f;
            float panelW = 220f * scale;
            float panelH = 80f * scale;
            float panelX = Screen.width - panelW - 15f * scale;
            float panelY = 15f * scale;

            // Background
            GUI.color = new Color(0.08f, 0.06f, 0.1f, 0.75f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), _whiteTex);
            // Border
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.6f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(panelX, panelY, 2, panelH), _whiteTex);
            GUI.DrawTexture(new Rect(panelX + panelW - 2, panelY, 2, panelH), _whiteTex);
            GUI.color = Color.white;

            // Title
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(panelX + 8 * scale, panelY + 6 * scale, panelW - 16 * scale, 20 * scale),
                _activeDisplayQuest.Title, titleStyle);

            // Progress
            var progressStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * scale),
                alignment = TextAnchor.UpperLeft
            };
            progressStyle.normal.textColor = _activeDisplayQuest.IsComplete
                ? new Color(0.3f, 0.9f, 0.3f)
                : new Color(0.8f, 0.78f, 0.72f);
            string progressText = _activeDisplayQuest.IsComplete
                ? "Complete! Return to NPC"
                : $"{_activeDisplayQuest.CurrentCount}/{_activeDisplayQuest.TargetCount} — {_activeDisplayQuest.Description}";
            GUI.Label(new Rect(panelX + 8 * scale, panelY + 26 * scale, panelW - 16 * scale, 50 * scale),
                progressText, progressStyle);

            // Quest complete flash
            if (_questCompleteFlash > 0)
            {
                float alpha = Mathf.Clamp01(_questCompleteFlash);
                var flashStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(28 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                flashStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, alpha);
                GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 40 * scale),
                    "QUEST COMPLETE!", flashStyle);
            }
        }
    }
}
