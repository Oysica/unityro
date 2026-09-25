using ROIO.Utils;

public partial class HC {

    // The char server refusing to let the account in (char_clif.cpp chclif_reject), e.g. when the
    // login it was handed has expired. Unhandled, the client sat on the server select screen.
    // 006c <error>.B
    [PacketHandler(HEADER, "HC_REFUSE_ENTER", SIZE)]
    public class REFUSE_ENTER : InPacket {

        public const PacketHeader HEADER = PacketHeader.HC_REFUSE_ENTER;
        public const int SIZE = 3;
        public PacketHeader Header => HEADER;

        public byte ErrorCode;

        public void Read(MemoryStreamReader br, int size) {
            ErrorCode = (byte) br.ReadByte();
        }
    }
}
