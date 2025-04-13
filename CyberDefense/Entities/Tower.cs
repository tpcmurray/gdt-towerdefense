using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace CyberDefense.Entities
{
    public class Tower : Entity
    {
        // Tower stats as per MVP requirements
        public float Range { get; set; }
        public int Damage { get; set; }
        public float FireRate { get; set; } // Shots per second
        public float FireCooldown { get; private set; } // Current cooldown time

        // Target tracking
        private Enemy currentTarget;

        // For multiplayer: track who placed this tower
        public int PlacedByPlayerId { get; set; }

        public Tower(Vector2 position, float range, int damage, float fireRate) 
            : base(position)
        {
            Range = range;
            Damage = damage;
            FireRate = fireRate;
            FireCooldown = 1;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update fire cooldown
            if (FireCooldown > 0)
            {
                FireCooldown -= deltaTime;
            }

            // If we have a current target, check if it's still valid
            if (currentTarget != null)
            {
                // Clear target if it's no longer active or out of range
                if (!currentTarget.IsActive || !IsInRange(currentTarget.Position))
                {
                    currentTarget = null;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            
            // Debug visualization of range (circle outline)
            /* When we have debugging visuals
            if (IsDebugMode)
            {
                DrawRangeCircle(spriteBatch);
            }
            */
        }

        public Projectile FireAtTarget(Enemy target)
        {
            // Only fire if cooldown is ready
            if (FireCooldown <= 0)
            {
                // Track this as our current target
                currentTarget = target;
                
                // Reset cooldown based on fire rate
                FireCooldown = 1.0f / FireRate;
                
                // Create and return a new projectile aimed at the target
                var projectile = new Projectile(
                    Position,
                    target,
                    Damage,
                    300f, // Projectile speed - increased for better gameplay
                    OwnerId
                );
                
                return projectile;
            }
            
            return null;
        }

        public bool IsInRange(Vector2 targetPosition)
        {
            // Check if target is within tower's range
            return Vector2.Distance(Position, targetPosition) <= Range;
        }

        public List<Enemy> FindTargetsInRange(List<Enemy> enemies)
        {
            List<Enemy> targetsInRange = new List<Enemy>();

            foreach (var enemy in enemies)
            {
                if (enemy.IsActive && IsInRange(enemy.Position))
                {
                    targetsInRange.Add(enemy);
                }
            }

            return targetsInRange;
        }

        public Enemy SelectTarget(List<Enemy> enemiesInRange)
        {
            // If we already have a valid target in range, stick with it
            if (currentTarget != null && currentTarget.IsActive && 
                enemiesInRange.Contains(currentTarget))
            {
                return currentTarget;
            }
            
            // Default target selection: first enemy in range
            // This can be expanded later with different targeting strategies
            if (enemiesInRange.Count > 0)
            {
                currentTarget = enemiesInRange[0];
                return currentTarget;
            }
            
            return null;
        }
    }
}