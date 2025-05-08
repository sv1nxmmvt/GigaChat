namespace GigaChat.Server.DTOs
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public Guid ChatId { get; set; }
        public UserDto Sender { get; set; } = new UserDto();
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public List<AttachmentDto> Attachments { get; set; } = new();
    }
}
