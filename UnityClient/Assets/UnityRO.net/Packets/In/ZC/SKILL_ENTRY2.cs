using ROIO.Utils;

public partial class ZC {

    // Official ZC_SKILL_ENTRY2 (packet_graffiti_entry, packets_struct.hpp:1457),
    // the Graffiti ground unit with its message. This client doesn't render
    // skill units, so this is a pure parse-and-discard registration - without
    // it the packet stream desyncs (see PacketHeader.NEWER_PACKETVER region).
    // Wire size: int16(2) + AID(4) + creatorAID(4) + x(2) + y(2) + job(1)
    // + isVisible(1) + isContens(1) + msg(80) = 97 bytes total.
    [PacketHandler(HEADER, "ZC_SKILL_ENTRY2", SIZE)]
    public class SKILL_ENTRY2 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_SKILL_ENTRY2;
        public const int SIZE = 97;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // Nothing to act on.
        }
    }
}
