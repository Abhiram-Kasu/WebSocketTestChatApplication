using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocketChatApplication.Services;

public sealed class WebSocketHandlerService
{
    private ConcurrentDictionary<int, ConcurrentBag<WebSocket>> _mappings = [];


    public bool AddToChat(int chatId, WebSocket ws)
    {
        if(_mappings.TryGetValue(chatId, out var sockets) && sockets is {Count: >0})
        {
            if (sockets.Contains(ws)) return false;
            sockets.Add(ws);
        }
        else
        {
            _mappings.TryAdd(chatId, [ws]);
        }

        return true;
    }

    public bool RemoveFromChat(int chatId, WebSocket ws)
    {
        if (!_mappings.TryGetValue(chatId, out var sockets) || sockets is not { Count: > 0 }) return false;
        if (_mappings.ContainsKey(chatId)) _mappings[chatId] = [..sockets.Except([ws])];
        return true;
    }

    public async Task<bool> SendChatAsync(int chatId, Message message, WebSocket senderWs, CancellationToken ct = new())
    {
        if(!_mappings.TryGetValue(chatId, out var sockets) || sockets is not {Count: > 0}) return false;
        var messageJson = JsonSerializer.Serialize(message, AppJsonSerializerContext.Default.Message );
        var segment = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(messageJson));
        //Update sockets here: 
        sockets = [..sockets.Where(x => x.State == WebSocketState.Open)];
        foreach (var ws in sockets.Except([senderWs]))
        {
            await ws.SendAsync(segment, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct );
        }
        return true;
    }
    
    
}

public record Message(int MessageId, int ChatId, string MessageContent, DateTime SentAt);

