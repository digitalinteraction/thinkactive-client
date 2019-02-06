using System;
using Android.App;
using Android.Bluetooth;
using Android.Content;

namespace ThinkActive.BroadcastReceivers
{
	[BroadcastReceiver(Enabled = true)]
	[IntentFilter(new[] { BluetoothAdapter.ActionStateChanged })]
	public class BluetoothBroadcastReceiver : Android.Content.BroadcastReceiver
	{
		public event EventHandler<Android.Bluetooth.State> onStateChanged;
        public static int? mLastState;

        public BluetoothBroadcastReceiver()
        {
        }

		public override void OnReceive(Context context, Intent intent)
        {
			String action = intent.Action;
            int state = intent.GetIntExtra(BluetoothAdapter.ExtraState, -1);

            // if this is the first run then set the current state as last state
            if (mLastState == null)
            {
                mLastState = state;
            }

            // prevent the same state change being propagated twice
            if (!state.Equals(mLastState))
            {
                mLastState = state;
                invokeStateChange(intent);
            }
        }

        private void invokeStateChange(Intent intent) {
            onStateChanged?.Invoke(this, (Android.Bluetooth.State)intent.GetIntExtra(BluetoothAdapter.ExtraState, BluetoothAdapter.Error));
        }
    }
}
