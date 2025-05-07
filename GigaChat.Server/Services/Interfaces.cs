using GigaChat.Server.DTOs;

namespace GigaChat.Server.Services
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(UserRegistrationDto registerDto);
        Task<AuthResultDto> LoginAsync(UserLoginDto loginDto);
        Task<bool> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string email, string password);
        Task<bool> ConfirmEmailAsync(string token, string email);
    }

    public interface IUserService
    {
        Task<UserDto> GetUserByIdAsync(Guid userId);
        Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm);
        Task<UserDto> UpdateUserProfileAsync(Guid userId, UserDto userDto);
        Task<bool> UpdateUserPasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<bool> DeleteUserAsync(Guid userId);
    }

    public interface IChatService
    {
        Task<IEnumerable<ChatDto>> GetUserChatsAsync(Guid userId);
        Task<ChatDto> GetChatByIdAsync(Guid chatId, Guid userId);
        Task<ChatDto> CreateChatAsync(CreateChatDto createChatDto, Guid creatorId);
        Task<ChatDto> UpdateChatAsync(Guid chatId, CreateChatDto updateChatDto, Guid userId);
        Task<bool> DeleteChatAsync(Guid chatId, Guid userId);
        Task<bool> AddUsersToChat(Guid chatId, List<Guid> userIds, Guid adminId);
        Task<bool> RemoveUserFromChat(Guid chatId, Guid userId, Guid adminId);
        Task<bool> MakeUserAdmin(Guid chatId, Guid userId, Guid adminId);
    }

    public interface IMessageService
    {
        Task<IEnumerable<MessageDto>> GetChatMessagesAsync(Guid chatId, Guid userId, int skip, int take);
        Task<MessageDto> SendMessageAsync(SendMessageDto messageDto, Guid senderId);
        Task<MessageDto> UpdateMessageAsync(Guid messageId, string content, Guid userId);
        Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
        Task<bool> MarkAsReadUpToAsync(Guid chatId, Guid userId, DateTime timestamp);
    }

    public interface IFileService
    {
        Task<AttachmentDto> UploadFileAsync(IFormFile file, Guid userId);
        Task<Stream> GetFileAsync(Guid attachmentId, Guid userId);
        Task<bool> DeleteFileAsync(Guid attachmentId, Guid userId);
    }
}
