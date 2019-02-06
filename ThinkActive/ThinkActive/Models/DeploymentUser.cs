using System;
using Newtonsoft.Json.Linq;

namespace ThinkActive.Models
{
    public class DeploymentUser
    {
		public int id { get; set; }
        public int deploymentId { get; set; }
		public DeploymentDevice device { get; set; }

		public DeploymentUser(){}
		// TODO: Check that lastblocksynced can be 0
		public DeploymentUser(JToken deploymentUser) {
			id = (int)deploymentUser["id"];
            deploymentId = (int)deploymentUser["deploymentId"];
			device = new DeploymentDevice
			{
				id = (int)deploymentUser["deploymentDeviceId"],
				macAddress = deploymentUser["macAddress"].ToString().ToUpper().Replace(":", ""),
				password = "YOUR_AUTH_CODE",
			};

			UInt16 value = 0;
            if (UInt16.TryParse(deploymentUser["lastBlockSynced"].ToString(), out value))
                device.lastBlockSynced = value;

			UInt32 defaultRTC = 0;
			if (UInt32.TryParse(deploymentUser["lastRTC"].ToString(), out defaultRTC))
				device.lastRTC = defaultRTC;

            DateTimeOffset date = DateTime.Now;
            if (DateTimeOffset.TryParse(deploymentUser["lastSyncTime"].ToString(), out date))
                device.lastSyncTime = date;
		}
    }
}
