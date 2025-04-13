using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CyberDefense.Entities;
using CyberDefense.Map;

namespace CyberDefense.Managers
{
    public class WaveManager
    {
        // Wave information
        public int CurrentWave { get; private set; }
        public int TotalWaves { get; private set; }
        public bool WaveInProgress { get; private set; }
        
        // Timing
        private float timeBetweenWaves;
        private float timeUntilNextWave;
        private float timeBetweenEnemies;
        private float timeUntilNextEnemy;
        
        // Enemies
        private List<Enemy> activeEnemies = new List<Enemy>();
        private int enemiesRemainingInWave;
        private List<Vector2> enemyPath;
        
        // Stats for the current wave
        private int enemiesPerWave;
        private int baseEnemyHealth;
        private float baseEnemySpeed;
        private int baseEnemyDamage;
        
        // Enemy texture
        private Texture2D enemyTexture;
        
        // Events
        public event Action<int> OnWaveCompleted;
        public event Action<int> OnWaveStarted;
        public event Action OnAllWavesCompleted;
        public event Action<Enemy> OnEnemySpawned;
        public event Action<Enemy, bool> OnEnemyDefeated; // bool indicates if it reached the end
        
        // For multiplayer synchronization
        public bool IsHost { get; set; } = true; // Only the host spawns enemies by default
        
        public WaveManager(int totalWaves, float timeBetweenWaves)
        {
            TotalWaves = totalWaves;
            this.timeBetweenWaves = timeBetweenWaves;
            
            // Initialize
            CurrentWave = 0;
            WaveInProgress = false;
            timeUntilNextWave = timeBetweenWaves;
            
            // Default enemy stats
            baseEnemyHealth = 100;
            baseEnemySpeed = 100f;
            baseEnemyDamage = 10;
            enemiesPerWave = 10;
            timeBetweenEnemies = 1.0f;
        }
        
        public void SetEnemyPath(List<Vector2> path)
        {
            enemyPath = path;
        }
        
        public void Update(GameTime gameTime, HomeBase playerBase)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Only process wave spawning logic if this client is the host
            if (IsHost)
            {
                // Handle wave timing and spawning
                if (!WaveInProgress)
                {
                    // Countdown to next wave
                    timeUntilNextWave -= deltaTime;
                    
                    if (timeUntilNextWave <= 0 && CurrentWave < TotalWaves)
                    {
                        StartNextWave();
                    }
                }
                else if (enemiesRemainingInWave > 0)
                {
                    // Spawn enemies
                    timeUntilNextEnemy -= deltaTime;
                    
                    if (timeUntilNextEnemy <= 0)
                    {
                        SpawnEnemy();
                        timeUntilNextEnemy = timeBetweenEnemies;
                    }
                }
                else if (activeEnemies.Count == 0)
                {
                    // Wave is complete when all enemies are defeated
                    CompleteCurrentWave();
                }
            }
            
            // Update all active enemies (happens on all clients)
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = activeEnemies[i];
                enemy.Update(gameTime);
                
                // Check if enemy reached the end
                if (enemy.IsActive && enemy.HasReachedEnd())
                {
                    // Deal damage to player base
                    playerBase.TakeDamage(enemy.Damage);
                    
                    // Deactivate enemy
                    enemy.IsActive = false;
                    
                    // Trigger event
                    OnEnemyDefeated?.Invoke(enemy, true);
                    
                    // Remove from active list
                    activeEnemies.RemoveAt(i);
                }
                else if (!enemy.IsActive)
                {
                    // Enemy was defeated in some other way (tower attack)
                    OnEnemyDefeated?.Invoke(enemy, false);
                    activeEnemies.RemoveAt(i);
                }
            }
        }
        
        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw all active enemies
            foreach (var enemy in activeEnemies)
            {
                enemy.Draw(spriteBatch);
            }
        }
        
        private void StartNextWave()
        {
            CurrentWave++;
            WaveInProgress = true;
            
            // Scale difficulty with wave number
            int enemyHealth = baseEnemyHealth + (CurrentWave - 1) * 20;
            float enemySpeed = baseEnemySpeed + (CurrentWave - 1) * 5;
            int enemyDamage = baseEnemyDamage + (CurrentWave - 1) * 2;
            
            // Increase enemies per wave as the game progresses
            enemiesRemainingInWave = enemiesPerWave + (CurrentWave - 1) * 2;
            
            // Reset enemy spawn timer
            timeUntilNextEnemy = 0;
            
            // Trigger event
            OnWaveStarted?.Invoke(CurrentWave);
        }
        
        private void CompleteCurrentWave()
        {
            WaveInProgress = false;
            
            // Reset timer for next wave
            timeUntilNextWave = timeBetweenWaves;
            
            // Trigger events
            OnWaveCompleted?.Invoke(CurrentWave);
            
            // Check if all waves are completed
            if (CurrentWave >= TotalWaves)
            {
                OnAllWavesCompleted?.Invoke();
            }
        }
        
        public void SetEnemyTexture(Texture2D texture)
        {
            this.enemyTexture = texture;
        }
        
        private void SpawnEnemy()
        {
            if (enemyPath == null || enemyPath.Count == 0)
                return;
                
            // Scale enemy stats based on current wave
            int health = baseEnemyHealth + (CurrentWave - 1) * 20;
            float speed = baseEnemySpeed + (CurrentWave - 1) * 5;
            int damage = baseEnemyDamage + (CurrentWave - 1) * 2;
            
            // Create enemy at the start of the path
            Vector2 spawnPosition = enemyPath[0];
            Enemy enemy = new Enemy(spawnPosition, health, speed, damage);
            
            // Set enemy texture if available
            if (enemyTexture != null)
            {
                var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (textureField != null)
                {
                    textureField.SetValue(enemy, enemyTexture);
                }
            }
            
            // Set enemy path
            enemy.SetPath(enemyPath);
            
            // Add to active enemies
            activeEnemies.Add(enemy);
            
            // Decrease remaining count
            enemiesRemainingInWave--;
            
            // Trigger event
            OnEnemySpawned?.Invoke(enemy);
        }
        
        public List<Enemy> GetActiveEnemies()
        {
            return activeEnemies;
        }
        
        // For network synchronization - add enemy that was spawned on another client
        public void AddNetworkSpawnedEnemy(Enemy enemy)
        {
            if (!IsHost)
            {
                activeEnemies.Add(enemy);
                enemy.SetPath(enemyPath);
            }
        }
        
        // Force start next wave (can be triggered by player or network)
        public void ForceStartNextWave()
        {
            if (IsHost && !WaveInProgress)
            {
                timeUntilNextWave = 0;
            }
        }
        
        // Reset the wave manager to initial state
        public void Reset()
        {
            // Reset wave counters
            CurrentWave = 0;
            WaveInProgress = false;
            timeUntilNextWave = timeBetweenWaves;
            
            // Clear existing enemies
            activeEnemies.Clear();
            enemiesRemainingInWave = 0;
        }
    }
}