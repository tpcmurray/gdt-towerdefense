using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CyberDefense.Entities
{
    public class HomeBase : Entity
    {
        public int MaxHealth { get; private set; }
        public int CurrentHealth { get; private set; }
        
        // Event for game over
        public event Action OnDestroyed;
        
        // For network synchronization
        public bool HealthChanged { get; private set; } = false;
        
        public HomeBase(Vector2 position, int maxHealth) 
            : base(position)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }
        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            // Reset health changed flag after sync
            HealthChanged = false;
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            
            // Draw health bar
            if (texture != null)
            {
                float healthPercentage = (float)CurrentHealth / MaxHealth;
                
                // Health bar background
                spriteBatch.Draw(
                    texture, // We'll replace with a proper health bar texture later
                    new Rectangle((int)Position.X - 25, (int)Position.Y + 30, 50, 5),
                    null,
                    Color.Red,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
                
                // Health bar fill
                spriteBatch.Draw(
                    texture, // We'll replace with a proper health bar texture later
                    new Rectangle((int)Position.X - 25, (int)Position.Y + 30, (int)(50 * healthPercentage), 5),
                    null,
                    Color.Green,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        public bool TakeDamage(int damage)
        {
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            HealthChanged = true;
            
            // Check if destroyed
            if (CurrentHealth <= 0)
            {
                IsActive = false;
                OnDestroyed?.Invoke();
                return true;
            }
            
            return false;
        }
        
        public void Heal(int amount)
        {
            CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged = true;
        }
        
        // For network synchronization
        public void SyncHealth(int newHealth)
        {
            if (CurrentHealth != newHealth)
            {
                CurrentHealth = newHealth;
                HealthChanged = true;
            }
        }
        
        // Reset health to initial value
        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            HealthChanged = true;
        }
    }
}