#pragma warning disable CS8603

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Interfaces;
using GigaChat.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GigaChat.Server.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatService> _logger;

        public ChatService(ApplicationDbContext context, ILogger<ChatService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<ChatDto>> GetUserChatsAsync(Guid userId)
        {
            var chatUsers = await _context.ChatUsers
                .Where(cu => cu.UserId == userId)
                .Include(cu => cu.Chat)
                    .ThenInclude(c => c.CreatedBy)
                .Include(cu => cu.Chat)
                    .ThenInclude(c => c.ChatUsers)
                        .ThenInclude(cu => cu.User)
                .Include(cu => cu.Chat)
                    .ThenInclude(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                        .ThenInclude(m => m.Sender)
                .ToListAsync();

            var chats = chatUsers.Select(cu => new ChatDto
            {
                Id = cu.Chat.Id,
                Name = cu.Chat.Name,
                Description = cu.Chat.Description,
                IsGroup = cu.Chat.IsGroup,
                ImageUrl = cu.Chat.ImageUrl,
                CreatedAt = cu.Chat.CreatedAt,
                CreatedBy = new UserDto
                {
                    Id = cu.Chat.CreatedBy.Id,
                    UserName = cu.Chat.CreatedBy.UserName,
                    Email = cu.Chat.CreatedBy.Email,
                    ProfilePictureUrl = cu.Chat.CreatedBy.ProfilePictureUrl,
                    LastActive = cu.Chat.CreatedBy.LastActive
                },
                Members = cu.Chat.ChatUsers.Select(u => new UserDto
                {
                    Id = u.User.Id,
                    UserName = u.User.UserName,
                    Email = u.User.Email,
                    ProfilePictureUrl = u.User.ProfilePictureUrl,
                    LastActive = u.User.LastActive
                }).ToList(),
                LastMessage = cu.Chat.Messages.Any() ? new MessageDto
                {
                    Id = cu.Chat.Messages.First().Id,
                    ChatId = cu.Chat.Messages.First().ChatId,
                    Sender = new UserDto
                    {
                        Id = cu.Chat.Messages.First().Sender.Id,
                        UserName = cu.Chat.Messages.First().Sender.UserName,
                        Email = cu.Chat.Messages.First().Sender.Email,
                        ProfilePictureUrl = cu.Chat.Messages.First().Sender.ProfilePictureUrl,
                        LastActive = cu.Chat.Messages.First().Sender.LastActive
                    },
                    Content = cu.Chat.Messages.First().Content,
                    SentAt = cu.Chat.Messages.First().SentAt,
                    IsEdited = cu.Chat.Messages.First().IsEdited,
                    Attachments = new List<AttachmentDto>()
                } : null,
                UnreadCount = cu.Chat.Messages.Count(m => m.SentAt > (cu.LastReadMessageTime ?? DateTime.MinValue))
            }).ToList();

            return chats;
        }

        // Завершение класса ChatService
        public async Task<ChatDto> GetChatByIdAsync(Guid chatId, Guid userId)
        {
            // Проверка, состоит ли пользователь в чате
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == userId);

            if (chatUser == null)
            {
                return null;
            }

            var chat = await _context.Chats
                .Include(c => c.CreatedBy)
                .Include(c => c.ChatUsers)
                    .ThenInclude(cu => cu.User)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null)
            {
                return null;
            }

            // Обновляем время последнего прочтения сообщения
            chatUser.LastReadMessageTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new ChatDto
            {
                Id = chat.Id,
                Name = chat.Name,
                Description = chat.Description,
                IsGroup = chat.IsGroup,
                ImageUrl = chat.ImageUrl,
                CreatedAt = chat.CreatedAt,
                CreatedBy = new UserDto
                {
                    Id = chat.CreatedBy.Id,
                    UserName = chat.CreatedBy.UserName,
                    Email = chat.CreatedBy.Email,
                    ProfilePictureUrl = chat.CreatedBy.ProfilePictureUrl,
                    LastActive = chat.CreatedBy.LastActive
                },
                Members = chat.ChatUsers.Select(u => new UserDto
                {
                    Id = u.User.Id,
                    UserName = u.User.UserName,
                    Email = u.User.Email,
                    ProfilePictureUrl = u.User.ProfilePictureUrl,
                    LastActive = u.User.LastActive
                }).ToList(),
                LastMessage = chat.Messages.Any() ? new MessageDto
                {
                    Id = chat.Messages.First().Id,
                    ChatId = chat.Messages.First().ChatId,
                    Sender = new UserDto
                    {
                        Id = chat.Messages.First().Sender.Id,
                        UserName = chat.Messages.First().Sender.UserName,
                        Email = chat.Messages.First().Sender.Email,
                        ProfilePictureUrl = chat.Messages.First().Sender.ProfilePictureUrl,
                        LastActive = chat.Messages.First().Sender.LastActive
                    },
                    Content = chat.Messages.First().Content,
                    SentAt = chat.Messages.First().SentAt,
                    IsEdited = chat.Messages.First().IsEdited,
                    Attachments = new List<AttachmentDto>() // Здесь нужно загрузить вложения
                } : null,
                UnreadCount = 0 // Считаем, что все сообщения прочитаны при открытии чата
            };
        }

        public async Task<ChatDto> CreateChatAsync(CreateChatDto createChatDto, Guid creatorId)
        {
            var creator = await _context.Users.FindAsync(creatorId);
            if (creator == null)
            {
                throw new InvalidOperationException("Создатель чата не найден");
            }

            // Проверяем, что все пользователи существуют
            var memberIds = createChatDto.MemberIds ?? new List<Guid>();
            if (!memberIds.Contains(creatorId))
            {
                memberIds.Add(creatorId); // Добавляем создателя в список участников
            }

            var members = await _context.Users
                .Where(u => memberIds.Contains(u.Id))
                .ToListAsync();

            if (members.Count != memberIds.Count)
            {
                throw new InvalidOperationException("Один или несколько участников не найдены");
            }

            // Создаем чат
            var chat = new Chat
            {
                Id = Guid.NewGuid(),
                Name = createChatDto.Name,
                Description = createChatDto.Description,
                IsGroup = createChatDto.IsGroup,
                CreatedAt = DateTime.UtcNow,
                CreatedById = creatorId,
                CreatedBy = creator
            };

            // Если это не групповой чат, устанавливаем имя как имя собеседника
            if (!chat.IsGroup && memberIds.Count == 2)
            {
                var otherUser = members.First(u => u.Id != creatorId);
                chat.Name = otherUser.UserName;
                chat.ImageUrl = otherUser.ProfilePictureUrl;
            }

            // Добавляем пользователей в чат
            foreach (var member in members)
            {
                chat.ChatUsers = chat.ChatUsers ?? new List<ChatUser>();
                chat.ChatUsers.Add(new ChatUser
                {
                    ChatId = chat.Id,
                    UserId = member.Id,
                    User = member,
                    IsAdmin = member.Id == creatorId, // Создатель - админ
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _context.Chats.AddAsync(chat);
            await _context.SaveChangesAsync();

            // Формируем результат
            return new ChatDto
            {
                Id = chat.Id,
                Name = chat.Name,
                Description = chat.Description,
                IsGroup = chat.IsGroup,
                ImageUrl = chat.ImageUrl,
                CreatedAt = chat.CreatedAt,
                CreatedBy = new UserDto
                {
                    Id = creator.Id,
                    UserName = creator.UserName,
                    Email = creator.Email,
                    ProfilePictureUrl = creator.ProfilePictureUrl,
                    LastActive = creator.LastActive
                },
                Members = members.Select(m => new UserDto
                {
                    Id = m.Id,
                    UserName = m.UserName,
                    Email = m.Email,
                    ProfilePictureUrl = m.ProfilePictureUrl,
                    LastActive = m.LastActive
                }).ToList(),
                UnreadCount = 0
            };
        }

        public async Task<ChatDto> UpdateChatAsync(Guid chatId, CreateChatDto updateChatDto, Guid userId)
        {
            // Проверяем, существует ли чат и является ли пользователь его администратором
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == userId && cu.IsAdmin);

            if (chatUser == null)
            {
                throw new InvalidOperationException("У вас нет прав для изменения этого чата");
            }

            var chat = await _context.Chats
                .Include(c => c.CreatedBy)
                .Include(c => c.ChatUsers)
                    .ThenInclude(cu => cu.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null)
            {
                throw new InvalidOperationException("Чат не найден");
            }

            // Обновляем информацию о чате
            chat.Name = updateChatDto.Name;
            chat.Description = updateChatDto.Description;

            // Если это групповой чат, обновляем список участников
            if (chat.IsGroup && updateChatDto.MemberIds != null)
            {
                // Получаем новых участников
                var existingMemberIds = chat.ChatUsers.Select(cu => cu.UserId).ToList();
                var newMemberIds = updateChatDto.MemberIds.Except(existingMemberIds).ToList();

                // Добавляем новых участников
                if (newMemberIds.Any())
                {
                    var newMembers = await _context.Users
                        .Where(u => newMemberIds.Contains(u.Id))
                        .ToListAsync();

                    foreach (var member in newMembers)
                    {
                        chat.ChatUsers.Add(new ChatUser
                        {
                            ChatId = chat.Id,
                            UserId = member.Id,
                            User = member,
                            IsAdmin = false,
                            JoinedAt = DateTime.UtcNow
                        });
                    }
                }

                // Удаляем участников, которых нет в новом списке (кроме создателя)
                var membersToRemove = chat.ChatUsers
                    .Where(cu => !updateChatDto.MemberIds.Contains(cu.UserId) && cu.UserId != chat.CreatedById)
                    .ToList();

                foreach (var member in membersToRemove)
                {
                    chat.ChatUsers.Remove(member);
                }
            }

            await _context.SaveChangesAsync();

            // Формируем результат
            return new ChatDto
            {
                Id = chat.Id,
                Name = chat.Name,
                Description = chat.Description,
                IsGroup = chat.IsGroup,
                ImageUrl = chat.ImageUrl,
                CreatedAt = chat.CreatedAt,
                CreatedBy = new UserDto
                {
                    Id = chat.CreatedBy.Id,
                    UserName = chat.CreatedBy.UserName,
                    Email = chat.CreatedBy.Email,
                    ProfilePictureUrl = chat.CreatedBy.ProfilePictureUrl,
                    LastActive = chat.CreatedBy.LastActive
                },
                Members = chat.ChatUsers.Select(cu => new UserDto
                {
                    Id = cu.User.Id,
                    UserName = cu.User.UserName,
                    Email = cu.User.Email,
                    ProfilePictureUrl = cu.User.ProfilePictureUrl,
                    LastActive = cu.User.LastActive
                }).ToList(),
                UnreadCount = 0
            };
        }

        public async Task<bool> DeleteChatAsync(Guid chatId, Guid userId)
        {
            // Проверяем, существует ли чат и является ли пользователь его администратором
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == userId && cu.IsAdmin);

            if (chatUser == null)
            {
                return false;
            }

            var chat = await _context.Chats
                .Include(c => c.ChatUsers)
                .Include(c => c.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null)
            {
                return false;
            }

            // Удаляем вложения сообщений
            foreach (var message in chat.Messages)
            {
                foreach (var attachment in message.Attachments)
                {
                    _context.Attachments.Remove(attachment);
                }
            }

            // Удаляем сообщения
            _context.Messages.RemoveRange(chat.Messages);

            // Удаляем связи пользователей с чатом
            _context.ChatUsers.RemoveRange(chat.ChatUsers);

            // Удаляем чат
            _context.Chats.Remove(chat);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddUsersToChat(Guid chatId, List<Guid> userIds, Guid adminId)
        {
            // Проверяем, существует ли чат и является ли пользователь его администратором
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == adminId && cu.IsAdmin);

            if (chatUser == null)
            {
                return false;
            }

            var chat = await _context.Chats
                .Include(c => c.ChatUsers)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null || !chat.IsGroup)
            {
                return false;
            }

            // Получаем существующих пользователей
            var existingUserIds = chat.ChatUsers.Select(cu => cu.UserId).ToList();
            var newUserIds = userIds.Except(existingUserIds).ToList();

            if (!newUserIds.Any())
            {
                return true; // Все пользователи уже в чате
            }

            // Проверяем, что все пользователи существуют
            var users = await _context.Users
                .Where(u => newUserIds.Contains(u.Id))
                .ToListAsync();

            if (users.Count != newUserIds.Count)
            {
                return false;
            }

            // Добавляем пользователей в чат
            foreach (var user in users)
            {
                chat.ChatUsers.Add(new ChatUser
                {
                    ChatId = chat.Id,
                    UserId = user.Id,
                    IsAdmin = false,
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveUserFromChat(Guid chatId, Guid userId, Guid adminId)
        {
            // Проверяем, существует ли чат и является ли пользователь его администратором
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == adminId && cu.IsAdmin);

            if (chatUser == null)
            {
                return false;
            }

            var chat = await _context.Chats
                .Include(c => c.ChatUsers)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null || !chat.IsGroup)
            {
                return false;
            }

            // Нельзя удалить создателя чата
            if (userId == chat.CreatedById)
            {
                return false;
            }

            // Удаляем пользователя из чата
            var userToRemove = chat.ChatUsers.FirstOrDefault(cu => cu.UserId == userId);
            if (userToRemove == null)
            {
                return false;
            }

            _context.ChatUsers.Remove(userToRemove);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MakeUserAdmin(Guid chatId, Guid userId, Guid adminId)
        {
            // Проверяем, существует ли чат и является ли пользователь его администратором
            var chatUser = await _context.ChatUsers
                .FirstOrDefaultAsync(cu => cu.ChatId == chatId && cu.UserId == adminId && cu.IsAdmin);

            if (chatUser == null)
            {
                return false;
            }

            var chat = await _context.Chats
                .Include(c => c.ChatUsers)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null || !chat.IsGroup)
            {
                return false;
            }

            // Делаем пользователя администратором
            var userToPromote = chat.ChatUsers.FirstOrDefault(cu => cu.UserId == userId);
            if (userToPromote == null)
            {
                return false;
            }

            userToPromote.IsAdmin = true;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}