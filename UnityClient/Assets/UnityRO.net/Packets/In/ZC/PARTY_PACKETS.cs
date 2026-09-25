using ROIO.Utils;
using ROIO.Utils.Extensions;

// Party packets without a file of their own (clif.cpp clif_party_*, packets_struct.hpp)
public partial class ZC {

    // 00fa <result: 0 made, 1 name taken, 2 already in a party, 3 not on this map>.B
    [PacketHandler(HEADER, "ZC_ACK_MAKE_GROUP", SIZE)]
    public class ACK_MAKE_GROUP : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ACK_MAKE_GROUP;
        public const int SIZE = 3;
        public PacketHeader Header => HEADER;

        public byte Result;

        public void Read(MemoryStreamReader br, int size) {
            Result = (byte) br.ReadByte();
        }
    }

    // Asked to join a party. 02c6 <party id>.L <party name>.24B
    [PacketHandler(HEADER, "ZC_PARTY_JOIN_REQ", SIZE)]
    public class PARTY_JOIN_REQ : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_PARTY_JOIN_REQ;
        public const int SIZE = 30;
        public PacketHeader Header => HEADER;

        public uint PartyId;
        public string PartyName;

        public void Read(MemoryStreamReader br, int size) {
            PartyId = br.ReadUInt();
            PartyName = br.ReadBinaryString(24).NetworkToText();
        }
    }

    // What came of an invite we sent (clif_party_invite_reply). 02c5 <name>.24B <result>.L
    [PacketHandler(HEADER, "ZC_PARTY_JOIN_REQ_ACK", SIZE)]
    public class PARTY_JOIN_REQ_ACK : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_PARTY_JOIN_REQ_ACK;
        public const int SIZE = 30;
        public PacketHeader Header => HEADER;

        public string Name;
        public int Result;

        public void Read(MemoryStreamReader br, int size) {
            Name = br.ReadBinaryString(24).NetworkToText();
            Result = br.ReadInt();
        }
    }

    // A member left or was expelled (clif_party_withdraw)
    // 0105 <account id>.L <name>.24B <result: 0 left, 1 expelled, 2 may not leave, 3 may not be expelled>.B
    [PacketHandler(HEADER, "ZC_DELETE_MEMBER_FROM_GROUP", SIZE)]
    public class DELETE_MEMBER_FROM_GROUP : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_DELETE_MEMBER_FROM_GROUP;
        public const int SIZE = 31;
        public PacketHeader Header => HEADER;

        public uint AID;
        public string Name;
        public byte Result;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            Name = br.ReadBinaryString(24).NetworkToText();
            Result = (byte) br.ReadByte();
        }
    }

    // Party chat, "name : message" (clif_party_message). 0109 <len>.W <account id>.L <message>.?B
    [PacketHandler(HEADER, "ZC_NOTIFY_CHAT_PARTY", SIZE)]
    public class NOTIFY_CHAT_PARTY : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_CHAT_PARTY;
        public const int SIZE = -1;
        public PacketHeader Header => HEADER;

        public uint AID;
        public string Message;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            Message = br.ReadBinaryString(size - 4).NetworkToText();
        }
    }

    // The leader changed (clif_party_leaderchanged). 07fc <old leader>.L <new leader>.L
    [PacketHandler(HEADER, "ZC_CHANGE_GROUP_MASTER", SIZE)]
    public class CHANGE_GROUP_MASTER : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_CHANGE_GROUP_MASTER;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public uint OldLeaderAID;
        public uint NewLeaderAID;

        public void Read(MemoryStreamReader br, int size) {
            OldLeaderAID = br.ReadUInt();
            NewLeaderAID = br.ReadUInt();
        }
    }

    // A member died or came back (clif_party_dead). 0ab2 <account id>.L <dead>.B
    [PacketHandler(HEADER, "ZC_GROUP_ISALIVE", SIZE)]
    public class GROUP_ISALIVE : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_GROUP_ISALIVE;
        public const int SIZE = 7;
        public PacketHeader Header => HEADER;

        public uint AID;
        public bool IsDead;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            IsDead = br.ReadByte() != 0;
        }
    }

    // A member's job or level changed (clif_party_job_and_level). 0abd <account id>.L <job>.W <level>.W
    [PacketHandler(HEADER, "ZC_NOTIFY_MEMBERINFO_TO_GROUPM", SIZE)]
    public class NOTIFY_MEMBERINFO_TO_GROUPM : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_NOTIFY_MEMBERINFO_TO_GROUPM;
        public const int SIZE = 10;
        public PacketHeader Header => HEADER;

        public uint AID;
        public short Job;
        public short BaseLevel;

        public void Read(MemoryStreamReader br, int size) {
            AID = br.ReadUInt();
            Job = br.ReadShort();
            BaseLevel = br.ReadShort();
        }
    }
}
