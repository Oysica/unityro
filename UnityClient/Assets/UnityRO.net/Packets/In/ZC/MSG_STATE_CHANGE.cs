using ROIO.Utils;

public partial class ZC {

    // A status ending, or starting without a timer (clif.cpp clif_status_change_sub).
    // 0196 <type>.W <id>.L <state>.B
    [PacketHandler(HEADER, "ZC_MSG_STATE_CHANGE", SIZE)]
    public class MSG_STATE_CHANGE : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_MSG_STATE_CHANGE;
        public const int SIZE = 9;
        public PacketHeader Header => HEADER;

        public short Type;     // EFST id
        public uint AID;
        public byte State;     // 1 = on, 0 = off

        public void Read(MemoryStreamReader br, int size) {
            Type = br.ReadShort();
            AID = br.ReadUInt();
            State = (byte) br.ReadByte();
        }
    }
}
