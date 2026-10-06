#pragma once

/* Conservative HID item walker. This is a gate, not a replacement for HidP. */
static int CanExtendDescriptor(const unsigned char *data, unsigned long length)
{
    unsigned long offset = 0, collections = 0, stack = 0;
    unsigned char report = 0, saved[16];
    int input = 0;
    if (data == 0 || length == 0 || length > 4096) return 0;
    while (offset < length) {
        unsigned char prefix = data[offset++];
        unsigned long size = prefix & 3, type = (prefix >> 2) & 3, tag = prefix >> 4;
        if (prefix == 0xFE || type == 3) return 0; /* No opaque long/reserved items. */
        if (size == 3) size = 4;
        if (size > length - offset) return 0;
        if (type == 1 && tag == 8) {
            if (size != 1 || data[offset] == 0 || data[offset] == 0x7F) return 0;
            report = data[offset];
        } else if (type == 1 && tag == 10) {
            if (size != 0 || stack == 16) return 0;
            saved[stack++] = report;
        } else if (type == 1 && tag == 11) {
            if (size != 0 || stack == 0) return 0;
            report = saved[--stack];
        } else if (type == 0 && tag == 10) {
            if (size != 1 || collections == 32) return 0;
            ++collections;
        } else if (type == 0 && tag == 12) {
            if (size != 0 || collections == 0) return 0;
            --collections;
        } else if (type == 0 && (tag == 8 || tag == 9 || tag == 11)) {
            if (collections == 0 || report == 0 || size == 0) return 0;
            if (tag == 8) input = 1;
        }
        offset += size;
    }
    return input && collections == 0 && stack == 0;
}
