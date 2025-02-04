using System;
using System.IO;
using System.Security.Cryptography;

//   Usage sample
//
namespace ModBusTerminal
{ 




    public class Crc32 : HashAlgorithm
    {
        public const UInt32 DefaultPolynomial = 0xedb88320;//0x04C11DB7;// 
        public const UInt32 DefaultSeed = 0xffffffff;

        private UInt32 hash;
        private UInt32 seed;
        private UInt32[] table;
        private static UInt32[] defaultTable;

        public uint GetCRC32B(byte[] data)
        {
            if (data.Length % 4 != 0) return 0xEAA0A; /// ERROR

            byte t, t1;
            for (int i = 0; i < data.Length / 4; ++i)
            {
                t = data[i * 4];
                t1 = data[i * 4 + 1];
                data[i * 4] = data[i * 4 + 3];
                data[i * 4 + 1] = data[i * 4 + 2];
                data[i * 4 + 2] = t1;
                data[i * 4 + 3] = t;
            }

            Crc32 crc = new Crc32();
            UInt32 hash = 0;
            foreach (byte b in crc.ComputeHash(data))
            {
                hash = Convert.ToUInt32((hash << 8) + (b ^ 0xff));
            }
            return hash;
        }


        public Crc32()
        {
            table = InitializeTable(DefaultPolynomial);
            seed = DefaultSeed;
            Initialize();
        }

        public Crc32(UInt32 polynomial, UInt32 seed)
        {
            table = InitializeTable(polynomial);
            this.seed = seed;
            Initialize();
        }

        public override void Initialize()
        {
            hash = seed;
        }

        protected override void HashCore(byte[] buffer, int start, int length)
        {
            hash = CalculateHash(table, hash, buffer, start, length);
        }

        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = UInt32ToBigEndianBytes(~hash);
            this.HashValue = hashBuffer;
            return hashBuffer;
        }

        public override int HashSize
        {
            get { return 32; }
        }

        public static UInt32 Compute(byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
        }

        public static UInt32 Compute(UInt32 seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), seed, buffer, 0, buffer.Length);
        }

        public static UInt32 Compute(UInt32 polynomial, UInt32 seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }

        private static UInt32[] InitializeTable(UInt32 polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
                return defaultTable;

            UInt32[] createTable = new UInt32[256];
            for (int i = 0; i < 256; i++)
            {
                UInt32 entry = (UInt32)i;
                for (int j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry = entry >> 1;
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
                defaultTable = createTable;

            return createTable;
        }

        private static UInt32 CalculateHash(UInt32[] table, UInt32 seed, byte[] buffer, int start, int size)
        {
            UInt32 crc = seed;
            for (int i = start; i < size; i++)
                unchecked
                {
                    crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
                }
            return crc;
        }

        private byte[] UInt32ToBigEndianBytes(UInt32 x)
        {
            return new byte[] {
            (byte)((x >> 24) & 0xff),
            (byte)((x >> 16) & 0xff),
            (byte)((x >> 8) & 0xff),
            (byte)(x & 0xff)
        };
        }

        public string Get(string FilePath)
        {
            Crc32 crc32 = new Crc32();
            String hash = String.Empty;

            using (FileStream fs = File.Open(FilePath, FileMode.Open))
                foreach (byte b in crc32.ComputeHash(fs)) hash += b.ToString("x2").ToLower();

            return hash;
        }
    }

}
