#pragma warning disable CS8603

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GigaChat.Server.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResultDto> RegisterAsync(UserRegistrationDto registerDto)
        {
            try
            {
                // Проверка, существует ли пользователь с таким email
                if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Пользователь с таким email уже существует"
                    };
                }

                // Проверка, существует ли пользователь с таким username
                if (await _context.Users.AnyAsync(u => u.UserName == registerDto.UserName))
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Пользователь с таким именем уже существует"
                    };
                }

                // Генерация соли и хеширование пароля
                var salt = BCrypt.Net.BCrypt.GenerateSalt();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password, salt);

                // Создание нового пользователя
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    UserName = registerDto.UserName,
                    Email = registerDto.Email,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    EmailConfirmed = false // Подтверждение по email
                };

                // Сохранение пользователя в БД
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                // Генерация JWT токена
                var token = GenerateJwtToken(user);

                return new AuthResultDto
                {
                    Success = true,
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        ProfilePictureUrl = user.ProfilePictureUrl,
                        LastActive = user.LastActive
                    },
                    Message = "Регистрация успешна"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации пользователя");
                return new AuthResultDto
                {
                    Success = false,
                    Message = "Произошла ошибка при регистрации"
                };
            }
        }

        public async Task<AuthResultDto> LoginAsync(UserLoginDto loginDto)
        {
            try
            {
                // Поиск пользователя по email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

                if (user == null)
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Пользователь с таким email не найден"
                    };
                }

                // Проверка пароля
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

                if (!isPasswordValid)
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Неверный пароль"
                    };
                }

                // Проверка подтверждения email (опционально)
                if (!user.EmailConfirmed)
                {
                    return new AuthResultDto
                    {
                        Success = false,
                        Message = "Email не подтвержден"
                    };
                }

                // Обновление времени последней активности
                user.LastActive = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Генерация JWT токена
                var token = GenerateJwtToken(user);

                return new AuthResultDto
                {
                    Success = true,
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        ProfilePictureUrl = user.ProfilePictureUrl,
                        LastActive = user.LastActive
                    },
                    Message = "Вход выполнен успешно"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе пользователя");
                return new AuthResultDto
                {
                    Success = false,
                    Message = "Произошла ошибка при входе"
                };
            }
        }

        public async Task<bool> RequestPasswordResetAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return false;
            }

            // Генерация токена сброса пароля
            var token = Guid.NewGuid().ToString();

            // Установка срока действия токена (24 часа)
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(24);

            await _context.SaveChangesAsync();

            // Здесь должна быть отправка email с токеном сброса пароля
            // TODO: Добавить сервис отправки email

            return true;
        }

        public async Task<bool> ResetPasswordAsync(string token, string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.ResetToken == token &&
                u.ResetTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return false;
            }

            // Хеширование нового пароля
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, salt);

            // Обновление пароля пользователя
            user.PasswordHash = passwordHash;
            user.Salt = salt;
            user.ResetToken = null;
            user.ResetTokenExpiry = null;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ConfirmEmailAsync(string token, string email)
        {
            // В реальном приложении здесь будет проверка валидности токена
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return false;
            }

            user.EmailConfirmed = true;
            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("username", user.UserName)
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserDto> GetUserByIdAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return null;

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl,
                LastActive = user.LastActive
            };
        }

        public async Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<UserDto>();

            var users = await _context.Users
                .Where(u => u.UserName.Contains(searchTerm) || u.Email.Contains(searchTerm))
                .Take(20)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    LastActive = u.LastActive
                })
                .ToListAsync();

            return users;
        }

        public async Task<UserDto> UpdateUserProfileAsync(Guid userId, UserDto userDto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return null;

            // Проверка на уникальность имени пользователя
            if (user.UserName != userDto.UserName)
            {
                var userWithSameName = await _context.Users.AnyAsync(u => u.UserName == userDto.UserName);
                if (userWithSameName)
                    throw new InvalidOperationException("Пользователь с таким именем уже существует");
            }

            user.UserName = userDto.UserName;
            user.ProfilePictureUrl = userDto.ProfilePictureUrl;
            user.LastActive = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                ProfilePictureUrl = user.ProfilePictureUrl,
                LastActive = user.LastActive
            };
        }

        public async Task<bool> UpdateUserPasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Проверка текущего пароля
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash);
            if (!isPasswordValid)
                return false;

            // Обновление пароля
            var salt = BCrypt.Net.BCrypt.GenerateSalt();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, salt);

            user.PasswordHash = passwordHash;
            user.Salt = salt;
            user.LastActive = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteUserAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Удаление пользователя из всех чатов
            var chatUsers = await _context.ChatUsers.Where(cu => cu.UserId == userId).ToListAsync();
            _context.ChatUsers.RemoveRange(chatUsers);

            // Удаление пользователя
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }

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

    public class FileService : IFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _uploadsFolder;
        private readonly ILogger<FileService> _logger;

        public FileService(ApplicationDbContext context, IConfiguration configuration, ILogger<FileService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;

            // Get uploads folder from config or use default
            _uploadsFolder = _configuration["FileStorage:UploadsFolder"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

            // Ensure the uploads directory exists
            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        public async Task<AttachmentDto> UploadFileAsync(IFormFile file, Guid userId)
        {
            try
            {
                // Validate user exists
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"Attempt to upload file by non-existent user: {userId}");
                    throw new ArgumentException("User not found");
                }

                // Validate file
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("File is empty");
                }

                // Create a unique file name to prevent collisions
                var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(_uploadsFolder, fileName);

                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create attachment record (without messageId initially)
                var attachment = new Attachment
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = file.ContentType,
                    FilePath = fileName, // Store only the filename, not the full path
                    FileSize = file.Length,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();

                // Return DTO with URL
                return new AttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    FileSize = attachment.FileSize,
                    Url = $"/api/files/{attachment.Id}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading file: {ex.Message}");
                throw;
            }
        }

        public async Task<Stream> GetFileAsync(Guid attachmentId, Guid userId)
        {
            try
            {
                // Find the attachment
                var attachment = await _context.Attachments
                    .Include(a => a.Message)
                    .ThenInclude(m => m.Chat)
                    .ThenInclude(c => c.ChatUsers)
                    .FirstOrDefaultAsync(a => a.Id == attachmentId);

                if (attachment == null)
                {
                    _logger.LogWarning($"Attachment not found: {attachmentId}");
                    return null;
                }

                // Check if user has access to this file (is part of the chat where the file was shared)
                if (attachment.Message != null)
                {
                    var hasAccess = attachment.Message.Chat.ChatUsers.Any(cu => cu.UserId == userId);
                    if (!hasAccess)
                    {
                        _logger.LogWarning($"User {userId} attempted to access unauthorized file {attachmentId}");
                        return null;
                    }
                }
                else
                {
                    // If the attachment isn't linked to a message yet, check if the user has temp access
                    // This is for newly uploaded files that haven't been attached to a message yet
                    // You might want to implement a better security mechanism here
                    var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
                    if (attachment.UploadedAt < fifteenMinutesAgo)
                    {
                        _logger.LogWarning($"User {userId} attempted to access temporary file {attachmentId} that has expired");
                        return null;
                    }
                }

                // Get file path and return stream
                var filePath = Path.Combine(_uploadsFolder, attachment.FilePath);
                if (!File.Exists(filePath))
                {
                    _logger.LogError($"File not found on disk: {filePath}");
                    return null;
                }

                return new FileStream(filePath, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving file: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(Guid attachmentId, Guid userId)
        {
            try
            {
                // Find the attachment
                var attachment = await _context.Attachments
                    .Include(a => a.Message)
                    .ThenInclude(m => m.Chat)
                    .ThenInclude(c => c.ChatUsers)
                    .FirstOrDefaultAsync(a => a.Id == attachmentId);

                if (attachment == null)
                {
                    return false;
                }

                // Check if user has permission to delete (creator of message or admin of chat)
                bool canDelete = false;

                if (attachment.Message != null)
                {
                    // If attached to a message, user must be sender or chat admin
                    canDelete = attachment.Message.SenderId == userId ||
                               (attachment.Message.Chat.ChatUsers
                                   .Any(cu => cu.UserId == userId && cu.IsAdmin));
                }
                else
                {
                    // If not attached to a message, user must be the uploader
                    // This is simplified; you might want a more sophisticated approach
                    var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
                    canDelete = attachment.UploadedAt > fifteenMinutesAgo;
                }

                if (!canDelete)
                {
                    _logger.LogWarning($"User {userId} attempted to delete unauthorized file {attachmentId}");
                    return false;
                }

                // Delete file from storage
                var filePath = Path.Combine(_uploadsFolder, attachment.FilePath);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Remove from database
                _context.Attachments.Remove(attachment);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {ex.Message}");
                return false;
            }
        }
    }
}