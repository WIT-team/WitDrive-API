using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Models
{
    public class ShareMap
    {
        public string ShareId { get; set; }
        public string ElementId { get; set; }
        public byte Type { get; set; }
        public bool Active { get; set; }
    }
}
