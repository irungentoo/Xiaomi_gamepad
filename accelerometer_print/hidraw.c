
#include <linux/types.h>
#include <linux/input.h>
#include <linux/hidraw.h>
#include <fcntl.h>
#include <unistd.h>


#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <errno.h>
int main(int argc, char **argv) {
    char *device = "/dev/hidraw5";
    if (argc > 1) device = argv[1];
    printf("%i\n", O_RDWR);
    int fd = open(device, O_RDWR |O_NONBLOCK);
    if (fd < 0) {
        printf("error can't open %s\n", device);
        return 1;
    }
    int desc_size = 0;
    int res = ioctl(fd, HIDIOCGRDESCSIZE, &desc_size);
    if (res < 0) {
        printf("error can't get report desc\n");
        return 1;
    }
    printf("report size %i\n", desc_size);
    struct hidraw_report_descriptor rpt_desc;
    rpt_desc.size = desc_size;
    res = ioctl(fd, HIDIOCGRDESC, &rpt_desc);
    if (res < 0) {
        perror("HIDIOCGRDESC");
    } else {
        printf("Report Descriptor:\n");
        int i;
        for (i = 0; i < rpt_desc.size; i++)
                printf("%02X ", rpt_desc.value[i]);
            puts("\n");
    }

    unsigned char enable_accel[21] = {0x31, 0x01, 0x08};
    res = ioctl(fd, HIDIOCSFEATURE(3), enable_accel);
        if (res < 0) {
        printf("ignore this error, it's normal\n");
        //return 1;
    }

    unsigned char buf[1024];
    while (1) {
        int res = read(fd, buf, 1024);
        if (res < 0) {
            continue;
        }

        printf("%i\n", res);
        int i;
        for (i = 0; i < res; ++i) {
            printf("%02X", buf[i]);
        }

        signed short x, y, z;
        memcpy(&x, buf + 12, 2);
        memcpy(&y, buf + 14, 2);
        memcpy(&z, buf + 16, 2);

        printf("\n %hi %hi %hi\n", x, y, z);
    }
}