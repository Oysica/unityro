/// <summary>
/// One slot of the shortcut bar as the server keeps it (packets_struct.hpp hotkey_data).
/// </summary>
public class Hotkey {
    public bool IsSkill;
    public int Id;          // item or skill id
    public short Count;     // item amount or skill level

    public bool IsEmpty => Id == 0;
}
