using ScpDriverInterface;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace mi
{
    public partial class MainForm : Form
    {
        private static ScpBus global_scpBus;
        private static Dictionary<String, Xiaomi_gamepad> mapped_devices;

        public MainForm()
        {
            InitializeComponent();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }

        private void InvokeUI(Action a)
        {
            this.BeginInvoke(new MethodInvoker(a));
        }

        private void detectDevices()
        {
            Int32 lastResults = 0;
            while (true)
            {
                Int32 result = searchDevice();
                if (result > 0)
                {
                    if (lastResults != result)
                    {
                        String text = result + " device(s) connected";
                        lastResults = result;
                        InvokeUI(() =>
                        {

                            notifyIcon1.BalloonTipTitle = "MiX360 Gamepad";
                            notifyIcon1.BalloonTipText = text;
                            notifyIcon1.ShowBalloonTip(500);
                        });
                    }
                }
                Thread.Sleep(5000);
            }

        }

       
        private Int32 searchDevice()
        {
            var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
            ScpBus scpBus = global_scpBus;
            Dictionary<string, Xiaomi_gamepad> already_mapped = mapped_devices;
            
            //Debug.WriteLine(Device.DevicePath);
            foreach (var deviceInstance in compatibleDevices)
            {
                HidDevice Device = deviceInstance;
                if (already_mapped.ContainsKey(Device.DevicePath))
                {
                    continue;
                }
                
                try
                {
                    Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                }
                catch
                {
                    var instanceId = devicePathToInstanceId(deviceInstance.DevicePath);
                    if (TryReEnableDevice(instanceId))
                    {
                        try
                        {
                            Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                        }
                        catch
                        {
                            Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                        }
                    }
                    else
                    {
                        Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                    }
                }

                byte[] Vibration = { 0x20, 0x00, 0x00 };
                if (Device.WriteFeatureData(Vibration) == false)
                {
                    Device.CloseDevice();
                    continue;
                }

                byte[] serialNumber;
                byte[] product;
                Device.ReadSerialNumber(out serialNumber);
                Device.ReadProduct(out product);

                Int32 index = mapped_devices.Count + 1;
                Xiaomi_gamepad gamepad = new Xiaomi_gamepad(Device, scpBus, index);
                mapped_devices.Add(Device.DevicePath, gamepad);
            }

            return mapped_devices.Count;
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
                success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1, ref deviceInfoData); // Checks that we have a unique device
               
                HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS propChangeParams = new HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS();
                propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
                propChangeParams.classInstallHeader.installFunction = HidLibrary.NativeMethods.DIF_PROPERTYCHANGE;
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_DISABLE;
                propChangeParams.scope = HidLibrary.NativeMethods.DICS_FLAG_GLOBAL;
                propChangeParams.hwProfile = 0;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    return false;

                }
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_ENABLE;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    return false;
                }

                HidLibrary.NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            global_scpBus.UnplugAll();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.ShowInTaskbar = false;
            this.Hide();

            ScpBus scpBus = new ScpBus();
            scpBus.UnplugAll();
            global_scpBus = scpBus;
            mapped_devices = new Dictionary<string, Xiaomi_gamepad>();

            Thread detectThread = new Thread(detectDevices);
            detectThread.IsBackground = true;
            detectThread.Start();
        }

        private void mnuItemExit_Click(object Sender, EventArgs e)
        {
            Application.Exit();
        }
    }

}
