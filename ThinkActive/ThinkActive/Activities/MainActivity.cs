using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Newtonsoft.Json;
using OpenMovement.AxLE.Comms;
using OpenMovement.AxLE.Comms.Bluetooth.Interfaces;
using OpenMovement.AxLE.Comms.Exceptions;
using OpenMovement.AxLE.Comms.Interfaces;
using OpenMovement.AxLE.Comms.Values;
using OpenMovement.AxLE.Service.Models;
using Plugin.BLE;
using ThinkActive.BroadcastReceivers;
using ThinkActive.Interfaces;
using ThinkActive.Models;
using ZXing.Mobile;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace ThinkActive
{
	[Activity(Label = "ThinkActive", MainLauncher = true, Icon = "@mipmap/icon", Theme = "@android:style/Theme.NoTitleBar.Fullscreen", LaunchMode = LaunchMode.SingleInstance)]
	public class MainActivity : global::Android.Support.V4.App.FragmentActivity
	{
		IList<DeploymentUser> _deploymentUsers;
       
		HashSet<string> _devicesNearby = new HashSet<string>();

		public static IBluetoothManager _bluetoothManager;
		public static IAxLEManager _axLEManager;
		ZXingScannerFragment _scanFragment;

		WebView _webView;
		ThinkActiveJSInterface _jsInterface;
		private ThinkActiveWebViewClient _webViewClient;
		string webViewUrl = "https://dev.thinkactive.io";

		private BluetoothManager _androidBluetoothManager;
		BluetoothBroadcastReceiver _bluetoothBroadcastReceiver;

        public ISet<string> _observedDevices = new HashSet<string>();

        private System.Timers.Timer _bluetoothToggleTimer;
        private DateTime _lastTimeBluetoothToggleChecked;
        private object _syncBluetoothToggle = new object();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
			SetContentView(Resource.Layout.activityMain);
           
			MobileBarcodeScanner.Initialize(Application);

			_webView = FindViewById<WebView>(Resource.Id.mainWebview);

			this.Window.SetFlags(WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn);

			AppCenter.Start("APP_CENTER_ID", typeof(Analytics), typeof(Crashes));

			try
			{
				// set up webview client
                _webViewClient = new ThinkActiveWebViewClient();
                _jsInterface = new ThinkActiveJSInterface();
                _webView.Settings.JavaScriptEnabled = true;
                _webView.AddJavascriptInterface(_jsInterface, "ThinkActiveApplication");
                _webView.SetWebViewClient(_webViewClient);
                _webView.Settings.SetAppCacheEnabled(false);
                WebView.SetWebContentsDebuggingEnabled(true);

                // Start Bluetooth
                _androidBluetoothManager = (BluetoothManager)GetSystemService(Context.BluetoothService);

                _bluetoothBroadcastReceiver = new BluetoothBroadcastReceiver();
                this.RegisterReceiver(_bluetoothBroadcastReceiver, new IntentFilter(BluetoothAdapter.ActionStateChanged));

                _deploymentUsers = new List<DeploymentUser>();

                // Listen for JSInterface events after page finished loading
                _webViewClient.PageFinished += (object sender, EventArgs e) => {};

                _jsInterface.OnSetDeploymentUsers += (object s, DeploymentUser[] deploymentUsers) =>
                {
                    _deploymentUsers = deploymentUsers;

                    foreach (var macAddress in _observedDevices)
                    {
                        DeploymentUser deploymentUser = _deploymentUsers.SingleOrDefault(du => du.device.macAddress == macAddress);
                        // if deploymentUser is found
                        if (deploymentUser != default(DeploymentUser))
                        {
                            RunOnUiThread(() =>
                            {
                                Console.WriteLine($"CONNECTION: SHOW AVATAR");
                                _webViewClient.DeviceJoined(_webView, deploymentUser);
                            });
                        }
                    }
                };

                // QR code scanned, webview is requesting device information from specific device
                _jsInterface.OnRequestData += async (object s, DeploymentUser deploymentUser) =>
                {
                    RunOnUiThread(() =>
                    {
                        _webViewClient.ConsoleLog(_webView, JsonConvert.SerializeObject(deploymentUser));
                    });

                    // try to connect to specfic device
                    if (deploymentUser != null)
                    {
                        // update the _deploymentUser list with the latest information
                        DeploymentUser outdatedDeploymentUser = _deploymentUsers.FirstOrDefault(dU => dU.id == deploymentUser.id);

                        if (outdatedDeploymentUser != null)
                        {
                            //var position = _deploymentUsers.IndexOf(outdatedDeploymentUser);
                            //_deploymentUsers[position] = deploymentUser;
                            outdatedDeploymentUser = deploymentUser;
                        }

                        await ConnectToDevice(deploymentUser);
                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            Toast.MakeText(ApplicationContext, "User not found", ToastLength.Short).Show();
                        });
                    }
                };

                _jsInterface.OnVibrateDevice += async (object s, string macAddress) =>
                {
                    RunOnUiThread(() =>
                    {
                        _webViewClient.ConsoleLog(_webView, $"OnVirbateDevice:{ macAddress }");
                    });

                    await VibrateDevice(macAddress);
                };

                // attach bluetooth receiver
                _bluetoothBroadcastReceiver.onStateChanged += async (object bluetoothSender, State state) => {
                    // check bluetooth state is off, restart if off
                    if (state == Android.Bluetooth.State.Off)
                    {
                        // turn back on
                        EnableBluetooth();
                    }
                    else if (state == Android.Bluetooth.State.On)
                    {
                        await SetupAxLEManager();
                    }
                };

                _webView.LoadUrl(webViewUrl);

                _bluetoothToggleTimer = new System.Timers.Timer
                {
                    Interval = 1000,
                    AutoReset = true
                };

                _bluetoothToggleTimer.Elapsed += (s,e) => {
                    lock (_syncBluetoothToggle)
                    {
                        var now = DateTime.Now;
                        DateTime[] resets = {
                            new DateTime(now.Year, now.Month, now.Day, 7, 00, 0),
                            new DateTime(now.Year, now.Month, now.Day, 12, 30, 0),
                        };
                        bool disable = false;
                        foreach (var reset in resets)
                        {
                            if (_lastTimeBluetoothToggleChecked != default(DateTime) && now >= reset && _lastTimeBluetoothToggleChecked < reset)
                            {
                                disable = true;
                            }
                        }
                        if (disable)
                        {
                            DisableBluetooth();
                        }
                        _lastTimeBluetoothToggleChecked = now;
                    }
                };

                _bluetoothToggleTimer.Start();
            }
			catch (Exception e) {
				Console.WriteLine(e.ToString());
			}
        }

		protected override void OnStart()
		{
			base.OnStart();
		}

		protected override async void OnResume()
        {
			base.OnResume();

            //// if bluetooth is enabled fire off enable ble request (caught by the bluetooth reciever which will
            //// then turn on the bluetooth device
            if (_androidBluetoothManager.Adapter.IsEnabled)
            {
                DisableBluetooth();
            }
            else
            {
                EnableBluetooth();
            }

            // refresh browser
            //_webView.LoadUrl("javascript:window.location.reload(true)");

            var needsPermissionRequest = ZXing.Net.Mobile.Android.PermissionsHandler.NeedsPermissionRequest(this);

            if (needsPermissionRequest)
                await ZXing.Net.Mobile.Android.PermissionsHandler.RequestPermissionsAsync(this);

            if (_scanFragment == null)
            {
                _scanFragment = new ZXingScannerFragment();

                SupportFragmentManager.BeginTransaction()
				                      .Replace(Resource.Id.mainBarcodeScannerFrameLayout, _scanFragment)
				                      .Commit();
				SupportFragmentManager.ExecutePendingTransactions();
            }

			if (!needsPermissionRequest) {
				StartScanningForQRCode();
			}
        }

        protected override void OnStop()
        {
            base.OnStop();
        }

        protected override async void OnPause()
        {
            base.OnPause();

            await TeardownBluetooth();
        }

		protected override void OnDestroy()
		{
			if (_webView != null)
				_webView.Destroy();
			base.OnDestroy();
		}

        protected async Task TeardownBluetooth()
        {
            if (_axLEManager != null)
            {
                await _axLEManager.StopScan();
                _axLEManager = null;
            }

            if (_bluetoothManager != null)
            {
                _bluetoothManager.Dispose();
                _bluetoothManager = null;
            }
        }

		public void EnableBluetooth()
        {
			_androidBluetoothManager.Adapter.Enable();
        }

        public async void DisableBluetooth()
        {
            await TeardownBluetooth();

            _androidBluetoothManager.Adapter.Disable();
        }

		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
		{
			global::ZXing.Net.Mobile.Android.PermissionsHandler.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		}

        async Task SetupAxLEManager()
		{
            if (_axLEManager != null)
			{
				await _axLEManager.StopScan();
			}

			if (_bluetoothManager != null) {
				_bluetoothManager.Dispose();
			}

			_bluetoothManager = new OpenMovement.AxLE.Comms.Bluetooth.Mobile.Android.BluetoothManager(CrossBluetoothLE.Current);
            _axLEManager = new AxLEManager(_bluetoothManager, 30000);

            _axLEManager.DeviceFound += (object sender, string macAddress) => {
                try
                {
                    //Console.WriteLine($"DeviceFound:{macAddress}");

                    _observedDevices.Add(macAddress);
                    DeploymentUser deploymentUser = _deploymentUsers.SingleOrDefault(du => du.device.macAddress == macAddress);

                    // if deploymentUser is found
                    if (deploymentUser != default(DeploymentUser))
                    {
                        RunOnUiThread(() =>
                        {
                            Console.WriteLine($"CONNECTION: SHOW AVATAR");
                            _webViewClient.DeviceJoined(_webView, deploymentUser);
                        });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.ToString());
					Crashes.TrackError(e);
                }
            };

            _axLEManager.DeviceLost += (object sender, string macAddress) =>
            {
                try
                {
                    _observedDevices.Remove(macAddress);
                    DeploymentUser deploymentUser = _deploymentUsers.SingleOrDefault(du => du.device.macAddress == macAddress);
                    // if deploymentUser is found
                    if (deploymentUser != default(DeploymentUser))
                    {
                        RunOnUiThread(() =>
                        {
                            Console.WriteLine($"CONNECTION: HIDE AVATAR");
                            _webViewClient.DeviceLeft(_webView, deploymentUser);
                        });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.ToString());
					Crashes.TrackError(e);
                }
            };

            _axLEManager.SwitchToHighPowerScan();
            _axLEManager.StartScan();
        }

		private void StartScanningForQRCode()
        {
            var opts = new MobileBarcodeScanningOptions
            {
                PossibleFormats = new List<ZXing.BarcodeFormat> {
                    ZXing.BarcodeFormat.QR_CODE
                },
                CameraResolutionSelector = availableResolutions => { return null; },
				AutoRotate = false,
				TryHarder = false,
				UseFrontCameraIfAvailable = true,
            };
            
            _scanFragment.StartScanning(result =>
			{
				// Null result means scanning was cancelled
				if (result == null || string.IsNullOrEmpty(result.Text))
				{
					Toast.MakeText(this, "Scanning Cancelled", ToastLength.Long).Show();
					return;
				}

				// Otherwise, proceed with result
				try
				{
					_scanFragment.PauseAnalysis();

                    var url = new Uri(result.Text);

                    // if we don't have a command, exit
					if (url.Segments.Length < 2)
                    {
                        throw new Exception("QR Code not recognised");
                    }
                    
					string command = url.Segments[1].Replace("/", "");

                    switch (command)
                    {
                        case "deploymentUser":
							// if the command isn't exit and we don't have an id to look for, exit
							if (url.Segments.Length != 3) {
								throw new Exception("Invalid QR code");
							}
                            
							int deploymentUserId = Int32.Parse(url.Segments[2]);
                            DeploymentUser deploymentUser = _deploymentUsers.SingleOrDefault(du => du.id == deploymentUserId);

                            if (deploymentUser == null)
                            {
                                throw new Exception("Invalid QR code");
                            }
                            else
                            {
                                RunOnUiThread(() =>
                                {
                                    _webViewClient.DeviceScanned(_webView, deploymentUser);
                                });
                            }
                            break;
                        case "food":
							if (url.Segments.Length != 3)
                            {
                                throw new Exception("Invalid QR code");
                            }

							int foodId = Int32.Parse(url.Segments[2]);
                           
                            // fire off food event
							RunOnUiThread(() =>
                            {
							    _webViewClient.FoodScanned(_webView, foodId);
                            });
                            break;
                        case "exit":
							// fire off exit event
							RunOnUiThread(() =>
                            {
								_webViewClient.ExitTablet(_webView);
								_webView.LoadUrl("javascript:location.reload(true)");
                            });
                            break;
                        default:
                            throw new Exception("QR code command not found");
                    }
				}
				catch (Exception e)
				{
					RunOnUiThread(() =>
					{
						Toast.MakeText(this, "Oops I can't understand that", ToastLength.Long).Show();
					});
					Crashes.TrackError(e);
				}

				_scanFragment.ResumeAnalysis();
			}, opts);
		}

        private async Task VibrateDevice(string macAddress)
        {
            IAxLE device = null;
            try
            {
                Console.WriteLine($"VIBRATE_DEVICE: ConnectDevice()");

                for (var retry = 0; ; retry++)
                {
                    try
                    {
                        device = await _axLEManager.ConnectDevice(macAddress);
                        break;
                    }
                    catch (OpenMovement.AxLE.Comms.Exceptions.CommandFailedException e)
                    {
                        Console.WriteLine($"VIBRATE_DEVICE: GATT error, retry {retry}");
                        Crashes.TrackError(e);
                        if (retry > 10)
                            throw;
                    }
                    catch (OpenMovement.AxLE.Comms.Exceptions.ConnectException e)
                    {
                        Console.WriteLine($"VIBRATE_DEVICE: ConnectException error, retry {retry}");
                        Crashes.TrackError(e);
                        if (retry > 10)
                            throw;
                    }
                }

                // try auth with known password
                Console.WriteLine($"VIBRATE_DEVICE: Authenticate()");
                bool authSuccess = await device.Authenticate("YOUR_AUTH_CODE");

                // if fails, reset device
                if (authSuccess)
                {
                    // buzz device
                    Console.WriteLine($"FLASH AND BUZZ START:{ DateTime.Now }");
                    //await device.LEDFlash();
                    await device.VibrateDevice();
                    Console.WriteLine($"FLASH AND BUZZ END:{ DateTime.Now }");
                }
            }
            catch (BlockSyncFailedException e)
            {
                // move read head to active block
                Console.WriteLine("BlockSyncFailedException:" + e.ToString());
                Crashes.TrackError(e);
            }
            catch (DeviceNotInRangeException e)
            {
                // display not in range
                Console.WriteLine("DeviceNotInRangeException:" + e.ToString());
                Crashes.TrackError(e);
            }
            catch (CommandFailedException e)
            {
                // display not in range
                Console.WriteLine("Command Failed Exception:" + e.ToString());
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception:" + e.ToString());
                Crashes.TrackError(e);
            }
            finally
            {
                if (device != null)
                {
                    await _axLEManager.DisconnectDevice(device);
                }
            }
        }

		private async Task ConnectToDevice(DeploymentUser dUser)
		{
			DeploymentUser deploymentUser = _deploymentUsers.SingleOrDefault(du => du.device.macAddress == dUser.device.macAddress);

			// if deploymentUser is found
			if (deploymentUser != default(DeploymentUser))
			{
				IAxLE device = null;
				try
				{
					await _axLEManager.StopScan();
					Console.WriteLine($"CONNECTION: ConnectDevice()");

					for (var retry = 0; ; retry++)
					{
						try
						{
							device = await _axLEManager.ConnectDevice(deploymentUser.device.macAddress);
							break;
						}
						catch (OpenMovement.AxLE.Comms.Exceptions.CommandFailedException e)
						{
							Console.WriteLine($"CONNECTION: GATT error, retry {retry}");
							Crashes.TrackError(e);
							if (retry > 10)
								throw;
						}
						catch (OpenMovement.AxLE.Comms.Exceptions.ConnectException e)
						{
							Console.WriteLine($"CONNECTION: ConnectException error, retry {retry}");
							Crashes.TrackError(e);
                            if (retry > 10)
                                throw;
						}
					}

					// try auth with known password
					Console.WriteLine($"CONNECTION: Authenticate()");
					bool authSuccess = await device.Authenticate(deploymentUser.device.password);
                    
					// if fails, reset device
					if (!authSuccess)
					{
						Console.WriteLine($"CONNECTION: ResetPassword()");
						await device.ResetPassword();
						Console.WriteLine($"CONNECTION: Authenticate()");
						await device.Authenticate(device.SerialNumber.Substring(device.SerialNumber.Length - 6));
						Console.WriteLine($"CONNECTION: SetPassword()");
						await device.SetPassword(deploymentUser.device.password);
						deploymentUser.device.lastBlockSynced = null;
						deploymentUser.device.lastRTC = null;
						deploymentUser.device.lastSyncTime = null;
					}

					// buzz device
					Console.WriteLine($"FLASH AND BUZZ START:{ DateTime.Now }");
					//await device.LEDFlash();
					await device.VibrateDevice();
					Console.WriteLine($"FLASH AND BUZZ END:{ DateTime.Now }");
                    
					// HACK: Use this butchered version to skip updating cueing
					//await ((OpenMovement.AxLE.Comms.AxLEv1_5)device).UpdateDeviceState((uint)(0xffffffff & ~0x0010));   // Don't update cueing status
					await device.UpdateDeviceState();

                    // proceed with sync and update deployment user
                    if (device.EpochPeriod != 300)
                    {
                        device.EpochPeriod = 300; // 5 minutes (300 seconds)
                    }

                    if (!deploymentUser.device.lastBlockSynced.HasValue || !deploymentUser.device.lastRTC.HasValue || !deploymentUser.device.lastSyncTime.HasValue)
					{
						BlockDetails blockDetails = await device.ReadBlockDetails();
						deploymentUser.device.lastBlockSynced = blockDetails.ActiveBlock;
						deploymentUser.device.lastRTC = blockDetails.Time;
						deploymentUser.device.lastSyncTime = DateTime.UtcNow;
						// send off server request to update these details
						DeviceSyncAttempt _deviceSyncAttempt = new DeviceSyncAttempt
						{
							deploymentUserId = deploymentUser.id,
							batteryLevel = device.Battery,
							deploymentDeviceId = deploymentUser.device.id,
							samples = new DeviceSampleRecord[0],
							lastBlockSynced = (blockDetails.ActiveBlock),
							lastRTC = blockDetails.Time,
							lastSyncTime = DateTime.UtcNow,
							raw = new byte[0][]
						};
						RunOnUiThread(() =>
						{
							_webViewClient.RecievedData(_webView, _deviceSyncAttempt);
						});
                        
                        WriteSyncAttemptToDisk(deploymentUser.deploymentId, deploymentUser.device.macAddress, _deviceSyncAttempt);
					}
					else
					{
						var start = DateTime.Now;
						// we have data on the device, sync the data using previous deploymentUser.device...
						Console.WriteLine($"BLOCK READ START:{ DateTime.Now }");
						var blocks = await device.SyncEpochData((UInt16)deploymentUser.device.lastBlockSynced, (UInt32)deploymentUser.device.lastRTC, (DateTimeOffset)deploymentUser.device.lastSyncTime);
						Console.WriteLine($"BLOCK READ END:{ DateTime.Now }");
						var end = DateTime.Now;

						/* 
						 * {
						 *      deploymentUserId: 15,
						 *      batteryLevel: 15,
						 *      deploymentDeviceId: 1,
						 *      epochInterval: 60000
						 *      samples: [
						 *          {
						 *              steps: 123,
						 *              batteryLevel: 15
						 *              recordedOn: 2018-05-31 14:23:01Z4
						 *          }
						 *      ],
						 *      raw: "RAW BYTES FROM SYNC"
						 * }
						 */
						DeviceSampleRecord[] samples = blocks.SelectMany(b =>
						{
							var deviceSampleRecords = new List<DeviceSampleRecord>();
							ulong samplesLength = (ulong)b.Samples.Length;
							for (ulong i = 0; i < samplesLength; i++)
							{
								deviceSampleRecords.Add(new DeviceSampleRecord()
								{
									steps = b.Samples[i].Steps,
									batteryLevel = b.Samples[i].Battery,
									recordedOn = b.BlockInfo.Timestamp.AddSeconds(i * b.BlockInfo.EpochPeriod)
								});
							}

							return deviceSampleRecords;
						}).ToArray();

						var lastBlock = blocks.LastOrDefault();

						var lastBlockNumber = lastBlock == default(EpochBlock) ?
							deploymentUser.device.lastBlockSynced : lastBlock.BlockInfo.BlockNumber;

						DeviceSyncAttempt _deviceSyncAttempt = new DeviceSyncAttempt
						{
							deploymentUserId = deploymentUser.id,
							batteryLevel = device.Battery,
							deploymentDeviceId = deploymentUser.device.id,
							samples = samples,
							lastBlockSynced = (ushort)lastBlockNumber,
							lastRTC = device.DeviceTime,
							lastSyncTime = DateTime.UtcNow,
							raw = blocks.Select(b => b.Raw).ToArray()
						};

						WriteSyncAttemptToDisk(deploymentUser.deploymentId, deploymentUser.device.macAddress, _deviceSyncAttempt);

						// save locally
						// post data off to tablet
						RunOnUiThread(() =>
						{
							_webViewClient.RecievedData(_webView, _deviceSyncAttempt);
						});
					}
				}
				catch (BlockSyncFailedException e)
				{
					// move read head to active block
					Console.WriteLine("BlockSyncFailedException:" + e.ToString());
					Crashes.TrackError(e);
					RunOnUiThread(() =>
                    {
						_webViewClient.RequestDataFailed(_webView);
                    });
				}
				catch (DeviceNotInRangeException e)
				{
					// display not in range
					Console.WriteLine("DeviceNotInRangeException:" + e.ToString());
					Crashes.TrackError(e);
					RunOnUiThread(() =>
                    {
                        _webViewClient.RequestDataFailed(_webView);
                    });
				}
				catch (CommandFailedException e)
				{
					// display not in range
                    Console.WriteLine("Command Failed Exception:" + e.ToString());
					Crashes.TrackError(e);
                    RunOnUiThread(() =>
                    {
                        _webViewClient.RequestDataFailed(_webView);
                    });
				}
				catch (Exception e)
				{
					Console.WriteLine("Exception:" + e.ToString());
					Crashes.TrackError(e);
					// restart this process
					RunOnUiThread(() =>
                    {
                        _webViewClient.RequestDataFailed(_webView);
                    });
				}
				finally
				{
					if (device != null)
					{
						await _axLEManager.DisconnectDevice(device);
					}

					//DisableBluetooth();
				}
			}
			else
			{
				Toast.MakeText(ApplicationContext, "Could not connect to your activity tracker", ToastLength.Short).Show();
			}
		}

        private void WriteSyncAttemptToDisk(int deploymentId, string mac, DeviceSyncAttempt attempt)
        {
			try
			{
				Java.IO.File sdCard = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads);
                Java.IO.File dir = new Java.IO.File($"{ sdCard.AbsolutePath }/ThinkActive/Deployments/{deploymentId.ToString()}/{attempt.deploymentUserId}/{mac}/");


                if (!dir.Exists()) {
					dir.Mkdirs();	
				}
                
				Java.IO.File file = new Java.IO.File(dir, $"{ (DateTime.UtcNow.ToString("s") + "Z").Replace(':', '-') + "-" + attempt.deploymentUserId.ToString() + "-" + mac}.json");

				if (!file.Exists())
                {
					Java.IO.FileWriter writer = new Java.IO.FileWriter(file);
					writer.Write(JsonConvert.SerializeObject(attempt));
                    writer.Flush();
                    writer.Close();
                }
			}
			catch (Exception e) {
				Console.WriteLine(e.ToString());
				Crashes.TrackError(e);
			}
        }
    }
}

