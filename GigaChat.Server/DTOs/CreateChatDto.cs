namespace GigaChat.Server.DTOs
{
    public class CreateChatDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsGroup { get; set; }
        public List<Guid> MemberIds { get; set; } = new();
    }
}
