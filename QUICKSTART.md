# Quick Start Guide - Collaborative Drawing

## 🚀 Quick Setup (3 Steps)

### Step 1: Start the Server on One Laptop
```powershell
cd DrawingServer
dotnet run
```
**📝 Note the LAN IP address** shown in the console (e.g., `192.168.1.40`)

### Step 2: Run Client on Each Laptop
```powershell
cd DrawingClient
dotnet run
```

### Step 3: Connect to Server
1. Enter the server's IP address (from Step 1)
2. Enter port: `5266`
3. Click **Connect**
4. Start drawing! 🎨

---

## 💡 Example Scenario

**Laptop A (Server)**:
```powershell
cd DrawingServer
dotnet run
```
Output shows: `Wi-Fi → 192.168.1.40`

**Laptop B, C, D (Clients)**:
```powershell
cd DrawingClient
dotnet run
```
- Enter IP: `192.168.1.40`
- Enter Port: `5266`
- Click **Connect**

Now all laptops can draw collaboratively in real-time! ✨

---

## 🔧 Common Connection IPs

| Scenario | IP Address to Use |
|----------|-------------------|
| Same machine (testing) | `127.0.0.1` or `localhost` |
| Different laptops (same Wi-Fi) | `192.168.x.x` (from server console) |
| Wired connection | `10.0.x.x` (from server console) |

---

## ⚠️ If Connection Fails

### Check These:
1. ✅ Server is running (console window open)
2. ✅ IP address is exactly as shown in server console
3. ✅ Port number is `5266`
4. ✅ Both devices on **same Wi-Fi network**
5. ✅ Windows Firewall allows port 5266

### Fix Firewall (Run as Administrator):
```powershell
New-NetFirewallRule -DisplayName "Drawing Server" -Direction Inbound -Protocol TCP -LocalPort 5266 -Action Allow
```

---

## 🎨 Drawing Tools Cheat Sheet

| Tool | Hotkey | Description |
|------|--------|-------------|
| Brush | - | Freehand drawing |
| Circle | - | Click & drag for circles |
| Square | - | Click & drag for squares |
| Rectangle | - | Click & drag for rectangles |
| Triangle | - | Click & drag for triangles |
| Pointer | - | Click to select, drag to move shapes |
| Eraser | - | Click to erase shapes |

**Keyboard Tip**: Use slider for brush size (1-20 px)

---

## 📸 Export Your Drawing

1. Click **Download** button
2. PDF saved to: `C:\Users\YourName\Downloads\`
3. Filename: `Whiteboard_YYYYMMDD_HHmmss.pdf`

---

## 🛑 Stopping the Server

Press `Ctrl+C` in the server console window.

---

## 🌟 Pro Tips

1. **Start server first**, then clients
2. **Server shows all client IPs** - verify connections
3. **"Clear Canvas" button** clears for everyone
4. **Disconnect button** to safely disconnect
5. **Status indicator** shows connection state (green = connected)

---

Need more details? Check the full [README.md](README.md)
