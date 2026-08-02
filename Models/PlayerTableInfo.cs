namespace PinkSlipsTool.Models;

public static class PlayerTableInfo
{
    public const int TableId = 4248;

    // Offset-table positions verified against the CFB25 (C27) save schema
    // (assetId 6494552). Fields are read from the packed (repacked) record layout.
    public const int FirstNameIdx = 146;
    public const int LastNameIdx = 174;
    public const int JerseyNumIdx = 168;
    public const int PositionIdx = 3;
    public const int OverallRatingIdx = 198;
    public const int SchoolYearIdx = 252;
    public const int TeamIndexIdx = 272;
    public const int TraitDevelopmentIdx = 282;

    // Injury system fields — correlated 1:1 against MFE CSV columns (InjuryStatus=col153,
    // InjuryType=col90, InjurySeverity=col45, SeasonHealthPool=col24, SeasonHealthPoolMax=col29).
    public const int InjuryStatusIdx = 161;
    public const int InjuryTypeIdx = 162;
    public const int InjurySeverityIdx = 160;
    public const int TotalInjuryDurationIdx = 280;
    public const int MaxInjuryDurationIdx = 183;
    public const int MinInjuryDurationIdx = 191;
    public const int LatestInjuryWeekIdx = 177;
    public const int LatestInjuryYearIdx = 178;
    // Ambiguous pairs: LatestInjuryStage AND WasPreviouslyInjured both map 1:1 to F176/F284;
    // CurrentYear/LastYearSeasonEndingInjuryWeek both map 1:1 to F141/F175. Write both members.
    public const int LatestInjuryStageIdx = 176;
    public const int WasPreviouslyInjuredIdx = 284;
    public const int CurrentYearEndingWeekIdx = 141;
    public const int LastYearEndingWeekIdx = 175;

    // Current season tracking (1 record each, rewritten on every advance):
    //   BeginAdvanceWeekEvent.F2 (w32) = current week
    //   Season_BeginYearEvent.F2 (w32)  = current year
    // Verified against live save: both read 3, matching the freshest active injury
    // (max LatestInjuryWeek among Injured players = 3, LatestInjuryYear = 3).
    public const int CurrentWeekTableId = 4388;
    public const int CurrentWeekFieldIdx = 2;
    public const int CurrentYearTableId = 5119;
    public const int CurrentYearFieldIdx = 2;

    // Verified bit widths in the packed layout
    public const int TeamIndexBits = 8;
    public const int OverallRatingBits = 7;
    public const int JerseyNumBits = 7;
    public const int PositionBits = 6;
    public const int SchoolYearBits = 3;
    public const int TraitDevelopmentBits = 3;

    // Injury enum values (verified against MFE CSV)
    public const int InjuryStatusInjured = 0;
    public const int InjuryStatusHealthy = 1;
    public const int InjurySeverityNone = 255;
    public const int InjuryTypeNone = 98;

    // Dev trait enum
    public const int DevTraitNormal = 0;
    public const int DevTraitImpact = 1;
    public const int DevTraitStar = 2;
    public const int DevTraitElite = 3;

    public static int SafeOffset(int[] offsets, int fieldIndex)
    {
        if (offsets == null || fieldIndex >= offsets.Length) return -1;
        return offsets[fieldIndex];
    }

    public static int SafeReadBits(byte[] record, int[] offsets, int[] widths, int fieldIndex, int defaultBits)
    {
        if (record == null || offsets == null) return 0;
        if (fieldIndex >= offsets.Length) return 0;
        if (offsets[fieldIndex] < 0) return 0;
        return RecordCodec.ReadBits(record, offsets[fieldIndex], defaultBits);
    }
}
