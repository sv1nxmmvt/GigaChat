using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GigaChat.Client.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace GigaChat.Client.Shared.Interfaces
{
    // Интерфейс для аутентификации
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task LogoutAsync();
        Task<UserModel> GetCurrentUserAsync();
        Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
        Task<bool> DeleteAccountAsync();
        bool IsAuthenticated { get; }
        string Token { get; }
        event Action OnAuthStateChanged;
    }

    // Интерфейс для работы с пользователями
    public interface IUserService
    {
        Task<UserModel> GetUserAsync(string userId);
        Task<List<UserModel>> SearchUsersAsync(string searchTerm);
        Task<UserModel> UpdateProfileAsync(UserModel user);
    }

    // Интерфейс для работы с чатами
    public interface IChatService
    {
        Task<List<ChatModel>> GetChatsAsync();
        Task<ChatModel> GetChatAsync(string chatId);
        Task<ChatModel> CreateChatAsync(CreateChatRequest request);
        Task<ChatModel> UpdateChatAsync(string chatId, CreateChatRequest request);
        Task<bool> DeleteChatAsync(string chatId);

        // События для реалтайм-уведомлений
        event Action<ChatModel> OnChatCreated;
        event Action<ChatModel> OnChatUpdated;
        event Action<string> OnChatDeleted;
    }

    // Интерфейс для работы с сообщениями
    public interface IMessageService
    {
        Task<List<MessageModel>> GetMessagesAsync(string chatId, int skip = 0, int take = 50);
        Task<MessageModel> SendMessageAsync(SendMessageRequest request);
        Task<MessageModel> UpdateMessageAsync(string messageId, UpdateMessageRequest request);
        Task<bool> DeleteMessageAsync(string messageId);

        // События для реалтайм-уведомлений
        event Action<MessageModel> OnMessageReceived;
        event Action<MessageModel> OnMessageUpdated;
        event Action<string> OnMessageDeleted;
    }

    // Интерфейс для работы с файлами/вложениями
    public interface IFileService
    {
        Task<AttachmentModel> UploadFileAsync(IBrowserFile file);
        Task<bool> DeleteFileAsync(string attachmentId);
        string GetFileUrl(string attachmentId);
    }

    // Интерфейс для работы с SignalR (реалтайм)
    public interface IChatHubService
    {
        Task ConnectAsync(string chatId);
        Task DisconnectAsync();
        Task SendMessageAsync(MessageModel message);
        Task JoinChatAsync(string chatId);
        Task LeaveChatAsync(string chatId);

        bool IsConnected { get; }
        event Action<MessageModel> OnMessageReceived;
        event Action<string> OnUserJoined;
        event Action<string> OnUserLeft;
        event Action<string> OnTyping;
    }

    // Интерфейс для хранения настроек и токена
    public interface IStorageService
    {
        Task<T> GetItemAsync<T>(string key);
        Task SetItemAsync<T>(string key, T value);
        Task RemoveItemAsync(string key);
        void Clear();
    }

    // Интерфейс для HTTP-запросов
    public interface IHttpService
    {
        Task<ApiResponse<T>> GetAsync<T>(string uri);
        Task<ApiResponse<T>> PostAsync<T>(string uri, object data);
        Task<ApiResponse<T>> PutAsync<T>(string uri, object data);
        Task<ApiResponse<T>> DeleteAsync<T>(string uri);
        Task<ApiResponse<T>> SendFormDataAsync<T>(string uri, MultipartFormDataContent content);
    }
}