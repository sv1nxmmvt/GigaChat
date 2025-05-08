#pragma warning disable CS8618

namespace GigaChat.Server.Models
{
    public class Chat
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsGroup { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid CreatedById { get; set; }
        public User CreatedBy { get; set; }
        public ICollection<ChatUser> ChatUsers { get; set; }
        public ICollection<Message> Messages { get; set; }
    }
}