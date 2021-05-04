using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;

namespace FakeServer.Network
{
    class MessageHeader
    {
        public struct RawData
        {
            public UInt32 Signature;     //  識別用シグネチャ
            public int Version;      //  構造体バージョン
            public int DataSize;         //  メッセージサイズ
            public UInt32 Reserve;      //  パディング用
            const int NAME_LENGTH = 16;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NAME_LENGTH)]
            public byte[] Name;        //    パケット名(16バイト)
            public const int HEADER_SIZE = 32;
            //            public fixed byte Name[16];

            public void SetHeader(UInt32 signature)
            {
                Debug.Assert(Marshal.SizeOf(this) == HEADER_SIZE);
                Signature = signature;
                Version = IPAddress.HostToNetworkOrder(CURRENT_VERSION);
                Reserve = 0;
            }

            public void SetName(string name)
            {
                Debug.Assert(name.Length < NAME_LENGTH);

                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(name);

                if (Name == null || Name.Length != NAME_LENGTH)
                {
                    Name = new byte[NAME_LENGTH];
                }

                int count = NAME_LENGTH - 1;
                if (bytes.Length < count)
                {
                    count = bytes.Length;
                }
                Buffer.BlockCopy(bytes, 0, Name, 0, count);

                Name[count] = 0;   //  終端
            }
            public void SetDataSize(int size)
            {
                DataSize = IPAddress.HostToNetworkOrder(size);
            }
            public byte[] ToArray()
            {
                int size = Marshal.SizeOf(this);

                byte[] bytes = new byte[size];

                GCHandle gchw = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                Marshal.StructureToPtr(this, gchw.AddrOfPinnedObject(), false);
                gchw.Free();
                return bytes;
            }

            public int SetupSystem(string name, int dataSize)
            {
                SetHeader(SIGNATURE_SYSTEM);
                SetName(name);
                SetDataSize(dataSize);
                return HEADER_SIZE + dataSize;
            }
            public int SetupUser(string name, int dataSize)
            {
                SetHeader(SIGNATURE_USER);
                SetName(name);
                SetDataSize(dataSize);
                return HEADER_SIZE + dataSize;
            }
        }
        //  シグネチャはこの書き方だとビッグエンディアンになります。
        public static readonly UInt32 SIGNATURE_SYSTEM = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("FNMS"), 0);
        public static readonly UInt32 SIGNATURE_USER = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("FNMU"), 0);    //  今回は未使用
        const int CURRENT_VERSION = 1;
        const int ACCEPT_VERSION = 1;

        public RawData Raw { get; private set; }
        public const int DEFAULT_HEADER_SIZE = 32;

        public UInt32 Signature { get { return Raw.Signature; } }//    パケット判別用シグネチャ

        public byte HeaderSize { get { return RawData.HEADER_SIZE; } }   //    ヘッダーサイズ
        public byte DataOffset { get { return HeaderSize; } }   //    データ部がリトルエンディアンか？
                                                                //        public UInt16 Reserve16 { get { return Raw.Reserve16; } }   //    予備
        public int DataSize { get; private set; }                         //    通信サイズ(パケット+データ)

        public int Version { get; private set; }                         //    通信サイズ(パケット+データ)

        public string Name { get; private set; }



        // ここからアクセスを楽にするための物になります 
        public int SendSize { get { return DataOffset + DataSize; } }
        public bool IsSystemMessage { get { return (Signature == SIGNATURE_SYSTEM); } }
        public bool IsUserMessage { get { return (Signature == SIGNATURE_USER); } }

        public MessageHeader()
        {

        }

        public MessageHeader(byte[] data)
        {
            Convert(data);
        }

        public bool SetRawData(RawData rawData)
        {
            int version = IPAddress.NetworkToHostOrder(Raw.Version);
            if (version < ACCEPT_VERSION)
            {
                return false;
            }
            Raw = rawData;
            DataSize = IPAddress.NetworkToHostOrder(Raw.DataSize);
            Version = version;
            Name = System.Text.Encoding.UTF8.GetString(Raw.Name.ToArray());
            Name = Name.Remove(Name.IndexOf('\0'));
            return true;
        }

        public bool Convert(byte[] data)
        {
            int size = Marshal.SizeOf(Raw);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(data, 0, ptr, size);
            Raw = (RawData)Marshal.PtrToStructure(ptr, typeof(RawData)); Marshal.FreeHGlobal(ptr);
            if (!IsSystemMessage && !IsUserMessage)
            {// シグネチャが違う 
                return false;
            }
            return SetRawData(Raw);
        }
    }
}
