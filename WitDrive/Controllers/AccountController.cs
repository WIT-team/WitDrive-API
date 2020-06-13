using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WitDrive.Dto;
using WitDrive.Interfaces;
using WitDrive.Models;
using Microsoft.AspNetCore.Http;
using MDBFS_Lib;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Configuration;
using MDBFS.Filesystem;
using MDBFS.Misc;
using Newtonsoft.Json.Linq;
using WitDrive.Infrastructure.Extensions;


namespace WitDrive.Controllers
{
    [Authorize]
    [Route("api/u/{userId}/[controller]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly IEmailSender emailSender;
        private readonly UserManager<User> userManager;
        private readonly FileSystemClient fsc;
        private readonly long space;
        public AccountController(IEmailSender emailSender, UserManager<User> userManager, IConfiguration config)
        {
            this.emailSender = emailSender;
            this.userManager = userManager;
            var mongoClient = new MongoDB.Driver.MongoClient(config.GetConnectionString("MongoDbConnection"));
            var database = mongoClient.GetDatabase(nameof(WitDrive));
            this.space = long.Parse(config.GetSection("DiskSpace").GetSection("Space").Value);
            this.fsc = new FileSystemClient(database, chunkSize: 32768);
        }

        [HttpGet("available-space")]
        public async Task<IActionResult> AvailableSpace(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var usedDiskSpace = await fsc.AccessControl.CalculateDiskUsageAsync(userId.ToString());

            JObject jObject = new JObject()
            {
                ["Used"] = usedDiskSpace,
                ["Available"] = space,
                ["Left"] = space - usedDiskSpace
            };

            return Ok(jObject.ToString());
        }

        [HttpPost("edit-password")]
        public async Task<IActionResult> ResetPassword(EditPasswordDto editPasswordDto, int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            User appUser = await userManager.GetUserAsync(this.User);
            var result = await userManager.ChangePasswordAsync(appUser, editPasswordDto.OldPassword, editPasswordDto.NewPassword);

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(result.Errors);
        }
    }
}