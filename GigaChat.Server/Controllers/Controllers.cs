using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GigaChat.Server.Services;
using GigaChat.Server.DTOs;
using Microsoft.AspNetCore.SignalR;
using GigaChat.Server.Hubs;
using System.ComponentModel.DataAnnotations;

namespace GigaChat.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto dto)
        {
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest("Passwords do not match.");

            var result = await _authService.RegisterAsync(dto);
            if (!result.Success)
                return BadRequest(result.Message);

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            if (!result.Success)
                return Unauthorized(result.Message);

            return Ok(result);
        }

        [HttpPost("request-reset")]
        public async Task<IActionResult> RequestPasswordReset([FromQuery] string email)
        {
            var success = await _authService.RequestPasswordResetAsync(email);
            if (!success)
                return NotFound();

            return NoContent();
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromQuery] string email, [FromQuery] string token, [FromBody] string newPassword)
        {
            var success = await _authService.ResetPasswordAsync(token, email, newPassword);
            if (!success)
                return BadRequest();

            return NoContent();
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string token)
        {
            var success = await _authService.ConfirmEmailAsync(token, email);
            if (!success)
                return BadRequest();

            return NoContent();
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            var users = await _userService.SearchUsersAsync(term);
            return Ok(users);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            try
            {
                var updated = await _userService.UpdateUserProfileAsync(userId, dto);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var success = await _userService.UpdateUserPasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
            if (!success)
                return BadRequest("Current password is incorrect.");

            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            var sub = User.FindFirst("sub")?.Value;
            if (sub == null)
                return Unauthorized();
            var userId = Guid.Parse(sub);

            var success = await _userService.DeleteUserAsync(userId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }

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

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var sub = User.FindFirst("sub")?.Value;
                return sub != null ? Guid.Parse(sub) : (Guid?)null;
            }
        }

        public class UploadFileDto
        {
            [Required]
            public IFormFile File { get; set; }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadFileDto dto)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var attachment = await _fileService.UploadFileAsync(dto.File, userId.Value);
            return Ok(attachment);
        }

        [HttpGet("{attachmentId}")]
        public async Task<IActionResult> Get(Guid attachmentId)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var stream = await _fileService.GetFileAsync(attachmentId, userId.Value);
            if (stream == null)
                return Forbid();

            return File(stream, "application/octet-stream");
        }

        [HttpDelete("{attachmentId}")]
        public async Task<IActionResult> Delete(Guid attachmentId)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var success = await _fileService.DeleteFileAsync(attachmentId, userId.Value);
            if (!success)
                return Forbid();

            return NoContent();
        }
    }
}