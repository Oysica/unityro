using ROIO.Utils;
using ROIO.Utils.Extensions;

// Friend list packets besides the list itself (clif.cpp clif_friendslist_toggle,
// clif_friendlist_req, clif_friendslist_reqack, clif_parse_FriendsListRemove)
public partial class ZC {

    // A friend logged in or out. 0206 <account id>.L <char id>.L <offline>.B <name>.24B
    [PacketHandler(HEADER, "ZC_FRIENDS_STATE", SIZE)]
    public class FRIENDS_STATE : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_FRIENDS_STATE;
        public const int SIZE = 35;
        public PacketHeader Header => HEADER;

        public uint AID;
        public uint CID;
        public bool IsOnline;
        public string Name;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            CID = br.ReadUInt();
            IsOnline = br.ReadByte() == 0;
            Name = br.ReadBinaryString(24).NetworkToText();
        }
    }

    // Someone asks to be our friend. 0207 <account id>.L <char id>.L <name>.24B
    [PacketHandler(HEADER, "ZC_REQ_ADD_FRIENDS", SIZE)]
    public class REQ_ADD_FRIENDS : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_REQ_ADD_FRIENDS;
        public const int SIZE = 34;
        public PacketHeader Header => HEADER;

        public uint AID;
        public uint CID;
        public string Name;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            CID = br.ReadUInt();
            Name = br.ReadBinaryString(24).NetworkToText();
        }
    }

    // What came of a friend request, ours or answered by us.
    // 0209 <result: 0 friends now, 1 refused, 2 our list full, 3 their list full>.W <account id>.L <char id>.L <name>.24B
    [PacketHandler(HEADER, "ZC_ADD_FRIENDS_LIST", SIZE)]
    public class ADD_FRIENDS_LIST : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ADD_FRIENDS_LIST;
        public const int SIZE = 36;
        public PacketHeader Header => HEADER;

        public ushort Result;
        public uint AID;
        public uint CID;
        public string Name;

        public void Read(MemoryStreamReader br, int size) {
            Result = br.ReadUShort();
            AID = br.ReadUInt();
            CID = br.ReadUInt();
            Name = br.ReadBinaryString(24).NetworkToText();
        }
    }

    // Off our friend list (removed by us or by them). 020a <account id>.L <char id>.L
    [PacketHandler(HEADER, "ZC_DELETE_FRIENDS", SIZE)]
    public class DELETE_FRIENDS : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_DELETE_FRIENDS;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public uint AID;
        public uint CID;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            CID = br.ReadUInt();
        }
    }
}
