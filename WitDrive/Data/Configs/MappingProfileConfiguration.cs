using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WitDrive.Dto;
using WitDrive.Models;

namespace WitDrive.Data.Configs
{
    public class MappingProfileConfiguration : Profile
    {
        public MappingProfileConfiguration()
        {
            CreateMap<User, UserForListDto>();
            CreateMap<UserForRegisterDto, User>();
        }
    }
}
