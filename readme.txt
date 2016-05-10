Xiaomi Gamepad

It's an excellent controller with an excellent price but it severly lacks documentation and the only place where the accelerometer in it works is when you use it with their T.V boxes.

The mi folder contains the source of a remapper that uses the SCPbus thing to make windows see it as an xbox controller. The rumble also works perfectly unlike in all the other software that I found.

the accelerometer_print folder contains a linux program that enables the accelerometer and prints the values.

Hardware:
It has the exact same layout as an Xbox 360 controller, the same buttons, joysticks, triggers and a dpad. The only thing different is that it has an accelerometer.

11 buttons (A, B, X, Y, L1, R1, both joysticks, start, back, MI button in the front)

1 dpad
2 joysticks
2 triggers

2 rumble motors with variable speeds
1 3 axis accelerometer



Protocol:

This is reversed engineered using mostly trial and error and looking at the decompiled sensors.gxbaby.so from an image of a Xiaomi tv box I found somewhere. I did this because I wanted to find out how to make the accelerometer work on my computer but I also discovered some other things. I'm posting this here in case it helps someone.

The only valid packets according to the HID descriptor is the input packet and the set feature rumble packet.


There are probably more packet types than these.


Set Feature Packets:

Rumble (length 3):
[byte (0x20)][byte (0 to 0xff) small motor rumble strength][byte (0 to 0xff) big motor rumble strength]

This is the only packet documented on the Xiaomi site. Used to make the controller rumble.

??? (length ???)
[byte (0x21)][bytes ???]

I managed to stop input from my controller with this packet but I'm not sure what the values I put were.


Callibration packet. (length 24 (maybe more))
[byte (0x22)][byte (each bit seems to denote if a section is enabled. 00000001 would mean only the first section is enabled.)][(8 bytes) section 1, LJoy][(8 bytes) section 2, Rjoy][(3 bytes) section 3, Ltrigger][(3 bytes) section 4, Rtrigger]

section 1 and 2 are used to callibrate the joysticks. Each contain 2 sub sections of 4 bytes each, one for each axis.

Each of the 4 bytes are:
[lower bound][lower middle][higher middle][higher bound]

lower bound, higher bound are used to set what values are the minimum and maximum. the middle values mean that everything between those values the controller will report as being 0x80 or that it it centered.

to callibrate, set the values to [0x00][0x7f][0x7f][0xff] for all the 4 axis then tweak both bounds until the joystick properly goes from 0 to 0xff instead of 0x15 to 0xee or whatever your controller does. note that the second axis bounds are inversed meaning that if your second axis goes from 0x13 to 0xff, you need to lower the higher bound, not the lower.

then make sure that the value is 0x80 when the controller is centered.

sections 3 and 4 are for the triggers.
Each of the 3 bytes are:
[lower bound][???][higher bound]

I have no idea what the middle ??? byte does, changing its value doesn't seem to do anything. I have set it at 0xff for my controller and it works.

to callibrate, set the values to [0x00][0x??][0xff] for both triggers then tweak both values until the value is


This is used to callibrate the joysticks/triggers on the controller. I found out the hard way by sending a long packet with that id and all 0xff and then realizing that my controller started reporting guarbage values. The values seem to be saved permantely on the controller so I had fun fixing it by figuring out the packet format by hand.


Stop input packet??? (length 2?)
[byte (0x23)][byte 0x01]

stops all input from the controller (except for the mi button).

[byte (0x23)][byte 0x00]
restarts the input.

Unpair packet??? (length 2?)
[byte (0x24)][byte 0x??]
Unpairs and closes the controller.


Note: for some reason when I tested it with hidraw ioctl(fd, HIDIOCSFEATURE(3), enable_accel) returns a fail for this packet when it returns successes for all the previous ones but don't let that fool you because it actually does enable the accelerometer (it worked here). This is one of two packets I saw in sensors.gxbaby.so 

Unable accelerometer packet. (length 3)
[byte (0x31)][byte 0x01][byte (report sensitivity. the lower the value, the less you have to move the controller before it sends a report)]


Disable accelerometer packet. (length 3)
[byte (0x31)][byte 0x00][byte 0x00]


??? (length 21)
[byte (0x04)][???]

this was the another packet sent with ioctl(fd, HIDIOCSFEATURE(21), ); in sensors.gxbaby.so . it looks like the input packet. I didn't do any tests with it but I'm putting it here because it's probably used for something.


Output packet:

there is one packet that worked with hidraw: write(fd, rumble, 3); it's the rumble packet (0x20) of length 3. One thing that is different from the Set Feature report version is that [0x20][0x00][0x00] does not stop the rumble comletely, [0x20][0x01][0x01] pretty much stops it but you can still hear it a bit if you take your ear and listen very closely. It is better to use this way of sending the rumble packet because "Set feature" stops the input from the controller due to how HID stuff works.


Input packet:

Input (length 21)
[byte (0x04)][byte (1 bit per button)][byte (1 bit per button)][byte 0][byte dpad][4 bytes = 4 joystick axis, 1 byte each axis][byte 0][byte 0][byte Ltrigger][byte Rtrigger][6 bytes accelerometer (2 bytes per axis, looks like signed little endian)][byte battery level][byte (MI button)]


