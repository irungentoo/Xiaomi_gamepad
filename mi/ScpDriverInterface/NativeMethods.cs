using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ScpDriverInterface
{
	internal static class NativeMethods
	{
		[StructLayout(LayoutKind.Sequential)]
		internal struct SP_DEVICE_INTERFACE_DATA
		{
			internal int cbSize;
			internal Guid InterfaceClassGuid;
			internal int Flags;
			internal IntPtr Reserved;
		}

		internal const uint FILE_ATTRIBUTE_NORMAL = 0x80;
		internal const uint FILE_FLAG_OVERLAPPED = 0x40000000;
		internal const uint FILE_SHARE_READ = 1;
		internal const uint FILE_SHARE_WRITE = 2;
		internal const uint GENERIC_READ = 0x80000000;
		internal const uint GENERIC_WRITE = 0x40000000;
		internal const uint OPEN_EXISTING = 3;
		internal const int DIGCF_PRESENT = 0x0002;
		internal const int DIGCF_DEVICEINTERFACE = 0x0010;

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, UIntPtr hTemplateFile);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool DeviceIoControl(SafeFileHandle hDevice, int dwIoControlCode, byte[] lpInBuffer, int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, ref int lpBytesReturned, IntPtr lpOverlapped);

		[DllImport("setupapi.dll", SetLastError = true)]
		internal static extern int SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

		[DllImport("setupapi.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		internal static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

		[DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, ref int requiredSize, ref SP_DEVICE_INTERFACE_DATA deviceInfoData);
	}
}