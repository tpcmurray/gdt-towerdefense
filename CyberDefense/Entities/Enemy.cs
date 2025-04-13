using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CyberDefense.Entities
{
    public class Enemy : Entity
    {
        // Enemy stats as per MVP requirements
        public int Health { get; private set; }
        public float Speed { get; set; }
        public int Damage { get; set; }
        
        // Path following
        private List<Vector2> path;
        private int currentPathIndex = 0;
        
        // For network synchronization
        public bool IsSyncedAcrossNetwork { get; set; } = false;
        
        public Enemy(Vector2 position, int health, float speed, int damage) 
            : base(position)
        {
            Health = health;
            Speed = speed;
            Damage = damage;
        }
        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            if (!IsActive || path == null || path.Count == 0)
                return;
            
            // Move along path
            if (currentPathIndex < path.Count)
            {
                // Get target point on path
                Vector2 targetPoint = path[currentPathIndex];
                
                // Calculate direction to target
                Vector2 direction = targetPoint - Position;
                
                // Check if we've reached the current point
                if (direction.Length() < Speed * (float)gameTime.ElapsedGameTime.TotalSeconds)
                {
                    // Move to next point
                    Position = targetPoint;
                    currentPathIndex++;
                }
                else
                {
                    // Normalize direction vector
                    if (direction != Vector2.Zero)
                    {
                        direction.Normalize();
                    }
                    
                    // Move towards target
                    Position += direction * Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                }
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            
            // Draw health bar above enemy
            if (texture != null)
            {
                float healthPercentage = (float)Health / 100; // Assuming 100 is max health
                
                // Health bar background
                Rectangle healthBarRect = new Rectangle(
                    (int)Position.X - 15,
                    (int)Position.Y - 20,
                    30,
                    4
                );
                
                // Draw red background
                spriteBatch.Draw(
                    texture,
                    healthBarRect,
                    null,
                    Color.Red,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
                
                // Draw green health amount
                healthBarRect.Width = (int)(30 * healthPercentage);
                spriteBatch.Draw(
                    texture,
                    healthBarRect,
                    null,
                    Color.Green,
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        public void SetPath(List<Vector2> path)
        {
            this.path = path;
            currentPathIndex = 0;
        }
        
        public bool TakeDamage(int damage)
        {
            Health -= damage;
            
            if (Health <= 0)
            {
                IsActive = false;
                return true; // Enemy was killed
            }
            
            return false;
        }
        
        public bool HasReachedEnd()
        {
            return path != null && currentPathIndex >= path.Count;
        }
    }
}