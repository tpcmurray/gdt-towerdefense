using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using CyberDefense.Entities;
using CyberDefense.Map;
using CyberDefense.Managers;
using CyberDefense.Networking;

namespace CyberDefense
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Game state
        private enum GameState
        {
            MainMenu,
            HostNameInput,
            ServerSelection,
            Connecting,
            Playing,
            GameOver
        }

        private GameState currentState = GameState.MainMenu;
        private KeyboardState previousKeyboardState;
        
        // Add tracking for previous mouse state
        private MouseState previousMouseState;
        
        // Core game components
        private GameMap gameMap;
        private WaveManager waveManager;
        private ResourceManager resourceManager;
        private NetworkManager networkManager;
        
        // Game entities
        private List<Tower> towers = new List<Tower>();
        private List<Projectile> projectiles = new List<Projectile>();
        private HomeBase playerBase;
        
        // Tower placement
        private bool isPlacingTower = false;
        private Tower placementTower;
        
        // Multiplayer
        private bool isMultiplayer = true;
        private bool isHost = true;
        
        // Debug
        private bool showDebugInfo = true;
        private SpriteFont debugFont;
        
        // Tower properties (default values for MVP)
        private float towerRange = 150f;
        private int towerDamage = 25;
        private float towerFireRate = 1.0f;
        private int towerCost = 50;

        // Placeholder textures
        private Texture2D towerTexture;
        private Texture2D enemyTexture;
        private Texture2D projectileTexture;
        private Texture2D baseTexture;

        // Host name input variables
        private string hostNameInput = "Host";
        private int hostNameCursorPosition = 4;
        private readonly int maxHostNameLength = 15;
        
        // Server selection variables
        private List<NetworkManager.DiscoveredHost> availableHosts = new List<NetworkManager.DiscoveredHost>();
        private int selectedHostIndex = -1;
        private bool isHostDiscoveryActive = false;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            
            // Set window size
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
        }

        protected override void Initialize()
        {
            // Initialize game components
            gameMap = new GameMap(40, 22, 32);  // 40x22 tiles of 32px each
            gameMap.CreateDefaultMap();
            
            waveManager = new WaveManager(10, 15.0f);  // 10 waves with 15 seconds between
            
            resourceManager = new ResourceManager(200);  // Start with 200 money
            
            networkManager = new NetworkManager();
            
            // Create player base at the end of the path
            var enemyPath = gameMap.GetEnemyPath();
            if (enemyPath != null && enemyPath.Count > 0)
            {
                Vector2 basePosition = enemyPath[enemyPath.Count - 1];
                playerBase = new HomeBase(basePosition, 100);  // 100 health
                
                // Subscribe to base destruction
                playerBase.OnDestroyed += () => {
                    currentState = GameState.GameOver;
                };
            }
            
            // Set up wave manager with path
            waveManager.SetEnemyPath(gameMap.GetEnemyPath());
            
            // Set up event handlers
            SetUpEventHandlers();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load our custom font
            debugFont = Content.Load<SpriteFont>("Fonts/GameFont");
            
            // Load sprite assets
            towerTexture = Content.Load<Texture2D>("Sprites/tower");
            enemyTexture = Content.Load<Texture2D>("Sprites/enemy");
            projectileTexture = Content.Load<Texture2D>("Sprites/projectile");
            
            // Create placeholder textures for other entities that don't have sprites yet
            CreatePlaceholderTextures();
            
            // Pass the enemy texture to the wave manager
            waveManager.SetEnemyTexture(enemyTexture);
            
            // Set the textures for our entities
            if (playerBase != null)
            {
                var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (textureField != null)
                {
                    textureField.SetValue(playerBase, baseTexture);
                }
            }
        }

        protected override void Update(GameTime gameTime)
        {
            // Get current keyboard and mouse state
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            
            // Update network manager in all states if we're in multiplayer mode
            if (isMultiplayer && networkManager != null)
            {
                networkManager.Update(gameTime);
            }
            
            // Process input based on current game state
            switch (currentState)
            {
                case GameState.MainMenu:
                    UpdateMainMenu(keyboardState, mouseState);
                    break;
                    
                case GameState.HostNameInput:
                    UpdateHostNameInput(keyboardState, mouseState);
                    break;
                    
                case GameState.ServerSelection:
                    UpdateServerSelection(gameTime, keyboardState, mouseState);
                    break;
                    
                case GameState.Connecting:
                    UpdateConnecting();
                    break;
                    
                case GameState.Playing:
                    UpdatePlaying(gameTime, keyboardState, mouseState);
                    break;
                    
                case GameState.GameOver:
                    UpdateGameOver(keyboardState);
                    break;
            }
            
            // Store keyboard and mouse state for next frame
            previousKeyboardState = keyboardState;
            previousMouseState = mouseState;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();
            
            // Always draw a debug outline to verify the rendering system works
            if (towerTexture != null)
            {
                // Draw frame border
                _spriteBatch.Draw(towerTexture, new Rectangle(0, 0, 1280, 4), Color.Green);
                _spriteBatch.Draw(towerTexture, new Rectangle(0, 716, 1280, 4), Color.Green);
                _spriteBatch.Draw(towerTexture, new Rectangle(0, 0, 4, 720), Color.Green);
                _spriteBatch.Draw(towerTexture, new Rectangle(1276, 0, 4, 720), Color.Green);
                
                // Draw state indicator box
                _spriteBatch.Draw(towerTexture, new Rectangle(10, 10, 200, 30), Color.DarkBlue);
                
                // Draw color-coded state blocks
                Color stateColor;
                switch (currentState)
                {
                    case GameState.MainMenu: stateColor = Color.Yellow; break;
                    case GameState.Connecting: stateColor = Color.Cyan; break; 
                    case GameState.Playing: stateColor = Color.Lime; break;
                    case GameState.GameOver: stateColor = Color.Red; break;
                    default: stateColor = Color.White; break;
                }
                _spriteBatch.Draw(towerTexture, new Rectangle(15, 15, 20, 20), stateColor);
            }
            
            // Draw based on game state
            switch (currentState)
            {
                case GameState.MainMenu:
                    DrawMainMenu();
                    break;
                    
                case GameState.HostNameInput:
                    DrawHostNameInput();
                    break;
                    
                case GameState.ServerSelection:
                    DrawServerSelection();
                    break;
                    
                case GameState.Connecting:
                    DrawConnecting();
                    break;
                    
                case GameState.Playing:
                    DrawPlaying();
                    break;
                    
                case GameState.GameOver:
                    DrawGameOver();
                    break;
            }
            
            _spriteBatch.End();

            base.Draw(gameTime);
        }
        
        #region Game State Updates
        
        private void UpdateMainMenu(KeyboardState keyboardState, MouseState mouseState)
        {
            // For MVP, just press H for host or C for client
            if (keyboardState.IsKeyDown(Keys.H) && !previousKeyboardState.IsKeyDown(Keys.H))
            {
                // Start as host
                isHost = true;
                StartMultiplayerGame();
            }
            else if (keyboardState.IsKeyDown(Keys.C) && !previousKeyboardState.IsKeyDown(Keys.C))
            {
                // Start as client
                isHost = false;
                StartMultiplayerGame();
            }
            else if (keyboardState.IsKeyDown(Keys.S) && !previousKeyboardState.IsKeyDown(Keys.S))
            {
                // Start single player
                isMultiplayer = false;
                StartSinglePlayerGame();
            }
        }
        
        private void UpdateHostNameInput(KeyboardState keyboardState, MouseState mouseState)
        {
            // Handle text input for host name
            foreach (var key in keyboardState.GetPressedKeys())
            {
                if (previousKeyboardState.IsKeyUp(key))
                {
                    if (key == Keys.Back && hostNameInput.Length > 0)
                    {
                        // Handle backspace
                        if (hostNameInput.Length > 0)
                        {
                            hostNameInput = hostNameInput.Substring(0, hostNameInput.Length - 1);
                            hostNameCursorPosition = Math.Max(0, hostNameCursorPosition - 1);
                        }
                    }
                    else if (key == Keys.Enter)
                    {
                        // Submit host name and start server
                        StartServer();
                    }
                    else if (key == Keys.Escape)
                    {
                        // Cancel and return to main menu
                        currentState = GameState.MainMenu;
                    }
                    else
                    {
                        // Get character for pressed key if it's a valid character
                        char? c = GetCharFromKey(key, keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift));
                        if (c.HasValue && hostNameInput.Length < maxHostNameLength)
                        {
                            // Add character to host name
                            hostNameInput += c.Value;
                            hostNameCursorPosition++;
                        }
                    }
                }
            }
        }

        private void UpdateServerSelection(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            // Handle up/down arrow keys to select from the list
            if (keyboardState.IsKeyDown(Keys.Down) && !previousKeyboardState.IsKeyDown(Keys.Down))
            {
                selectedHostIndex = Math.Min(selectedHostIndex + 1, availableHosts.Count - 1);
            }
            else if (keyboardState.IsKeyDown(Keys.Up) && !previousKeyboardState.IsKeyDown(Keys.Up))
            {
                selectedHostIndex = Math.Max(selectedHostIndex - 1, 0);
            }
            else if (keyboardState.IsKeyDown(Keys.Enter) && !previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                // Connect to the selected host
                if (selectedHostIndex >= 0 && selectedHostIndex < availableHosts.Count)
                {
                    ConnectToSelectedHost();
                }
            }
            else if (keyboardState.IsKeyDown(Keys.Escape) && !previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                // Cancel and return to main menu
                StopHostDiscovery();
                currentState = GameState.MainMenu;
            }
            else if (keyboardState.IsKeyDown(Keys.R) && !previousKeyboardState.IsKeyDown(Keys.R))
            {
                // Refresh the host list
                RefreshHostList();
            }
            
            // Update network manager to receive broadcasts
            networkManager.Update(gameTime);
        }
        
        private void UpdateConnecting()
        {
            // Poll for network events during connection phase
            networkManager.Update(new GameTime());
            
            // Add a timeout/cancel option with Escape key
            KeyboardState keyboardState = Keyboard.GetState();
            if (keyboardState.IsKeyDown(Keys.Escape) && !previousKeyboardState.IsKeyDown(Keys.Escape))
            {
                // Cancel connection attempt
                networkManager.Disconnect();
                currentState = GameState.MainMenu;
                return;
            }
            
            // Check if connection was established
            if (networkManager.IsConnected)
            {
                // Move to playing state
                currentState = GameState.Playing;
                
                Console.WriteLine($"Connected! Player ID: {networkManager.PlayerId}, Server: {networkManager.IsServer}");
                
                // If client, set wave manager to not be host
                if (!networkManager.IsServer)
                {
                    waveManager.IsHost = false;
                }
            }
        }
        
        private void UpdatePlaying(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            // Update network if in multiplayer
            if (isMultiplayer)
            {
                networkManager.Update(gameTime);
            }
            
            // Update resource manager
            resourceManager.Update(gameTime);
            
            // Update wave manager
            waveManager.Update(gameTime, playerBase);
            
            // Handle tower placement
            UpdateTowerPlacement(keyboardState, mouseState);
            
            // Update all towers
            foreach (var tower in towers)
            {
                tower.Update(gameTime);
                
                // Check if tower can fire
                if (waveManager.GetActiveEnemies().Count > 0)
                {
                    var enemiesInRange = tower.FindTargetsInRange(waveManager.GetActiveEnemies());
                    if (enemiesInRange.Count > 0)
                    {
                        var target = tower.SelectTarget(enemiesInRange);
                        var projectile = tower.FireAtTarget(target);
                        
                        if (projectile != null)
                        {
                            // Set the projectile texture
                            var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (textureField != null)
                            {
                                textureField.SetValue(projectile, projectileTexture);
                            }
                            
                            projectiles.Add(projectile);
                        }
                    }
                }
            }
            
            // Update projectiles
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                projectiles[i].Update(gameTime);
                
                // Remove inactive projectiles
                if (!projectiles[i].IsActive)
                {
                    projectiles.RemoveAt(i);
                }
            }
            
            // Handle wave control hotkeys
            if (isHost || !isMultiplayer)
            {
                if (keyboardState.IsKeyDown(Keys.N) && !previousKeyboardState.IsKeyDown(Keys.N))
                {
                    // Force start next wave
                    waveManager.ForceStartNextWave();
                }
            }
            
            // Debug toggle
            if (keyboardState.IsKeyDown(Keys.F3) && !previousKeyboardState.IsKeyDown(Keys.F3))
            {
                showDebugInfo = !showDebugInfo;
            }
        }
        
        private void UpdateGameOver(KeyboardState keyboardState)
        {
            // Press R to restart
            if (keyboardState.IsKeyDown(Keys.R) && !previousKeyboardState.IsKeyDown(Keys.R))
            {
                RestartGame();
            }
            
            // Press M to return to main menu
            if (keyboardState.IsKeyDown(Keys.M) && !previousKeyboardState.IsKeyDown(Keys.M))
            {
                ReturnToMainMenu();
            }
        }
        
        #endregion
        
        #region Game State Drawing
        
        private void DrawMainMenu()
        {
            // Debug info - check if textures are actually created
            if (towerTexture == null)
            {
                // Create a white pixel texture on the fly for emergency rendering
                Texture2D pixel = new Texture2D(GraphicsDevice, 1, 1);
                pixel.SetData(new[] { Color.White });
                
                // Draw some visible elements using this pixel
                _spriteBatch.Draw(pixel, new Rectangle(50, 50, 200, 50), Color.Red);
                _spriteBatch.Draw(pixel, new Rectangle(50, 120, 200, 50), Color.Yellow);
                _spriteBatch.Draw(pixel, new Rectangle(50, 190, 200, 50), Color.Blue);
                return;
            }
            
            // Draw directly using colored rectangles since we don't have a font yet
            // Title bar
            _spriteBatch.Draw(towerTexture, new Rectangle(450, 200, 380, 60), Color.DarkBlue);
            
            // Menu buttons
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 300, 320, 40), Color.Blue); // Host button
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 350, 320, 40), Color.Blue); // Client button
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 400, 320, 40), Color.Blue); // Single player button
            
            // Use different colors to make it obvious which button is which
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 300, 40, 40), Color.Cyan); // H button
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 350, 40, 40), Color.Green); // C button
            _spriteBatch.Draw(towerTexture, new Rectangle(480, 400, 40, 40), Color.Yellow); // S button
            
            // Draw menu labels using shapes
            _spriteBatch.Draw(towerTexture, new Rectangle(530, 310, 20, 20), Color.White); // H label
            _spriteBatch.Draw(towerTexture, new Rectangle(530, 360, 20, 20), Color.White); // C label
            _spriteBatch.Draw(towerTexture, new Rectangle(530, 410, 20, 20), Color.White); // S label
            
            // Use font if available (but we know it's not in the current implementation)
            if (debugFont != null)
            {
                _spriteBatch.DrawString(debugFont, "CYBER DEFENSE", new Vector2(640, 200), Color.Cyan, 0, Vector2.Zero, 2.0f, SpriteEffects.None, 0);
                _spriteBatch.DrawString(debugFont, "Press 'H' to Host Game", new Vector2(640, 300), Color.White);
                _spriteBatch.DrawString(debugFont, "Press 'C' to Connect as Client", new Vector2(640, 340), Color.White);
                _spriteBatch.DrawString(debugFont, "Press 'S' for Single Player", new Vector2(640, 380), Color.White);
            }
        }
        
        private void DrawHostNameInput()
        {
            // Draw background
            _spriteBatch.Draw(towerTexture, new Rectangle(0, 0, 1280, 720), new Color(0, 0, 50, 150));
            
            // Draw input window
            _spriteBatch.Draw(towerTexture, new Rectangle(440, 260, 400, 200), new Color(50, 50, 100));
            _spriteBatch.Draw(towerTexture, new Rectangle(450, 270, 380, 180), new Color(30, 30, 60));
            
            // Show instruction text
            if (debugFont != null)
            {
                // Title
                _spriteBatch.DrawString(
                    debugFont,
                    "ENTER HOST NAME",
                    new Vector2(640, 290),
                    Color.Cyan,
                    0f,
                    new Vector2(debugFont.MeasureString("ENTER HOST NAME").X / 2, 0),
                    1.2f,
                    SpriteEffects.None,
                    0f
                );
                
                // Input box background
                _spriteBatch.Draw(towerTexture, new Rectangle(490, 330, 300, 40), Color.Black);
                
                // Input text
                _spriteBatch.DrawString(
                    debugFont,
                    hostNameInput + (DateTime.Now.Millisecond < 500 ? "|" : ""), // Blinking cursor
                    new Vector2(640, 345),
                    Color.White,
                    0f,
                    new Vector2(debugFont.MeasureString(hostNameInput).X / 2, 0),
                    1.0f,
                    SpriteEffects.None,
                    0f
                );
                
                // Instructions
                _spriteBatch.DrawString(
                    debugFont,
                    "Press ENTER to start server",
                    new Vector2(640, 390),
                    Color.White,
                    0f,
                    new Vector2(debugFont.MeasureString("Press ENTER to start server").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
                
                _spriteBatch.DrawString(
                    debugFont,
                    "Press ESC to cancel",
                    new Vector2(640, 420),
                    Color.Gray,
                    0f,
                    new Vector2(debugFont.MeasureString("Press ESC to cancel").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        private void DrawServerSelection()
        {
            // Draw background
            _spriteBatch.Draw(towerTexture, new Rectangle(0, 0, 1280, 720), new Color(0, 0, 50, 150));
            
            // Draw selection window
            _spriteBatch.Draw(towerTexture, new Rectangle(390, 200, 500, 400), new Color(50, 50, 100));
            _spriteBatch.Draw(towerTexture, new Rectangle(400, 210, 480, 380), new Color(30, 30, 60));
            
            if (debugFont != null)
            {
                // Title
                _spriteBatch.DrawString(
                    debugFont,
                    "SELECT SERVER",
                    new Vector2(640, 230),
                    Color.Cyan,
                    0f,
                    new Vector2(debugFont.MeasureString("SELECT SERVER").X / 2, 0),
                    1.2f,
                    SpriteEffects.None,
                    0f
                );
                
                // Show scanning message if no hosts found
                if (availableHosts.Count == 0)
                {
                    string dots = new string('.', (int)(DateTime.Now.Second % 3) + 1);
                    _spriteBatch.DrawString(
                        debugFont,
                        $"Scanning for hosts{dots}",
                        new Vector2(640, 300),
                        Color.Yellow,
                        0f,
                        new Vector2(debugFont.MeasureString($"Scanning for hosts...").X / 2, 0),
                        1.0f,
                        SpriteEffects.None,
                        0f
                    );
                }
                else
                {
                    // List available hosts
                    int yPos = 280;
                    for (int i = 0; i < availableHosts.Count; i++)
                    {
                        var host = availableHosts[i];
                        Color textColor = (i == selectedHostIndex) ? Color.Lime : Color.White;
                        
                        // Selection indicator
                        if (i == selectedHostIndex)
                        {
                            _spriteBatch.Draw(towerTexture, new Rectangle(420, yPos, 440, 30), new Color(80, 80, 150));
                        }
                        
                        // Host name and IP
                        _spriteBatch.DrawString(
                            debugFont,
                            $"{host.Name} ({host.EndPoint.Address})",
                            new Vector2(440, yPos + 5),
                            textColor,
                            0f,
                            Vector2.Zero,
                            0.9f,
                            SpriteEffects.None,
                            0f
                        );
                        
                        yPos += 40;
                    }
                }
                
                // Instructions
                _spriteBatch.DrawString(
                    debugFont,
                    "Up/Down arrows to select, ENTER to connect",
                    new Vector2(640, 500),
                    Color.White,
                    0f,
                    new Vector2(debugFont.MeasureString("Up/Down arrows to select, ENTER to connect").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
                
                _spriteBatch.DrawString(
                    debugFont,
                    "Press R to refresh servers",
                    new Vector2(640, 530),
                    Color.White,
                    0f,
                    new Vector2(debugFont.MeasureString("Press R to refresh servers").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
                
                _spriteBatch.DrawString(
                    debugFont,
                    "Press ESC to cancel",
                    new Vector2(640, 560),
                    Color.Gray,
                    0f,
                    new Vector2(debugFont.MeasureString("Press ESC to cancel").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        private void DrawConnecting()
        {
            // Draw connecting background
            _spriteBatch.Draw(towerTexture, new Rectangle(0, 0, 1280, 720), new Color(0, 0, 50, 150));
            
            // Draw connection status window
            _spriteBatch.Draw(towerTexture, new Rectangle(440, 260, 400, 200), new Color(50, 50, 100));
            _spriteBatch.Draw(towerTexture, new Rectangle(450, 270, 380, 180), new Color(30, 30, 60));
            
            // Show connecting status text
            if (debugFont != null)
            {
                // Status title
                _spriteBatch.DrawString(
                    debugFont,
                    isHost ? "STARTING SERVER" : "CONNECTING TO SERVER",
                    new Vector2(640, 300),
                    Color.Cyan,
                    0f,
                    new Vector2(debugFont.MeasureString(isHost ? "STARTING SERVER" : "CONNECTING TO SERVER").X / 2, 0),
                    1.0f,
                    SpriteEffects.None,
                    0f
                );
                
                // IP address info
                string ipInfo = isHost ? "Hosting on 127.0.0.1:9050" : "Connecting to 127.0.0.1:9050";
                _spriteBatch.DrawString(
                    debugFont,
                    ipInfo,
                    new Vector2(640, 330),
                    Color.White,
                    0f,
                    new Vector2(debugFont.MeasureString(ipInfo).X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
                
                // Connection status animation
                string dots = new string('.', (int)(DateTime.Now.Second % 3) + 1);
                _spriteBatch.DrawString(
                    debugFont,
                    isHost ? $"Starting{dots}" : $"Connecting{dots}",
                    new Vector2(640, 370),
                    Color.Yellow,
                    0f,
                    new Vector2(debugFont.MeasureString($"Connecting...").X / 2, 0),
                    1.0f,
                    SpriteEffects.None,
                    0f
                );
                
                // Escape to cancel
                _spriteBatch.DrawString(
                    debugFont,
                    "Press ESC to cancel",
                    new Vector2(640, 420),
                    Color.Gray,
                    0f,
                    new Vector2(debugFont.MeasureString("Press ESC to cancel").X / 2, 0),
                    0.8f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        
        private void DrawPlaying()
        {
            // Draw map
            gameMap.Draw(_spriteBatch);
            
            // Draw home base
            playerBase?.Draw(_spriteBatch);
            
            // Draw towers
            foreach (var tower in towers)
            {
                tower.Draw(_spriteBatch);
            }
            
            // Draw enemies
            waveManager.Draw(_spriteBatch);
            
            // Draw projectiles
            foreach (var projectile in projectiles)
            {
                projectile.Draw(_spriteBatch);
            }
            
            // Draw tower placement preview
            if (isPlacingTower && placementTower != null)
            {
                // Draw transparent version of tower
                placementTower.Draw(_spriteBatch);
            }
            
            // Draw UI
            DrawUI();
            
            // Draw debug info
            if (showDebugInfo && debugFont != null)
            {
                DrawDebugInfo();
            }
        }
        
        private void DrawGameOver()
        {
            // Draw game over screen
            if (debugFont != null)
            {
                _spriteBatch.DrawString(debugFont, "GAME OVER", new Vector2(640, 320), Color.Red, 0, Vector2.Zero, 2.0f, SpriteEffects.None, 0);
                _spriteBatch.DrawString(debugFont, "Press 'R' to Restart", new Vector2(640, 380), Color.White);
                _spriteBatch.DrawString(debugFont, "Press 'M' for Main Menu", new Vector2(640, 420), Color.White);
            }
        }
        
        private void DrawUI()
        {
            // Draw UI elements like resources, wave info, etc.
            if (debugFont != null)
            {
                _spriteBatch.DrawString(debugFont, $"Money: {resourceManager.Money}", new Vector2(20, 20), Color.Yellow);
                _spriteBatch.DrawString(debugFont, $"Wave: {waveManager.CurrentWave}/{waveManager.TotalWaves}", new Vector2(20, 50), Color.White);
                _spriteBatch.DrawString(debugFont, $"Base Health: {playerBase?.CurrentHealth ?? 0}", new Vector2(20, 80), Color.Green);
                
                if (isMultiplayer)
                {
                    string roleText = networkManager.IsServer ? "HOST" : "CLIENT";
                    _spriteBatch.DrawString(debugFont, $"Role: {roleText} (ID: {networkManager.PlayerId})", new Vector2(20, 110), Color.Cyan);
                }
                
                // Tower placement instructions
                _spriteBatch.DrawString(debugFont, "Press 'T' to place tower (50 money)", new Vector2(20, 680), Color.White);
                _spriteBatch.DrawString(debugFont, "Press 'N' to start next wave", new Vector2(920, 680), Color.White);
            }
        }
        
        private void DrawDebugInfo()
        {
            // Draw debug information
            if (debugFont != null)
            {
                // Use a hardcoded fps value until we can calculate it properly
                int fps = 60;
                _spriteBatch.DrawString(debugFont, $"FPS: {fps}", new Vector2(1100, 20), Color.Lime);
                _spriteBatch.DrawString(debugFont, $"Active Enemies: {waveManager.GetActiveEnemies().Count}", new Vector2(1100, 50), Color.White);
                _spriteBatch.DrawString(debugFont, $"Towers: {towers.Count}", new Vector2(1100, 80), Color.White);
                _spriteBatch.DrawString(debugFont, $"Projectiles: {projectiles.Count}", new Vector2(1100, 110), Color.White);
            }
        }
        
        #endregion
        
        #region Game Logic Methods
        
        private void SetUpEventHandlers()
        {
            // Wave manager events
            waveManager.OnEnemyDefeated += (enemy, reachedEnd) => {
                if (!reachedEnd)
                {
                    // Add money for killing an enemy
                    int reward = resourceManager.CalculateEnemyReward(waveManager.CurrentWave);
                    resourceManager.AddMoney(reward);
                    
                    // Sync money in multiplayer
                    if (isMultiplayer)
                    {
                        networkManager.SyncMoney(resourceManager.Money);
                    }
                }
            };
            
            waveManager.OnWaveCompleted += (waveNumber) => {
                // Add bonus money for completing a wave
                int reward = resourceManager.CalculateWaveCompletionReward(waveNumber);
                resourceManager.AddMoney(reward);
                
                // Sync money in multiplayer
                if (isMultiplayer)
                {
                    networkManager.SyncMoney(resourceManager.Money);
                }
            };
            
            // Network events
            networkManager.OnTowerPlaced += (playerId, position) => {
                // Only process if it's not our own tower (we already placed it)
                if (playerId != networkManager.PlayerId)
                {
                    PlaceTowerAt(position);
                }
            };
            
            networkManager.OnMoneyChanged += (playerId, amount) => {
                // Only process if it's from another player
                if (playerId != networkManager.PlayerId)
                {
                    resourceManager.SyncMoney(amount);
                }
            };
            
            networkManager.OnWaveStarted += (hostId, waveNumber) => {
                // Client-only: Sync wave state from host
                if (!networkManager.IsServer)
                {
                    // Implement wave syncing logic here
                }
            };
            
            networkManager.OnEnemySpawned += (enemy) => {
                // Client-only: Add enemy spawned by host
                if (!networkManager.IsServer)
                {
                    waveManager.AddNetworkSpawnedEnemy(enemy);
                }
            };
        }
        
        private void StartMultiplayerGame()
        {
            if (isHost)
            {
                // Start as host - need hostname input
                currentState = GameState.HostNameInput;
            }
            else
            {
                // Start as client - need to find/select server
                currentState = GameState.ServerSelection;
                StartHostDiscovery();
            }
        }

        private void StartSinglePlayerGame()
        {
            // Initialize game for single player
            waveManager.Reset();
            resourceManager.Reset();
            currentState = GameState.Playing;
        }

        private void StartServer()
        {
            // Initialize the network as a server
            networkManager.Initialize();
            networkManager.StartServer(hostNameInput);
            
            // Change state to connecting (which shows "Starting server")
            currentState = GameState.Connecting;
            
            // Reset game
            waveManager.Reset();
            resourceManager.Reset();
        }

        private void StartHostDiscovery()
        {
            networkManager.Initialize();
            networkManager.StartHostDiscovery();
            isHostDiscoveryActive = true;
            
            // Add event handlers for discovered hosts
            networkManager.OnHostDiscovered += (host) => {
                availableHosts.Add(host);
                selectedHostIndex = 0;
            };
            
            networkManager.OnHostUpdated += (host) => {
                // Host is already in the list and was updated automatically
            };
            
            networkManager.OnHostLost += (hostName) => {
                availableHosts.RemoveAll(h => h.Name == hostName);
                if (selectedHostIndex >= availableHosts.Count)
                    selectedHostIndex = availableHosts.Count - 1;
            };
        }

        private void StopHostDiscovery()
        {
            isHostDiscoveryActive = false;
            networkManager.StopHostDiscovery();
        }

        private void RefreshHostList()
        {
            // Clear the list and restart discovery
            availableHosts.Clear();
            selectedHostIndex = -1;
            
            // Restart discovery
            StopHostDiscovery();
            StartHostDiscovery();
        }

        private void ConnectToSelectedHost()
        {
            if (selectedHostIndex >= 0 && selectedHostIndex < availableHosts.Count)
            {
                var selectedHost = availableHosts[selectedHostIndex];
                
                // Stop discovery while connecting
                StopHostDiscovery();
                
                // Connect to the selected host
                networkManager.ConnectToHost(selectedHost);
                
                // Change state to connecting
                currentState = GameState.Connecting;
            }
        }

        private void RestartGame()
        {
            // Reset game state
            towers.Clear();
            projectiles.Clear();
            waveManager.Reset();
            resourceManager.Reset();
            
            // Reset player base health
            if (playerBase != null)
            {
                playerBase.ResetHealth();
            }
            
            // Change state to playing
            currentState = GameState.Playing;
        }

        private void ReturnToMainMenu()
        {
            // Disconnect from any network session
            if (isMultiplayer && networkManager != null)
            {
                networkManager.Disconnect();
            }
            
            // Reset game state
            towers.Clear();
            projectiles.Clear();
            waveManager.Reset();
            resourceManager.Reset();
            
            // Reset player base health
            if (playerBase != null)
            {
                playerBase.ResetHealth();
            }
            
            // Change state to main menu
            currentState = GameState.MainMenu;
        }

        private void UpdateTowerPlacement(KeyboardState keyboardState, MouseState mouseState)
        {
            // Toggle tower placement mode with 'T' key
            if (keyboardState.IsKeyDown(Keys.T) && !previousKeyboardState.IsKeyDown(Keys.T))
            {
                // Toggle placement mode
                isPlacingTower = !isPlacingTower;
                
                if (isPlacingTower)
                {
                    // Create a new tower for placement preview
                    placementTower = new Tower(Vector2.Zero, towerRange, towerDamage, towerFireRate);
                    
                    // Set the tower texture
                    var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (textureField != null)
                    {
                        textureField.SetValue(placementTower, towerTexture);
                    }
                }
                else
                {
                    placementTower = null;
                }
            }
            
            // Handle tower placement
            if (isPlacingTower && placementTower != null)
            {
                // Convert mouse position to grid position
                Vector2 mousePos = new Vector2(mouseState.X, mouseState.Y);
                Vector2 gridPos = gameMap.WorldToGrid(mousePos);
                Vector2 snapPos = gameMap.GridToWorld(gridPos);
                
                // Update placement tower position
                placementTower.Position = snapPos;
                
                // Check if position is valid
                bool validPosition = gameMap.IsTowerPlacementValid(gridPos);
                
                // Update tower color
                var colorField = typeof(Entity).GetField("color", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (colorField != null)
                {
                    colorField.SetValue(placementTower, validPosition ? new Color(255, 255, 255, 150) : new Color(255, 0, 0, 150));
                }
                
                // Place tower on left click
                if (mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
                {
                    if (validPosition && resourceManager.Money >= towerCost)
                    {
                        // Deduct cost
                        resourceManager.SpendMoney(towerCost);
                        
                        // Create new tower at position
                        Tower newTower = new Tower(snapPos, towerRange, towerDamage, towerFireRate);
                        
                        // Set the tower texture
                        var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (textureField != null)
                        {
                            textureField.SetValue(newTower, towerTexture);
                        }
                        
                        // Add to towers list
                        towers.Add(newTower);
                        
                        // Sync tower placement in multiplayer
                        if (isMultiplayer)
                        {
                            networkManager.SyncTowerPlacement(networkManager.PlayerId, snapPos, 0);
                        }
                        
                        // Mark tower position as occupied in the map
                        gameMap.SetTowerPlacement(gridPos, true);
                    }
                }
                
                // Cancel placement on right click
                if (mouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released)
                {
                    isPlacingTower = false;
                    placementTower = null;
                }
            }
        }

        private char? GetCharFromKey(Keys key, bool shift)
        {
            // Check for letter keys
            if (key >= Keys.A && key <= Keys.Z)
            {
                return shift ? (char)(key - Keys.A + 'A') : (char)(key - Keys.A + 'a');
            }
            
            // Check for number keys
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                if (shift)
                {
                    switch (key)
                    {
                        case Keys.D1: return '!';
                        case Keys.D2: return '@';
                        case Keys.D3: return '#';
                        case Keys.D4: return '$';
                        case Keys.D5: return '%';
                        case Keys.D6: return '^';
                        case Keys.D7: return '&';
                        case Keys.D8: return '*';
                        case Keys.D9: return '(';
                        case Keys.D0: return ')';
                    }
                }
                else
                {
                    return (char)(key - Keys.D0 + '0');
                }
            }
            
            // Check for numpad keys
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)(key - Keys.NumPad0 + '0');
            }
            
            // Check for special keys
            switch (key)
            {
                case Keys.Space: return ' ';
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.OemTilde: return shift ? '~' : '`';
                default: return null;
            }
        }

        private void CreatePlaceholderTextures()
        {
            // Create a placeholder texture for the base
            baseTexture = new Texture2D(GraphicsDevice, 32, 32);
            Color[] baseData = new Color[32 * 32];
            for (int i = 0; i < baseData.Length; i++)
            {
                // Create a unique pattern for the base
                int x = i % 32;
                int y = i / 32;
                
                // Create a castle-like icon
                if ((x == 0 || x == 31 || y == 0 || y == 31) ||
                    (x >= 8 && x <= 10 && y <= 15) || 
                    (x >= 21 && x <= 23 && y <= 15) ||
                    (y >= 8 && y <= 10 && x >= 10 && x <= 21) ||
                    (y == 0 && (x == 0 || x == 7 || x == 15 || x == 23 || x == 31)))
                {
                    baseData[i] = new Color(50, 100, 200); // Blue-ish castle
                }
                else
                {
                    baseData[i] = Color.Transparent;
                }
            }
            baseTexture.SetData(baseData);
        }

        private void PlaceTowerAt(Vector2 position)
        {
            // Convert world position to grid position
            Vector2 gridPos = new Vector2(
                (int)(position.X / gameMap.TileSize),
                (int)(position.Y / gameMap.TileSize)
            );
            
            // Create new tower at position
            Tower newTower = new Tower(position, towerRange, towerDamage, towerFireRate);
            
            // Set the tower texture
            var textureField = typeof(Entity).GetField("texture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (textureField != null)
            {
                textureField.SetValue(newTower, towerTexture);
            }
            
            // Add to towers list
            towers.Add(newTower);
            
            // Mark tower position as occupied in the map if the method exists
            try
            {
                // This is a dynamic call that will be fixed later
                var methodInfo = gameMap.GetType().GetMethod("SetTowerPlacement");
                if (methodInfo != null)
                {
                    methodInfo.Invoke(gameMap, new object[] { gridPos, true });
                }
            }
            catch
            {
                // If the method doesn't exist yet, we'll implement it later
            }
        }
        
        #endregion
    }
}
