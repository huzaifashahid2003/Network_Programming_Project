using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;

namespace DrawingServer
{
    class Program
    {
        private static readonly TcpConnectionManager _connectionManager = new TcpConnectionManager();
        private static TcpListener? _listener;
        private const int DEFAULT_PORT = 5266;

        static async Task Main(string[] args)
        {
            Console.Title = "Collaborative Drawing Server";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║   COLLABORATIVE DRAWING TCP SERVER             ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝");
            Console.ResetColor();

            // Display all available LAN IP addresses
            DisplayServerIpAddresses();

            int port = DEFAULT_PORT;
            Console.WriteLine($"\nStarting TCP Server on port {port}...");

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Server started successfully!");
                Console.WriteLine($"✓ Listening on port {port}");
                Console.WriteLine($"✓ Clients can connect using: <SERVER_LAN_IP>:{port}");
                Console.ResetColor();
                Console.WriteLine("\nWaiting for clients to connect...\n");

                // Start accepting clients
                while (true)
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Server Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void DisplayServerIpAddresses()
        {
            Console.WriteLine("\n📡 Available Network Interfaces:");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    var properties = networkInterface.GetIPProperties();
                    foreach (var ip in properties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  • {networkInterface.Name,-25} → {ip.Address}");
                            Console.ResetColor();
                        }
                    }
                }
            }
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            var clientIp = clientEndPoint?.Address.ToString() ?? "Unknown";
            var clientPort = clientEndPoint?.Port ?? 0;
            var clientId = Guid.NewGuid().ToString();

            // Add client to connection manager
            _connectionManager.AddClient(clientId, client, clientIp, clientPort);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✓ NEW CONNECTION");
            Console.WriteLine($"                   Client ID: {clientId[..8]}...");
            Console.WriteLine($"                   IP Address: {clientIp}:{clientPort}");
            Console.WriteLine($"                   Total Clients: {_connectionManager.GetClientCount()}");
            Console.ResetColor();

            try
            {
                var stream = client.GetStream();

                while (client.Connected)
                {
                    // Read 4-byte length prefix
                    var lengthBuffer = new byte[4];
                    int bytesRead = 0;
                    
                    while (bytesRead < 4)
                    {
                        int read = await stream.ReadAsync(lengthBuffer, bytesRead, 4 - bytesRead);
                        if (read == 0)
                        {
                            // Client disconnected
                            return;
                        }
                        bytesRead += read;
                    }

                    // Convert length prefix from big-endian
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBuffer);
                    
                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                    // Validate message length
                    if (messageLength <= 0 || messageLength > 1024 * 1024) // Max 1MB per message
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ Invalid message length from {clientIp}: {messageLength}");
                        Console.ResetColor();
                        break;
                    }

                    // Read the actual message
                    var messageBuffer = new byte[messageLength];
                    bytesRead = 0;
                    
                    while (bytesRead < messageLength)
                    {
                        int read = await stream.ReadAsync(messageBuffer, bytesRead, messageLength - bytesRead);
                        if (read == 0)
                        {
                            // Client disconnected mid-message
                            return;
                        }
                        bytesRead += read;
                    }

                    // Get the drawing event message
                    var message = Encoding.UTF8.GetString(messageBuffer);

                    // Broadcast to all other clients
                    await _connectionManager.BroadcastAsync(message, clientId);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠ Client error ({clientIp}): {ex.Message}");
                Console.ResetColor();
            }
            finally
            {
                // Remove client from manager
                _connectionManager.RemoveClient(clientId);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✗ DISCONNECTED");
                Console.WriteLine($"                   IP Address: {clientIp}:{clientPort}");
                Console.WriteLine($"                   Total Clients: {_connectionManager.GetClientCount()}");
                Console.ResetColor();

                client.Close();
            }
        }
    }
}
