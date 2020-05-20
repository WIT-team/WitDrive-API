using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WitDrive.Interfaces;
using MDBFS_Lib;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace WitDrive.Controllers
{
    [Route("api/u/{userId}/[controller]")]
    [ApiController]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IConfiguration config;
        private readonly IFilesService filesService;
        private readonly FileRepository repo;
        public FilesController(IFilesService filesService, IConfiguration config)
        {
            this.config = config;
            this.filesService = filesService;
            this.repo = new FileRepository(config.GetConnectionString("MongoDbConnection"));
        }

        [HttpGet("root")]
        public async Task<IActionResult> GetRootDir(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var res = await repo.GetRootDirectoryAsync(Convert.ToString(userId));

            if (res.success)
            {
                return Ok(res.result);
            }

            return BadRequest("Failed to retrieve root data");
        }

        [HttpPost("upload")]
        public async Task<IActionResult> FileUpload(int userId, string directoryId, [FromForm] IFormFile file)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var res = await repo.UploadFileAsyncReturnCode(Convert.ToString(userId), directoryId, file.FileName, filesService.ConvertToByteArray(file));

            if (res.success)
            {
                return Ok();
            }

            return BadRequest("Failed to upload file");
        }

        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> FileDownload(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            MDBFS_Lib.Util.AsyncResult<KeyValuePair<string, byte[]>?> res = await repo.DownloadFileAsync(Convert.ToString(userId), fileId);

            if (res.success)
            {
                string fileName = res.result.Value.Key;
                byte[] data = res.result.Value.Value;
                return File(data, MimeTypes.GetMimeType(fileName), fileName);
            }

            return BadRequest("Failed to download file");
        }

        [HttpPatch("share/{fileId}")]
        public async Task<IActionResult> EnableFileSharing(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var res = await repo.EnableFileSharingAsync(Convert.ToString(userId), fileId);

            if (res.success)
            {
                return Ok();
            }

            return BadRequest("Failed to share file");
        }

        [HttpPatch("disable-sharing/{fileId}")]
        public async Task<IActionResult> DisableFileSharing(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var res = await repo.DisableFileSharingAsyncReturnCode(Convert.ToString(userId), fileId);

            if (res.success)
            {
                return Ok();
            }

            return BadRequest("Failed to disable file sharing");
        }

        [HttpDelete("{fileId}")]
        public async Task<IActionResult> FileDelete(int userId, string fileId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var res = await repo.DeleteFileAsyncReturnCode(Convert.ToString(userId), fileId);

            if (res.success)
            {
                return Ok();
            }

            return BadRequest("Failed to delete file");
        }
    }
}
