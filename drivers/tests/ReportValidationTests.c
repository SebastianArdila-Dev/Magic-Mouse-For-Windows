#include <assert.h>
#include "../MagicMouseBridge/ReportValidation.h"

int main(void)
{
    unsigned char data[161] = { 0x29 };
    unsigned long length;
    assert(!ValidMouseReport(0x030D, 0, 6));
    for (length = 0; length <= 160; ++length)
        assert(ValidMouseReport(0x030D, data, length) ==
            (length >= 6 && length <= 126 && (length - 6) % 8 == 0));
    assert(!ValidMouseReport(0x0269, data, 14));
    data[0] = 0x12;
    for (length = 0; length <= 160; ++length) {
        int expected = length == 8 || (length >= 14 && length <= 134 && (length - 14) % 8 == 0);
        assert(ValidMouseReport(0x0269, data, length) == expected);
        assert(ValidMouseReport(0x0323, data, length) == expected);
    }
    assert(!ValidMouseReport(0x030D, data, 14));
    assert(!ValidMouseReport(0x9999, data, 14));
    assert(!ValidMouseReport(0x0269, data, 161));
    return 0;
}
