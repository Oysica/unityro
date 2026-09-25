using ROIO.Utils;

public partial class ZC {

    // A party member's HP, to members nearby (clif.cpp clif_party_hp)
    // 080e <account id>.L <hp>.L <max hp>.L
    [PacketHandler(HEADER, "ZC_NOTIFY_HP_TO_GROUPM", SIZE)]
    public class NOTIFY_HP_TO_GROUPM : InPacket {

        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_HP_TO_GROUPM;
        public const int SIZE = 14;
        public PacketHeader Header => HEADER;

        public uint AID;
        public int Hp;
        public int MaxHp;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            Hp = br.ReadInt();
            MaxHp = br.ReadInt();
        }
    }
}
