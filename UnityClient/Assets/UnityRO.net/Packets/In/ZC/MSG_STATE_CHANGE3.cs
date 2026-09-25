using ROIO.Utils;

public partial class ZC {

    // A status starting, with its duration (clif.cpp clif_status_change_sub).
    // 0983 <type>.W <id>.L <state>.B <total msec>.L <remain msec>.L { <val>.L }*3
    [PacketHandler(HEADER, "ZC_MSG_STATE_CHANGE3", SIZE)]
    public class MSG_STATE_CHANGE3 : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_MSG_STATE_CHANGE3;
        public const int SIZE = 29;
        public PacketHeader Header => HEADER;

        public short Type;     // EFST id
        public uint AID;
        public byte State;     // 1 = on, 0 = off
        public int TotalMs;
        public int RemainMs;
        public int Val1, Val2, Val3;

        public void Read(MemoryStreamReader br, int size) {
            Type = br.ReadShort();
            AID = br.ReadUInt();
            State = (byte) br.ReadByte();
            TotalMs = br.ReadInt();
            RemainMs = br.ReadInt();
            Val1 = br.ReadInt();
            Val2 = br.ReadInt();
            Val3 = br.ReadInt();
        }
    }
}
