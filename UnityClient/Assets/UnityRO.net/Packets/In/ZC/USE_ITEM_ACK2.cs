using ROIO.Utils;

public partial class ZC {

    [PacketHandler(HEADER, "ZC_USE_ITEM_ACK2", SIZE)]
    public class USE_ITEM_ACK2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_USE_ITEM_ACK2;
        public const int SIZE = 15;
        public PacketHeader Header => HEADER;

        public short index;
        public uint id;
        public uint AID;
        public short count;
        public byte result;

        /// 01c8 <index>.W <name id>.L <id>.L <amount>.W <result>.B (ZC_USE_ITEM_ACK2)
        /// The item id is 4 bytes since PACKETVER_MAIN_NUM 20181121 (packets_struct.hpp PACKET_ZC_USE_ITEM_ACK);
        /// reading it as 2 bytes desynced the stream after every item use.
        public void Read(MemoryStreamReader fp, int size) {
            this.index = fp.ReadShort();
            this.id = fp.ReadUInt();
            this.AID = fp.ReadUInt();
            this.count = fp.ReadShort();
            this.result = (byte)fp.ReadByte();
        }
    }
}
