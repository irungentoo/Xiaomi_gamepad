using System;

namespace HidLibrary
{
	[Flags]
	public enum ShareMode
	{
		Exclusive = 0,
		ShareRead = NativeMethods.FILE_SHARE_READ,
		ShareWrite = NativeMethods.FILE_SHARE_WRITE
	}
}