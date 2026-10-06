#include <ntddk.h>
#include <wdf.h>
#include <hidport.h>
#include <hidpddi.h>
#include <wdmsec.h>
#include <ntstrsafe.h>
#include <initguid.h>
#include <devpkey.h>
#include "Protocol.h"
#include "ReportValidation.h"
#include "DescriptorValidation.h"

/* A private interface on a raw child PDO, never the global mouse device class. */
DEFINE_GUID(GUID_DEVINTERFACE_MAGIC_MOUSE_BRIDGE, 0x5e3f432c,0x47c6,0x4c64,0x9b,0x77,0x8c,0x8a,0xa1,0x3d,0x9e,0xf2);
DEFINE_GUID(GUID_MAGIC_MOUSE_RAW_PDO, 0xb8e9eaaf,0x5886,0x4c9b,0x88,0xd7,0x3d,0x52,0xc9,0xe1,0x82,0xce);

typedef struct _BRIDGE_CONTEXT {
    WDFSPINLOCK Lock;
    WDFDEVICE RawPdo;
    HID_DEVICE_ATTRIBUTES Identity;
    BOOLEAN Verified;
    BOOLEAN Enabled;
    BOOLEAN ClientConnected;
    HIDP_DEVICE_DESC Parsed;
    PHIDP_PREPARSED_DATA MouseData;
    UCHAR MouseReportId;
    USHORT MouseLength, XLink, YLink, ButtonLink;
    BOOLEAN MouseReady;
    BOOLEAN MirrorReady, MirrorAdvertised;
    MM_BRIDGE_INFO Info;
    MM_BRIDGE_PACKET Ring[MM_BRIDGE_RING_SIZE];
    ULONG Head, Count;
    ULONGLONG Sequence;
    BOOLEAN Dropped;
} BRIDGE_CONTEXT;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(BRIDGE_CONTEXT, BridgeContext);

typedef struct _RAW_CONTEXT { WDFDEVICE Parent; } RAW_CONTEXT;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(RAW_CONTEXT, RawContext);

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD BridgeAdd;
EVT_WDF_DEVICE_D0_ENTRY BridgeD0Entry;
EVT_WDF_DEVICE_D0_EXIT BridgeD0Exit;
EVT_WDF_IO_QUEUE_IO_INTERNAL_DEVICE_CONTROL BridgeInternalIoctl;
EVT_WDF_REQUEST_COMPLETION_ROUTINE BridgeReadComplete;
EVT_WDF_REQUEST_COMPLETION_ROUTINE BridgeDescriptorComplete;
EVT_WDF_OBJECT_CONTEXT_CLEANUP BridgeCleanup;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL BridgeClientIoctl;
EVT_WDF_DEVICE_FILE_CREATE BridgeFileCreate;
EVT_WDF_FILE_CLEANUP BridgeFileCleanup;

/* This opaque vendor collection increases the shared transport read buffer. Native mouse
   reports are still reconstructed using the original, unmodified preparsed descriptor. */
static const UCHAR MirrorDescriptor[] = {
    0xA4, 0x06,0x00,0xFF, 0x09,0x01, 0xA1,0x01, 0x85,0x7F,
    0x15,0x00, 0x26,0xFF,0x00, 0x75,0x08, 0x95,0xA0,
    0x09,0x01, 0x81,0x02, 0xC0, 0xB4
};

C_ASSERT(sizeof(MM_BRIDGE_PACKET) == 200);
C_ASSERT(FIELD_OFFSET(MM_BRIDGE_PACKET, Report) == 40);
C_ASSERT(sizeof(MM_BRIDGE_INFO) == 528);

static BOOLEAN Recognised(USHORT vendor, USHORT product)
{
    return (vendor == 0x05AC || vendor == 0x004C) &&
        (product == 0x030D || product == 0x0269 || product == 0x0323);
}

/* These requests go to the existing transport, not to an arbitrary user address. */
static NTSTATUS TransportRequest(WDFDEVICE device, ULONG code, PVOID buffer, ULONG length, BOOLEAN input)
{
    WDFREQUEST request;
    WDFMEMORY memory;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_REQUEST_SEND_OPTIONS options;
    WDFIOTARGET target = WdfDeviceGetIoTarget(device);
    NTSTATUS status = WdfRequestCreate(WDF_NO_OBJECT_ATTRIBUTES, target, &request);
    if (!NT_SUCCESS(status)) return status;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = request;
    status = WdfMemoryCreatePreallocated(&attributes, buffer, length, &memory);
    if (NT_SUCCESS(status)) {
        status = WdfIoTargetFormatRequestForInternalIoctl(target, request, code,
            input ? memory : NULL, NULL, input ? NULL : memory, NULL);
    }
    if (NT_SUCCESS(status)) {
        /* HID minidrivers receive their kernel buffer in IRP.UserBuffer. */
        WdfRequestWdmGetIrp(request)->UserBuffer = buffer;
        WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_SYNCHRONOUS | WDF_REQUEST_SEND_OPTION_TIMEOUT);
        WDF_REQUEST_SEND_OPTIONS_SET_TIMEOUT(&options, WDF_REL_TIMEOUT_IN_SEC(2));
        if (!WdfRequestSend(request, target, &options)) status = WdfRequestGetStatus(request);
        else status = WdfRequestGetStatus(request);
    }
    WdfObjectDelete(request);
    return status;
}

static BOOLEAN TranslateMouse(BRIDGE_CONTEXT *ctx, const UCHAR *raw, ULONG length, UCHAR *native)
{
    LONG x, y;
    UCHAR buttons;
    USAGE usages[2];
    ULONG count = 0;
    NTSTATUS status;
    if (!ctx->MouseReady) return FALSE;
    if (raw[0] == 0x29 && length >= 6) {
        ULONG bitsX = raw[1] | ((raw[3] & 0x0C) << 6);
        ULONG bitsY = raw[2] | ((raw[3] & 0x30) << 4);
        x = (bitsX & 0x200) ? (LONG)bitsX - 1024 : (LONG)bitsX;
        y = (bitsY & 0x200) ? (LONG)bitsY - 1024 : (LONG)bitsY;
        buttons = raw[3] & 3;
    } else if (raw[0] == 0x12 && length >= 8) {
        x = (SHORT)(raw[2] | (raw[3] << 8)); y = (SHORT)(raw[4] | (raw[5] << 8)); buttons = raw[1] & 3;
    } else return FALSE;
    RtlZeroMemory(native, MM_BRIDGE_MAX_REPORT);
    status = HidP_InitializeReportForID(HidP_Input, ctx->MouseReportId, ctx->MouseData, (PCHAR)native, ctx->MouseLength);
    if (status != HIDP_STATUS_SUCCESS) return FALSE;
    status = HidP_SetUsageValue(HidP_Input, 1, ctx->XLink, 0x30, (ULONG)x, ctx->MouseData, (PCHAR)native, ctx->MouseLength);
    if (status != HIDP_STATUS_SUCCESS) return FALSE;
    status = HidP_SetUsageValue(HidP_Input, 1, ctx->YLink, 0x31, (ULONG)y, ctx->MouseData, (PCHAR)native, ctx->MouseLength);
    if (status != HIDP_STATUS_SUCCESS) return FALSE;
    if (buttons & 1) usages[count++] = 1;
    if (buttons & 2) usages[count++] = 2;
    if (count > 0 && HidP_SetUsages(HidP_Input, 9, ctx->ButtonLink, usages, &count,
        ctx->MouseData, (PCHAR)native, ctx->MouseLength) != HIDP_STATUS_SUCCESS) return FALSE;
    return TRUE;
}

static VOID InitialiseNativeMouse(WDFDEVICE device)
{
    BRIDGE_CONTEXT *ctx = BridgeContext(device);
    HID_DESCRIPTOR descriptor;
    PUCHAR bytes;
    NTSTATUS status;
    USHORT index;
    if (ctx->Parsed.CollectionDesc != NULL) return;
    RtlZeroMemory(&descriptor, sizeof(descriptor));
    status = TransportRequest(device, IOCTL_HID_GET_DEVICE_DESCRIPTOR, &descriptor, sizeof(descriptor), FALSE);
    if (!NT_SUCCESS(status) || descriptor.bNumDescriptors != 1 || descriptor.DescriptorList[0].wReportLength == 0 ||
        descriptor.DescriptorList[0].wReportLength > 4096) return;
    bytes = ExAllocatePool2(POOL_FLAG_NON_PAGED, descriptor.DescriptorList[0].wReportLength, 'dBMM');
    if (bytes == NULL) return;
    status = TransportRequest(device, IOCTL_HID_GET_REPORT_DESCRIPTOR, bytes, descriptor.DescriptorList[0].wReportLength, FALSE);
    if (NT_SUCCESS(status)) ctx->MirrorReady = CanExtendDescriptor(bytes, descriptor.DescriptorList[0].wReportLength) != 0;
    if (NT_SUCCESS(status) && ctx->MirrorReady) status = HidP_GetCollectionDescription(bytes, descriptor.DescriptorList[0].wReportLength,
        NonPagedPoolNx, &ctx->Parsed);
    ExFreePoolWithTag(bytes, 'dBMM');
    if (!NT_SUCCESS(status) || !ctx->MirrorReady) return;
    for (index = 0; index < ctx->Parsed.CollectionDescLength; ++index) {
        HIDP_COLLECTION_DESC *collection = &ctx->Parsed.CollectionDesc[index];
        HIDP_CAPS caps;
        HIDP_VALUE_CAPS values[16];
        HIDP_BUTTON_CAPS buttons[8];
        USHORT nvalues = RTL_NUMBER_OF(values), nbuttons = RTL_NUMBER_OF(buttons), i;
        PHIDP_PREPARSED_DATA parsed = collection->PreparsedData;
        BOOLEAN x = FALSE, y = FALSE, button = FALSE;
        UCHAR report = 0;
        if (collection->UsagePage != 1 || collection->Usage != 2 ||
            HidP_GetCaps(parsed, &caps) != HIDP_STATUS_SUCCESS || caps.InputReportByteLength > MM_BRIDGE_MAX_REPORT) continue;
        if (HidP_GetValueCaps(HidP_Input, values, &nvalues, parsed) != HIDP_STATUS_SUCCESS) continue;
        for (i = 0; i < nvalues; ++i) {
            if (values[i].UsagePage != 1 || values[i].IsRange || values[i].IsAbsolute) continue;
            /* Wide, signed relative values are required: never truncate large native motion. */
            if (values[i].BitSize != 16 || values[i].LogicalMin != -32768 || values[i].LogicalMax != 32767) continue;
            if (values[i].NotRange.Usage == 0x30) { report = values[i].ReportID; ctx->XLink = values[i].LinkCollection; x = TRUE; }
        }
        if (!x || report == 0) continue;
        for (i = 0; i < nvalues; ++i) {
            if (values[i].UsagePage == 1 && !values[i].IsRange && !values[i].IsAbsolute &&
                values[i].BitSize == 16 && values[i].LogicalMin == -32768 && values[i].LogicalMax == 32767 && values[i].NotRange.Usage == 0x31 && values[i].ReportID == report) {
                ctx->YLink = values[i].LinkCollection; y = TRUE; break;
            }
        }
        if (!y || HidP_GetButtonCaps(HidP_Input, buttons, &nbuttons, parsed) != HIDP_STATUS_SUCCESS) continue;
        for (i = 0; i < nbuttons; ++i) {
            if (buttons[i].UsagePage == 9 && buttons[i].ReportID == report && buttons[i].IsRange &&
                buttons[i].Range.UsageMin <= 1 && buttons[i].Range.UsageMax >= 2) { ctx->ButtonLink = buttons[i].LinkCollection; button = TRUE; break; }
        }
        if (!button) continue;
        ctx->MouseData = parsed; ctx->MouseReportId = report; ctx->MouseLength = caps.InputReportByteLength; ctx->MouseReady = TRUE;
        break;
    }
}

VOID BridgeCleanup(WDFOBJECT object)
{
    BRIDGE_CONTEXT *ctx = BridgeContext((WDFDEVICE)object);
    if (ctx->Parsed.CollectionDesc != NULL) HidP_FreeCollectionDescription(&ctx->Parsed);
}

static NTSTATUS CreateRawPdo(WDFDEVICE parent)
{
    PWDFDEVICE_INIT init = WdfPdoInitAllocate(parent);
    WDFDEVICE child;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_IO_QUEUE_CONFIG queue;
    WDF_FILEOBJECT_CONFIG files;
    NTSTATUS status;
    DECLARE_CONST_UNICODE_STRING(deviceId, L"SebastianArdila\\MagicMouseBridge");
    DECLARE_CONST_UNICODE_STRING(instanceId, L"Bridge");
    DECLARE_CONST_UNICODE_STRING(access, L"D:P(D;;GA;;;NU)(A;;GA;;;SY)(A;;GA;;;BA)(A;;GR;;;IU)");
    if (init == NULL) return STATUS_INSUFFICIENT_RESOURCES;
    status = WdfPdoInitAssignRawDevice(init, &GUID_MAGIC_MOUSE_RAW_PDO);
    if (NT_SUCCESS(status)) status = WdfDeviceInitAssignSDDLString(init, &access);
    if (NT_SUCCESS(status)) status = WdfPdoInitAssignDeviceID(init, &deviceId);
    if (NT_SUCCESS(status)) status = WdfPdoInitAssignInstanceID(init, &instanceId);
    if (!NT_SUCCESS(status)) { WdfDeviceInitFree(init); return status; }
    WDF_FILEOBJECT_CONFIG_INIT(&files, BridgeFileCreate, WDF_NO_EVENT_CALLBACK, BridgeFileCleanup);
    WdfDeviceInitSetFileObjectConfig(init, &files, WDF_NO_OBJECT_ATTRIBUTES);
    WdfDeviceInitSetExclusive(init, TRUE);
    WdfDeviceInitSetIoType(init, WdfDeviceIoBuffered);
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, RAW_CONTEXT);
    attributes.ExecutionLevel = WdfExecutionLevelPassive;
    status = WdfDeviceCreate(&init, &attributes, &child);
    if (!NT_SUCCESS(status)) { if (init != NULL) WdfDeviceInitFree(init); return status; }
    RawContext(child)->Parent = parent;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue, WdfIoQueueDispatchSequential);
    queue.EvtIoDeviceControl = BridgeClientIoctl;
    status = WdfIoQueueCreate(child, &queue, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (NT_SUCCESS(status)) status = WdfDeviceCreateDeviceInterface(child, &GUID_DEVINTERFACE_MAGIC_MOUSE_BRIDGE, NULL);
    if (NT_SUCCESS(status)) status = WdfFdoAddStaticChild(parent, child);
    if (!NT_SUCCESS(status)) { WdfObjectDelete(child); return status; }
    BridgeContext(parent)->RawPdo = child;
    return STATUS_SUCCESS;
}

NTSTATUS DriverEntry(PDRIVER_OBJECT driverObject, PUNICODE_STRING registryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, BridgeAdd);
    return WdfDriverCreate(driverObject, registryPath, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS BridgeAdd(WDFDRIVER driver, PWDFDEVICE_INIT init)
{
    WDFDEVICE device;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_IO_QUEUE_CONFIG queue;
    WDF_PNPPOWER_EVENT_CALLBACKS power;
    NTSTATUS status;
    UNREFERENCED_PARAMETER(driver);
    WdfFdoInitSetFilter(init);
    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&power);
    power.EvtDeviceD0Entry = BridgeD0Entry;
    power.EvtDeviceD0Exit = BridgeD0Exit;
    WdfDeviceInitSetPnpPowerEventCallbacks(init, &power);
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, BRIDGE_CONTEXT);
    attributes.EvtCleanupCallback = BridgeCleanup;
    attributes.ExecutionLevel = WdfExecutionLevelPassive;
    status = WdfDeviceCreate(&init, &attributes, &device);
    if (!NT_SUCCESS(status)) return status;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = device;
    status = WdfSpinLockCreate(&attributes, &BridgeContext(device)->Lock);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue, WdfIoQueueDispatchParallel);
    queue.EvtIoInternalDeviceControl = BridgeInternalIoctl;
    status = WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) return status;
    /* Losing the optional bridge must never prevent the original device from starting. */
    (void)CreateRawPdo(device);
    return STATUS_SUCCESS;
}

NTSTATUS BridgeD0Entry(WDFDEVICE device, WDF_POWER_DEVICE_STATE previous)
{
    BRIDGE_CONTEXT *ctx = BridgeContext(device);
    HID_DEVICE_ATTRIBUTES identity;
    MM_BRIDGE_INFO info;
    WDF_DEVICE_PROPERTY_DATA property;
    DEVPROPTYPE propertyType;
    ULONG bytes;
    NTSTATUS status;
    UNREFERENCED_PARAMETER(previous);
    RtlZeroMemory(&identity, sizeof(identity)); identity.Size = sizeof(identity);
    InitialiseNativeMouse(device);
    status = TransportRequest(device, IOCTL_HID_GET_DEVICE_ATTRIBUTES, &identity, sizeof(identity), FALSE);
    RtlZeroMemory(&info, sizeof(info));
    info.Magic = MM_BRIDGE_MAGIC; info.Version = MM_BRIDGE_VERSION;
    info.VendorId = identity.VendorID; info.ProductId = identity.ProductID; info.Firmware = identity.VersionNumber;
    WDF_DEVICE_PROPERTY_DATA_INIT(&property, &DEVPKEY_Device_InstanceId);
    if (!NT_SUCCESS(WdfDeviceQueryPropertyEx(device, &property, sizeof(info.InstanceId), info.InstanceId, &bytes, &propertyType)) ||
        propertyType != DEVPROP_TYPE_STRING) info.InstanceId[0] = 0;
    WdfSpinLockAcquire(ctx->Lock);
    ctx->Verified = NT_SUCCESS(status) && Recognised(identity.VendorID, identity.ProductID);
    ctx->Identity = identity; ctx->Enabled = FALSE;
    ctx->Head = ctx->Count = 0; ctx->Dropped = TRUE;
    ctx->Info = info;
    WdfSpinLockRelease(ctx->Lock);
    return STATUS_SUCCESS;
}

NTSTATUS BridgeD0Exit(WDFDEVICE device, WDF_POWER_DEVICE_STATE target)
{
    BRIDGE_CONTEXT *ctx = BridgeContext(device);
    UNREFERENCED_PARAMETER(target);
    WdfSpinLockAcquire(ctx->Lock);
    ctx->Verified = ctx->Enabled = FALSE; ctx->Count = 0; ctx->Dropped = TRUE;
    WdfSpinLockRelease(ctx->Lock);
    return STATUS_SUCCESS;
}

VOID BridgeInternalIoctl(WDFQUEUE queue, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code)
{
    WDFDEVICE device = WdfIoQueueGetDevice(queue);
    UNREFERENCED_PARAMETER(outputLength); UNREFERENCED_PARAMETER(inputLength);
    WdfRequestFormatRequestUsingCurrentType(request);
    if (code == IOCTL_HID_READ_REPORT) WdfRequestSetCompletionRoutine(request, BridgeReadComplete, device);
    else if (code == IOCTL_HID_GET_DEVICE_DESCRIPTOR || code == IOCTL_HID_GET_REPORT_DESCRIPTOR)
        WdfRequestSetCompletionRoutine(request, BridgeDescriptorComplete, device);
    else {
        WDF_REQUEST_SEND_OPTIONS options;
        WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_SEND_AND_FORGET);
        if (!WdfRequestSend(request, WdfDeviceGetIoTarget(device), &options)) WdfRequestComplete(request, WdfRequestGetStatus(request));
        return;
    }
    if (!WdfRequestSend(request, WdfDeviceGetIoTarget(device), WDF_NO_SEND_OPTIONS))
        WdfRequestComplete(request, WdfRequestGetStatus(request));
}

VOID BridgeDescriptorComplete(WDFREQUEST request, WDFIOTARGET target, PWDF_REQUEST_COMPLETION_PARAMS params, WDFCONTEXT context)
{
    PIRP irp = WdfRequestWdmGetIrp(request);
    WDF_REQUEST_PARAMETERS original;
    ULONG_PTR size = params->IoStatus.Information;
    BRIDGE_CONTEXT *ctx = BridgeContext((WDFDEVICE)context);
    UNREFERENCED_PARAMETER(target);
    WDF_REQUEST_PARAMETERS_INIT(&original); WdfRequestGetParameters(request, &original);
    if (ctx->MirrorReady && NT_SUCCESS(params->IoStatus.Status) && irp->UserBuffer != NULL &&
        size <= original.Parameters.DeviceIoControl.OutputBufferLength) {
        if (original.Parameters.DeviceIoControl.IoControlCode == IOCTL_HID_GET_DEVICE_DESCRIPTOR && size >= sizeof(HID_DESCRIPTOR)) {
            HID_DESCRIPTOR *descriptor = (HID_DESCRIPTOR *)irp->UserBuffer;
            if (descriptor->bNumDescriptors == 1 && descriptor->DescriptorList[0].wReportLength > 0 && descriptor->DescriptorList[0].wReportLength <= 4096)
                descriptor->DescriptorList[0].wReportLength += (USHORT)sizeof(MirrorDescriptor);
        } else if (original.Parameters.DeviceIoControl.IoControlCode == IOCTL_HID_GET_REPORT_DESCRIPTOR && size <= 4096 &&
            size + sizeof(MirrorDescriptor) <= original.Parameters.DeviceIoControl.OutputBufferLength &&
            CanExtendDescriptor((const UCHAR *)irp->UserBuffer, (ULONG)size)) {
            RtlCopyMemory((PUCHAR)irp->UserBuffer + size, MirrorDescriptor, sizeof(MirrorDescriptor));
            size += sizeof(MirrorDescriptor);
            WdfSpinLockAcquire(ctx->Lock); ctx->MirrorAdvertised = TRUE; WdfSpinLockRelease(ctx->Lock);
        }
    }
    WdfRequestCompleteWithInformation(request, params->IoStatus.Status, size);
}

VOID BridgeReadComplete(WDFREQUEST request, WDFIOTARGET target, PWDF_REQUEST_COMPLETION_PARAMS params, WDFCONTEXT context)
{
    BRIDGE_CONTEXT *ctx = BridgeContext((WDFDEVICE)context);
    PIRP irp = WdfRequestWdmGetIrp(request);
    ULONG_PTR size = params->IoStatus.Information;
    BOOLEAN translate = FALSE;
    WDF_REQUEST_PARAMETERS original;
    UNREFERENCED_PARAMETER(target);
    WDF_REQUEST_PARAMETERS_INIT(&original); WdfRequestGetParameters(request, &original);
    if (NT_SUCCESS(params->IoStatus.Status) && irp->UserBuffer != NULL && size > 0 &&
        size <= MM_BRIDGE_MAX_REPORT && size <= original.Parameters.DeviceIoControl.OutputBufferLength) {
        const UCHAR *data = (const UCHAR *)irp->UserBuffer;
        if (data[0] == 0x29 || data[0] == 0x12) {
            LARGE_INTEGER timestamp; KeQuerySystemTimePrecise(&timestamp);
            WdfSpinLockAcquire(ctx->Lock);
            if (ctx->Verified && ctx->Enabled && ValidMouseReport(ctx->Identity.ProductID, data, (ULONG)size)) {
                ULONG index;
                MM_BRIDGE_PACKET *packet;
                translate = TRUE;
                if (!ctx->ClientConnected) {
                    WdfSpinLockRelease(ctx->Lock);
                    goto TranslateNative;
                }
                if (ctx->Count == MM_BRIDGE_RING_SIZE) {
                    ctx->Head = (ctx->Head + 1) % MM_BRIDGE_RING_SIZE; --ctx->Count; ctx->Dropped = TRUE;
                }
                index = (ctx->Head + ctx->Count) % MM_BRIDGE_RING_SIZE;
                packet = &ctx->Ring[index]; RtlZeroMemory(packet, sizeof(*packet));
                packet->Magic = MM_BRIDGE_MAGIC; packet->Version = MM_BRIDGE_VERSION;
                packet->Length = (ULONG)size; packet->Sequence = ++ctx->Sequence;
                packet->FileTime = timestamp.QuadPart;
                packet->VendorId = ctx->Identity.VendorID; packet->ProductId = ctx->Identity.ProductID;
                RtlCopyMemory(packet->Report, data, size); ++ctx->Count;
            }
            WdfSpinLockRelease(ctx->Lock);
        }
    }
TranslateNative:
    if (translate) {
        UCHAR native[MM_BRIDGE_MAX_REPORT];
        if (TranslateMouse(ctx, (const UCHAR *)irp->UserBuffer, (ULONG)size, native) &&
            ctx->MouseLength <= original.Parameters.DeviceIoControl.OutputBufferLength) {
            RtlCopyMemory(irp->UserBuffer, native, ctx->MouseLength);
            WdfRequestCompleteWithInformation(request, params->IoStatus.Status, ctx->MouseLength);
            return;
        }
    }
    /* Unknown or inactive reports retain their original completion. */
    WdfRequestCompleteWithInformation(request, params->IoStatus.Status, params->IoStatus.Information);
}

VOID BridgeFileCreate(WDFDEVICE device, WDFREQUEST request, WDFFILEOBJECT file)
{
    BRIDGE_CONTEXT *ctx = BridgeContext(RawContext(device)->Parent);
    if (WdfFileObjectGetFileName(file)->Length != 0) {
        WdfRequestComplete(request, STATUS_OBJECT_NAME_INVALID); return;
    }
    WdfSpinLockAcquire(ctx->Lock);
    ctx->ClientConnected = TRUE;
    ctx->Head = ctx->Count = 0; ctx->Dropped = TRUE;
    WdfSpinLockRelease(ctx->Lock);
    WdfRequestComplete(request, STATUS_SUCCESS);
}

VOID BridgeFileCleanup(WDFFILEOBJECT file)
{
    BRIDGE_CONTEXT *ctx = BridgeContext(RawContext(WdfFileObjectGetDevice(file))->Parent);
    WdfSpinLockAcquire(ctx->Lock);
    ctx->ClientConnected = FALSE; ctx->Head = ctx->Count = 0; ctx->Dropped = TRUE;
    WdfSpinLockRelease(ctx->Lock);
    /* Keep translating native motion after the client exits: touch mode is device state. */
}

VOID BridgeClientIoctl(WDFQUEUE queue, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code)
{
    WDFDEVICE parent = RawContext(WdfIoQueueGetDevice(queue))->Parent;
    BRIDGE_CONTEXT *ctx = BridgeContext(parent);
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    PVOID buffer = NULL;
    size_t bytes = 0;
    UNREFERENCED_PARAMETER(outputLength); UNREFERENCED_PARAMETER(inputLength);
    if (code == IOCTL_MM_BRIDGE_INFO) {
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(MM_BRIDGE_INFO), &buffer, NULL);
        if (NT_SUCCESS(status)) {
            WdfSpinLockAcquire(ctx->Lock);
            if (!ctx->Verified) status = STATUS_DEVICE_NOT_READY;
            else { RtlCopyMemory(buffer, &ctx->Info, sizeof(ctx->Info)); bytes = sizeof(ctx->Info); }
            WdfSpinLockRelease(ctx->Lock);
        }
    } else if (code == IOCTL_MM_BRIDGE_REPORT) {
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(MM_BRIDGE_PACKET), &buffer, NULL);
        if (NT_SUCCESS(status)) {
            WdfSpinLockAcquire(ctx->Lock);
            if (!ctx->Verified) status = STATUS_DEVICE_NOT_READY;
            else if (ctx->Count == 0) status = STATUS_NO_MORE_ENTRIES;
            else {
                MM_BRIDGE_PACKET *packet = &ctx->Ring[ctx->Head];
                if (ctx->Dropped) { packet->Flags |= MM_BRIDGE_DISCONTINUITY; ctx->Dropped = FALSE; }
                RtlCopyMemory(buffer, packet, sizeof(*packet)); bytes = sizeof(*packet);
                ctx->Head = (ctx->Head + 1) % MM_BRIDGE_RING_SIZE; --ctx->Count;
            }
            WdfSpinLockRelease(ctx->Lock);
        }
    } else if (code == IOCTL_MM_BRIDGE_ENABLE_TOUCH) {
        USHORT product;
        BOOLEAN verified, advertised;
        UCHAR command[3] = { 0xF1, 0x02, 0x01 };
        HID_XFER_PACKET packet;
        WdfSpinLockAcquire(ctx->Lock); verified = ctx->Verified; advertised = ctx->MirrorAdvertised; product = ctx->Identity.ProductID; WdfSpinLockRelease(ctx->Lock);
        if (!verified) status = STATUS_DEVICE_NOT_READY;
        else if (!ctx->MouseReady || !ctx->MirrorReady || !advertised) status = STATUS_NOT_SUPPORTED;
        else {
            if (product == 0x030D) { command[0] = 0xD7; command[1] = 0x01; }
            packet.reportId = command[0]; packet.reportBuffer = command;
            packet.reportBufferLen = product == 0x030D ? 2 : 3;
            status = TransportRequest(parent, IOCTL_HID_SET_FEATURE, &packet, sizeof(packet), TRUE);
            WdfSpinLockAcquire(ctx->Lock); ctx->Enabled = NT_SUCCESS(status) && ctx->Verified; WdfSpinLockRelease(ctx->Lock);
        }
    }
    WdfRequestCompleteWithInformation(request, status, bytes);
}
