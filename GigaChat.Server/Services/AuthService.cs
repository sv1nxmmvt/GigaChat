#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8625

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Interfaces;
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
}