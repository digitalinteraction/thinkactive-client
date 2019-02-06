using System;
namespace ThinkActive.Models
{
    public class DeviceSyncAttempt
    {
		public int deploymentUserId { get; set; }
		public int batteryLevel { get; set; }
		public int deploymentDeviceId { get; set; }
		public DeviceSampleRecord[] samples { get; set; }
		public byte[][] raw { get; set; }
		public UInt16 lastBlockSynced { get; set; }
        public UInt32 lastRTC { get; set; }
        public DateTimeOffset? lastSyncTime { get; set; }
    }
}
