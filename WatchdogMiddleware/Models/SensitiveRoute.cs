using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchdogMiddleware.Models
{
    public class SensitiveRoute
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public bool Encrypt { get; set; }
        public bool DoNotLog { get; set; }
    }
}
