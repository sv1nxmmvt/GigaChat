#pragma warning disable CS8618

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GigaChat.Server.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace GigaChat.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var sub = User.FindFirst("sub")?.Value;
                return sub != null ? Guid.Parse(sub) : (Guid?)null;
            }
        }

        public class UploadFileDto
        {
            [Required]
            public IFormFile File { get; set; }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadFileDto dto)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var attachment = await _fileService.UploadFileAsync(dto.File, userId.Value);
            return Ok(attachment);
        }

        [HttpGet("{attachmentId}")]
        public async Task<IActionResult> Get(Guid attachmentId)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var stream = await _fileService.GetFileAsync(attachmentId, userId.Value);
            if (stream == null)
                return Forbid();

            return File(stream, "application/octet-stream");
        }

        [HttpDelete("{attachmentId}")]
        public async Task<IActionResult> Delete(Guid attachmentId)
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            var success = await _fileService.DeleteFileAsync(attachmentId, userId.Value);
            if (!success)
                return Forbid();

            return NoContent();
        }
    }
}
