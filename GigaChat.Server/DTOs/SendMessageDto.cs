namespace GigaChat.Server.DTOs
{
    public class SendMessageDto
    {
        public Guid ChatId { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<Guid> AttachmentIds { get; set; } = new();
    }
}
