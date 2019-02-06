using System;
namespace ThinkActive.Models
{
    public class DeviceSampleRecord
    {
		public int steps { get; set; }
		public int batteryLevel { get; set; }
		public DateTimeOffset recordedOn { get; set; }
    }
}
