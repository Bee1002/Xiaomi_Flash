// CRC32 para descompresión BZip2 (algoritmo compatible con bzip2/libbzip2).
// Basado en la implementación de DotNetZip / Apache Commons Compress.

namespace Ionic.Crc
{
    internal class CRC32
    {
        private int _register = -1;

        private static readonly uint[] CrcTable = BuildCrcTable();

        public CRC32(bool reverseBits)
        {
            if (!reverseBits)
                throw new System.ArgumentException("BZip2 requires reverseBits=true");
            Reset();
        }

        public int Crc32Result => ~_register;

        public void Reset()
        {
            _register = -1;
        }

        public void UpdateCRC(byte b)
        {
            int index = (_register >> 24) ^ b;
            _register = (int)((uint)(_register << 8) ^ CrcTable[index & 0xFF]);
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            const uint polynomial = 0x04C11DB7;

            for (uint i = 0; i < 256; i++)
            {
                uint remainder = i << 24;
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((remainder & 0x80000000) != 0)
                        remainder = (remainder << 1) ^ polynomial;
                    else
                        remainder <<= 1;
                }
                table[i] = remainder;
            }

            return table;
        }
    }
}
