using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WitDrive.Models
{
    public class SharedFile
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }
        public int Size { get; set; }
        public bool Shared { get; set; }
        public string ShareID { get; set; }
    }
}
