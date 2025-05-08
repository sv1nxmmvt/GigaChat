namespace GigaChat.Client.Shared.Models
{
    // Основные модели данных, соответствующие серверным DTO

    public class UserModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string ProfilePictureUrl { get; set; }
        public DateTime LastActive { get; set; }

        // Вспомогательные свойства для UI
        public bool IsOnline => DateTime.UtcNow.Subtract(LastActive).TotalMinutes < 5;
        public string DisplayName => string.IsNullOrEmpty(UserName) ? Email : UserName;
    }

    public class ChatModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsGroup { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserModel CreatedBy { get; set; }
        public List<UserModel> Members { get; set; } = new List<UserModel>();
        public MessageModel LastMessage { get; set; }
        public int UnreadCount { get; set; }

        // Вспомогательные свойства для UI
        public string DisplayName => IsGroup ? Name : Members.FirstOrDefault(m => m.Id != CurrentUserId)?.DisplayName ?? Name;
        public string CurrentUserId { get; set; } // Устанавливается в сервисе после получения чата
    }

    public class MessageModel
    {
        public string Id { get; set; }
        public string ChatId { get; set; }
        public UserModel Sender { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; }
        public List<AttachmentModel> Attachments { get; set; } = new List<AttachmentModel>();

        // Вспомогательные свойства для UI
        public bool IsFromCurrentUser { get; set; } // Устанавливается в сервисе после получения сообщения
        public string FormattedTime => SentAt.ToLocalTime().ToString("HH:mm");
        public string FormattedDate => SentAt.ToLocalTime().ToString("dd.MM.yyyy");
    }

    public class AttachmentModel
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public string Url { get; set; }

        // Вспомогательные свойства для UI
        public bool IsImage => ContentType.StartsWith("image/");
        public string FormattedSize => 
            FileSize < 1024 ? $"{FileSize} B" :
            FileSize < 1024 * 1024 ? $"{FileSize / 1024} KB" :
                                     $"{FileSize / (1024 * 1024)} MB";
    }

    // Модели запросов к API

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }

    public class CreateChatRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsGroup { get; set; }
        public List<string> MemberIds { get; set; } = new List<string>();
    }

    public class SendMessageRequest
    {
        public string ChatId { get; set; }
        public string Content { get; set; }
        public List<string> AttachmentIds { get; set; } = new List<string>();
    }

    public class UpdateMessageRequest
    {
        public string Content { get; set; }
    }

    // Модели ответов от API

    public class AuthResponse
    {
        public string Token { get; set; }
        public UserModel User { get; set; }
        public string Message { get; set; }
        public bool IsSuccess => !string.IsNullOrEmpty(Token);
    }

    public class ApiResponse<T>
    {
        public T Data { get; set; }
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
    }
}