using System;
using System.Threading;
using HidLibrary;
using ScpDriverInterface;

namespace mi
{
	public class Xiaomi_gamepad
	{
		public HidDevice Device { get; set; }
		private byte[] Vibration = { 0x20, 0x00, 0x00 };
		private Mutex rumble_mutex = new Mutex();
		//private byte[] enableAccelerometer = { 0x31, 0x01, 0x08 };

		public Xiaomi_gamepad(HidDevice device, ScpBus scpBus, int index)
		{
			Device = device;
			Device.WriteFeatureData(Vibration);

			Thread rThread = new Thread(() => rumble_thread(Device));
			// rThread.Priority = ThreadPriority.BelowNormal; 
			rThread.Start();

			Thread iThread = new Thread(() => input_thread(Device, scpBus, index));
			iThread.Priority = ThreadPriority.Highest;
			iThread.Start();
		}

		private void rumble_thread(HidDevice Device)
		{
			byte[] local_vibration = { 0x20, 0x00, 0x00 };
			while (true)
			{
				rumble_mutex.WaitOne();
				if (local_vibration[2] != Vibration[2] || Vibration[1] != local_vibration[1])
				{
					local_vibration[2] = Vibration[2];
					local_vibration[1] = Vibration[1];
					rumble_mutex.ReleaseMutex();
					Device.WriteFeatureData(local_vibration);
					//Console.WriteLine("Big Motor: {0}, Small Motor: {1}", Vibration[2], Vibration[1]);
				}
				else
				{
					rumble_mutex.ReleaseMutex();
				}
				Thread.Sleep(20);
			}
		}

		private void input_thread(HidDevice Device, ScpBus scpBus, int index)
		{
			scpBus.PlugIn(index);
			X360Controller controller = new X360Controller();
			int timeout = 30;
			long last_changed = 0;
			long last_mi_button = 0;
			while (true)
			{
				HidDeviceData data = Device.Read(timeout);
				var currentState = data.Data;
				bool changed = false;
				if (data.Status == HidDeviceData.ReadStatus.Success && currentState.Length >= 21 && currentState[0] == 4)
				{
					//Console.WriteLine(Program.ByteArrayToHexString(currentState));
					X360Buttons Buttons = X360Buttons.None;
					if ((currentState[1] & 1) != 0) Buttons |= X360Buttons.A;
					if ((currentState[1] & 2) != 0) Buttons |= X360Buttons.B;
					if ((currentState[1] & 8) != 0) Buttons |= X360Buttons.X;
					if ((currentState[1] & 16) != 0) Buttons |= X360Buttons.Y;
					if ((currentState[1] & 64) != 0) Buttons |= X360Buttons.LeftBumper;
					if ((currentState[1] & 128) != 0) Buttons |= X360Buttons.RightBumper;

					if ((currentState[2] & 32) != 0) Buttons |= X360Buttons.LeftStick;
					if ((currentState[2] & 64) != 0) Buttons |= X360Buttons.RightStick;

					if (currentState[4] != 15)
					{
						if (currentState[4] == 0 || currentState[4] == 1 || currentState[4] == 7) Buttons |= X360Buttons.Up;
						if (currentState[4] == 4 || currentState[4] == 3 || currentState[4] == 5) Buttons |= X360Buttons.Down;
						if (currentState[4] == 6 || currentState[4] == 5 || currentState[4] == 7) Buttons |= X360Buttons.Left;
						if (currentState[4] == 2 || currentState[4] == 1 || currentState[4] == 3) Buttons |= X360Buttons.Right;
					}

					if ((currentState[2] & 8) != 0) Buttons |= X360Buttons.Start;
					if ((currentState[2] & 4) != 0) Buttons |= X360Buttons.Back;



					if ((currentState[20] & 1) != 0)
					{
						last_mi_button = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
						Buttons |= X360Buttons.Logo;
					}
					if (last_mi_button != 0) Buttons |= X360Buttons.Logo;


					if (controller.Buttons != Buttons)
					{
						changed = true;
						controller.Buttons = Buttons;
					}

					short LeftStickX = (short)((Math.Max(-127.0, currentState[5] - 128) / 127) * 32767);
					if (LeftStickX == -32767)
						LeftStickX = -32768;
					
					if (LeftStickX != controller.LeftStickX)
					{
						changed = true;
						controller.LeftStickX = LeftStickX;
					}

					short LeftStickY = (short)((Math.Max(-127.0, currentState[6] - 128) / 127) * -32767);
					if (LeftStickY == -32767)
						LeftStickY = -32768;
					
					if (LeftStickY != controller.LeftStickY)
					{
						changed = true;
						controller.LeftStickY = LeftStickY;
					}

					short RightStickX = (short)((Math.Max(-127.0, currentState[7] - 128) / 127) * 32767);
					if (RightStickX == -32767)
						RightStickX = -32768;
					
					if (RightStickX != controller.RightStickX)
					{
						changed = true;
						controller.RightStickX = RightStickX;
					}

					short RightStickY = (short)((Math.Max(-127.0, currentState[8] - 128) / 127) * -32767);
					if (RightStickY == -32767)
						RightStickY = -32768;
					
					if (RightStickY != controller.RightStickY)
					{
						changed = true;
						controller.RightStickY = RightStickY;
					}

					if (controller.LeftTrigger != currentState[11])
					{
						changed = true;
						controller.LeftTrigger = currentState[11];
					}

					if (controller.RightTrigger != currentState[12])
					{
						changed = true;
						controller.RightTrigger = currentState[12];

					}
				}

				if (data.Status == HidDeviceData.ReadStatus.WaitTimedOut || (!changed && ((last_changed + timeout) < (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond))))
				{
					changed = true;
				}

				if (changed)
				{
					//Console.WriteLine("changed");
					//Console.WriteLine((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond));
					byte[] outputReport = new byte[8];
					scpBus.Report(index, controller.GetReport(), outputReport);

					if (outputReport[1] == 0x08)
					{
						byte bigMotor = outputReport[3];
						byte smallMotor = outputReport[4];
						rumble_mutex.WaitOne();
						if (bigMotor != Vibration[2] || Vibration[1] != smallMotor)
						{
							Vibration[1] = smallMotor;
							Vibration[2] = bigMotor;
						}
						rumble_mutex.ReleaseMutex();
					}

					if (last_mi_button != 0)
					{
						if ((last_mi_button + 100) < (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond))
						{
							last_mi_button = 0;
							controller.Buttons ^= X360Buttons.Logo;
						}
					}

					last_changed = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				}
			}
		}
	}
}
