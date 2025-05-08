using GigaChat.Server.DTOs;

namespace GigaChat.Server.Interfaces
{
    public interface IMessageService
    {
        Task<IEnumerable<MessageDto>> GetChatMessagesAsync(Guid chatId, Guid userId, int skip, int take);
        Task<MessageDto> SendMessageAsync(SendMessageDto messageDto, Guid senderId);
        Task<MessageDto> UpdateMessageAsync(Guid messageId, string content, Guid userId);
        Task<bool> DeleteMessageAsync(Guid messageId, Guid userId);
        Task<bool> MarkAsReadUpToAsync(Guid chatId, Guid userId, DateTime timestamp);
    }
}
