// Player trade requests (clif.cpp clif_parse_TradeRequest, TradeAck, TradeAddItem, TradeOk,
// TradeCancel, TradeCommit)
public partial class CZ {

    // Asks them to trade. 00e4 <account id>.L
    public class REQ_EXCHANGE_ITEM : OutPacket {
        public const int SIZE = 6;
        private readonly uint accountId;

        public REQ_EXCHANGE_ITEM(uint accountId) : base(PacketHeader.CZ_REQ_EXCHANGE_ITEM, SIZE) {
            this.accountId = accountId;
        }

        public override void Send() {
            Write(accountId);
            base.Send();
        }
    }

    // Our answer to a trade asked of us. 00e6 <result: 3 accept, 4 refuse>.B
    public class ACK_EXCHANGE_ITEM : OutPacket {
        public const int SIZE = 3;
        private readonly bool accept;

        public ACK_EXCHANGE_ITEM(bool accept) : base(PacketHeader.CZ_ACK_EXCHANGE_ITEM, SIZE) {
            this.accept = accept;
        }

        public override void Send() {
            Write((byte) (accept ? 3 : 4));
            base.Send();
        }
    }

    // Puts an item of the inventory (its index) or zeny (index 0) in. 00e8 <index>.W <amount>.L
    public class ADD_EXCHANGE_ITEM : OutPacket {
        public const int SIZE = 8;
        private readonly short index;
        private readonly int amount;

        public ADD_EXCHANGE_ITEM(short index, int amount) : base(PacketHeader.CZ_ADD_EXCHANGE_ITEM, SIZE) {
            this.index = index;
            this.amount = amount;
        }

        public override void Send() {
            Write(index);
            Write(amount);
            base.Send();
        }
    }

    // OK: nothing more goes in from us. 00eb
    public class CONCLUDE_EXCHANGE_ITEM : OutPacket {
        public CONCLUDE_EXCHANGE_ITEM() : base(PacketHeader.CZ_CONCLUDE_EXCHANGE_ITEM, 2) { }
    }

    // 00ed
    public class CANCEL_EXCHANGE_ITEM : OutPacket {
        public CANCEL_EXCHANGE_ITEM() : base(PacketHeader.CZ_CANCEL_EXCHANGE_ITEM, 2) { }
    }

    // Both OK: the trade goes through. 00ef
    public class EXEC_EXCHANGE_ITEM : OutPacket {
        public EXEC_EXCHANGE_ITEM() : base(PacketHeader.CZ_EXEC_EXCHANGE_ITEM, 2) { }
    }
}
