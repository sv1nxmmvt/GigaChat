#pragma warning disable CS8603

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GigaChat.Server.Services
{
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
}