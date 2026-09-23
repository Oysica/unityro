using ROIO.Utils;

public partial class ZC {

    // Official ZC_REQ_GROUPINFO_CHANGE_V2 (clif.cpp:9114/9129, PACKETVER >=
    // 20090603 replacement for the old 3-byte ZC_GROUPINFO_CHANGE). This
    // client doesn't read party exp/item-share settings back off this
    // packet, so it's a pure parse-and-discard registration - without it
    // the packet stream desyncs (see PacketHeader.NEWER_PACKETVER region).
    // Wire size: int16(2) + expOption(4) + itemPickRule(1) + itemShareRule(1)
    // = 8 bytes total.
    [PacketHandler(HEADER, "ZC_REQ_GROUPINFO_CHANGE_V2", SIZE)]
    public class REQ_GROUPINFO_CHANGE_V2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_REQ_GROUPINFO_CHANGE_V2;
        public const int SIZE = 8;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 6-byte body (expOption + itemPickRule + itemShareRule); nothing to act on.
        }
    }
}
