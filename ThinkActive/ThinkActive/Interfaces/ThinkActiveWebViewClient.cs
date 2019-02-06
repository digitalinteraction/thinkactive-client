using System;
using Android.Webkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThinkActive.Models;

namespace ThinkActive.Interfaces
{
	public class ThinkActiveWebViewClient : WebViewClient
    {
        public event EventHandler PageFinished;

        // API 21+
        public override void OnReceivedError(WebView view, IWebResourceRequest request, WebResourceError error)
        {
            ConsoleLog(view, error.ToString());
            base.OnReceivedError(view, request, error);
        }

        public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
        {
            view.LoadUrl(request.Url.ToString());
            return false;
        }

        public override void OnPageFinished(WebView view, string url)
        {
            PageFinished?.Invoke(this, null);
        }

        public void ConsoleLog(WebView view, JObject output)
        {
            view.EvaluateJavascript($"console.log('{ JsonConvert.SerializeObject(output) }');", null);
        }

        public void ConsoleLog(WebView view, String output)
        {
            view.EvaluateJavascript($"console.log('{ output }');", null);
        }
        
        public void DeviceJoined(WebView view, DeploymentUser deploymentUser)
        {
            try
            {
                JObject returnObject = new JObject();
                returnObject.Add("deploymentUserId", deploymentUser.id);
                this.ConsoleLog(view, returnObject);
                view.EvaluateJavascript($"window.onAndroidEvent('DeviceJoined', '{ JsonConvert.SerializeObject(returnObject) }');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }
        
        public void DeviceLeft(WebView view, DeploymentUser deploymentUser)
        {
            try
            {
                JObject returnObject = new JObject();
                returnObject.Add("deploymentUserId", deploymentUser.id);
                this.ConsoleLog(view, returnObject);
                view.EvaluateJavascript($"window.onAndroidEvent('DeviceLeft', '{ JsonConvert.SerializeObject(returnObject) }');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

        public void DeviceScanned(WebView view, DeploymentUser deploymentUser)
        {
            try
            {
                JObject returnObject = new JObject();
                returnObject.Add("deploymentUserId", deploymentUser.id);

                this.ConsoleLog(view, returnObject);
				Console.WriteLine($"window.onAndroidEvent('DeviceScanned', '{ JsonConvert.SerializeObject(returnObject) }');");
                view.EvaluateJavascript($"window.onAndroidEvent('DeviceScanned', '{ JsonConvert.SerializeObject(returnObject) }');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

        public void RecievedData(WebView view, DeviceSyncAttempt deviceSyncAttempt)
        {
            try
            {
                this.ConsoleLog(view, JsonConvert.SerializeObject(deviceSyncAttempt));
				Console.WriteLine($"window.onAndroidEvent('RecievedData', '{ JsonConvert.SerializeObject(deviceSyncAttempt) }');");


				view.EvaluateJavascript($"window.onAndroidEvent('RecievedData', '{ JsonConvert.SerializeObject(deviceSyncAttempt) }');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

		public void RequestDataFailed(WebView view)
        {
            try
            {
				this.ConsoleLog(view, "RequestDataFailed");
				view.EvaluateJavascript("window.onAndroidEvent('RequestDataFailed');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

		public void ExitTablet(WebView view)
        {
            try
            {            
				this.ConsoleLog(view, "ExitTablet");
				view.EvaluateJavascript("window.onAndroidEvent('ExitTablet');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }

		public void FoodScanned(WebView view, int foodId)
        {
            try
            {
				JObject returnObject = new JObject();
                returnObject.Add("foodId", foodId);

                this.ConsoleLog(view, returnObject);
				Console.WriteLine($"window.onAndroidEvent('FoodScanned', '{ JsonConvert.SerializeObject(returnObject) }');");
				view.EvaluateJavascript($"window.onAndroidEvent('FoodScanned', '{ JsonConvert.SerializeObject(returnObject) }');", null);
            }
            catch (Exception e)
            {
                Console.WriteLine($"JSONError: { e.ToString() }");
            }
        }
    }
}
