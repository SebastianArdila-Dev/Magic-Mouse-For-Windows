#include <assert.h>
#include <string.h>
#include "../MagicMouseBridge/DescriptorValidation.h"

int main(void)
{
    const unsigned char good[] = {0x05,1,0x09,2,0xA1,1,0x85,1,0x75,8,0x95,3,0x81,2,0xC0};
    unsigned char copy[sizeof(good)];
    const unsigned char pop[] = {0xB4};
    const unsigned char longItem[] = {0xFE,0,0};
    unsigned long i;
    assert(CanExtendDescriptor(good, sizeof(good)));
    assert(!CanExtendDescriptor(0, sizeof(good)));
    assert(!CanExtendDescriptor(pop, sizeof(pop)));
    assert(!CanExtendDescriptor(longItem, sizeof(longItem)));
    for (i = 0; i < sizeof(good); ++i) assert(!CanExtendDescriptor(good, i));
    memcpy(copy, good, sizeof(good)); copy[7] = 0x7F;
    assert(!CanExtendDescriptor(copy, sizeof(copy)));
    copy[7] = 0; assert(!CanExtendDescriptor(copy, sizeof(copy)));
    copy[7] = 1; copy[sizeof(copy)-1] = 0xA4;
    assert(!CanExtendDescriptor(copy, sizeof(copy)));
    return 0;
}
