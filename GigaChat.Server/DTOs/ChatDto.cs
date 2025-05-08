namespace GigaChat.Server.DTOs
{
    public class ChatDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsGroup { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public UserDto CreatedBy { get; set; } = new UserDto();
        public List<UserDto> Members { get; set; } = new List<UserDto>();
        public MessageDto? LastMessage { get; set; } // Сделано nullable
        public int UnreadCount { get; set; }
    }
}
