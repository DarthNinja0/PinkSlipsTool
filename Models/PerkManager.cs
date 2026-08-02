namespace PinkSlipsTool.Models;

public class PerkDef
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int StarCost { get; set; }
    public string Color { get; set; }
    public bool NeedsDynastyFile { get; set; } // requires loaded dynasty file
}

public class PerkManager
{
    public static readonly List<PerkDef> DefaultPerks = new()
    {
        new() { Name = "Steal Player", Description = "Take a player from any team", StarCost = 4, Color = "#FF0000", NeedsDynastyFile = true },
        new() { Name = "Dev Upgrade", Description = "Upgrade a player's dev trait", StarCost = 2, Color = "#1E90FF", NeedsDynastyFile = true },
        new() { Name = "Emergency QB", Description = "Convert any WR to QB for one game", StarCost = 2, Color = "#FFD700" },
        new() { Name = "Retire Player", Description = "Force a player to retire immediately", StarCost = 3, Color = "#FF4FA3" },
        new() { Name = "Chat Picks", Description = "View opponent's play calls for one quarter", StarCost = 4, Color = "#32CD32" },
        new() { Name = "Drug Test", Description = "Give any player a one-game injury", StarCost = 3, Color = "#FFA500", NeedsDynastyFile = true },
        new() { Name = "Recruit Boost", Description = "+10% interest on top recruit", StarCost = 5, Color = "#FF69B4" },
        new() { Name = "Transfer Portal", Description = "Guaranteed 5-star transfer next season", StarCost = 4, Color = "#00CED1" },
        new() { Name = "Stadium Upgrade", Description = "Unlock facility upgrade now", StarCost = 5, Color = "#9370DB" },
        new() { Name = "Extra Spin", Description = "Earn another free wheel spin", StarCost = 3, Color = "#FF6347" },
    };

    public int StarsAvailable { get; set; }
    public List<string> PerksApplied { get; set; } = new();
    public DynastyFile DynastyFile { get; set; }

    public bool CanAfford(PerkDef perk) => StarsAvailable >= perk.StarCost;
    public bool CanApply(PerkDef perk) => !perk.NeedsDynastyFile || DynastyFile != null;

    public bool ApplyPerk(PerkDef perk)
    {
        if (!CanAfford(perk)) return false;
        if (perk.NeedsDynastyFile && DynastyFile == null) return false;
        StarsAvailable -= perk.StarCost;
        PerksApplied.Add(perk.Name);
        return true;
    }
}
