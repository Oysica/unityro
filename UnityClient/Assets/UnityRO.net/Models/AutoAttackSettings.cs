using ROIO.Utils;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// The auto attack (內掛) settings the server keeps per character: the AaSettingsBlock that both
/// the snapshot (Pandas.AA_SET_SNAPSHOT) and the update (Pandas.AA_SET_UPDATE) carry, 517 bytes in
/// the order of clif.cpp and the official client's tools/hwid_dll/src/protocol.h.
/// </summary>
public class AutoAttackSettings {

    public const int SIZE = 517;
    public const int SKILL_SLOTS = 5;
    public const int ITEM_SLOTS = 10;
    public const int MOB_SLOTS = 30;
    public const int HEAL_SLOTS = 3;
    public const int ESCAPE_MOB_SLOTS = 15;
    public const int AUTOBUY_SLOTS = 5;
    public const int STORAGE_SLOTS = 20;

    public class SkillSlot {
        public bool Enabled;
        public ushort SkillId;
        public byte Level;
        // The spell picked for skills that ask for one (SA_AUTOSPELL)
        public ushort SubSkillId;
    }

    public class ItemSlot {
        public bool Enabled;
        public uint ItemId;
        // Ignored by the server (the item's status decides when it's used again); kept as sent
        public uint CooldownMs;
    }

    public class HealSlot {
        public bool Enabled;
        public ushort SkillId;
        public byte Level;
        public byte MinHpPercent;
    }

    public class BuySlot {
        public ushort ItemId;
        // Buy when fewer than this are left...
        public ushort ThresholdQty;
        // ...this many
        public ushort BuyQty;
    }

    // Potions: two HP and two SP slots, each firing on its own threshold
    public bool HpPotionEnabled;
    public byte HpPotionThreshold;
    public uint HpPotionItemId;
    public bool SpPotionEnabled;
    public byte SpPotionThreshold;
    public uint SpPotionItemId;
    public bool HpPotionEnabled2;
    public byte HpPotionThreshold2;
    public uint HpPotionItemId2;
    public bool SpPotionEnabled2;
    public byte SpPotionThreshold2;
    public uint SpPotionItemId2;

    public SkillSlot[] BuffSkills = NewArray<SkillSlot>(SKILL_SLOTS);
    public ItemSlot[] BuffItems = NewArray<ItemSlot>(ITEM_SLOTS);
    public SkillSlot[] AttackSkills = NewArray<SkillSlot>(SKILL_SLOTS);

    // Teleport
    public bool TpUseFlyWing;
    public bool TpUseTeleportSkill;
    public byte TpMinHpPercent;
    public byte TpNoMobSeconds;
    public bool TpAvoidMvp;
    public bool TpAvoidMiniBoss;
    public bool TpReturnToSavePoint;
    public byte TpEscapeRange;
    public byte TpMonsterSurround;
    public byte TpKillTimeoutSeconds;

    // Monsters to attack; none attacks all
    public bool MobAggressive;
    public List<uint> MobWhitelist = new List<uint>();

    public bool HatEffectEnabled;
    // 0 stop, 1 save point, 2 log out
    public byte ActionOnEnd;

    public HealSlot[] HealSkills = NewArray<HealSlot>(HEAL_SLOTS);

    // Monsters to teleport away from
    public List<uint> EscapeMobList = new List<uint>();

    // 0 always attack normally, 1 never, 2 only with SP under 100
    public byte StopMelee;
    // Revive items to use when dead, 0 = off, else how many times (1-5)
    public byte TokenSiegfried;
    // 0 off, 1 Green Potion, 2 Panacea, 3 Royal Jelly, 4 Holy Water
    public byte AutoCureMode;
    // Minutes left in a snapshot. In an update any non-zero value sets the time left, so updates
    // always send 0 (see ToBytes)
    public ushort DurationMinutes;

    public BuySlot[] AutoBuy = NewArray<BuySlot>(AUTOBUY_SLOTS);

    // Items put into storage when the bag gets heavy
    public List<uint> StorageList = new List<uint>();

    public bool PvpCounterAttack;
    public bool HealCuresStatus;

    // Sitting to regain HP/SP
    public bool SitEnabled;
    public byte SitMinHp;
    public byte SitMaxHp;
    public byte SitMinSp;
    public byte SitMaxSp;
    public bool SitReactAttack;
    public bool SitReactTeleport;
    public bool SitUseTensionRelax;

    // Picking up loot
    public bool PickupEnabled;
    // 0 fight first, 1 pick up first
    public byte PickupPriority;
    // Bit N allows item type N; 0 picks up everything
    public uint PickupTypeMask;
    public byte PickupEquipMinOptions;

    private static T[] NewArray<T>(int count) where T : new() {
        var array = new T[count];
        for (var i = 0; i < count; i++) {
            array[i] = new T();
        }
        return array;
    }

    public static AutoAttackSettings Read(MemoryStreamReader br) {
        var s = new AutoAttackSettings();

        s.HpPotionEnabled = br.ReadByte() != 0;
        s.HpPotionThreshold = (byte) br.ReadByte();
        s.HpPotionItemId = br.ReadUInt();
        s.SpPotionEnabled = br.ReadByte() != 0;
        s.SpPotionThreshold = (byte) br.ReadByte();
        s.SpPotionItemId = br.ReadUInt();
        s.HpPotionEnabled2 = br.ReadByte() != 0;
        s.HpPotionThreshold2 = (byte) br.ReadByte();
        s.HpPotionItemId2 = br.ReadUInt();
        s.SpPotionEnabled2 = br.ReadByte() != 0;
        s.SpPotionThreshold2 = (byte) br.ReadByte();
        s.SpPotionItemId2 = br.ReadUInt();

        foreach (var slot in s.BuffSkills) {
            ReadSkillSlot(br, slot);
        }
        foreach (var slot in s.BuffItems) {
            slot.Enabled = br.ReadByte() != 0;
            slot.ItemId = br.ReadUInt();
            slot.CooldownMs = br.ReadUInt();
        }
        foreach (var slot in s.AttackSkills) {
            ReadSkillSlot(br, slot);
        }

        s.TpUseFlyWing = br.ReadByte() != 0;
        s.TpUseTeleportSkill = br.ReadByte() != 0;
        s.TpMinHpPercent = (byte) br.ReadByte();
        s.TpNoMobSeconds = (byte) br.ReadByte();
        s.TpAvoidMvp = br.ReadByte() != 0;
        s.TpAvoidMiniBoss = br.ReadByte() != 0;
        s.TpReturnToSavePoint = br.ReadByte() != 0;
        s.TpEscapeRange = (byte) br.ReadByte();
        s.TpMonsterSurround = (byte) br.ReadByte();
        s.TpKillTimeoutSeconds = (byte) br.ReadByte();

        s.MobAggressive = br.ReadByte() != 0;
        s.MobWhitelist = ReadIdList(br, br.ReadByte(), MOB_SLOTS);

        s.HatEffectEnabled = br.ReadByte() != 0;
        s.ActionOnEnd = (byte) br.ReadByte();

        foreach (var slot in s.HealSkills) {
            slot.Enabled = br.ReadByte() != 0;
            slot.SkillId = br.ReadUShort();
            slot.Level = (byte) br.ReadByte();
            slot.MinHpPercent = (byte) br.ReadByte();
        }

        s.EscapeMobList = ReadIdList(br, br.ReadByte(), ESCAPE_MOB_SLOTS);

        s.StopMelee = (byte) br.ReadByte();
        s.TokenSiegfried = (byte) br.ReadByte();
        s.AutoCureMode = (byte) br.ReadByte();
        s.DurationMinutes = br.ReadUShort();

        foreach (var slot in s.AutoBuy) {
            slot.ItemId = br.ReadUShort();
            slot.ThresholdQty = br.ReadUShort();
            slot.BuyQty = br.ReadUShort();
        }

        s.StorageList = ReadIdList(br, br.ReadByte(), STORAGE_SLOTS);

        s.PvpCounterAttack = br.ReadByte() != 0;
        s.HealCuresStatus = br.ReadByte() != 0;

        s.SitEnabled = br.ReadByte() != 0;
        s.SitMinHp = (byte) br.ReadByte();
        s.SitMaxHp = (byte) br.ReadByte();
        s.SitMinSp = (byte) br.ReadByte();
        s.SitMaxSp = (byte) br.ReadByte();
        s.SitReactAttack = br.ReadByte() != 0;
        s.SitReactTeleport = br.ReadByte() != 0;
        s.SitUseTensionRelax = br.ReadByte() != 0;

        s.PickupEnabled = br.ReadByte() != 0;
        s.PickupPriority = (byte) br.ReadByte();
        s.PickupTypeMask = br.ReadUInt();
        s.PickupEquipMinOptions = (byte) br.ReadByte();

        return s;
    }

    private static void ReadSkillSlot(MemoryStreamReader br, SkillSlot slot) {
        slot.Enabled = br.ReadByte() != 0;
        slot.SkillId = br.ReadUShort();
        slot.Level = (byte) br.ReadByte();
        slot.SubSkillId = br.ReadUShort();
    }

    // A count, then every slot of the fixed array; ids past the count mean nothing
    private static List<uint> ReadIdList(MemoryStreamReader br, int count, int slots) {
        var ids = new List<uint>();
        for (var i = 0; i < slots; i++) {
            var id = br.ReadUInt();
            if (i < count && id != 0) {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    /// The block as the server reads it. For an update the minutes left are left out (0 = leave
    /// the time as it is), as the official client does.
    /// </summary>
    public byte[] ToBytes(bool forUpdate = true) {
        using var stream = new MemoryStream(SIZE);
        using var w = new BinaryWriter(stream);

        w.Write(Flag(HpPotionEnabled));
        w.Write(HpPotionThreshold);
        w.Write(HpPotionItemId);
        w.Write(Flag(SpPotionEnabled));
        w.Write(SpPotionThreshold);
        w.Write(SpPotionItemId);
        w.Write(Flag(HpPotionEnabled2));
        w.Write(HpPotionThreshold2);
        w.Write(HpPotionItemId2);
        w.Write(Flag(SpPotionEnabled2));
        w.Write(SpPotionThreshold2);
        w.Write(SpPotionItemId2);

        foreach (var slot in BuffSkills) {
            WriteSkillSlot(w, slot);
        }
        foreach (var slot in BuffItems) {
            w.Write(Flag(slot.Enabled));
            w.Write(slot.ItemId);
            w.Write(slot.CooldownMs);
        }
        foreach (var slot in AttackSkills) {
            WriteSkillSlot(w, slot);
        }

        w.Write(Flag(TpUseFlyWing));
        w.Write(Flag(TpUseTeleportSkill));
        w.Write(TpMinHpPercent);
        w.Write(TpNoMobSeconds);
        w.Write(Flag(TpAvoidMvp));
        w.Write(Flag(TpAvoidMiniBoss));
        w.Write(Flag(TpReturnToSavePoint));
        w.Write(TpEscapeRange);
        w.Write(TpMonsterSurround);
        w.Write(TpKillTimeoutSeconds);

        w.Write(Flag(MobAggressive));
        WriteIdList(w, MobWhitelist, MOB_SLOTS);

        w.Write(Flag(HatEffectEnabled));
        w.Write(ActionOnEnd);

        foreach (var slot in HealSkills) {
            w.Write(Flag(slot.Enabled));
            w.Write(slot.SkillId);
            w.Write(slot.Level);
            w.Write(slot.MinHpPercent);
        }

        WriteIdList(w, EscapeMobList, ESCAPE_MOB_SLOTS);

        w.Write(StopMelee);
        w.Write(TokenSiegfried);
        w.Write(AutoCureMode);
        w.Write(forUpdate ? (ushort) 0 : DurationMinutes);

        foreach (var slot in AutoBuy) {
            w.Write(slot.ItemId);
            w.Write(slot.ThresholdQty);
            w.Write(slot.BuyQty);
        }

        WriteIdList(w, StorageList, STORAGE_SLOTS);

        w.Write(Flag(PvpCounterAttack));
        w.Write(Flag(HealCuresStatus));

        w.Write(Flag(SitEnabled));
        w.Write(SitMinHp);
        w.Write(SitMaxHp);
        w.Write(SitMinSp);
        w.Write(SitMaxSp);
        w.Write(Flag(SitReactAttack));
        w.Write(Flag(SitReactTeleport));
        w.Write(Flag(SitUseTensionRelax));

        w.Write(Flag(PickupEnabled));
        w.Write(PickupPriority);
        w.Write(PickupTypeMask);
        w.Write(PickupEquipMinOptions);

        w.Flush();
        return stream.ToArray();
    }

    private static byte Flag(bool value) => (byte) (value ? 1 : 0);

    private static void WriteSkillSlot(BinaryWriter w, SkillSlot slot) {
        w.Write(Flag(slot.Enabled));
        w.Write(slot.SkillId);
        w.Write(slot.Level);
        w.Write(slot.SubSkillId);
    }

    private static void WriteIdList(BinaryWriter w, List<uint> ids, int slots) {
        var count = System.Math.Min(ids.Count, slots);
        w.Write((byte) count);
        for (var i = 0; i < slots; i++) {
            w.Write(i < count ? ids[i] : 0u);
        }
    }

    public AutoAttackSettings Clone() {
        using var br = new MemoryStreamReader(ToBytes(forUpdate: false));
        return Read(br);
    }

    /// <summary>
    /// Snapshot names are UTF-8 (the server converts them for the official client's overlay), in
    /// fixed fields that may cut a character in half
    /// </summary>
    public static string ReadUtf8Name(MemoryStreamReader br, int length) {
        var bytes = br.ReadBytes(length);
        var end = System.Array.IndexOf(bytes, (byte) 0);
        return Encoding.UTF8.GetString(bytes, 0, end < 0 ? length : end).TrimEnd('�');
    }
}

/// <summary>
/// A skill the character learned, as the snapshot lists them for the skill pickers
/// </summary>
public class AutoAttackLearnedSkill {
    // skill_db Inf bits, plus DEALS_DAMAGE set by the server for skills that deal damage
    public const byte INF_ATTACK = 0x01;
    public const byte INF_GROUND = 0x02;
    public const byte INF_SELF = 0x04;
    public const byte INF_SUPPORT = 0x10;
    public const byte DEALS_DAMAGE = 0x80;

    public ushort SkillId;
    public byte Level;
    public byte Inf;
    public string Name;
}

/// <summary>
/// A consumable in the bag, as the snapshot lists them for the item pickers
/// </summary>
public class AutoAttackInventoryItem {
    public uint ItemId;
    public ushort Amount;
    // rAthena IT_* (0 = healing)
    public byte Type;
    // In the server's list of items usable as buffs
    public bool BuffWhitelisted;
    public string Name;
}

/// <summary>
/// A monster or an item and its name (the snapshot's mob and buy lists)
/// </summary>
public class AutoAttackNamedId {
    public uint Id;
    public string Name;
}
