using Microsoft.AspNetCore.Mvc;
using GigaChat.Server.Interfaces;
using GigaChat.Server.DTOs;

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
}