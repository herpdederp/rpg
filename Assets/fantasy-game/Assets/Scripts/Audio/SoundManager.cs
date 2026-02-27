// Assets/Scripts/Audio/SoundManager.cs
// ======================================
// Procedural audio system using AudioClips generated from code.
// Zero external asset dependencies — all sounds are synthesized at runtime.
// Handles footsteps, combat, ambient, and UI sounds.

using UnityEngine;
using System.Collections.Generic;

namespace FantasyGame.Audio
{
    public class SoundManager : MonoBehaviour
    {
        // Singleton
        public static SoundManager Instance { get; private set; }

        // Audio sources
        private AudioSource _sfxSource;
        private AudioSource _ambientSource;
        private AudioSource _footstepSource;

        // Generated clips
        private AudioClip _footstepGrass;
        private AudioClip _footstepStone;
        private AudioClip _swordSwing;
        private AudioClip _swordHit;
        private AudioClip _enemyHit;
        private AudioClip _enemyDeath;
        private AudioClip _chestOpen;
        private AudioClip _itemPickup;
        private AudioClip _levelUp;
        private AudioClip _campfireLoop;
        private AudioClip _ambientWind;
        private AudioClip _breakObject;
        private AudioClip _dialogueOpen;
        private AudioClip _playerHurt;
        private AudioClip _playerDeath;

        // Footstep state
        private Transform _player;
        private Player.ThirdPersonController _controller;
        private float _footstepTimer;
        private float _footstepInterval = 0.4f;

        // Ambient state
        private float _ambientFadeTarget = 1f;

        public void Init(Transform player)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _player = player;
            _controller = player.GetComponent<Player.ThirdPersonController>();

            // Create audio sources
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f; // 2D
            _sfxSource.volume = 0.5f;

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.playOnAwake = false;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.loop = true;
            _ambientSource.volume = 0.15f;

            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.playOnAwake = false;
            _footstepSource.spatialBlend = 0f;
            _footstepSource.volume = 0.25f;

            GenerateAllClips();
            StartAmbient();

            Debug.Log("[SoundManager] Initialized with procedural audio.");
        }

        // ===================================================================
        // Public API
        // ===================================================================

        public void PlaySwordSwing()
        {
            PlaySFX(_swordSwing, 0.35f, Random.Range(0.9f, 1.1f));
        }

        public void PlaySwordHit()
        {
            PlaySFX(_swordHit, 0.5f, Random.Range(0.85f, 1.15f));
        }

        public void PlayEnemyHit()
        {
            PlaySFX(_enemyHit, 0.4f, Random.Range(0.8f, 1.2f));
        }

        public void PlayEnemyDeath()
        {
            PlaySFX(_enemyDeath, 0.45f, Random.Range(0.9f, 1.1f));
        }

        public void PlayChestOpen()
        {
            PlaySFX(_chestOpen, 0.5f, 1f);
        }

        public void PlayItemPickup()
        {
            PlaySFX(_itemPickup, 0.4f, Random.Range(1.0f, 1.3f));
        }

        public void PlayLevelUp()
        {
            PlaySFX(_levelUp, 0.6f, 1f);
        }

        public void PlayBreakObject()
        {
            PlaySFX(_breakObject, 0.5f, Random.Range(0.8f, 1.2f));
        }

        public void PlayDialogueOpen()
        {
            PlaySFX(_dialogueOpen, 0.3f, 1f);
        }

        public void PlayPlayerHurt()
        {
            PlaySFX(_playerHurt, 0.45f, Random.Range(0.9f, 1.1f));
        }

        public void PlayPlayerDeath()
        {
            PlaySFX(_playerDeath, 0.6f, 1f);
        }

        // ===================================================================
        // Update: footsteps
        // ===================================================================

        private void Update()
        {
            if (_controller == null || _player == null) return;

            // Footsteps based on movement speed
            float speed = _controller.CurrentSpeed;
            if (speed > 0.5f && _controller.IsGrounded)
            {
                _footstepInterval = speed > 6f ? 0.28f : 0.42f;
                _footstepTimer -= Time.deltaTime;
                if (_footstepTimer <= 0f)
                {
                    _footstepTimer = _footstepInterval;
                    PlayFootstep();
                }
            }
            else
            {
                _footstepTimer = 0f;
            }
        }

        private void PlayFootstep()
        {
            float pitch = Random.Range(0.85f, 1.15f);
            float vol = Random.Range(0.15f, 0.3f);
            _footstepSource.pitch = pitch;
            _footstepSource.volume = vol;
            _footstepSource.PlayOneShot(_footstepGrass);
        }

        private void StartAmbient()
        {
            _ambientSource.clip = _ambientWind;
            _ambientSource.Play();
        }

        private void PlaySFX(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, volume);
        }

        // ===================================================================
        // Procedural audio generation
        // ===================================================================

        private void GenerateAllClips()
        {
            _footstepGrass = GenerateFootstepGrass();
            _footstepStone = GenerateFootstepStone();
            _swordSwing = GenerateSwordSwing();
            _swordHit = GenerateSwordHit();
            _enemyHit = GenerateEnemyHit();
            _enemyDeath = GenerateEnemyDeath();
            _chestOpen = GenerateChestOpen();
            _itemPickup = GenerateItemPickup();
            _levelUp = GenerateLevelUp();
            _breakObject = GenerateBreakObject();
            _dialogueOpen = GenerateDialogueOpen();
            _ambientWind = GenerateAmbientWind();
            _playerHurt = GeneratePlayerHurt();
            _playerDeath = GeneratePlayerDeath();
        }

        // --- Footstep: soft noise burst (grass) ---
        private AudioClip GenerateFootstepGrass()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.08f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = (1f - t) * (1f - t); // Fast decay
                float noise = (Random.value * 2f - 1f) * 0.3f;
                // Low-pass approximation
                float lowFreq = Mathf.Sin(t * 220f) * 0.15f;
                data[i] = (noise + lowFreq) * envelope;
            }
            return CreateClip("footstep_grass", data, sampleRate);
        }

        // --- Footstep: harder tap (stone) ---
        private AudioClip GenerateFootstepStone()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.06f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = (1f - t) * (1f - t) * (1f - t);
                float click = Mathf.Sin(t * 800f) * 0.4f;
                float noise = (Random.value * 2f - 1f) * 0.2f;
                data[i] = (click + noise) * envelope;
            }
            return CreateClip("footstep_stone", data, sampleRate);
        }

        // --- Sword swing: whoosh ---
        private AudioClip GenerateSwordSwing()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.25f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.6f;
                // Rising pitch whoosh
                float freq = Mathf.Lerp(200f, 600f, t);
                float noise = (Random.value * 2f - 1f) * 0.3f;
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f) * 0.2f;
                data[i] = (noise * 0.5f + tone) * envelope;
            }
            return CreateClip("sword_swing", data, sampleRate);
        }

        // --- Sword hit: metallic impact ---
        private AudioClip GenerateSwordHit()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 15f);
                float tone1 = Mathf.Sin(i * 440f / sampleRate * Mathf.PI * 2f) * 0.3f;
                float tone2 = Mathf.Sin(i * 880f / sampleRate * Mathf.PI * 2f) * 0.15f;
                float tone3 = Mathf.Sin(i * 1320f / sampleRate * Mathf.PI * 2f) * 0.1f;
                float noise = (Random.value * 2f - 1f) * 0.15f;
                data[i] = (tone1 + tone2 + tone3 + noise) * envelope;
            }
            return CreateClip("sword_hit", data, sampleRate);
        }

        // --- Enemy hit: thud ---
        private AudioClip GenerateEnemyHit()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.12f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 12f);
                float tone = Mathf.Sin(i * 150f / sampleRate * Mathf.PI * 2f) * 0.5f;
                float noise = (Random.value * 2f - 1f) * 0.2f;
                data[i] = (tone + noise) * envelope;
            }
            return CreateClip("enemy_hit", data, sampleRate);
        }

        // --- Enemy death: descending tone + noise ---
        private AudioClip GenerateEnemyDeath()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.4f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = (1f - t);
                float freq = Mathf.Lerp(400f, 80f, t);
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f) * 0.4f;
                float noise = (Random.value * 2f - 1f) * 0.1f * (1f - t);
                data[i] = (tone + noise) * envelope;
            }
            return CreateClip("enemy_death", data, sampleRate);
        }

        // --- Chest open: creaky hinge ---
        private AudioClip GenerateChestOpen()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.5f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.5f;
                // Creaky rising tone
                float freq = Mathf.Lerp(100f, 350f, t * t);
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f) * 0.3f;
                // Add some grinding noise
                float grind = (Random.value * 2f - 1f) * 0.15f * Mathf.Sin(t * Mathf.PI);
                data[i] = (tone + grind) * envelope;
            }
            return CreateClip("chest_open", data, sampleRate);
        }

        // --- Item pickup: bright ding ---
        private AudioClip GenerateItemPickup()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.2f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 8f);
                float tone1 = Mathf.Sin(i * 880f / sampleRate * Mathf.PI * 2f) * 0.3f;
                float tone2 = Mathf.Sin(i * 1320f / sampleRate * Mathf.PI * 2f) * 0.2f;
                data[i] = (tone1 + tone2) * envelope;
            }
            return CreateClip("item_pickup", data, sampleRate);
        }

        // --- Level up: ascending arpeggio ---
        private AudioClip GenerateLevelUp()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.8f);
            float[] data = new float[samples];
            float[] notes = { 523f, 659f, 784f, 1047f }; // C5, E5, G5, C6
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                int noteIndex = Mathf.Min((int)(t * notes.Length), notes.Length - 1);
                float noteT = (t * notes.Length) - noteIndex;
                float envelope = Mathf.Exp(-noteT * 3f) * (1f - t * 0.5f);
                float freq = notes[noteIndex];
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f) * 0.25f;
                float harmony = Mathf.Sin(i * freq * 1.5f / sampleRate * Mathf.PI * 2f) * 0.1f;
                data[i] = (tone + harmony) * envelope;
            }
            return CreateClip("level_up", data, sampleRate);
        }

        // --- Break object: crunch ---
        private AudioClip GenerateBreakObject()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.2f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 10f);
                float noise = (Random.value * 2f - 1f) * 0.5f;
                float crunch = Mathf.Sin(i * 200f / sampleRate * Mathf.PI * 2f) * 0.3f;
                crunch *= (Random.value > 0.5f ? 1f : -1f); // Irregular
                data[i] = (noise + crunch) * envelope;
            }
            return CreateClip("break_object", data, sampleRate);
        }

        // --- Dialogue open: soft chime ---
        private AudioClip GenerateDialogueOpen()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 6f);
                float tone = Mathf.Sin(i * 660f / sampleRate * Mathf.PI * 2f) * 0.2f;
                float overtone = Mathf.Sin(i * 1320f / sampleRate * Mathf.PI * 2f) * 0.08f;
                data[i] = (tone + overtone) * envelope;
            }
            return CreateClip("dialogue_open", data, sampleRate);
        }

        // --- Player hurt: low thud ---
        private AudioClip GeneratePlayerHurt()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.15f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 10f);
                float tone = Mathf.Sin(i * 120f / sampleRate * Mathf.PI * 2f) * 0.5f;
                float noise = (Random.value * 2f - 1f) * 0.15f;
                data[i] = (tone + noise) * envelope;
            }
            return CreateClip("player_hurt", data, sampleRate);
        }

        // --- Player death: dramatic descending ---
        private AudioClip GeneratePlayerDeath()
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 1.0f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = (1f - t * 0.8f);
                float freq = Mathf.Lerp(300f, 60f, t);
                float tone = Mathf.Sin(i * freq / sampleRate * Mathf.PI * 2f) * 0.35f;
                float sub = Mathf.Sin(i * freq * 0.5f / sampleRate * Mathf.PI * 2f) * 0.2f;
                float noise = (Random.value * 2f - 1f) * 0.05f * (1f - t);
                data[i] = (tone + sub + noise) * envelope;
            }
            return CreateClip("player_death", data, sampleRate);
        }

        // --- Ambient wind: filtered noise loop ---
        private AudioClip GenerateAmbientWind()
        {
            int sampleRate = 22050;
            int samples = sampleRate * 4; // 4 seconds loop
            float[] data = new float[samples];
            float prev = 0f;
            float prev2 = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                // Slow modulation
                float mod = Mathf.Sin(t * Mathf.PI * 2f * 0.3f) * 0.5f + 0.5f;
                float noise = (Random.value * 2f - 1f) * 0.15f * mod;
                // Simple 2-pole low-pass filter
                float filtered = noise * 0.05f + prev * 0.6f + prev2 * 0.35f;
                prev2 = prev;
                prev = filtered;
                // Add gentle nature-like chirps
                float chirp = Mathf.Sin(i * 3000f / sampleRate * Mathf.PI * 2f) * 0.02f
                    * Mathf.Max(0, Mathf.Sin(t * Mathf.PI * 2f * 2f + 1f));
                data[i] = filtered + chirp;
            }
            return CreateClip("ambient_wind", data, sampleRate);
        }

        private AudioClip CreateClip(string name, float[] data, int sampleRate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
