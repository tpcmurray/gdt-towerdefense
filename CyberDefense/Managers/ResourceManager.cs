using System;
using Microsoft.Xna.Framework;

namespace CyberDefense.Managers
{
    public class ResourceManager
    {
        // Resource values
        public int Money { get; private set; }
        
        // For multiplayer
        public bool IsSharedMoney { get; set; } = true;  // Whether money is shared between players
        public bool ResourcesChanged { get; private set; } = false; // Flag for network sync
        
        // Events
        public event Action<int> OnMoneyChanged;
        
        public ResourceManager(int startingMoney = 100)
        {
            Money = startingMoney;
        }
        
        public void Update(GameTime gameTime)
        {
            // Reset change flag after sync
            ResourcesChanged = false;
        }
        
        public bool CanAfford(int cost)
        {
            return Money >= cost;
        }
        
        public bool SpendMoney(int amount)
        {
            if (CanAfford(amount))
            {
                Money -= amount;
                ResourcesChanged = true;
                OnMoneyChanged?.Invoke(Money);
                return true;
            }
            
            return false;
        }
        
        public void AddMoney(int amount)
        {
            Money += amount;
            ResourcesChanged = true;
            OnMoneyChanged?.Invoke(Money);
        }
        
        // For network synchronization
        public void SyncMoney(int newAmount)
        {
            if (Money != newAmount)
            {
                Money = newAmount;
                ResourcesChanged = true;
                OnMoneyChanged?.Invoke(Money);
            }
        }
        
        // Calculate reward for defeating enemy based on wave number
        public int CalculateEnemyReward(int waveNumber)
        {
            // Base reward plus bonus for higher waves
            return 10 + (waveNumber - 1) * 2;
        }
        
        // Calculate reward for completing a wave
        public int CalculateWaveCompletionReward(int waveNumber)
        {
            // Bonus money for completing a wave
            return 50 + (waveNumber - 1) * 15;
        }
        
        // Reset resources to initial state
        public void Reset(int startingMoney = 200)
        {
            Money = startingMoney;
            ResourcesChanged = true;
            OnMoneyChanged?.Invoke(Money);
        }
    }
}