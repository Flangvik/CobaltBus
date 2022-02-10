using System;
using System.Collections.Generic;
using System.Text;

namespace CobaltBus.Models
{
    public class Beacon
    {

        public int Id { get; set; }
        public string BeaconId { get; set; }
        public string Queue { get; set; }
        public bool Active { get; set; }
    }
}
