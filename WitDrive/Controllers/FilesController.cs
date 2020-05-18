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

namespace WitDrive.Controllers
{
    [Route("api/{userId}/[controller]")]
    [ApiController]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IFilesService filesService;
        private readonly FileRepository repo;
        public FilesController(IFilesService filesService)
        {
            this.repo = new FileRepository("mongodb+srv://App:8aOnnxaohkXlFyZn@witdbcluster0-cchqy.mongodb.net/test?retryWrites=true&w=majority");
            this.filesService = filesService;
        }
        
        [HttpPost("upload")]
        public async Task<IActionResult> FileUpload(int userId, string directoryId, [FromForm] IFormFile file)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }
            byte[] essa = filesService.ConvertToByteArray(file);
            if (repo.UploadFile(Convert.ToString(userId), directoryId, file.FileName, essa))
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

            //var res = await repo.DownloadFileAsync(Convert.ToString(userId), fileId);

            //if (res.success)
            //{
            //    string fileName = res.result.Key;
            //    byte[] data = res.result.Value;
            //    return File(data, fileName.Split('.').Last());
            //}
            byte[] data;
            if (repo.DownloadFile(Convert.ToString(userId), fileId, out data))
            {
                //HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                return File(data, MimeTypes.GetMimeType("plikWord.docx"));
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

            if (await repo.DeleteFileAsync(Convert.ToString(userId), fileId))
            {
                return Ok();
            }

            return BadRequest("Failed to delete file");
        }
    }
}
