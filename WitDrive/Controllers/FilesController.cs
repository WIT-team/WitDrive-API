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
                return File(data, MimeTypes.GetMimeType(fileName));
            }

            return BadRequest("Failed to download file");
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
