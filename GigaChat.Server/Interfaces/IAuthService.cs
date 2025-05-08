using GigaChat.Server.DTOs;

namespace GigaChat.Server.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(UserRegistrationDto registerDto);
        Task<AuthResultDto> LoginAsync(UserLoginDto loginDto);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string email, string password);
        Task<bool> ConfirmEmailAsync(string token, string email);
    }
}
