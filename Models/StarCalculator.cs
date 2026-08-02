namespace PinkSlipsTool.Models;

public class StarCalculation
{
    public int TotalStars { get; set; }
    public bool PerfectGame { get; set; }
    public List<string> ConditionsMet { get; set; } = new();
}

public class StarCalculator
{
    public StarCalculation Calculate(int myScore, int oppScore, int turnoverDiff,
        int passYards, int passTDs, int rushYards, int rushTDs,
        int recYards, int recTDs, int sacks, int ints, int defTDs, int stTDs)
    {
        var result = new StarCalculation();
        var stars = 0;

        var won = myScore > oppScore;
        var winMargin = myScore - oppScore;

        if (won)
        {
            stars++;
            result.ConditionsMet.Add("Win");
        }

        if (winMargin >= 14)
        {
            stars++;
            result.ConditionsMet.Add("Win by 14+");
        }
        if (winMargin >= 21)
        {
            stars++;
            result.ConditionsMet.Add("Win by 21+");
        }

        if (oppScore == 0)
        {
            stars += 2;
            result.ConditionsMet.Add("Shutout (0 points allowed)");
        }

        var conditions = 0;
        if (passYards >= 300) conditions++;
        if (rushYards >= 100) conditions++;
        if (recYards >= 100) conditions++;
        if (conditions > 0)
        {
            stars += conditions;
            if (passYards >= 300) result.ConditionsMet.Add($"300+ passing yards ({passYards})");
            if (rushYards >= 100) result.ConditionsMet.Add($"100+ rushing yards ({rushYards})");
            if (recYards >= 100) result.ConditionsMet.Add($"100+ receiving yards ({recYards})");
        }

        var tdConditions = 0;
        if (passTDs >= 3) tdConditions++;
        if (rushTDs >= 2) tdConditions++;
        if (recTDs >= 2) tdConditions++;
        if (tdConditions > 0)
        {
            stars += tdConditions;
            if (passTDs >= 3) result.ConditionsMet.Add($"3+ pass TD ({passTDs})");
            if (rushTDs >= 2) result.ConditionsMet.Add($"2+ rush TD ({rushTDs})");
            if (recTDs >= 2) result.ConditionsMet.Add($"2+ rec TD ({recTDs})");
        }

        if (sacks >= 2)
        {
            stars++;
            result.ConditionsMet.Add($"2+ sacks ({sacks})");
        }
        if (ints >= 1)
        {
            stars++;
            result.ConditionsMet.Add($"1+ INT ({ints})");
        }

        if (defTDs > 0)
        {
            stars += defTDs;
            result.ConditionsMet.Add($"Defensive TD ({defTDs})");
        }

        if (turnoverDiff > 0)
        {
            stars++;
            result.ConditionsMet.Add($"Win turnover battle (+{turnoverDiff})");
        }

        if (stTDs > 0)
        {
            stars += stTDs;
            result.ConditionsMet.Add($"Special teams TD ({stTDs})");
        }

        result.TotalStars = Math.Min(stars, 10);
        result.PerfectGame = stars >= 10;

        return result;
    }
}
