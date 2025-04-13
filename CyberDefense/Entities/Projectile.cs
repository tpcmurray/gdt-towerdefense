using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CyberDefense.Entities
{
    public class Projectile : Entity
    {
        public int Damage { get; set; }
        public float Speed { get; set; }
        public bool HasHit { get; private set; } = false;
        
        private Enemy target;
        private Vector2 direction;
        
        public Projectile(Vector2 position, Enemy target, int damage, float speed, int ownerId) 
            : base(position)
        {
            this.target = target;
            Damage = damage;
            Speed = speed;
            OwnerId = ownerId;
            
            // Calculate initial direction toward target
            direction = target.Position - position;
            if (direction != Vector2.Zero)
            {
                direction.Normalize();
            }
        }
        
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            if (!IsActive)
            {
                return;
            }
            
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // If target is gone, continue in last known direction
            if (target == null || !target.IsActive)
            {
                // Move along the last known direction
                Position += direction * Speed * deltaTime;
                
                // Deactivate if went too far off screen (simple cleanup)
                if (Position.X < -50 || Position.X > 1330 || Position.Y < -50 || Position.Y > 770)
                {
                    IsActive = false;
                }
                return;
            }
            
            // Update direction to track target
            direction = target.Position - Position;
            float distanceToTarget = direction.Length();
            
            if (direction != Vector2.Zero)
            {
                direction.Normalize();
            }
            
            // Move towards target
            Position += direction * Speed * deltaTime;
            
            // Check for collision with target
            if (distanceToTarget < 12) // Simple collision detection
            {
                // Apply damage to target
                bool killed = target.TakeDamage(Damage);
                
                // Deactivate projectile
                HasHit = true;
                IsActive = false;
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch)
        {
            if (IsActive && texture != null)
            {
                // Draw the projectile rotated in the direction of travel
                float rotation = (float)Math.Atan2(direction.Y, direction.X);
                
                spriteBatch.Draw(
                    texture,
                    Position,
                    null,
                    Color.White * Opacity,
                    rotation,
                    new Vector2(texture.Width / 2, texture.Height / 2),
                    Vector2.One,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}