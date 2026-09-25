// Party requests (clif.cpp clif_parse_CreateParty2, PartyInvite2, ReplyPartyInvite2, LeaveParty,
// RemovePartyMember, PartyMessage, PartyChangeOption, PartyChangeLeader, PartyTick)
public partial class CZ {

    // 01e8 <party name>.24B <item pickup share>.B <item loot share>.B
    public class MAKE_GROUP2 : OutPacket {
        public const int SIZE = 28;

        private readonly string name;
        private readonly bool sharePickup;
        private readonly bool shareLoot;

        public MAKE_GROUP2(string name, bool sharePickup, bool shareLoot) : base(PacketHeader.CZ_MAKE_GROUP2, SIZE) {
            this.name = name;
            this.sharePickup = sharePickup;
            this.shareLoot = shareLoot;
        }

        public override void Send() {
            Write(name, 24);
            Write((byte) (sharePickup ? 1 : 0));
            Write((byte) (shareLoot ? 1 : 0));
            base.Send();
        }
    }

    // Invites a character by name. 02c4 <name>.24B
    public class PARTY_JOIN_REQ : OutPacket {
        public const int SIZE = 26;

        private readonly string name;

        public PARTY_JOIN_REQ(string name) : base(PacketHeader.CZ_PARTY_JOIN_REQ, SIZE) {
            this.name = name;
        }

        public override void Send() {
            Write(name, 24);
            base.Send();
        }
    }

    // Answers an invite. 02c7 <party id>.L <accept>.B
    public class PARTY_JOIN_REQ_ACK : OutPacket {
        public const int SIZE = 7;

        private readonly uint partyId;
        private readonly bool accept;

        public PARTY_JOIN_REQ_ACK(uint partyId, bool accept) : base(PacketHeader.CZ_PARTY_JOIN_REQ_ACK, SIZE) {
            this.partyId = partyId;
            this.accept = accept;
        }

        public override void Send() {
            Write(partyId);
            Write((byte) (accept ? 1 : 0));
            base.Send();
        }
    }

    // 0100
    public class REQ_LEAVE_GROUP : OutPacket {
        public const int SIZE = 2;

        public REQ_LEAVE_GROUP() : base(PacketHeader.CZ_REQ_LEAVE_GROUP, SIZE) { }
    }

    // Leader only. 0103 <account id>.L <name>.24B
    public class REQ_EXPEL_GROUP_MEMBER : OutPacket {
        public const int SIZE = 30;

        private readonly uint accountId;
        private readonly string name;

        public REQ_EXPEL_GROUP_MEMBER(uint accountId, string name) : base(PacketHeader.CZ_REQ_EXPEL_GROUP_MEMBER, SIZE) {
            this.accountId = accountId;
            this.name = name;
        }

        public override void Send() {
            Write(accountId);
            Write(name, 24);
            base.Send();
        }
    }

    // Party chat; the server wants "name : message", as for public chat. 0108 <len>.W <message>.?B
    public class REQUEST_CHAT_PARTY : OutPacket {

        private readonly string message;

        public REQUEST_CHAT_PARTY(string message) : base(PacketHeader.CZ_REQUEST_CHAT_PARTY, -1) {
            this.message = $"{Session.CurrentSession.Entity.GetBaseStatus().name} : {message}";
        }

        public override void Send() {
            Write(message);
            base.Send();
        }
    }

    // Leader only. 07d7 <exp shared>.L <item pickup share>.B <item loot share>.B
    public class GROUPINFO_CHANGE_V2 : OutPacket {
        public const int SIZE = 8;

        private readonly bool shareExp;
        private readonly bool sharePickup;
        private readonly bool shareLoot;

        public GROUPINFO_CHANGE_V2(bool shareExp, bool sharePickup, bool shareLoot) : base(PacketHeader.CZ_GROUPINFO_CHANGE_V2, SIZE) {
            this.shareExp = shareExp;
            this.sharePickup = sharePickup;
            this.shareLoot = shareLoot;
        }

        public override void Send() {
            Write(shareExp ? 1 : 0);
            Write((byte) (sharePickup ? 1 : 0));
            Write((byte) (shareLoot ? 1 : 0));
            base.Send();
        }
    }

    // Leader only; the new leader must be on the same map. 07da <account id>.L
    public class CHANGE_GROUP_MASTER : OutPacket {
        public const int SIZE = 6;

        private readonly uint accountId;

        public CHANGE_GROUP_MASTER(uint accountId) : base(PacketHeader.CZ_CHANGE_GROUP_MASTER, SIZE) {
            this.accountId = accountId;
        }

        public override void Send() {
            Write(accountId);
            base.Send();
        }
    }

    // Whether to refuse party invites. 02c8 <refuse>.B
    public class PARTY_CONFIG : OutPacket {
        public const int SIZE = 3;

        private readonly bool refuse;

        public PARTY_CONFIG(bool refuse) : base(PacketHeader.CZ_PARTY_CONFIG, SIZE) {
            this.refuse = refuse;
        }

        public override void Send() {
            Write((byte) (refuse ? 1 : 0));
            base.Send();
        }
    }
}
