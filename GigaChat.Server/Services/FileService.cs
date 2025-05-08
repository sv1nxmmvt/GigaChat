#pragma warning disable CS8603

using GigaChat.Server.Data;
using GigaChat.Server.DTOs;
using GigaChat.Server.Interfaces;
using GigaChat.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GigaChat.Server.Services
{
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