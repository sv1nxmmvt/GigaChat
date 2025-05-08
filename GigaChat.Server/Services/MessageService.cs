#pragma warning disable CS8603

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Interfaces;
using GigaChat.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GigaChat.Server.Services
{
    public class MessageService : IMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MessageService> _logger;

        public MessageService(ApplicationDbContext context, ILogger<MessageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<MessageDto>> GetChatMessagesAsync(Guid chatId, Guid userId, int skip, int take)
        {
            // Проверяем, является ли пользователь участником чата
            var isMember = await _context.ChatUsers
                .AnyAsync(cu => cu.ChatId == chatId && cu.UserId == userId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException("Пользователь не является участником чата");
            }

            // Получаем сообщения с пагинацией
            var messages = await _context.Messages
                .Where(m => m.ChatId == chatId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .Include(m => m.Sender)
                .Include(m => m.Attachments)
                .ToListAsync();

            // Обновляем время последнего прочтения
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == userId);

            if (chatUser != null && messages.Any())
            {
                chatUser.LastReadMessageTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Преобразуем в DTO и возвращаем в обратном порядке (от старых к новым)
            return messages
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ChatId = m.ChatId,
                    Sender = new UserDto
                    {
                        Id = m.Sender.Id,
                        UserName = m.Sender.UserName,
                        Email = m.Sender.Email,
                        ProfilePictureUrl = m.Sender.ProfilePictureUrl,
                        LastActive = m.Sender.LastActive
                    },
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsEdited = m.IsEdited,
                    Attachments = m.Attachments.Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        FileSize = a.FileSize,
                        Url = $"/api/files/{a.Id}"
                    }).ToList()
                })
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        public async Task<MessageDto> SendMessageAsync(SendMessageDto messageDto, Guid senderId)
        {
            // Проверяем, является ли пользователь участником чата
            var isMember = await _context.ChatUsers
                .AnyAsync(cu => cu.ChatId == messageDto.ChatId && cu.UserId == senderId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException("Пользователь не является участником чата");
            }

            var sender = await _context.Users.FindAsync(senderId);
            if (sender == null)
            {
                throw new InvalidOperationException("Отправитель не найден");
            }

            // Создаем новое сообщение
            var message = new Message
            {
                Id = Guid.NewGuid(),
                ChatId = messageDto.ChatId,
                SenderId = senderId,
                Content = messageDto.Content,
                SentAt = DateTime.UtcNow,
                IsEdited = false,
                IsDeleted = false
            };

            // Обрабатываем вложения, если они есть
            if (messageDto.AttachmentIds != null && messageDto.AttachmentIds.Any())
            {
                var attachments = await _context.Attachments
                    .Where(a => messageDto.AttachmentIds.Contains(a.Id))
                    .ToListAsync();

                if (attachments.Count != messageDto.AttachmentIds.Count)
                {
                    throw new InvalidOperationException("Некоторые вложения не найдены");
                }

                message.Attachments = attachments;
            }

            await _context.Messages.AddAsync(message);

            // Обновляем время последней активности пользователя
            sender.LastActive = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Формируем результат
            return new MessageDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                Sender = new UserDto
                {
                    Id = sender.Id,
                    UserName = sender.UserName,
                    Email = sender.Email,
                    ProfilePictureUrl = sender.ProfilePictureUrl,
                    LastActive = sender.LastActive
                },
                Content = message.Content,
                SentAt = message.SentAt,
                IsEdited = message.IsEdited,
                Attachments = message.Attachments?.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    Url = $"/api/files/{a.Id}"
                }).ToList() ?? new List<AttachmentDto>()
            };
        }

        public async Task<MessageDto> UpdateMessageAsync(Guid messageId, string content, Guid userId)
        {
            // Проверяем, существует ли сообщение и является ли пользователь его отправителем
            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId && !m.IsDeleted);

            if (message == null)
            {
                throw new InvalidOperationException("Сообщение не найдено или у вас нет прав для его редактирования");
            }

            // Обновляем содержимое сообщения
            message.Content = content;
            message.IsEdited = true;

            await _context.SaveChangesAsync();

            // Формируем результат
            return new MessageDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                Sender = new UserDto
                {
                    Id = message.Sender.Id,
                    UserName = message.Sender.UserName,
                    Email = message.Sender.Email,
                    ProfilePictureUrl = message.Sender.ProfilePictureUrl,
                    LastActive = message.Sender.LastActive
                },
                Content = message.Content,
                SentAt = message.SentAt,
                IsEdited = message.IsEdited,
                Attachments = message.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    Url = $"/api/files/{a.Id}"
                }).ToList()
            };
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
        {
            // Проверяем, существует ли сообщение и является ли пользователь его отправителем
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId && !m.IsDeleted);

            if (message == null)
            {
                // Проверяем, может ли пользователь как администратор чата удалить сообщение
                var isAdmin = await _context.ChatUsers
                    .AnyAsync(cu => cu.ChatId == message.ChatId && cu.UserId == userId && cu.IsAdmin);

                if (!isAdmin)
                {
                    return false;
                }
            }

            // Помечаем сообщение как удаленное (soft delete)
            message.IsDeleted = true;
            message.Content = "[Сообщение удалено]";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkAsReadUpToAsync(Guid chatId, Guid userId, DateTime timestamp)
        {
            // Проверяем, является ли пользователь участником чата
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == userId);

            if (chatUser == null)
            {
                return false;
            }

            // Обновляем время последнего прочтения
            chatUser.LastReadMessageTime = timestamp;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}