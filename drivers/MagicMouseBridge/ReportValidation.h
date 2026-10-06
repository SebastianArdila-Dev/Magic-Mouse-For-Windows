#pragma once

/* Validate the wire envelope before reading motion or copying into the bridge. */
static int ValidMouseReport(unsigned short product, const unsigned char *data, unsigned long length)
{
    unsigned long header;
    if (data == 0 || length == 0 || length > 160) return 0;
    if (product == 0x030D) {
        if (data[0] != 0x29) return 0;
        header = 6;
    } else if (product == 0x0269 || product == 0x0323) {
        if (data[0] != 0x12) return 0;
        if (length == 8) return 1;
        header = 14;
    } else return 0;
    return length >= header && (length - header) % 8 == 0 && (length - header) / 8 <= 15;
}
