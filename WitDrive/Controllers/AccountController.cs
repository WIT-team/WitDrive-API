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
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly IConfiguration config;
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

        [Authorize]
        [HttpGet("/u/{userId}/space-info")]
        public async Task<IActionResult> Space(int userId)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var usedDiskSpace = await fsc.AccessControl.CalculateDiskUsageAsync(userId.ToString());

            JObject jObject = new JObject();
            jObject["Used"] = usedDiskSpace;
            jObject["Available"] = space;
            jObject["Left"] = space - usedDiskSpace;

            return Ok(jObject.ToString());
        }

        //[HttpPost("forgot-password")]
        //public async Task<IActionResult> ForgotPassword(ForgotPasswordDto forgotPasswordDto)
        //{
        //    var user = await userManager.FindByEmailAsync(forgotPasswordDto.Email);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    var token = await userManager.GeneratePasswordResetTokenAsync(user);
        //    var callback = Url.RouteUrl("ResetPasswordModel", new { token, email = user.Email }, Request.Scheme);

        //    var message = new Message(new string[] { forgotPasswordDto.Email }, "Link to reset your password", callback);
        //    await emailSender.SendEmailAsync(message);

        //    return Ok();
        //}

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ResetPassword(ResetPasswordDto resetPasswordDto)
        //{

        //    var user = await userManager.FindByEmailAsync(resetPasswordDto.Email);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    var result = await userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
        //    if (!result.Succeeded)
        //    {
        //        return BadRequest(result.Errors);
        //    }

        //    return Ok();
        //}

        //[HttpPost("reset-password")]
        //public async Task<IActionResult> ResetPassword([FromQuery] ResetPasswordDto resetPasswordDto)
        //{
        //    var user = await userManager.FindByEmailAsync(resetPasswordDto.Email);
        //    if (user == null)
        //    {
        //        return NotFound();
        //    }

        //    var result = await userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.Password);
        //    if (!result.Succeeded)
        //    {
        //        return BadRequest(result.Errors);
        //    }

        //    return Ok();
        //}

        //[HttpGet("reset-password-model", Name = "ResetPasswordModel")]
        //public IActionResult ResetPassword(string token, string email)
        //{
        //    var model = new ResetPasswordDto { Token = token, Email = email };
        //    return Ok(model);
        //}

        [HttpPost("u/{userId}/edit-password")]
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