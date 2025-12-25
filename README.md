# Collaborative Drawing Application - TCP Edition

A real-time collaborative whiteboard application that allows multiple users on the same Wi-Fi network to draw together using TCP sockets.

## Architecture

### 🖥️ Server (Console Application)
- **Technology**: .NET 10.0 Console Application
- **Protocol**: TCP Server
- **Port**: 5266 (configurable)
- **Features**:
  - Displays all available network interfaces with LAN IP addresses
  - Shows real-time client connections with IP addresses
  - Broadcasts drawing events to all connected clients
  - Clean, human-readable console output with timestamps

### 🎨 Client (WPF Application)
- **Technology**: .NET 10.0 WPF Desktop Application
- **Protocol**: TCP Client
- **Features**:
  - Connect to server using LAN IP address
  - Real-time collaborative drawing
  - 7 drawing tools: Brush, Eraser, Pointer, Circle, Square, Rectangle, Triangle
  - Customizable brush size (1-20 pixels)
  - 6 color options
  - Select and drag shapes
  - Export to PDF
  - Clear canvas (synchronized across clients)

### 📦 Shared Library
- **Technology**: .NET 10.0 Class Library
- **Purpose**: Shared data models and enums (DrawEvent, ShapeType, EventType)

---

## Setup Instructions

### 1. Prerequisites
- .NET 10.0 SDK installed
- Windows OS (for WPF client)
- All devices on the same Wi-Fi network

### 2. Starting the Server

```powershell
cd DrawingServer
dotnet run
```

**Server Output Example:**
```
╔════════════════════════════════════════════════╗
║   COLLABORATIVE DRAWING TCP SERVER             ║
╚════════════════════════════════════════════════╝

📡 Available Network Interfaces:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  • Wi-Fi                     → 192.168.1.100
  • Ethernet                  → 10.0.0.50
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Starting TCP Server on port 5266...
✓ Server started successfully!
✓ Listening on port 5266
✓ Clients can connect using: <SERVER_LAN_IP>:5266

Waiting for clients to connect...

[14:30:45] ✓ NEW CONNECTION
           Client ID: a3b4c5d6...
           IP Address: 192.168.1.105:54321
           Total Clients: 1

[14:31:20] ✓ NEW CONNECTION
           Client ID: e7f8g9h0...
           IP Address: 192.168.1.110:54322
           Total Clients: 2
```

### 3. Connecting Clients

1. **Launch the Client Application:**
   ```powershell
   cd DrawingClient
   dotnet run
   ```

2. **Configure Connection:**
   - Enter the server's LAN IP address (e.g., `192.168.1.100`)
   - Enter the port number (default: `5266`)
   - Click "Connect"

3. **Start Drawing:**
   - Once connected, the status will show "Connected to [IP]:[Port]"
   - Draw on the canvas - all connected clients will see your drawings in real-time

### 4. Running Multiple Clients

To test on the same machine (development):
- Run the server once
- Run the client multiple times
- Connect each client to `127.0.0.1:5266` or the actual LAN IP

To test on different laptops (production):
- Run the server on one laptop (note its LAN IP from the server console)
- Run clients on other laptops
- Connect clients using the server's LAN IP (e.g., `192.168.1.100:5266`)

---

## Features

### Drawing Tools
| Tool | Description |
|------|-------------|
| **Brush** | Freehand drawing with customizable size |
| **Eraser** | Click to erase shapes at cursor position |
| **Pointer** | Select and drag shapes around canvas |
| **Circle** | Draw perfect circles |
| **Square** | Draw perfect squares |
| **Rectangle** | Draw rectangles |
| **Triangle** | Draw triangles |

### Customization
- **Brush Size**: Adjustable from 1 to 20 pixels
- **Colors**: Black, Red, Blue, Green, Yellow, Purple

### Collaboration Features
- ✅ Real-time synchronization across all clients
- ✅ Clear canvas (broadcasts to all users)
- ✅ Erase operations synchronized
- ✅ Connection status indicator

### Export
- **Download Button**: Saves current canvas as PDF to Downloads folder
- **Filename Format**: `Whiteboard_YYYYMMDD_HHmmss.pdf`

---

## Technical Details

### Communication Protocol
1. Client connects to server via TCP
2. Drawing events are serialized to JSON
3. JSON sent over TCP stream
4. Server broadcasts to all other connected clients
5. Clients deserialize JSON and render shapes

### DrawEvent JSON Structure
```json
{
  "Type": 0,          // 0=Draw, 1=Clear, 2=Erase
  "Shape": 0,         // 0=Brush, 1=Circle, 2=Square, etc.
  "StartX": 100.5,
  "StartY": 150.0,
  "EndX": 200.5,
  "EndY": 250.0,
  "Color": "#FF0000",
  "Width": 3.0
}
```

### Connection Management
- **Server**: Uses `TcpConnectionManager` with thread-safe `ConcurrentDictionary`
- **Client**: Uses `TcpClient` with `NetworkStream` for async I/O
- **Graceful Disconnect**: Both server and client handle connection failures elegantly

### Code Quality
- ✅ Clean, human-readable code with XML comments
- ✅ Async/await for non-blocking operations
- ✅ Exception handling for network errors
- ✅ Clear separation of concerns
- ✅ Descriptive variable and method names

---

## Troubleshooting

### Cannot Connect to Server
- ✅ Ensure server is running
- ✅ Verify server IP address is correct (check server console for IPs)
- ✅ Check firewall settings (allow port 5266)
- ✅ Confirm both devices are on same Wi-Fi network
- ✅ Try pinging the server IP first

### Connection Lost During Drawing
- Server may have stopped
- Network connection interrupted
- Client will show "Connection Lost" status

### Firewall Configuration (Windows)
```powershell
# Allow inbound TCP connections on port 5266
New-NetFirewallRule -DisplayName "Drawing Server" -Direction Inbound -Protocol TCP -LocalPort 5266 -Action Allow
```

---

## Project Structure

```
canvas-drawing-sync-main/
├── DrawingServer/             # TCP Server (Console App)
│   ├── Program.cs            # Main server logic with network interface display
│   ├── TcpConnectionManager.cs  # Client connection management
│   └── DrawingServer.csproj
│
├── DrawingClient/             # WPF Client Application
│   ├── MainWindow.xaml       # UI definition
│   ├── MainWindow.xaml.cs    # Client logic with TCP communication
│   └── DrawingClient.csproj
│
└── DrawingShared/             # Shared data models
    ├── DrawEvent.cs          # Drawing event model
    └── DrawingShared.csproj
```

---

## Future Enhancements
- 🔲 Persistent canvas state (new clients see existing drawings)
- 🔲 Multiple rooms/channels
- 🔲 User authentication
- 🔲 Undo/Redo functionality
- 🔲 Text tool
- 🔲 Fill tool for shapes
- 🔲 Image import/paste
- 🔲 Chat functionality

---

## License
This project is provided as-is for educational and collaborative purposes.

## Contributors
- Clean code architecture with TCP implementation
- Human-readable console output
- LAN IP discovery and display
- Real-time collaborative features
