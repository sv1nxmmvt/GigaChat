using GigaChat.Server.DTOs;

namespace GigaChat.Server.Interfaces
{
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
}
