using System;
namespace ThinkActive.Models
{
    public class DeploymentDevice
    {
		public int id { set; get; }
		public string macAddress { get; set; }
		public string password { get; set; }
		public UInt16? lastBlockSynced { get; set; }
		public UInt32? lastRTC { get; set; }
		public DateTimeOffset? lastSyncTime { get; set; }
		public byte[][] raw { get; set; }
    }
}
