using ROIO.Utils;
using ROIO.Utils.Extensions;

// Player trade (clif.cpp clif_traderequest, clif_traderesponse, clif_tradeadditem, clif_tradeitemok,
// clif_tradedeal_lock, clif_tradecancelled, clif_tradecompleted, clif_tradeundo)
public partial class ZC {

    // Someone asks to trade with us. 01f4 <name>.24B <char id>.L <base level>.W
    [PacketHandler(HEADER, "ZC_REQ_EXCHANGE_ITEM2", SIZE)]
    public class REQ_EXCHANGE_ITEM2 : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_REQ_EXCHANGE_ITEM2;
        public const int SIZE = 32;
        public PacketHeader Header => HEADER;

        public string Name;
        public uint CharId;
        public short Level;

        public void Read(MemoryStreamReader br, int size) {
            Name = br.ReadBinaryString(24).NetworkToText();
            CharId = br.ReadUInt();
            Level = br.ReadShort();
        }
    }

    // The answer to a trade asked for, ours or theirs.
    // 01f5 <result: 0 too far, 1 no such character, 2 failed, 3 accepted, 4 refused, 5 busy>.B <char id>.L <base level>.W
    [PacketHandler(HEADER, "ZC_ACK_EXCHANGE_ITEM2", SIZE)]
    public class ACK_EXCHANGE_ITEM2 : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ACK_EXCHANGE_ITEM2;
        public const int SIZE = 9;
        public PacketHeader Header => HEADER;

        public byte Result;
        public uint CharId;
        public short Level;

        public void Read(MemoryStreamReader br, int size) {
            Result = (byte) br.ReadByte();
            CharId = br.ReadUInt();
            Level = br.ReadShort();
        }
    }

    // What the other side put in; item id 0 is zeny, in the amount.
    // 0b42 <item id>.L <type>.B <amount>.L <identified>.B <damaged>.B <card>.L*4
    //      { <option id>.W <value>.W <param>.B }*5 <location>.L <look>.W <refine>.B <grade>.B
    [PacketHandler(HEADER, "ZC_ADD_EXCHANGE_ITEM4", SIZE)]
    public class ADD_EXCHANGE_ITEM4 : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ADD_EXCHANGE_ITEM4;
        public const int SIZE = 62;
        public PacketHeader Header => HEADER;

        public uint ItemId;
        public byte ItemType;
        public int Amount;
        public bool Identified;
        public bool Damaged;
        public uint[] Cards = new uint[4];
        public byte Refine;
        public byte Grade;

        public void Read(MemoryStreamReader br, int size) {
            ItemId = br.ReadUInt();
            ItemType = (byte) br.ReadByte();
            Amount = br.ReadInt();
            Identified = br.ReadByte() != 0;
            Damaged = br.ReadByte() != 0;
            for (var i = 0; i < Cards.Length; i++) {
                Cards[i] = br.ReadUInt();
            }
            // The random options, where it's worn and its look: not shown
            br.ReadBytes(5 * 5 + 4 + 2);
            Refine = (byte) br.ReadByte();
            Grade = (byte) br.ReadByte();
        }
    }

    // What came of an item we put in. 00ea <index>.W <result: 0 in, 1 they'd be overweight, 2 trade over, 3 their inventory full, 4 too many of it>.B
    [PacketHandler(HEADER, "ZC_ACK_ADD_EXCHANGE_ITEM", SIZE)]
    public class ACK_ADD_EXCHANGE_ITEM : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_ACK_ADD_EXCHANGE_ITEM;
        public const int SIZE = 5;
        public PacketHeader Header => HEADER;

        public short Index;
        public byte Result;

        public void Read(MemoryStreamReader br, int size) {
            Index = br.ReadShort();
            Result = (byte) br.ReadByte();
        }
    }

    // A side pressed OK. 00ec <who: 0 us, 1 them>.B
    [PacketHandler(HEADER, "ZC_CONCLUDE_EXCHANGE_ITEM", SIZE)]
    public class CONCLUDE_EXCHANGE_ITEM : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_CONCLUDE_EXCHANGE_ITEM;
        public const int SIZE = 3;
        public PacketHeader Header => HEADER;

        public bool Them;

        public void Read(MemoryStreamReader br, int size) {
            Them = br.ReadByte() != 0;
        }
    }

    // 00ee
    [PacketHandler(HEADER, "ZC_CANCEL_EXCHANGE_ITEM", SIZE)]
    public class CANCEL_EXCHANGE_ITEM : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_CANCEL_EXCHANGE_ITEM;
        public const int SIZE = 2;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) { }
    }

    // The trade done. 00f0 <result: 0 done, 1 failed>.B
    [PacketHandler(HEADER, "ZC_EXEC_EXCHANGE_ITEM", SIZE)]
    public class EXEC_EXCHANGE_ITEM : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_EXEC_EXCHANGE_ITEM;
        public const int SIZE = 3;
        public PacketHeader Header => HEADER;

        public byte Result;

        public void Read(MemoryStreamReader br, int size) {
            Result = (byte) br.ReadByte();
        }
    }

    // 00f1 (the server's own note: unknown purpose)
    [PacketHandler(HEADER, "ZC_EXCHANGEITEM_UNDO", SIZE)]
    public class EXCHANGEITEM_UNDO : InPacket {
        public const PacketHeader HEADER = PacketHeader.ZC_EXCHANGEITEM_UNDO;
        public const int SIZE = 2;
        public PacketHeader Header => HEADER;

        public void Read(MemoryStreamReader br, int size) { }
    }
}
