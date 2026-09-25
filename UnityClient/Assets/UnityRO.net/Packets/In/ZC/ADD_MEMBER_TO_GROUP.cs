using ROIO.Utils;
using ROIO.Utils.Extensions;

public partial class ZC {

    // A member joined, or its details changed (clif.cpp clif_party_member_info)
    // 0ae4 <account id>.L <char id>.L <leader: 0 yes>.L <job>.W <base level>.W <x>.W <y>.W <offline>.B
    // <party name>.24B <name>.24B <map>.16B <item pickup share>.B <item loot share>.B
    [PacketHandler(HEADER, "ZC_ADD_MEMBER_TO_GROUP", SIZE)]
    public class ADD_MEMBER_TO_GROUP : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_ADD_MEMBER_TO_GROUP;
        public const int SIZE = 89;
        public PacketHeader Header => HEADER;

        public uint AID;
        public uint GID;
        public bool IsLeader;
        public short Job;
        public short BaseLevel;
        public short X;
        public short Y;
        public bool IsOnline;
        public string PartyName;
        public string Name;
        public string Map;
        public bool SharePickup;
        public bool ShareLoot;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            GID = br.ReadUInt();
            IsLeader = br.ReadUInt() == 0;
            Job = br.ReadShort();
            BaseLevel = br.ReadShort();
            X = br.ReadShort();
            Y = br.ReadShort();
            IsOnline = br.ReadByte() == 0;
            PartyName = br.ReadBinaryString(24).NetworkToText();
            Name = br.ReadBinaryString(24).NetworkToText();
            Map = br.ReadBinaryString(16);
            SharePickup = br.ReadByte() != 0;
            ShareLoot = br.ReadByte() != 0;
        }
    }
}
