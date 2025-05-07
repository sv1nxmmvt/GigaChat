using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using GigaChat.Server.DTOs;

namespace GigaChat.Server.Hubs
{
    public class ChatHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var http = Context.GetHttpContext();
            var chatId = http?.Request.Query["chatId"].ToString();
            if (!string.IsNullOrEmpty(chatId))
                await Groups.AddToGroupAsync(Context.ConnectionId, chatId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var http = Context.GetHttpContext();
            var chatId = http?.Request.Query["chatId"].ToString();
            if (!string.IsNullOrEmpty(chatId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(MessageDto message)
        {
            await Clients.Group(message.ChatId.ToString())
                         .SendAsync("ReceiveMessage", message);
        }
    }
}
