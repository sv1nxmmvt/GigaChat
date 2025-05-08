using GigaChat.Server.DTOs;

namespace GigaChat.Server.Interfaces
{
    public interface IFileService
    {
        Task<AttachmentDto> UploadFileAsync(IFormFile file, Guid userId);
        Task<Stream> GetFileAsync(Guid attachmentId, Guid userId);
        Task<bool> DeleteFileAsync(Guid attachmentId, Guid userId);
    }
}
