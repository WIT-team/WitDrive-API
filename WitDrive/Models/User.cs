using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Models
{
    public class User : IdentityUser<int>
    {
        public virtual ICollection<UserRole> UserRoles { get; set; }
    }
}
