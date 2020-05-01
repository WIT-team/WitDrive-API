using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Models;

namespace WitDrive.Data
{
    public static class PopulateDb
    {
        public static void PopulateDbWithUsers(UserManager<User> userManager, RoleManager<Role> roleManager)
        {
            if (!userManager.Users.Any())
            {
                var userData = System.IO.File.ReadAllText("Data/PopulateData.json");
                var users = JsonConvert.DeserializeObject<List<User>>(userData);

                //create roles
                var roles = new List<Role>
                {
                    new Role {Name = "Member"},
                };

                foreach (var role in roles)
                {
                    roleManager.CreateAsync(role).Wait();
                }

                foreach (var user in users)
                {
                    userManager.CreateAsync(user, "L0ngP@$$w0rd").Wait();
                    userManager.AddToRoleAsync(user, "Member").Wait();
                }
            }
        }
    }
}
