/// <summary>
/// Whether players may fight on this map, as the server says with each one
/// (ZC_NOTIFY_MAPPROPERTY2, clif.cpp clif_map_property): on PvP maps whoever isn't in the party,
/// on GvG maps, battlegrounds and in a guild war whoever isn't in the guild. The server has the last
/// word on each attack
/// </summary>
public static class MapRules {

    private const int PARTY = 1 << 0; // PvP: anyone out of the party
    private const int GUILD = 1 << 1; // GvG, battlegrounds, guild wars: anyone out of the guild

    private static int Flags;

    public static void Set(int flags) {
        Flags = flags;
    }

    /// <summary>
    /// A player who may be attacked here
    /// </summary>
    public static bool IsEnemy(Entity entity) {
        if (entity == null || entity.Type != EntityType.PC) {
            return false;
        }
        var self = Session.CurrentSession != null ? Session.CurrentSession.Entity as Entity : null;
        if (self == null || entity == self) {
            return false;
        }

        if ((Flags & PARTY) != 0 && !InMyParty(entity)) {
            return true;
        }
        return (Flags & GUILD) != 0 && !InMyGuild(entity, self);
    }

    private static bool InMyParty(Entity entity) {
        var party = PartyWindow.Instance;
        return party != null && party.InParty && party.Members.Exists(member => member.AID == entity.AID);
    }

    private static bool InMyGuild(Entity entity, Entity self) {
        return self.Status.guild_id != 0 && entity.Status.guild_id == self.Status.guild_id;
    }
}
