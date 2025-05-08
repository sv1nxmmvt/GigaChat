using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GigaChat.Server.Interfaces;
using GigaChat.Server.DTOs;
using Microsoft.AspNetCore.SignalR;
using GigaChat.Server.Hubs;

namespace GigaChat.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly IHubContext<ChatHub> _hubContext;

        public MessagesController(IMessageService messageService, IHubContext<ChatHub> hubContext)
        {
            _messageService = messageService;
            _hubContext = hubContext;
        }

        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetMessages(Guid chatId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var messages = await _messageService.GetChatMessagesAsync(chatId, userId, skip, take);
            return Ok(messages);
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var message = await _messageService.SendMessageAsync(dto, userId);
            await _hubContext.Clients.Group(dto.ChatId.ToString())
                .SendAsync("ReceiveMessage", message);
            return Ok(message);
        }

        [HttpPut("{messageId}")]
        public async Task<IActionResult> Update(Guid messageId, [FromBody] UpdateMessageDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var updated = await _messageService.UpdateMessageAsync(messageId, dto.Content, userId);
            return Ok(updated);
        }

        [HttpDelete("{messageId}")]
        public async Task<IActionResult> Delete(Guid messageId)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var success = await _messageService.DeleteMessageAsync(messageId, userId);
            if (!success)
                return Forbid();

            return NoContent();
        }
    }
}
