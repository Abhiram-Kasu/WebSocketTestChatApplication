using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebSockets;
using WebSocketChatApplication.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});


builder.Services.AddSingleton<WebSocketHandlerService>();
var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120),
});


app.Map("/chat", async (HttpContext context, WebSocketHandlerService service) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var chatId = context.Request.Headers["ChatId"];
    if(chatId.Count == 0 || !int.TryParse(chatId, out var id))
    {
        context.Response.StatusCode = 400;
        return;
    }
    
    
    var ws = await context.WebSockets.AcceptWebSocketAsync();
    if (!service.AddToChat(id, ws))
    {
        context.Response.StatusCode = 400;
        return;
    }

    var buffer = new byte[1024];
    var segment = new Memory<byte>(buffer);
    while (ws.State == WebSocketState.Open)
    {
        var res = await ws.ReceiveAsync(segment, new ());
        if (res.MessageType == WebSocketMessageType.Close)
        {
            service.RemoveFromChat(id, ws);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", new ());
            break;
        }
        
        if (res.MessageType == WebSocketMessageType.Text)
        {
            try
            {

                var message =
                    JsonSerializer.Deserialize<Message>(segment.Span[..res.Count], AppJsonSerializerContext.Default.Message);
                           await service.SendChatAsync(id, message!, ws);

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to deserialize Message");
                Console.WriteLine(e);
                
                await ws.SendAsync(new ReadOnlyMemory<byte>("Failed to deserialize Message"u8.ToArray()), WebSocketMessageType.Text, true, new ());
            }
        }
    }
    
    
});


app.Run();

public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(Message))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}