using ROIO.Utils;

public partial class ZC {

    // Official ZC_BROADCAST (packets.hpp:146, clif.cpp:7734). Server-wide
    // announcement text. This client doesn't have a chat sink wired up for
    // it yet, so it's a pure parse-and-discard registration for now -
    // without it the packet stream desyncs (see PacketHeader.NEWER_PACKETVER
    // region). SIZE <= 0 tells PacketSerializer to read the packet's own
    // length field (int16 right after the header) and consume exactly that
    // many bytes.
    [PacketHandler(HEADER, "ZC_BROADCAST", SIZE)]
    public class BROADCAST : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_BROADCAST;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // Variable body (message text); nothing to act on yet.
        }
    }
}
