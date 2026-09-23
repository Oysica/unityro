using ROIO.Utils;

public partial class ZC {

    // Official ZC_LIST_EMOTE (packets_struct.hpp:6364, PACKETVER >=
    // 20230920). Variable-length list of available emotes - this client
    // has its own emote list UI unrelated to this packet, so it's a pure
    // parse-and-discard registration - without it the packet stream
    // desyncs (see PacketHeader.NEWER_PACKETVER region). SIZE <= 0 tells
    // PacketSerializer to read the packet's own length field (int16 right
    // after the header) and consume exactly that many bytes.
    [PacketHandler(HEADER, "ZC_LIST_EMOTE", SIZE)]
    public class LIST_EMOTE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_LIST_EMOTE;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) {
            // Variable body (synchroTime + unknown + sublist[]); nothing to act on.
        }
    }
}
