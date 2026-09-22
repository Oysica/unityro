using ROIO.Utils;
using System.IO;
using System.Net;

public partial class AC {
    [PacketHandler(HEADER, "AC_ACCEPT_LOGIN3")]
    public class ACCEPT_LOGIN3 : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_ACCEPT_LOGIN3;

        // Server-side layout is PACKET_AC_ACCEPT_LOGIN_sub in
        // rathena/src/common/packets.hpp, under PACKETVER >= 20170315:
        //     uint32 ip; uint16 port; char name[20];
        //     uint16 users; uint16 type; uint16 new_;   ->  32 bytes
        //     uint8  unknown[128];                      -> +128 = 160
        // The trailing 128 bytes were added to rAthena after this client was
        // last updated (Dec 2022). With the old value of 32 a single
        // char-server (160 bytes) is miscounted as five, four of them garbage.
        public const int BLOCK_SIZE = 160;

        // How much of each block this parser actually reads; the remainder is
        // the unknown[] padding and is skipped.
        private const int BLOCK_READ = 32;

        public int LoginID1 { get; set; }
        public int AccountID { get; set; }
        public int LoginID2 { get; set; }
        public byte Sex { get; set; }
        public CharServerInfo[] Servers { get; set; }

        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            
            LoginID1 = br.ReadInt();
            AccountID = br.ReadInt();
            LoginID2 = br.ReadInt();

            br.Seek(30, SeekOrigin.Current);

            Sex = (byte) br.ReadByte();

            br.Seek(17, SeekOrigin.Current);

            long serverCount = (br.Length - br.Position) / BLOCK_SIZE;
            Servers = new CharServerInfo[serverCount];
            for(int i = 0; i < serverCount; i++) {
                CharServerInfo csi = new CharServerInfo();
                csi.IP = new IPAddress(br.ReadUInt());
                csi.Port = br.ReadUShort();
                csi.Name = br.ReadBinaryString(20);
                csi.UserCount = br.ReadUShort();
                csi.State = br.ReadShort();
                csi.Property = br.ReadUShort();

                // skip PACKET_AC_ACCEPT_LOGIN_sub.unknown[128]
                br.Seek(BLOCK_SIZE - BLOCK_READ, SeekOrigin.Current);

                Servers[i] = csi;
            }
        }
    }
}