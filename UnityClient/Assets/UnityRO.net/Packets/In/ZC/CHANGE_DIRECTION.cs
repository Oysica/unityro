using ROIO.Utils;

public partial class ZC {

    // Official ZC_CHANGE_DIRECTION (packets.hpp:627), sent whenever another
    // unit turns in place. Unregistered, it desynced the stream and dropped
    // everything batched behind it (see PacketHeader.NEWER_PACKETVER region).
    // Wire size: int16(2) + srcId(4) + headDir(2) + dir(1) = 9 bytes total.
    [PacketHandler(HEADER, "ZC_CHANGE_DIRECTION", SIZE)]
    public class CHANGE_DIRECTION : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_CHANGE_DIRECTION;
        public const int SIZE = 9;
        public PacketHeader Header => HEADER;

        public uint GID;
        public ushort HeadDir;
        public byte Dir;

        public void Read(MemoryStreamReader br, int size) {
            GID = br.ReadUInt();
            HeadDir = br.ReadUShort();
            Dir = (byte) br.ReadByte();
        }
    }
}
