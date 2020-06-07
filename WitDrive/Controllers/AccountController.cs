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

namespace WitDrive.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IEmailSender emailSender;
        private readonly UserManager<User> userManager;

        public AccountController(IEmailSender emailSender, UserManager<User> userManager)
        {
            this.emailSender = emailSender;
            this.userManager = userManager;
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