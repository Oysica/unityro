using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class Pandas {

    // Auto attack (內掛) status, pushed every second to every online player by map-server
    // (clif.cpp clif_aa_status/aa_status_push_timer) for the official client's Gshield.dll
    // overlay, and shown in the auto attack window here. PACKET_AA_STATUS, 101 bytes.
    [PacketHandler(HEADER, "AC_AA_STATUS", SIZE)]
    public class AA_STATUS : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_AA_STATUS;
        public const int SIZE = 101;
        public PacketHeader Header => HEADER;

        public const uint MAGIC = 0xC0DEAA01;

        public const byte STATE_OFF = 0;
        public const byte STATE_AUTO_ATTACK = 1;
        public const byte STATE_AUTO_SUPPORT = 2;

        public uint Magic;
        public byte State;
        public uint ElapsedSeconds;
        public uint KillCount;
        public uint ExpPerHour;
        // Zeny spent this session (the field was per hour once)
        public uint ZenySpent;
        public ushort InventoryUsed;
        public ushort InventoryMax;
        public string Map;
        // Time left to use auto attack; 0 when used up (or unlimited, see Unlimited)
        public uint DurationMsLeft;
        public string CharName;
        public uint Zeny;
        public ushort BaseLevel;
        public string JobName;
        public bool Unlimited;

        public void Read(MemoryStreamReader br, int size) {
            Magic = br.ReadUInt();
            State = (byte) br.ReadByte();
            br.ReadByte(); // reserved
            ElapsedSeconds = br.ReadUInt();
            KillCount = br.ReadUInt();
            ExpPerHour = br.ReadUInt();
            ZenySpent = br.ReadUInt();
            InventoryUsed = br.ReadUShort();
            InventoryMax = br.ReadUShort();
            Map = br.ReadBinaryString(12);
            DurationMsLeft = br.ReadUInt();
            CharName = br.ReadBinaryString(24).NetworkToText();
            Zeny = br.ReadUInt();
            BaseLevel = br.ReadUShort();
            br.ReadUShort(); // padding
            JobName = br.ReadBinaryString(24).NetworkToText();
            Unlimited = br.ReadByte() != 0;
        }
    }
}
