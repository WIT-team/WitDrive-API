using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Infrastructure.Helpers
{
    public class RequestValidation
    {
        public static bool IsRequestValid<T>(T obj) where T : class
        {
            foreach (var property in obj.GetType().GetProperties())
            {
                if (property.GetValue(obj, null) == null)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
