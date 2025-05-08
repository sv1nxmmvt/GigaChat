using GigaChat.Server.DTOs;

namespace GigaChat.Server.Interfaces
{
    public interface IUserService
    {
        Task<UserDto> GetUserByIdAsync(Guid userId);
        Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm);
        Task<UserDto> UpdateUserProfileAsync(Guid userId, UserDto userDto);
        Task<bool> UpdateUserPasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<bool> DeleteUserAsync(Guid userId);
    }
}
