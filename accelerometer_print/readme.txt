Hidraw program to enable and read accelerometer data.

Build:
gcc hidraw.c -o hidraw

Run:
./hidraw /dev/hidraw5

replace /dev/hidraw5 with your hidraw device.

if it gives you a can't open error: sudo chmod a+rw /dev/hidraw5
or whatever your hidraw dev might fix it until you replug it or reboot.
