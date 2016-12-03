using ScpDriverInterface;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace mi
{
    public partial class MainForm : Form
    {
        private static ScpBus global_scpBus;

        public void Log(string Text)
        {
            ConsoleBox.Text += Text + "\n";
        }

        public MainForm()
        {
           
            InitializeComponent();
            ScpBus scpBus = new ScpBus();
            scpBus.UnplugAll();
            global_scpBus = scpBus;

            Thread.Sleep(400);

            Xiaomi_gamepad[] gamepads = new Xiaomi_gamepad[4];
            int index = 1;
            var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
            foreach (var deviceInstance in compatibleDevices)
            {
                Log(deviceInstance.ToString());
                HidDevice Device = deviceInstance;
                try
                {
                    Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                }
                catch
                {
                    Log("Could not open gamepad in exclusive mode. Try re-enable device.");
                    var instanceId = devicePathToInstanceId(deviceInstance.DevicePath);
                    if (TryReEnableDevice(instanceId))
                    {
                        try
                        {
                            Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                            Log("Opened in exclusive mode.");
                        }
                        catch
                        {
                            Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                            Log("Opened in shared mode.");
                        }
                    }
                    else
                    {
                        Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                        Log("Opened in shared mode.");
                    }
                }

                byte[] Vibration = { 0x20, 0x00, 0x00 };
                if (Device.WriteFeatureData(Vibration) == false)
                {
                    Log("Could not write to gamepad (is it closed?), skipping");
                    Device.CloseDevice();
                    continue;
                }

                byte[] serialNumber;
                byte[] product;
                Device.ReadSerialNumber(out serialNumber);
                Device.ReadProduct(out product);


                gamepads[index - 1] = new Xiaomi_gamepad(Device, scpBus, index);
                ++index;

                if (index >= 5)
                {
                    break;
                }
            }
            Log(index - 1 + " controllers connected");
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

        private bool TryReEnableDevice(string deviceInstanceId)
        {
            try
            {
                bool success;
                Guid hidGuid = new Guid();
                HidLibrary.NativeMethods.HidD_GetHidGuid(ref hidGuid);
                IntPtr deviceInfoSet = HidLibrary.NativeMethods.SetupDiGetClassDevs(ref hidGuid, deviceInstanceId, 0, HidLibrary.NativeMethods.DIGCF_PRESENT | HidLibrary.NativeMethods.DIGCF_DEVICEINTERFACE);
                HidLibrary.NativeMethods.SP_DEVINFO_DATA deviceInfoData = new HidLibrary.NativeMethods.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
                success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
                if (!success)
                {
                    Log("Error getting device info data, error code = " + Marshal.GetLastWin32Error());
                }
                success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1, ref deviceInfoData); // Checks that we have a unique device
                if (success)
                {
                    Log("Can't find unique device");
                }

                HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS propChangeParams = new HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS();
                propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
                propChangeParams.classInstallHeader.installFunction = HidLibrary.NativeMethods.DIF_PROPERTYCHANGE;
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_DISABLE;
                propChangeParams.scope = HidLibrary.NativeMethods.DICS_FLAG_GLOBAL;
                propChangeParams.hwProfile = 0;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    Log("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    Log("Error disabling device, error code = " + Marshal.GetLastWin32Error());
                    return false;

                }
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_ENABLE;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    Log("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    Log("Error enabling device, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }

                HidLibrary.NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

                return true;
            }
            catch
            {
                Log("Can't reenable device");
                return false;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            global_scpBus.UnplugAll();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                this.ShowInTaskbar = false;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon1.Visible = false;
        }
    }
}
