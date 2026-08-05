namespace PinkSlipsTool.Models;

public class PerkDef
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int StarCost { get; set; }
    public string Color { get; set; }
    public bool NeedsDynastyFile { get; set; } // requires loaded dynasty file
    public string ShortName { get; set; }      // optional short label for the wheel slice
}

public class PerkManager
{
    public static readonly List<PerkDef> DefaultPerks = new()
    {
        new() { Name = "Steal Player", Description = "Take a player from any team", StarCost = 4, Color = "#FF0000", NeedsDynastyFile = true, ShortName = "Steal" },
        new() { Name = "Dev Upgrade", Description = "Upgrade a player's dev trait", StarCost = 2, Color = "#1E90FF", NeedsDynastyFile = true, ShortName = "Dev Up" },
        new() { Name = "Dev Downgrade", Description = "Downgrade a player's dev trait (penalty)", StarCost = 1, Color = "#8B0000", NeedsDynastyFile = true, ShortName = "Dev Down" },
        new() { Name = "Injury Heal", Description = "Instantly heal a player's injury", StarCost = 3, Color = "#00FF7F", NeedsDynastyFile = true, ShortName = "Heal" },
        new() { Name = "Transfer Shock", Description = "Send one of your players to a rival team (penalty)", StarCost = 2, Color = "#FF4500", NeedsDynastyFile = true, ShortName = "Shock" },
        new() { Name = "Drug Test", Description = "Give a player a one-game injury", StarCost = 3, Color = "#FFA500", NeedsDynastyFile = true, ShortName = "Drug Test" },
        new() { Name = "Team Illness", Description = "A random player gets injured (penalty)", StarCost = 2, Color = "#FF8C00", NeedsDynastyFile = true, ShortName = "Illness" },
        new() { Name = "Academic Ineligibility", Description = "A player must sit the game (penalty)", StarCost = 2, Color = "#B22222", NeedsDynastyFile = true, ShortName = "Ineligible" },
        new() { Name = "Position Coach", Description = "Change a player's position", StarCost = 2, Color = "#00CED1", NeedsDynastyFile = true, ShortName = "Position" },
        new() { Name = "Fifth Year", Description = "Grant a player an extra year of eligibility", StarCost = 2, Color = "#7FFF00", NeedsDynastyFile = true, ShortName = "5th Year" },
        new() { Name = "FA Sign", Description = "Sign any free agent to your team", StarCost = 3, Color = "#4169E1", NeedsDynastyFile = true, ShortName = "FA Sign" },
        new() { Name = "Double Steal", Description = "Steal TWO players from any teams", StarCost = 6, Color = "#FF1493", NeedsDynastyFile = true, ShortName = "2x Steal" },
        new() { Name = "Emergency QB", Description = "Convert any WR to QB for one game", StarCost = 2, Color = "#FFD700", ShortName = "Emrg QB" },
        new() { Name = "Retire Player", Description = "Force a player to retire immediately", StarCost = 3, Color = "#FF4FA3", ShortName = "Retire" },
        new() { Name = "Chat Picks", Description = "View opponent's play calls for one quarter", StarCost = 4, Color = "#32CD32", ShortName = "Chat" },
        new() { Name = "Recruit Boost", Description = "+10% interest on top recruit", StarCost = 5, Color = "#FF69B4", ShortName = "Recruit" },
        new() { Name = "Transfer Portal", Description = "Guaranteed 5-star transfer next season", StarCost = 4, Color = "#00CED1", ShortName = "Portal" },
        new() { Name = "Stadium Upgrade", Description = "Unlock facility upgrade now", StarCost = 5, Color = "#9370DB", ShortName = "Stadium" },
        new() { Name = "NIL Boost", Description = "+NIL money for recruiting", StarCost = 5, Color = "#00FF00", ShortName = "NIL" },
        new() { Name = "Playbook Leak", Description = "See opponent's first play this week", StarCost = 3, Color = "#7CFC00", ShortName = "Leak" },
        new() { Name = "Red Shirt", Description = "Protect a player from injury penalties for a week", StarCost = 2, Color = "#DC143C", ShortName = "Red Shirt" },
        new() { Name = "Facility Boost", Description = "Pick the bonus for next week's training", StarCost = 5, Color = "#FF00FF", ShortName = "Facility" },
        new() { Name = "Extra Spin", Description = "Earn another free wheel spin", StarCost = 3, Color = "#FF6347", ShortName = "Extra Spin" },
    };

    // Curated wheel slices (fewer so the labels stay readable). Penalties are weighted
    // to land more often; the big rewards are rare. Instances are shared with DefaultPerks.
    public static readonly List<PerkDef> WheelPerks = new()
    {
        DefaultPerks[0],  // Steal Player     (weight 1)
        DefaultPerks[1],  // Dev Upgrade      (weight 2)
        DefaultPerks[2],  // Dev Downgrade    (weight 5)
        DefaultPerks[3],  // Injury Heal      (weight 1)
        DefaultPerks[4],  // Transfer Shock   (weight 4)
        DefaultPerks[5],  // Drug Test        (weight 5)
        DefaultPerks[6],  // Team Illness     (weight 4)
        DefaultPerks[7],  // Ineligibility    (weight 4)
        DefaultPerks[9],  // Fifth Year       (weight 2)
        DefaultPerks[10], // FA Sign          (weight 2)
        DefaultPerks[13], // Retire Player    (weight 4)
        DefaultPerks[22], // Extra Spin       (weight 3)
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
