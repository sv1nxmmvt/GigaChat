using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GigaChat.Server.Interfaces;
using GigaChat.Server.DTOs;

namespace GigaChat.Server.Controllers
{
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
}
