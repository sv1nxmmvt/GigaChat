using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GigaChat.Server.Interfaces;
using GigaChat.Server.DTOs;

namespace GigaChat.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatsController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatsController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserChats()
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var chats = await _chatService.GetUserChatsAsync(userId);
            return Ok(chats);
        }

        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetChat(Guid chatId)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var chat = await _chatService.GetChatByIdAsync(chatId, userId);
            if (chat == null)
                return NotFound();

            return Ok(chat);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChatDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var chat = await _chatService.CreateChatAsync(dto, userId);
            return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, chat);
        }

        [HttpPut("{chatId}")]
        public async Task<IActionResult> Update(Guid chatId, [FromBody] CreateChatDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            try
            {
                var chat = await _chatService.UpdateChatAsync(chatId, dto, userId);
                return Ok(chat);
            }
            catch (InvalidOperationException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpDelete("{chatId}")]
        public async Task<IActionResult> Delete(Guid chatId)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var success = await _chatService.DeleteChatAsync(chatId, userId);
            if (!success)
                return Forbid();

            return NoContent();
        }
    }
}
