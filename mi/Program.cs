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


		[STAThreadAttribute]
		static void Main(string[] args)
		{
			ScpBus scpBus = new ScpBus();
			scpBus.UnplugAll();
			global_scpBus = scpBus;

			handler = new ConsoleEventDelegate(ConsoleEventCallback);
			SetConsoleCtrlHandler(handler, true);

			Thread.Sleep(400);
			var controllersManager = new Thread(() => ManageControllers(scpBus));
			controllersManager.Start();


			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			var ni = new NotifyIcon();


			try
			{
				using (var pi = new ProcessIcon())
				{
					pi.Display();
					Application.Run();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Program Terminated Unexpectedly",
						MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			scpBus.UnplugAll();
		}

		private static void ManageControllers(ScpBus scpBus)
		{
			var nrConnected = 0;
			while (true)
			{
				var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
				var existingDevices = Gamepads.Select(g => g.Device).ToList();
				var newDevices = compatibleDevices.Where(d => !existingDevices.Contains(d));
				foreach (var deviceInstance in newDevices)
				{
					Console.WriteLine(deviceInstance);
					var device = deviceInstance;
					try
					{
						device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
					}
					catch
					{
						Console.WriteLine("Could not open gamepad in exclusive mode. Try reconnecting the device.");
						var instanceId = devicePathToInstanceId(deviceInstance.DevicePath);
						if (TryReEnableDevice(instanceId))
						{
							try
							{
								device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
								Console.WriteLine("Opened in exclusive mode.");
							}
							catch
							{
								device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
								Console.WriteLine("Opened in shared mode.");
							}
						}
						else
						{
							device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
							Console.WriteLine("Opened in shared mode.");
						}
					}

					byte[] vibration = { 0x20, 0x00, 0x00 };
					if (device.WriteFeatureData(vibration) == false)
					{
						Console.WriteLine("Could not write to gamepad (is it closed?), skipping");
						device.CloseDevice();
						continue;
					}

					byte[] serialNumber;
					byte[] product;
					device.ReadSerialNumber(out serialNumber);
					device.ReadProduct(out product);


					Gamepads.Add(new Xiaomi_gamepad(device, scpBus, Gamepads.Count + 1));
				}
				if (Gamepads.Count != nrConnected)
				{
					Console.WriteLine("{0} controllers connected", Gamepads.Count);
				}
				nrConnected = Gamepads.Count;
				if (nrConnected == 4)
				{
					Thread.Sleep(10000);
					continue;
				}
				Thread.Sleep(5000);
			}
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
					Console.WriteLine("Error getting device info data, error code = " + Marshal.GetLastWin32Error());
				}
				success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1, ref deviceInfoData);
				// Checks that we have a unique device
				if (success)
				{
					Console.WriteLine("Can't find unique device");
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
					Console.WriteLine("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
					return false;
				}
				success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE,
					deviceInfoSet, ref deviceInfoData);
				if (!success)
				{
					Console.WriteLine("Error disabling device, error code = " + Marshal.GetLastWin32Error());
					return false;

				}
				propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_ENABLE;
				success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData,
					ref propChangeParams, Marshal.SizeOf(propChangeParams));
				if (!success)
				{
					Console.WriteLine("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
					return false;
				}
				success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE,
					deviceInfoSet, ref deviceInfoData);
				if (!success)
				{
					Console.WriteLine("Error enabling device, error code = " + Marshal.GetLastWin32Error());
					return false;
				}

				HidLibrary.NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

				return true;
			}
			catch
			{
				Console.WriteLine("Can't re-enable device");
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
