using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Xiaomi_Flash.Compression
{
    internal enum LzmaAction
    {
        Run = 0,
        Finish = 1
    }

    internal enum LzmaResult
    {
        Ok = 0,
        StreamEnd = 1,
        NoCheck = 2,
        UnsupportedCheck = 3,
        GetCheck = 4,
        MemError = 5,
        MemlimitError = 6,
        FormatError = 7,
        OptionsError = 8,
        DataError = 9,
        BufError = 10,
        ProgError = 11
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LzmaStream
    {
        public IntPtr next_in;
        public UIntPtr avail_in;
        public ulong total_in;

        public IntPtr next_out;
        public UIntPtr avail_out;
        public ulong total_out;

        public IntPtr allocator;
        public IntPtr internal_state;

        public IntPtr reserved_ptr1;
        public IntPtr reserved_ptr2;
        public IntPtr reserved_ptr3;
        public IntPtr reserved_ptr4;

        public ulong reserved_int1;
        public ulong reserved_int2;
        public UIntPtr reserved_int3;
        public UIntPtr reserved_int4;

        public int reserved_enum1;
        public int reserved_enum2;
    }

    internal static class LzmaNative
    {
        private const string LibraryName = "liblzma";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern LzmaResult lzma_auto_decoder(ref LzmaStream strm, ulong memlimit, uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern LzmaResult lzma_code(ref LzmaStream strm, LzmaAction action);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void lzma_end(ref LzmaStream strm);

        internal static void AutoDecoder(ref LzmaStream strm, ulong memlimit, uint flags)
        {
            LzmaResult result = lzma_auto_decoder(ref strm, memlimit, flags);
            if (result != LzmaResult.Ok)
                throw new InvalidDataException("lzma_auto_decoder failed: " + result);
        }

        internal static LzmaResult Code(ref LzmaStream strm, LzmaAction action)
        {
            return lzma_code(ref strm, action);
        }

        internal static void End(ref LzmaStream strm)
        {
            lzma_end(ref strm);
        }
    }
}
