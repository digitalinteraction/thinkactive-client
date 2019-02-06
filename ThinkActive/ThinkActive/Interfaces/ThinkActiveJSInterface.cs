using System;
using System.Collections.Generic;
using System.Linq;
using Android.Webkit;
using Java.Interop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThinkActive.Models;

namespace ThinkActive.Interfaces
{
	public class ThinkActiveJSInterface : Java.Lang.Object
    {
        public event EventHandler<DeploymentUser[]> OnSetDeploymentUsers;
        public event EventHandler<DeploymentUser> OnRequestData;
        public event EventHandler<String> OnVibrateDevice;

        [Export]
        [JavascriptInterface]
        public void SetDeploymentUsers(string deploymentUsers)
        {
            try
            {
                HashSet<DeploymentUser> deploymentUserHashSet = new HashSet<DeploymentUser>();
                
                JArray JSONDeploymentUsers = JArray.Parse(deploymentUsers);

                for (int i = 0; i < JSONDeploymentUsers.Count; i++)
                {
					deploymentUserHashSet.Add(new DeploymentUser(JSONDeploymentUsers[i]));
                }

                var users = deploymentUserHashSet.ToArray<DeploymentUser>();

                OnSetDeploymentUsers?.Invoke(this, users);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

        [Export]
        [JavascriptInterface]
        public void RequestData(string deploymentUser)
        {
            try
            {
				JObject JSONDeploymentUser = JObject.Parse(deploymentUser);
				DeploymentUser _deploymentUser = new DeploymentUser(JSONDeploymentUser);


				OnRequestData?.Invoke(this, _deploymentUser);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

        [Export]
        [JavascriptInterface]
        public void VibrateDevice(string macAddress)
        {
            try
            {
                OnVibrateDevice?.Invoke(this, macAddress.Replace(":", "").Replace("\"", ""));
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }
    }
}
