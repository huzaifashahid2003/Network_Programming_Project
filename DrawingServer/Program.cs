using DrawingServer;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<WebSocketConnectionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseWebSockets();

app.Map("/ws", async (HttpContext context, WebSocketConnectionManager connectionManager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socketId = connectionManager.AddSocket(webSocket);
        
        var buffer = new byte[1024 * 4];
        
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // Broadcast to other clients
                    await connectionManager.BroadcastAsync(message, socketId);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connectionManager.RemoveSocketAsync(socketId);
                }
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected abruptly
            await connectionManager.RemoveSocketAsync(socketId);
        }
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Run();
