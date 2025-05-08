#pragma warning disable CS8618

namespace GigaChat.Server.Models
{
    public class Attachment
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Message Message { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}