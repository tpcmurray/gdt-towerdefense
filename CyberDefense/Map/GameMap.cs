using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CyberDefense.Map
{
    public class GameMap
    {
        // Tile types enum
        public enum TileType
        {
            Path,           // Where enemies can walk
            Buildable,      // Where towers can be placed
            NonBuildable,   // Background, not usable
            Start,          // Enemy spawn point
            End             // Base/goal for enemies
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileSize { get; private set; }
        
        private TileType[,] mapData;
        private Texture2D[] tileTextures;
        
        // Path from start to end for enemies to follow
        private List<Vector2> enemyPath;
        
        // For multiplayer synchronization
        public int MapId { get; set; }
        public bool IsSyncedAcrossNetwork { get; set; } = false;
        
        public GameMap(int width, int height, int tileSize)
        {
            Width = width;
            Height = height;
            TileSize = tileSize;
            mapData = new TileType[width, height];
            
            // Default to non-buildable
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    mapData[x, y] = TileType.NonBuildable;
                }
            }
        }
        
        public void LoadContent(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            // Load tile textures - these will need to be created in the Content project
            // For now we'll use placeholder tiles
            tileTextures = new Texture2D[5]; // One for each TileType
            
            // In a complete implementation, we'd load actual textures:
            // tileTextures[(int)TileType.Path] = content.Load<Texture2D>("Tiles/path");
            // tileTextures[(int)TileType.Buildable] = content.Load<Texture2D>("Tiles/buildable");
            // etc.
        }
        
        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw the map tiles
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    // Skip drawing if we don't have textures yet
                    if (tileTextures == null || tileTextures[(int)mapData[x, y]] == null)
                        continue;
                        
                    spriteBatch.Draw(
                        tileTextures[(int)mapData[x, y]],
                        new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize),
                        Color.White
                    );
                }
            }
        }
        
        public void SetTile(int x, int y, TileType type)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                mapData[x, y] = type;
            }
        }
        
        public TileType GetTile(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                return mapData[x, y];
            }
            
            return TileType.NonBuildable; // Default for out of bounds
        }
        
        public bool CanBuildAt(int x, int y)
        {
            return GetTile(x, y) == TileType.Buildable;
        }
        
        public Vector2 GetTileCenter(int x, int y)
        {
            return new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
        }
        
        public Vector2 WorldToTile(Vector2 worldPosition)
        {
            return new Vector2(
                (int)(worldPosition.X / TileSize),
                (int)(worldPosition.Y / TileSize)
            );
        }
        
        // Create a path from start to end for enemies to follow
        public void GeneratePath()
        {
            // Find start and end points
            Point start = Point.Zero;
            Point end = Point.Zero;
            bool foundStart = false;
            bool foundEnd = false;
            
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (mapData[x, y] == TileType.Start)
                    {
                        start = new Point(x, y);
                        foundStart = true;
                    }
                    else if (mapData[x, y] == TileType.End)
                    {
                        end = new Point(x, y);
                        foundEnd = true;
                    }
                }
            }
            
            // If we didn't find start or end, we can't generate a path
            if (!foundStart || !foundEnd)
            {
                enemyPath = new List<Vector2>();
                return;
            }
            
            // For MVP we'll use a simple path directly between points
            // Later this can be replaced with A* pathfinding
            enemyPath = new List<Vector2>();
            
            // Add the start point
            enemyPath.Add(GetTileCenter(start.X, start.Y));
            
            // For simplicity in MVP, we'll assume the path tiles are correctly placed
            // and just find all path tiles
            List<Point> pathPoints = new List<Point>();
            pathPoints.Add(start);
            
            // Keep track of visited tiles to avoid loops
            bool[,] visited = new bool[Width, Height];
            visited[start.X, start.Y] = true;
            
            // Find path tiles
            FindNextPathTile(start, end, pathPoints, visited);
            
            // Convert path points to world positions
            foreach (var point in pathPoints)
            {
                if (point != start) // Skip start since we already added it
                {
                    enemyPath.Add(GetTileCenter(point.X, point.Y));
                }
            }
            
            // Add the end point if it's not already in the path
            if (!pathPoints.Contains(end))
            {
                enemyPath.Add(GetTileCenter(end.X, end.Y));
            }
        }
        
        private bool FindNextPathTile(Point current, Point end, List<Point> path, bool[,] visited)
        {
            // If we reached the end, we're done
            if (current == end)
                return true;
                
            // Check in all four directions (up, right, down, left)
            Point[] directions = new Point[4]
            {
                new Point(0, -1), // Up
                new Point(1, 0),  // Right
                new Point(0, 1),  // Down
                new Point(-1, 0)  // Left
            };
            
            foreach (var dir in directions)
            {
                Point next = new Point(current.X + dir.X, current.Y + dir.Y);
                
                // Check if in bounds
                if (next.X < 0 || next.X >= Width || next.Y < 0 || next.Y >= Height)
                    continue;
                    
                // Check if it's a path tile and not visited
                if ((mapData[next.X, next.Y] == TileType.Path || mapData[next.X, next.Y] == TileType.End) 
                    && !visited[next.X, next.Y])
                {
                    // Mark as visited
                    visited[next.X, next.Y] = true;
                    
                    // Add to path
                    path.Add(next);
                    
                    // Recurse to find next tile
                    if (FindNextPathTile(next, end, path, visited))
                        return true;
                        
                    // If no path from here, backtrack
                    path.RemoveAt(path.Count - 1);
                }
            }
            
            return false;
        }
        
        public List<Vector2> GetEnemyPath()
        {
            return enemyPath;
        }
        
        // Create a simple default map - this can be replaced with loading maps from files later
        public void CreateDefaultMap()
        {
            // Set the border and some internal areas as non-buildable
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    // Set most tiles as buildable by default
                    mapData[x, y] = TileType.Buildable;
                    
                    // Make borders non-buildable
                    if (x == 0 || x == Width - 1 || y == 0 || y == Height - 1)
                    {
                        mapData[x, y] = TileType.NonBuildable;
                    }
                }
            }
            
            // Define a path from left to right
            int pathY = Height / 2;
            
            // Start point on left
            mapData[0, pathY] = TileType.Start;
            
            // Path going across with a few turns
            for (int x = 1; x < Width - 1; x++)
            {
                // Make a simple zigzag path
                if (x % 8 == 0 && pathY > 2)
                    pathY -= 2;
                else if (x % 4 == 0 && pathY < Height - 3)
                    pathY += 2;
                    
                mapData[x, pathY] = TileType.Path;
            }
            
            // End point on right
            mapData[Width - 1, pathY] = TileType.End;
            
            // Generate the path for enemies to follow
            GeneratePath();
        }

        // Convert world coordinates to grid coordinates
        public Vector2 WorldToGrid(Vector2 worldPosition)
        {
            return new Vector2(
                (int)(worldPosition.X / TileSize),
                (int)(worldPosition.Y / TileSize)
            );
        }

        // Convert grid coordinates to world coordinates (centered in tile)
        public Vector2 GridToWorld(Vector2 gridPosition)
        {
            return new Vector2(
                gridPosition.X * TileSize + TileSize / 2,
                gridPosition.Y * TileSize + TileSize / 2
            );
        }

        // Check if a tower can be placed at the given grid position
        public bool IsTowerPlacementValid(Vector2 gridPosition)
        {
            int x = (int)gridPosition.X;
            int y = (int)gridPosition.Y;
            
            // Check if in bounds
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return false;
                
            // Check if tile is buildable
            return mapData[x, y] == TileType.Buildable;
        }
        
        // Mark a grid cell as occupied or free for tower placement
        public void SetTowerPlacement(Vector2 gridPosition, bool isOccupied)
        {
            int x = (int)gridPosition.X;
            int y = (int)gridPosition.Y;
            
            // Check if in bounds
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return;
                
            // Set tile type based on tower placement
            mapData[x, y] = isOccupied ? TileType.NonBuildable : TileType.Buildable;
        }
    }
}