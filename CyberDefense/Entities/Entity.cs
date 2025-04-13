using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CyberDefense.Entities
{
    public abstract class Entity
    {
        // Basic properties for all game entities
        public Vector2 Position { get; set; }
        public bool IsActive { get; set; } = true;
        public int Id { get; set; }
        
        // For networking purposes - to track which player owns/created this entity
        public int OwnerId { get; set; }
        
        // Added opacity property for visual effects like transparency
        public float Opacity { get; set; } = 1.0f;
        
        protected Texture2D texture;
        
        public Entity(Vector2 position)
        {
            Position = position;
        }
        
        public virtual void LoadContent(Microsoft.Xna.Framework.Content.ContentManager content, string textureName)
        {
            texture = content.Load<Texture2D>(textureName);
        }
        
        public virtual void Update(GameTime gameTime)
        {
            // Base update logic
        }
        
        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (IsActive && texture != null)
            {
                spriteBatch.Draw(
                    texture, 
                    Position, 
                    null, 
                    Color.White * Opacity, 
                    0f, 
                    new Vector2(texture.Width / 2, texture.Height / 2), 
                    Vector2.One, 
                    SpriteEffects.None, 
                    0f
                );
            }
        }
    }
}