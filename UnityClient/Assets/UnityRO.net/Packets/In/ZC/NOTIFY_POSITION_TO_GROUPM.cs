using ROIO.Utils;

public partial class ZC {

    // Where a party member on the same map is, for the minimap (clif.cpp clif_party_xy)
    // 0107 <account id>.L <x>.W <y>.W
    [PacketHandler(HEADER, "ZC_NOTIFY_POSITION_TO_GROUPM", SIZE)]
    public class NOTIFY_POSITION_TO_GROUPM : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_POSITION_TO_GROUPM;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public uint AID;
        public short X;
        public short Y;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            X = br.ReadShort();
            Y = br.ReadShort();
        }
    }
}
