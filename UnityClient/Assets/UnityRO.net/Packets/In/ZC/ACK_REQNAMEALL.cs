using ROIO.Utils;

public partial class ZC {

    // Official ZC_ACK_REQNAMEALL (packets_struct.hpp:3568, PACKETVER_MAIN_NUM
    // >= 20150225). Extended name-lookup response (adds party/guild/position
    // name to the plain ZC_ACK_REQNAME). This client doesn't consume it, so
    // it's a pure parse-and-discard registration - without it the packet
    // stream desyncs (see PacketHeader.NEWER_PACKETVER region). Wire size:
    // int16(2) + gid(4) + name[24] + party_name[24] + guild_name[24] +
    // position_name[24] + title_id(4) = 106 bytes total.
    [PacketHandler(HEADER, "ZC_ACK_REQNAMEALL", SIZE)]
    public class ACK_REQNAMEALL : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_ACK_REQNAMEALL;
        public const int SIZE = 106;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 104-byte body; nothing to act on (no party/guild name display here).
        }
    }
}
