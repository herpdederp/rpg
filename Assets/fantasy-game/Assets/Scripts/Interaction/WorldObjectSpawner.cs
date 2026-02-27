// Assets/Scripts/Interaction/WorldObjectSpawner.cs
// ==================================================
// Spawns interactable objects around the world: chests, crates,
// barrels, campfires, and NPCs at predefined or procedural locations.

using UnityEngine;
using FantasyGame.RPG;

namespace FantasyGame.Interaction
{
    public class WorldObjectSpawner : MonoBehaviour
    {
        private Transform _player;
        private QuestManager _questManager;

        public void Init(Transform player, QuestManager questManager)
        {
            _player = player;
            _questManager = questManager;
            SpawnFixedObjects();
        }

        private void SpawnFixedObjects()
        {
            // --- NPC near spawn ---
            SpawnNPC(
                new Vector3(5f, 0, 5f),
                "Elder Oakbeard",
                new DialogueLine[]
                {
                    new DialogueLine("Elder Oakbeard", "Welcome, adventurer! These lands have become dangerous."),
                    new DialogueLine("Elder Oakbeard", "Slimes have overrun the lowlands. Could you help clear them out?"),
                    new DialogueLine("Elder Oakbeard", "Defeat 5 Slimes and return to me for a reward."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Elder Oakbeard", "Well done! The lowlands are safer now."),
                    new DialogueLine("Elder Oakbeard", "Take these potions as thanks."),
                },
                "kill_slimes"
            );

            // --- Second NPC ---
            SpawnNPC(
                new Vector3(-8f, 0, 12f),
                "Scout Fern",
                new DialogueLine[]
                {
                    new DialogueLine("Scout Fern", "I've been tracking wolf packs in these hills."),
                    new DialogueLine("Scout Fern", "Bring me 3 Wolf Pelts and I'll trade you something special."),
                },
                new DialogueLine[]
                {
                    new DialogueLine("Scout Fern", "Impressive! You're a true hunter."),
                    new DialogueLine("Scout Fern", "This enchanted blade is yours. Use it well!"),
                },
                "collect_pelts"
            );

            // --- Campfire at spawn ---
            SpawnCampfire(new Vector3(2f, 0, -3f));

            // --- Campfire further out ---
            SpawnCampfire(new Vector3(35f, 0, 25f));

            // --- Treasure chests scattered ---
            SpawnChest(new Vector3(15f, 0, 10f));
            SpawnChest(new Vector3(-20f, 0, 30f));
            SpawnChest(new Vector3(40f, 0, -15f));

            // --- Breakable crates near spawn ---
            SpawnBreakable(new Vector3(8f, 0, -2f), "Crate", "potion_small", 0.5f);
            SpawnBreakable(new Vector3(9f, 0, -1f), "Crate", "potion_small", 0.3f);
            SpawnBreakable(new Vector3(7f, 0, -3f), "Barrel", "potion_small", 0.4f);

            // --- More breakables further out ---
            SpawnBreakable(new Vector3(25f, 0, 20f), "Crate", "potion_large", 0.3f);
            SpawnBreakable(new Vector3(-15f, 0, 25f), "Barrel", "bone_fragment", 0.4f);

            Debug.Log("[WorldObjectSpawner] Fixed world objects placed.");
        }

        private void SpawnNPC(Vector3 pos, string name, DialogueLine[] lines,
            DialogueLine[] completeLines, string questId)
        {
            pos = SnapToTerrain(pos);

            var npcGo = new GameObject($"NPC_{name}");
            npcGo.transform.position = pos;

            // Visual: capsule body + sphere head
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(npcGo.transform);
            body.transform.localPosition = new Vector3(0, 0.75f, 0);
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Object.Destroy(bodyCol);

            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                // Different colors per NPC
                mat.color = name.Contains("Elder")
                    ? new Color(0.3f, 0.5f, 0.25f)  // Green robe
                    : new Color(0.5f, 0.35f, 0.25f); // Brown leather
                bodyRenderer.material = mat;
            }

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(npcGo.transform);
            head.transform.localPosition = new Vector3(0, 1.5f, 0);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) Object.Destroy(headCol);

            var headRenderer = head.GetComponent<Renderer>();
            if (headRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.85f, 0.7f, 0.6f); // Skin tone
                headRenderer.material = mat;
            }

            var dialogue = npcGo.AddComponent<DialogueNPC>();
            dialogue.Init(name, lines, completeLines, questId);
        }

        private void SpawnCampfire(Vector3 pos)
        {
            pos = SnapToTerrain(pos);

            var fireGo = new GameObject("Campfire");
            fireGo.transform.position = pos;

            // Log base (brown cylinder)
            for (int i = 0; i < 3; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.transform.SetParent(fireGo.transform);
                float angle = i * 60f;
                log.transform.localPosition = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * 0.15f, 0.05f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * 0.15f
                );
                log.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
                log.transform.localRotation = Quaternion.Euler(0, angle, 75f);

                var logCol = log.GetComponent<Collider>();
                if (logCol != null) Object.Destroy(logCol);

                var logRenderer = log.GetComponent<Renderer>();
                if (logRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.35f, 0.2f, 0.1f);
                    logRenderer.material = mat;
                }
            }

            // Stone ring
            for (int i = 0; i < 6; i++)
            {
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.transform.SetParent(fireGo.transform);
                float angle = i * 60f * Mathf.Deg2Rad;
                stone.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 0.35f, 0.05f,
                    Mathf.Sin(angle) * 0.35f
                );
                stone.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);

                var stoneCol = stone.GetComponent<Collider>();
                if (stoneCol != null) Object.Destroy(stoneCol);

                var stoneRenderer = stone.GetComponent<Renderer>();
                if (stoneRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.4f, 0.38f, 0.35f);
                    stoneRenderer.material = mat;
                }
            }

            // Add campfire interactable + point light
            var campfire = fireGo.AddComponent<Campfire>();
            campfire.Init();

            var pointLight = fireGo.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.7f, 0.3f);
            pointLight.intensity = 2f;
            pointLight.range = 8f;
            pointLight.transform.localPosition = Vector3.up * 0.5f;
        }

        private void SpawnChest(Vector3 pos)
        {
            pos = SnapToTerrain(pos);

            var chestGo = new GameObject("TreasureChest");
            chestGo.transform.position = pos;

            // Base box
            var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBox.transform.SetParent(chestGo.transform);
            baseBox.transform.localPosition = new Vector3(0, 0.2f, 0);
            baseBox.transform.localScale = new Vector3(0.6f, 0.3f, 0.4f);
            var baseCol = baseBox.GetComponent<Collider>();
            if (baseCol != null) Object.Destroy(baseCol);

            var baseRenderer = baseBox.GetComponent<Renderer>();
            if (baseRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.5f, 0.3f, 0.15f);
                baseRenderer.material = mat;
            }

            // Lid (pivot at back edge)
            var lidPivot = new GameObject("LidPivot");
            lidPivot.transform.SetParent(chestGo.transform);
            lidPivot.transform.localPosition = new Vector3(0, 0.35f, -0.2f);

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.transform.SetParent(lidPivot.transform);
            lid.transform.localPosition = new Vector3(0, 0.08f, 0.2f);
            lid.transform.localScale = new Vector3(0.62f, 0.12f, 0.42f);
            var lidCol = lid.GetComponent<Collider>();
            if (lidCol != null) Object.Destroy(lidCol);

            var lidRenderer = lid.GetComponent<Renderer>();
            if (lidRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.55f, 0.35f, 0.15f);
                lidRenderer.material = mat;
            }

            // Lock (gold sphere on front)
            var lockSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lockSphere.transform.SetParent(chestGo.transform);
            lockSphere.transform.localPosition = new Vector3(0, 0.25f, 0.21f);
            lockSphere.transform.localScale = Vector3.one * 0.06f;
            var lockCol = lockSphere.GetComponent<Collider>();
            if (lockCol != null) Object.Destroy(lockCol);

            var lockRenderer = lockSphere.GetComponent<Renderer>();
            if (lockRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.9f, 0.75f, 0.2f);
                mat.SetFloat("_Metallic", 0.8f);
                lockRenderer.material = mat;
            }

            // Add chest collider on parent
            var chestCol = chestGo.AddComponent<BoxCollider>();
            chestCol.size = new Vector3(0.6f, 0.5f, 0.4f);
            chestCol.center = new Vector3(0, 0.25f, 0);

            var chest = chestGo.AddComponent<TreasureChest>();
            chest.Init(lidPivot.transform);
        }

        private void SpawnBreakable(Vector3 pos, string type, string lootId, float lootChance)
        {
            pos = SnapToTerrain(pos);

            var breakGo = new GameObject($"Breakable_{type}");
            breakGo.transform.position = pos;

            if (type == "Barrel")
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.transform.SetParent(breakGo.transform);
                barrel.transform.localPosition = new Vector3(0, 0.35f, 0);
                barrel.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                var bCol = barrel.GetComponent<Collider>();
                if (bCol != null) Object.Destroy(bCol);

                var bRenderer = barrel.GetComponent<Renderer>();
                if (bRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.45f, 0.3f, 0.15f);
                    mat.SetColor("_BaseColor", new Color(0.45f, 0.3f, 0.15f));
                    bRenderer.material = mat;
                }
            }
            else // Crate
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.transform.SetParent(breakGo.transform);
                crate.transform.localPosition = new Vector3(0, 0.3f, 0);
                crate.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var cCol = crate.GetComponent<Collider>();
                if (cCol != null) Object.Destroy(cCol);

                var cRenderer = crate.GetComponent<Renderer>();
                if (cRenderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(0.55f, 0.4f, 0.2f);
                    mat.SetColor("_BaseColor", new Color(0.55f, 0.4f, 0.2f));
                    cRenderer.material = mat;
                }
            }

            var breakable = breakGo.AddComponent<BreakableObject>();
            breakable.ObjectName = type;
            breakable.Init(10, lootId, lootChance);
        }

        private Vector3 SnapToTerrain(Vector3 pos)
        {
            Vector3 rayOrigin = new Vector3(pos.x, 200f, pos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 400f))
            {
                return hit.point + Vector3.up * 0.02f;
            }
            return pos;
        }
    }
}
