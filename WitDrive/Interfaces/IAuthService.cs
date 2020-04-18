using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Models;

namespace WitDrive.Interfaces
{
    public interface IAuthService
    {
        Task<string> GenerateJwtToken(User user, UserManager<User> userManager, IConfiguration config);
    }
}
