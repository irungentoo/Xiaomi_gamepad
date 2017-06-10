using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HidLibrary;
using System.Windows.Forms;
using ScpDriverInterface;
using System.Threading;
using System.Runtime.InteropServices;

namespace mi
{
	class Program
	{
		private static ScpBus global_scpBus;

		static bool ConsoleEventCallback(int eventType)
		{
			if (eventType == 2)
			{
				global_scpBus.UnplugAll();
			}
			return false;
		}

		static ConsoleEventDelegate handler; // Keeps it from getting garbage collected
																				 // Pinvoke
		private delegate bool ConsoleEventDelegate(int eventType);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);



		public static string ByteArrayToHexString(byte[] bytes)
		{
			return string.Join(string.Empty, Array.ConvertAll(bytes, b => b.ToString("X2")));
		}

		static Mutex singleInstanceMutex = new Mutex(true, "{298c40ea-b004-4a7f-9910-d3bf3591b18b}");

		[STAThreadAttribute]
		static void Main(string[] args)
		{
			if (!IsSingleInstance()) Environment.Exit(0);
			NIcon = new NotifyIcon();
			ScpBus scpBus = new ScpBus();
			scpBus.UnplugAll();
			global_scpBus = scpBus;

			handler = new ConsoleEventDelegate(ConsoleEventCallback);
			SetConsoleCtrlHandler(handler, true);

			Thread.Sleep(400);
			var controllersManager = new Thread(() => ManageControllers(scpBus));


			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			try
			{
				try
				{
					using (var pi = new ProcessIcon())
					{
						pi.Display();
						controllersManager.Start();
						Application.Run();
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "Program Terminated Unexpectedly",
						MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				controllersManager.Abort();
				scpBus.UnplugAll();
				foreach (var device in Gamepads.Select(g => g.Device))
				{
					device.CloseDevice();
				}
				singleInstanceMutex.ReleaseMutex();
			}
			finally
			{
				Environment.Exit(0);
			}
		}

		public static NotifyIcon NIcon { get; set; }

		private static bool IsSingleInstance()
		{
			if (singleInstanceMutex.WaitOne(TimeSpan.Zero, true))
			{
				return true;
			}
			else
			{
				MessageBox.Show("Another copy is already running");
				return false;
			}
		}

		private static void ManageControllers(ScpBus scpBus)
		{
			var nrConnected = 0;
			while (true)
			{
				var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
				var existingDevices = Gamepads.Select(g => g.Device).ToList();
				var newDevices = compatibleDevices.Where(d => !existingDevices.Select(e => e.DevicePath).Contains(d.DevicePath));
				foreach (var gamepad in Gamepads.ToList())
				{
					if (!gamepad.check_connected())
					{
						gamepad.unplug();
						Gamepads.Remove(gamepad);
					}
				}
				foreach (var deviceInstance in newDevices)
				{
					var device = deviceInstance;
					try
					{
						device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
					}
					catch
					{
						InformUser("Could not open gamepad in exclusive mode. Try reconnecting the device.");
						var instanceId = devicePathToInstanceId(deviceInstance.DevicePath);
						if (TryReEnableDevice(instanceId))
						{
							try
							{
								device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
								//InformUser("Opened in exclusive mode.");
							}
							catch
							{
								device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
								//InformUser("Opened in shared mode.");
							}
						}
						else
						{
							device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
							//InformUser("Opened in shared mode.");
						}
					}

					byte[] vibration = { 0x20, 0x00, 0x00 };
					if (device.WriteFeatureData(vibration) == false)
					{
						InformUser("Could not write to gamepad (is it closed?), skipping");
						device.CloseDevice();
						continue;
					}

					byte[] serialNumber;
					byte[] product;
					device.ReadSerialNumber(out serialNumber);
					device.ReadProduct(out product);


					var usedIndexes = Gamepads.Select(g => g.Index);
					var index = 1;
					while (usedIndexes.Contains(index))
					{
						index++;
					}
					Gamepads.Add(new Xiaomi_gamepad(device, scpBus, index));
				}
				if (Gamepads.Count != nrConnected)
				{
					InformUser($"{Gamepads.Count} controllers connected");
				}
				Thread.Sleep(1000);
			}
		}

		private static void InformUser(string text)
		{
			NIcon.Text = "Export Datatable Utlity";
			NIcon.Visible = true;
			NIcon.BalloonTipTitle = "Mi controller";
			NIcon.BalloonTipText = text;
			NIcon.ShowBalloonTip(100);
			//var content = new ToastContent()
			//{
			//	Visual = new ToastVisual
			//	{
			//		BindingGeneric = new ToastBindingGeneric()
			//		{
			//			AppLogoOverride = new ToastGenericAppLogo
			//			{
			//				HintCrop = ToastGenericAppLogoCrop.Circle,
			//				Source = "http://messageme.com/lei/profile.jpg"
			//			},
			//			Children =
			//			{
			//					new AdaptiveText {Text = text },
			//			},
			//			Attribution = new ToastGenericAttributionText
			//			{
			//				Text = "Alert"
			//			},
			//		}
			//	}
			//};
			//var toast = new ToastNotification(content.GetContent());

			//// Display toast
			//ToastNotificationManager.CreateToastNotifier().Show(toast);
		}

		public static List<Xiaomi_gamepad> Gamepads { get; set; } = new List<Xiaomi_gamepad>();

		private static bool TryReEnableDevice(string deviceInstanceId)
		{
			try
			{
				Guid hidGuid = new Guid();
				HidLibrary.NativeMethods.HidD_GetHidGuid(ref hidGuid);
				IntPtr deviceInfoSet = HidLibrary.NativeMethods.SetupDiGetClassDevs(ref hidGuid, deviceInstanceId, 0,
					HidLibrary.NativeMethods.DIGCF_PRESENT | HidLibrary.NativeMethods.DIGCF_DEVICEINTERFACE);
				HidLibrary.NativeMethods.SP_DEVINFO_DATA deviceInfoData = new HidLibrary.NativeMethods.SP_DEVINFO_DATA();
				deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
				var success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
				if (!success)
				{
					InformUser("Error getting device info data, error code = " + Marshal.GetLastWin32Error());
				}
				success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1, ref deviceInfoData);
				// Checks that we have a unique device
				if (success)
				{
					InformUser("Can't find unique device");
				}

				HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS propChangeParams = new HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS();
				propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
				propChangeParams.classInstallHeader.installFunction = HidLibrary.NativeMethods.DIF_PROPERTYCHANGE;
				propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_DISABLE;
				propChangeParams.scope = HidLibrary.NativeMethods.DICS_FLAG_GLOBAL;
				propChangeParams.hwProfile = 0;
				success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
					ref propChangeParams, Marshal.SizeOf(propChangeParams));
				if (!success)
				{
					InformUser("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
					return false;
				}
				success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE,
					deviceInfoSet, ref deviceInfoData);
				if (!success)
				{
					InformUser("Error disabling device, error code = " + Marshal.GetLastWin32Error());
					return false;

				}
				propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_ENABLE;
				success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
					ref propChangeParams, Marshal.SizeOf(propChangeParams));
				if (!success)
				{
					InformUser("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
					return false;
				}
				success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE,
					deviceInfoSet, ref deviceInfoData);
				if (!success)
				{
					InformUser("Error enabling device, error code = " + Marshal.GetLastWin32Error());
					return false;
				}

				HidLibrary.NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

				return true;
			}
			catch
			{
				InformUser("Can't re-enable device");
				return false;
			}
		}

		private static string devicePathToInstanceId(string devicePath)
		{
			string deviceInstanceId = devicePath;
			deviceInstanceId = deviceInstanceId.Remove(0, deviceInstanceId.LastIndexOf('\\') + 1);
			deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.LastIndexOf('{'));
			deviceInstanceId = deviceInstanceId.Replace('#', '\\');
			if (deviceInstanceId.EndsWith("\\"))
			{
				deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.Length - 1);
			}
			return deviceInstanceId;

		}
	}
}
