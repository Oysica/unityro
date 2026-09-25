using ROIO.Utils;

public partial class ZC {

    // A status already running on a unit that comes into view (clif.cpp clif_efst_status_change).
    // 0984 <id>.L <type>.W <total msec>.L <remain msec>.L { <val>.L }*3
    [PacketHandler(HEADER, "ZC_EFST_SET_ENTER", SIZE)]
    public class EFST_SET_ENTER : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_EFST_SET_ENTER;
        public const int SIZE = 28;
        public PacketHeader Header => HEADER;

        public uint AID;
        public short Type;     // EFST id
        public int TotalMs;
        public int RemainMs;
        public int Val1, Val2, Val3;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            Type = br.ReadShort();
            TotalMs = br.ReadInt();
            RemainMs = br.ReadInt();
            Val1 = br.ReadInt();
            Val2 = br.ReadInt();
            Val3 = br.ReadInt();
        }
    }
}
