using ROIO.Utils;
using System.Collections.Generic;

public partial class Pandas {

    // Auto attack (內掛) settings, with what the pickers offer: the skills learned, the
    // consumables in the bag, the monsters on the map, names for the saved monster lists and the
    // items auto buy may buy. Sent for Pandas.AA_SET_REQUEST and after an update
    // (clif.cpp clif_aa_send_settings_snapshot, PACKET_AA_SETSNAPSHOT, 7249 bytes).
    [PacketHandler(HEADER, "AC_AA_SET_SNAPSHOT", SIZE)]
    public class AA_SET_SNAPSHOT : InPacket {

        public const PacketHeader HEADER = PacketHeader.AC_AA_SET_SNAPSHOT;
        public const int SIZE = 7249;
        public PacketHeader Header => HEADER;

        public const uint MAGIC = 0xC0DEAA02;

        private const int NAME_LENGTH = 32;
        private const int LEARNED_SKILLS = 40;
        private const int INVENTORY_ITEMS = 30;
        private const int MAP_MOBS = 25;
        private const int SAVED_MOBS = 45;
        private const int BUY_LIST = 50;

        public uint Magic;
        public AutoAttackSettings Settings;
        public List<AutoAttackLearnedSkill> LearnedSkills = new List<AutoAttackLearnedSkill>();
        public List<AutoAttackInventoryItem> InventoryItems = new List<AutoAttackInventoryItem>();
        public List<AutoAttackNamedId> MapMobs = new List<AutoAttackNamedId>();
        public List<AutoAttackNamedId> SavedMobs = new List<AutoAttackNamedId>();
        public List<AutoAttackNamedId> BuyList = new List<AutoAttackNamedId>();
        // VIP only: auto buy and auto storage
        public bool Vip;

        public void Read(MemoryStreamReader br, int size) {
            Magic = br.ReadUInt();
            Settings = AutoAttackSettings.Read(br);

            // Each list is a count and then every slot of its fixed array
            var count = br.ReadByte();
            for (var i = 0; i < LEARNED_SKILLS; i++) {
                var skill = new AutoAttackLearnedSkill {
                    SkillId = br.ReadUShort(),
                    Level = (byte) br.ReadByte(),
                    Inf = (byte) br.ReadByte(),
                    Name = AutoAttackSettings.ReadUtf8Name(br, NAME_LENGTH)
                };
                if (i < count) {
                    LearnedSkills.Add(skill);
                }
            }

            count = br.ReadByte();
            for (var i = 0; i < INVENTORY_ITEMS; i++) {
                var item = new AutoAttackInventoryItem {
                    ItemId = br.ReadUInt(),
                    Amount = br.ReadUShort(),
                    Type = (byte) br.ReadByte(),
                    BuffWhitelisted = br.ReadByte() != 0,
                    Name = AutoAttackSettings.ReadUtf8Name(br, NAME_LENGTH)
                };
                if (i < count) {
                    InventoryItems.Add(item);
                }
            }

            ReadNamedIds(br, MapMobs, MAP_MOBS);
            ReadNamedIds(br, SavedMobs, SAVED_MOBS);
            ReadNamedIds(br, BuyList, BUY_LIST);

            Vip = br.ReadByte() != 0;
        }

        private static void ReadNamedIds(MemoryStreamReader br, List<AutoAttackNamedId> list, int slots) {
            var count = br.ReadByte();
            for (var i = 0; i < slots; i++) {
                var entry = new AutoAttackNamedId {
                    Id = br.ReadUShort(),
                    Name = AutoAttackSettings.ReadUtf8Name(br, NAME_LENGTH)
                };
                if (i < count) {
                    list.Add(entry);
                }
            }
        }
    }
}
