using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Xna.Framework;
using CyberDefense.Entities;
using System.Text;

namespace CyberDefense.Networking
{
    public class NetworkManager : INetEventListener
    {
        // Network configuration
        private const int DEFAULT_PORT = 9050;
        private const string CONNECTION_KEY = "CyberDefenseGame";
        private const string DISCOVERY_KEY = "CyberDefenseDiscovery";
        
        // LiteNetLib components
        private NetManager netManager;
        private NetDataWriter dataWriter;
        
        // Connection state
        public bool IsServer { get; private set; }
        public bool IsConnected { get; private set; }
        public int PlayerId { get; private set; }
        public string HostName { get; private set; } = "Unknown Host";
        
        // Discovery state
        private bool isDiscoveryRunning = false;
        private System.Timers.Timer discoveryTimer;
        private List<DiscoveredHost> discoveredHosts = new List<DiscoveredHost>();
        
        // Server-specific
        private int nextClientId = 1;  // Server is always ID 0
        private Dictionary<int, NetPeer> clientPeers = new Dictionary<int, NetPeer>();
        
        // Events
        public event Action<int> OnPlayerConnected;    // playerId
        public event Action<int> OnPlayerDisconnected; // playerId
        
        // Game state sync events - higher level systems can subscribe to these
        public event Action<int, Vector2> OnTowerPlaced;   // playerId, position
        public event Action<int, int> OnMoneyChanged;      // playerId, amount
        public event Action<int, int> OnWaveStarted;       // hostId, waveNumber
        public event Action<Enemy> OnEnemySpawned;
        
        // Host discovery events
        public event Action<DiscoveredHost> OnHostDiscovered;
        public event Action<DiscoveredHost> OnHostUpdated;
        public event Action<string> OnHostLost;
        
        // Represents a discovered host on the network
        public class DiscoveredHost
        {
            public string Name { get; set; }
            public IPEndPoint EndPoint { get; set; }
            public DateTime LastSeen { get; set; }
            public int Port { get; set; }
            
            public override string ToString()
            {
                return $"{Name} ({EndPoint.Address})";
            }
        }
        
        public NetworkManager()
        {
            dataWriter = new NetDataWriter();
            netManager = new NetManager(this)
            {
                AutoRecycle = true
            };
        }
        
        public void Initialize()
        {
            netManager.Start();
        }
        
        public void StartServer(string hostName, int port = DEFAULT_PORT)
        {
            if (IsConnected)
                return;
            
            // Store the host name
            HostName = hostName;
            
            // Make sure we're initialized before starting server
            netManager.Start(port);
            
            // Configure to allow connections from any IP address
            netManager.IPv6Enabled = false; // Ensuring IPv4 compatibility
            netManager.BroadcastReceiveEnabled = true;
            
            // Enable connection request handling
            netManager.DisconnectTimeout = 30000; // 30 seconds
            netManager.UpdateTime = 15; // Update more frequently (15ms)
            
            IsServer = true;
            PlayerId = 0;  // Server is always ID 0
            IsConnected = true;
            
            Console.WriteLine($"=== SERVER STARTED on port {port} with key '{CONNECTION_KEY}' ===");
            Console.WriteLine($"=== Host Name: {HostName} ===");
            
            // Start broadcasting presence on the network
            StartHostDiscoveryBroadcast();
        }
        
        private void StartHostDiscoveryBroadcast()
        {
            // Create a timer to broadcast host info every 1 second
            discoveryTimer = new System.Timers.Timer(1000);
            discoveryTimer.Elapsed += (sender, e) => {
                if (IsServer && IsConnected)
                {
                    BroadcastHostInfo();
                }
            };
            discoveryTimer.AutoReset = true;
            discoveryTimer.Enabled = true;
            
            // Also broadcast immediately
            BroadcastHostInfo();
        }
        
        private void BroadcastHostInfo()
        {
            try
            {
                dataWriter.Reset();
                dataWriter.Put(DISCOVERY_KEY); // Header to identify our broadcasts
                dataWriter.Put(HostName);
                dataWriter.Put(GetLocalIPAddress());
                dataWriter.Put(netManager.LocalPort);
                
                // Broadcast to 255.255.255.255 (network broadcast address)
                netManager.SendBroadcast(dataWriter, DEFAULT_PORT);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting host info: {ex.Message}");
            }
        }
        
        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1"; // Fallback to localhost
        }
        
        public void StartHostDiscovery()
        {
            if (isDiscoveryRunning)
                return;
                
            // Make sure we have a working network manager
            if (netManager == null)
            {
                Initialize();
            }
            
            Console.WriteLine("Starting host discovery with enhanced debug configuration...");
            
            // Stop the manager if it's running to reconfigure it properly
            if (netManager.IsRunning)
            {
                netManager.Stop();
            }
            
            // Set critical broadcast-related settings
            netManager.UnconnectedMessagesEnabled = true;
            netManager.BroadcastReceiveEnabled = true;
            netManager.IPv6Enabled = false; // Ensure IPv4 compatibility
            netManager.UpdateTime = 15; // Increase update frequency for better responsiveness
                
            // Start NetManager in discover mode (IMPORTANT: use DEFAULT_PORT to match broadcast port)
            netManager.Start(DEFAULT_PORT);
            
            isDiscoveryRunning = true;
            discoveredHosts.Clear();
            
            // Create timer to clean up old hosts
            System.Timers.Timer cleanupTimer = new System.Timers.Timer(3000);
            cleanupTimer.Elapsed += (sender, e) => {
                CleanupStaleHosts();
            };
            cleanupTimer.AutoReset = true;
            cleanupTimer.Enabled = true;
            
            Console.WriteLine($"Host discovery started on port {DEFAULT_PORT}. Looking for broadcasts with key '{DISCOVERY_KEY}'...");
            Console.WriteLine("Waiting for broadcast messages from host...");
        }
        
        private void CleanupStaleHosts()
        {
            List<DiscoveredHost> staleHosts = new List<DiscoveredHost>();
            
            // Find hosts that haven't been seen for more than 5 seconds
            foreach (var host in discoveredHosts)
            {
                if ((DateTime.Now - host.LastSeen).TotalSeconds > 5)
                {
                    staleHosts.Add(host);
                }
            }
            
            // Remove stale hosts
            foreach (var host in staleHosts)
            {
                discoveredHosts.Remove(host);
                OnHostLost?.Invoke(host.Name);
                Console.WriteLine($"Lost host: {host.Name}");
            }
        }
        
        public void StopHostDiscovery()
        {
            isDiscoveryRunning = false;
            if (!IsConnected && !IsServer && netManager.IsRunning)
            {
                netManager.Stop();
            }
        }
        
        public List<DiscoveredHost> GetDiscoveredHosts()
        {
            return new List<DiscoveredHost>(discoveredHosts);
        }
        
        public void ConnectToHost(DiscoveredHost host)
        {
            ConnectToServer(host.EndPoint.Address.ToString(), host.Port);
        }
        
        public void ConnectToServer(string address, int port = DEFAULT_PORT)
        {
            if (IsConnected)
                return;
            
            // Make sure we're initialized
            netManager.Start();
            
            // Set network parameters to improve connection reliability
            netManager.DisconnectTimeout = 30000; // 30 seconds
            netManager.UpdateTime = 15; // Update more frequently (15ms)
            netManager.ReconnectDelay = 500; // Try reconnect after 500ms
            netManager.MaxConnectAttempts = 10; // Try to connect up to 10 times
            
            // Create and prepare key data with exact string matching
            NetDataWriter connectionData = new NetDataWriter();
            // Make sure we write the exact string that the server expects
            connectionData.Reset();
            connectionData.Put(CONNECTION_KEY);
            
            // Add debug output
            Console.WriteLine($"Attempting to connect to {address}:{port} with key '{CONNECTION_KEY}'");
            // Also output hex representation of the key for debugging
            Console.WriteLine($"Connection key hex: {BitConverter.ToString(Encoding.UTF8.GetBytes(CONNECTION_KEY))}");
            
            // Connect with a proper connection key in NetDataWriter
            netManager.Connect(address, port, connectionData);
            
            IsServer = false;
            IsConnected = false;  // Will be set to true on successful connection
            
            // Add timeout handling
            System.Threading.Tasks.Task.Run(async () => {
                await System.Threading.Tasks.Task.Delay(5000); // 5 second timeout
                if (!IsConnected) {
                    Console.WriteLine("Connection attempt timed out - check firewall settings");
                }
            });
        }
        
        public void Disconnect()
        {
            if (IsConnected)
            {
                netManager.Stop();
                IsConnected = false;
                
                if (IsServer)
                {
                    clientPeers.Clear();
                }
            }
        }
        
        public void Update(GameTime gameTime)
        {
            // Poll for network events
            netManager.PollEvents();
        }
        
        // Place tower on all clients
        public void SyncTowerPlacement(int playerId, Vector2 position, int towerId)
        {
            if (!IsConnected)
                return;
                
            dataWriter.Reset();
            dataWriter.Put((byte)PacketType.TowerPlaced);
            dataWriter.Put(playerId);
            dataWriter.Put(position.X);
            dataWriter.Put(position.Y);
            dataWriter.Put(towerId);
            
            if (IsServer)
            {
                // Server broadcasts to all clients
                foreach (var peer in clientPeers.Values)
                {
                    peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);
                }
            }
            else
            {
                // Client sends to server
                netManager.FirstPeer?.Send(dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }
        
        // Sync money amount (for shared resources)
        public void SyncMoney(int amount)
        {
            if (!IsConnected)
                return;
                
            dataWriter.Reset();
            dataWriter.Put((byte)PacketType.MoneyChanged);
            dataWriter.Put(PlayerId);
            dataWriter.Put(amount);
            
            if (IsServer)
            {
                // Server broadcasts to all clients
                foreach (var peer in clientPeers.Values)
                {
                    peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);
                }
            }
            else
            {
                // Client sends to server
                netManager.FirstPeer?.Send(dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }
        
        // Start a wave (from host)
        public void SyncWaveStart(int waveNumber)
        {
            if (!IsConnected || !IsServer)
                return;
                
            dataWriter.Reset();
            dataWriter.Put((byte)PacketType.WaveStarted);
            dataWriter.Put(waveNumber);
            
            // Server broadcasts to all clients
            foreach (var peer in clientPeers.Values)
            {
                peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }
        
        // Sync enemy spawn (from host)
        public void SyncEnemySpawn(Enemy enemy)
        {
            if (!IsConnected || !IsServer)
                return;
                
            dataWriter.Reset();
            dataWriter.Put((byte)PacketType.EnemySpawned);
            dataWriter.Put(enemy.Id);
            dataWriter.Put(enemy.Position.X);
            dataWriter.Put(enemy.Position.Y);
            dataWriter.Put(enemy.Health);
            dataWriter.Put(enemy.Speed);
            dataWriter.Put(enemy.Damage);
            
            // Server broadcasts to all clients
            foreach (var peer in clientPeers.Values)
            {
                peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);
            }
        }
        
        // INetEventListener implementation
        public void OnPeerConnected(NetPeer peer)
        {
            if (IsServer)
            {
                int clientId = nextClientId++;
                clientPeers.Add(clientId, peer);
                
                // Send client their ID
                dataWriter.Reset();
                dataWriter.Put((byte)PacketType.PlayerJoined);
                dataWriter.Put(clientId);
                peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);
                
                Console.WriteLine($"Client {clientId} connected");
                OnPlayerConnected?.Invoke(clientId);
            }
            else
            {
                IsConnected = true;
                Console.WriteLine("Connected to server");
            }
        }
        
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (IsServer)
            {
                // Find which client disconnected
                int disconnectedClientId = -1;
                foreach (var kvp in clientPeers)
                {
                    if (kvp.Value == peer)
                    {
                        disconnectedClientId = kvp.Key;
                        break;
                    }
                }
                
                if (disconnectedClientId != -1)
                {
                    clientPeers.Remove(disconnectedClientId);
                    Console.WriteLine($"Client {disconnectedClientId} disconnected: {disconnectInfo.Reason}");
                    OnPlayerDisconnected?.Invoke(disconnectedClientId);
                }
            }
            else
            {
                IsConnected = false;
                Console.WriteLine($"Disconnected from server: {disconnectInfo.Reason}");
            }
        }
        
        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Console.WriteLine($"Network error: {socketError}");
        }
        
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            try
            {
                byte packetType = reader.GetByte();
                
                switch ((PacketType)packetType)
                {
                    case PacketType.PlayerJoined:
                        // Client receives its ID from server
                        if (!IsServer)
                        {
                            PlayerId = reader.GetInt();
                            Console.WriteLine($"Received player ID: {PlayerId}");
                        }
                        break;
                    
                    case PacketType.TowerPlaced:
                        {
                            int playerId = reader.GetInt();
                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            int towerId = reader.GetInt();
                            
                            // Notify game that a tower was placed
                            OnTowerPlaced?.Invoke(playerId, new Vector2(x, y));
                        }
                        break;
                    
                    case PacketType.MoneyChanged:
                        {
                            int playerId = reader.GetInt();
                            int amount = reader.GetInt();
                            
                            // Notify game of money change
                            OnMoneyChanged?.Invoke(playerId, amount);
                        }
                        break;
                    
                    case PacketType.WaveStarted:
                        {
                            int waveNumber = reader.GetInt();
                            
                            // Notify game that a wave started
                            OnWaveStarted?.Invoke(PlayerId, waveNumber);
                        }
                        break;
                    
                    case PacketType.EnemySpawned:
                        {
                            int enemyId = reader.GetInt();
                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            int health = reader.GetInt();
                            float speed = reader.GetFloat();
                            int damage = reader.GetInt();
                            
                            // Create enemy and notify game
                            Enemy enemy = new Enemy(new Vector2(x, y), health, speed, damage)
                            {
                                Id = enemyId,
                                IsSyncedAcrossNetwork = true
                            };
                            
                            OnEnemySpawned?.Invoke(enemy);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing packet: {ex.Message}");
            }
            finally
            {
                reader.Recycle();
            }
        }
        
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            try
            {
                // Add detailed logging for debugging with byte data
                Console.WriteLine($"RECEIVED UNCONNECTED: Type={messageType}, From={remoteEndPoint}, IsDiscoveryRunning={isDiscoveryRunning}");
                
                if (reader.AvailableBytes <= 0)
                {
                    Console.WriteLine("WARNING: Empty packet received, no data to process");
                    return;
                }
                
                // Peek at raw data for debug purposes
                byte[] rawData = reader.RawData;
                if (rawData != null && rawData.Length > 0)
                {
                    Console.WriteLine($"PACKET RAW DATA (first 20 bytes): {BitConverter.ToString(rawData, reader.Position, Math.Min(20, reader.AvailableBytes))}");
                    
                    // Try to show string representation of packet start
                    try {
                        int headerSize = Math.Min(30, reader.AvailableBytes);
                        string packetHeader = Encoding.ASCII.GetString(rawData, reader.Position, headerSize);
                        Console.WriteLine($"PACKET HEADER: '{packetHeader}'");
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Error parsing packet header: {ex.Message}");
                    }
                }
                
                // Check if this is a discovery broadcast
                if (messageType == UnconnectedMessageType.Broadcast && !IsServer && isDiscoveryRunning)
                {
                    Console.WriteLine($"Processing potential discovery broadcast from {remoteEndPoint}");
                    
                    try {
                        string key = reader.GetString();
                        Console.WriteLine($"Discovery key received: '{key}', expected: '{DISCOVERY_KEY}'");
                        
                        if (key == DISCOVERY_KEY)
                        {
                            string hostName = reader.GetString();
                            string hostIP = reader.GetString();
                            int hostPort = reader.GetInt();
                            
                            Console.WriteLine($"SUCCESS: Found host {hostName} at {hostIP}:{hostPort}");
                            
                            // Override the IP with the actual sender IP to handle NAT
                            IPEndPoint actualEndPoint = new IPEndPoint(remoteEndPoint.Address, hostPort);
                            
                            // Check if we already know about this host
                            DiscoveredHost existingHost = discoveredHosts.Find(h => 
                                h.EndPoint.Address.ToString() == remoteEndPoint.Address.ToString() && h.Port == hostPort);
                                
                            if (existingHost != null)
                            {
                                // Update existing host
                                existingHost.Name = hostName;
                                existingHost.LastSeen = DateTime.Now;
                                existingHost.Port = hostPort;
                                
                                OnHostUpdated?.Invoke(existingHost);
                                Console.WriteLine($"Updated existing host: {hostName}");
                            }
                            else
                            {
                                // Add new host
                                DiscoveredHost newHost = new DiscoveredHost
                                {
                                    Name = hostName,
                                    EndPoint = actualEndPoint,
                                    LastSeen = DateTime.Now,
                                    Port = hostPort
                                };
                                
                                discoveredHosts.Add(newHost);
                                OnHostDiscovered?.Invoke(newHost);
                                
                                Console.WriteLine($"*** DISCOVERED NEW HOST: {hostName} at {remoteEndPoint.Address}:{hostPort} ***");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Received broadcast with wrong key: '{key}', skipping");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing discovery packet: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        // Try to read and dump the remaining data as hex
                        try {
                            byte[] remainingData = new byte[reader.AvailableBytes];
                            Buffer.BlockCopy(reader.RawData, reader.Position, remainingData, 0, reader.AvailableBytes);
                            Console.WriteLine($"REMAINING DATA: {BitConverter.ToString(remainingData)}");
                        }
                        catch {}
                    }
                }
                // Handle game discovery requests from earlier versions
                else if (messageType == UnconnectedMessageType.Broadcast && IsServer)
                {
                    dataWriter.Reset();
                    dataWriter.Put("CyberDefense Game Server");
                    netManager.SendUnconnectedMessage(dataWriter, remoteEndPoint);
                    Console.WriteLine($"Game discovery request from {remoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR processing unconnected message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Ensure reader is properly recycled
                reader.Recycle();
            }
        }
        
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            // Could be used for ping display or latency compensation
        }
        
        public void OnConnectionRequest(ConnectionRequest request)
        {
            string receivedKey = request.Data.GetString();
            Console.WriteLine($"Connection request received from {request.RemoteEndPoint} with key: '{receivedKey}'");
            
            // Additional debug info - compare byte by byte
            byte[] expectedBytes = Encoding.UTF8.GetBytes(CONNECTION_KEY);
            byte[] receivedBytes = Encoding.UTF8.GetBytes(receivedKey);
            Console.WriteLine($"Expected key: '{CONNECTION_KEY}' (hex: {BitConverter.ToString(expectedBytes)})");
            Console.WriteLine($"Received key: '{receivedKey}' (hex: {BitConverter.ToString(receivedBytes)})");
            
            if (IsServer)
            {
                // Accept connection with the right key
                if (receivedKey == CONNECTION_KEY)
                {
                    Console.WriteLine($"Accepting connection from {request.RemoteEndPoint} (key matches)");
                    request.Accept();
                }
                else
                {
                    Console.WriteLine($"Rejecting connection from {request.RemoteEndPoint} (incorrect key)");
                    request.Reject();
                }
            }
        }
    }
    
    // Packet types for network messages
    public enum PacketType : byte
    {
        PlayerJoined = 1,
        TowerPlaced = 2,
        MoneyChanged = 3,
        WaveStarted = 4,
        EnemySpawned = 5,
        HostInfo = 6
    }
}