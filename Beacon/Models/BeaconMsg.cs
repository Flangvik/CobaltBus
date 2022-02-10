using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Models
{
    public class BeaconMsg
    {
        public int Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Command { get; set; }
        public string Payload { get; set; }
        public string Queue { get; set; }
    }
}
