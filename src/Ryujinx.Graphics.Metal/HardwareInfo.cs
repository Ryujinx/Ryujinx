using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Metal
{
    static partial class HardwareInfoTools
    {

        private readonly static IntPtr _kCFAllocatorDefault = IntPtr.Zero;
        private readonly static UInt32 _kCFStringEncodingASCII = 0x0600;
        private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [LibraryImport(IOKit, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr IOServiceMatching(string name);

        [LibraryImport(IOKit)]
        private static partial IntPtr IOServiceGetMatchingService(IntPtr mainPort, IntPtr matching);

        [LibraryImport(IOKit)]
        private static partial IntPtr IORegistryEntryCreateCFProperty(IntPtr entry, IntPtr key, IntPtr allocator, UInt32 options);

        [LibraryImport(CoreFoundation, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr CFStringCreateWithCString(IntPtr allocator, string cString, UInt32 encoding);

        [LibraryImport(CoreFoundation)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool CFStringGetCString(IntPtr theString, IntPtr buffer, long bufferSizes, UInt32 encoding);

        [LibraryImport(CoreFoundation)]
        public static partial IntPtr CFDataGetBytePtr(IntPtr theData);

        static string GetNameFromId(uint id)
        {
            return id switch
            {
                0x1002 => "AMD",
                0x106B => "Apple",
                0x10DE => "NVIDIA",
                0x13B5 => "ARM",
                0x8086 => "Intel",
                _ => $"0x{id:X}"
            };
        }

        public static string GetVendor()
        {
            var serviceDict = IOServiceMatching("IOGPU");
            var service = IOServiceGetMatchingService(IntPtr.Zero, serviceDict);
            var cfString = CFStringCreateWithCString(_kCFAllocatorDefault, "vendor-id", _kCFStringEncodingASCII);
            var cfProperty = IORegistryEntryCreateCFProperty(service, cfString, _kCFAllocatorDefault, 0);

            byte[] buffer = new byte[4];
            var bufferPtr = CFDataGetBytePtr(cfProperty);
            Marshal.Copy(bufferPtr, buffer, 0, buffer.Length);

            var vendorId = BitConverter.ToUInt32(buffer);

            return GetNameFromId(vendorId);
        }

        public static string GetModel()
        {
            var serviceDict = IOServiceMatching("IOGPU");
            var service = IOServiceGetMatchingService(IntPtr.Zero, serviceDict);
            var cfString = CFStringCreateWithCString(_kCFAllocatorDefault, "model", _kCFStringEncodingASCII);
            var cfProperty = IORegistryEntryCreateCFProperty(service, cfString, _kCFAllocatorDefault, 0);

            char[] buffer = new char[64];
            IntPtr bufferPtr = Marshal.AllocHGlobal(buffer.Length);

            if (CFStringGetCString(cfProperty, bufferPtr, buffer.Length, _kCFStringEncodingASCII))
            {
                var model = Marshal.PtrToStringUTF8(bufferPtr);
                Marshal.FreeHGlobal(bufferPtr);
                return model;
            }

            return "";
        }
    }
}
