#pragma warning disable CS8618

namespace GigaChat.Server.Models
{
    public class ChatUser
    {
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadMessageTime { get; set; }
    }
}