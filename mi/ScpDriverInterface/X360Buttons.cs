using System;

namespace ScpDriverInterface
{
	/// <summary>
	/// The buttons to be used with an X360Controller object.
	/// </summary>
	[Flags]
	public enum X360Buttons
	{
		None = 0,

		Up = 1 << 0,
		Down = 1 << 1,
		Left = 1 << 2,
		Right = 1 << 3,

		Start = 1 << 4,
		Back = 1 << 5,

		LeftStick = 1 << 6,
		RightStick = 1 << 7,

		LeftBumper = 1 << 8,
		RightBumper = 1 << 9,

		Logo = 1 << 10,

		A = 1 << 12,
		B = 1 << 13,
		X = 1 << 14,
		Y = 1 << 15,
	}
}