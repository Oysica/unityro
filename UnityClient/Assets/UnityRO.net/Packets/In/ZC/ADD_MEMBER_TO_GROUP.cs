using ROIO.Utils;

public partial class ZC {

    // Official ZC_ADD_MEMBER_TO_GROUP / "partymemberinfo" (packets_struct.hpp:2050,
    // clif.cpp:8955, PACKETVER >= 20171207 header value). Sent when a party
    // member's info changes (join, move, level up, etc). This client has no
    // party window that consumes this, so it's a pure parse-and-discard
    // registration - without it the packet stream desyncs (see
    // PacketHeader.NEWER_PACKETVER region). Wire size: int16(2) + AID(4) +
    // GID(4) + leader(4) + class_(2) + baseLevel(2) + x(2) + y(2) +
    // offline(1) + partyName[24] + playerName[24] + mapName[16] +
    // sharePickup(1) + shareLoot(1) = 89 bytes total.
    [PacketHandler(HEADER, "ZC_ADD_MEMBER_TO_GROUP", SIZE)]
    public class ADD_MEMBER_TO_GROUP : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_ADD_MEMBER_TO_GROUP;
        public const int SIZE = 89;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // 87-byte body; nothing to act on.
        }
    }
}
