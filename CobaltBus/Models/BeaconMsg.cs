using System;
using System.Collections.Generic;
using System.Text;

namespace CobaltBus.Models
{
    public class BeaconMsg
    {
        public int Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Command { get; set; }
        public string Queue { get; set; }
        public string Payload { get; set; }
    }
}

