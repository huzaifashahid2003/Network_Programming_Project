using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace DrawingServer
{
    /// <summary>
    /// Manages all connected TCP clients and handles broadcasting messages
    /// </summary>
    public class TcpConnectionManager
    {
        private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();

        /// <summary>
        /// Represents a connected client with their connection details
        /// </summary>
        private class ClientConnection
        {
            public TcpClient Client { get; set; }
            public string IpAddress { get; set; }
            public int Port { get; set; }
            public DateTime ConnectedAt { get; set; }
            public SemaphoreSlim WriteLock { get; set; } = new SemaphoreSlim(1, 1);

            public ClientConnection(TcpClient client, string ipAddress, int port)
            {
                Client = client;
                IpAddress = ipAddress;
                Port = port;
                ConnectedAt = DateTime.Now;
            }
        }

        /// <summary>
        /// Adds a new client to the connection manager
        /// </summary>
        public void AddClient(string clientId, TcpClient client, string ipAddress, int port)
        {
            var connection = new ClientConnection(client, ipAddress, port);
            _clients.TryAdd(clientId, connection);
        }

        /// <summary>
        /// Removes a client from the connection manager
        /// </summary>
        public void RemoveClient(string clientId)
        {
            _clients.TryRemove(clientId, out _);
        }

        /// <summary>
        /// Gets the current number of connected clients
        /// </summary>
        public int GetClientCount()
        {
            return _clients.Count;
        }

        /// <summary>
        /// Broadcasts a message to all connected clients except the sender
        /// </summary>
        public async Task BroadcastAsync(string message, string senderId)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var tasksToAwait = new List<Task>();

            foreach (var (clientId, connection) in _clients)
            {
                // Don't send the message back to the sender
                if (clientId == senderId)
                    continue;

                // Only send to connected clients
                if (!connection.Client.Connected)
                {
                    RemoveClient(clientId);
                    continue;
                }

                tasksToAwait.Add(SendMessageToClientAsync(clientId, connection, messageBytes));
            }

            // Wait for all broadcasts to complete
            await Task.WhenAll(tasksToAwait);
        }

        /// <summary>
        /// Sends a message to a specific client with proper framing (length prefix)
        /// </summary>
        private async Task SendMessageToClientAsync(string clientId, ClientConnection connection, byte[] messageBytes)
        {
            await connection.WriteLock.WaitAsync();
            try
            {
                if (!connection.Client.Connected)
                {
                    RemoveClient(clientId);
                    return;
                }

                var stream = connection.Client.GetStream();
                
                // Send message length as 4-byte prefix (big-endian for clarity)
                var lengthPrefix = BitConverter.GetBytes(messageBytes.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lengthPrefix);
                
                await stream.WriteAsync(lengthPrefix, 0, 4);
                await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception)
            {
                // If sending fails, remove the client
                RemoveClient(clientId);
            }
            finally
            {
                connection.WriteLock.Release();
            }
        }

        /// <summary>
        /// Gets information about all connected clients
        /// </summary>
        public List<string> GetConnectedClientsInfo()
        {
            var clientsInfo = new List<string>();

            foreach (var (clientId, connection) in _clients)
            {
                var info = $"Client {clientId[..8]}... | IP: {connection.IpAddress}:{connection.Port} | Connected: {connection.ConnectedAt:HH:mm:ss}";
                clientsInfo.Add(info);
            }

            return clientsInfo;
        }
    }
}
