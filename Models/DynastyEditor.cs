namespace PinkSlipsTool.Models;

public class PlayerData
{
    public int RecordIndex { get; set; }
    public string Name { get; set; }
    public int TeamIndex { get; set; }
    public int PositionValue { get; set; }
    public string Position => PositionNames.GetValueOrDefault(PositionValue, $"Pos{PositionValue}");
    public int OverallRating { get; set; }
    public int JerseyNum { get; set; }
    public int SchoolYear { get; set; }
    public int TraitDevelopment { get; set; }
    public string DevTrait => TraitDevelopment switch
    {
        0 => "Normal",
        1 => "Impact",
        2 => "Star",
        3 => "Elite",
        _ => $"Dev{ TraitDevelopment}"
    };
    public string YearLabel => SchoolYear switch
    {
        0 => "FR", 1 => "SO", 2 => "JR", 3 => "SR", 4 => "GR",
        _ => $"Y{SchoolYear}"
    };
    public bool IsInjured { get; set; }
    public string Display => $"{JerseyNum,3} {Name,-22} {Position,-3} {OverallRating,3} {YearLabel}  {DevTrait}{(IsInjured ? "  [INJ]" : "")}";
    public string CutDisplay => $"{OverallRating,3} {Position,-3} {Name}";
}

public class DynastyEditor
{
    private readonly DynastyFile _dynasty;
    private FranchiseTable _playerTable;
    private FranchiseTable _teamTable;
    private FranchiseTable _coachTable;
    private FranchiseTable _seasonWeekTable;
    private FranchiseTable _seasonYearTable;
    private FranchiseTable _rosterArray;

    // Offset-table positions verified from the CFB25 (C27) save schema.
    // Player fields are read from the packed (repacked) record layout.
    private const int TeamIdxCol = 272;
    private const int FirstNameCol = 146;
    private const int LastNameCol = 174;
    private const int PositionCol = 3;
    private const int OverallRatingCol = 198;
    private const int JerseyNumCol = 168;
    private const int SchoolYearCol = 252;
    private const int TraitDevCol = 282;
    private const int InjuryStatusCol = 161;
    private const int InjuryTypeCol = 162;
    private const int InjurySeverityCol = 160;
    private const int TotalInjuryDurationCol = 280;
    private const int MaxInjuryDurationCol = 183;
    private const int MinInjuryDurationCol = 191;
    private const int LatestInjuryWeekCol = 177;
    private const int LatestInjuryYearCol = 178;
    private const int LatestInjuryStageCol = 176;
    private const int WasPreviouslyInjuredCol = 284;
    private const int CurrentYearEndingWeekCol = 141;
    private const int LastYearEndingWeekCol = 175;

    private int _teamIdxField = TeamIdxCol;
    private int _posField = PositionCol;
    private int _ovrField = OverallRatingCol;
    private int _jerseyField = JerseyNumCol;
    private int _schoolYearField = SchoolYearCol;
    private int _traitDevField = TraitDevCol;
    private int _firstNameField = FirstNameCol;
    private int _lastNameField = LastNameCol;
    private int _injuryStatusField = InjuryStatusCol;
    private int _injuryTypeField = InjuryTypeCol;
    private int _injurySeverityField = InjurySeverityCol;
    private int _totalInjuryDurationField = TotalInjuryDurationCol;
    private int _maxInjuryDurationField = MaxInjuryDurationCol;
    private int _minInjuryDurationField = MinInjuryDurationCol;
    private int _latestInjuryWeekField = LatestInjuryWeekCol;
    private int _latestInjuryYearField = LatestInjuryYearCol;
    private int _latestInjuryStageField = LatestInjuryStageCol;
    private int _wasPreviouslyInjuredField = WasPreviouslyInjuredCol;
    private int _currentYearEndingWeekField = CurrentYearEndingWeekCol;
    private int _lastYearEndingWeekField = LastYearEndingWeekCol;

    private int WidthAt(int fieldIdx) =>
        _playerTable?.FieldBitWidths != null && fieldIdx >= 0 && fieldIdx < _playerTable.FieldBitWidths.Length
            ? _playerTable.FieldBitWidths[fieldIdx] : -1;

    public DynastyEditor(DynastyFile dynasty)
    {
        _dynasty = dynasty;
        _playerTable = dynasty.GetTable(PlayerTableInfo.TableId);
        _teamTable = dynasty.GetTable(6311);
        _coachTable = dynasty.GetTable(4176);
        _seasonWeekTable = dynasty.GetTable(PlayerTableInfo.CurrentWeekTableId);
        _seasonYearTable = dynasty.GetTable(PlayerTableInfo.CurrentYearTableId);
        _rosterArray = dynasty.GetArrayTable(RosterArrayTableId);
        DetectFields();
        BuildMasterToRowMap();
    }

    public bool IsReady => _playerTable != null;

    private void DetectFields()
    {
        // Hardcoded indices verified from MFE CSV export — validate in bounds
        if (_playerTable?.FieldOffsets == null) return;
        var n = _playerTable.FieldOffsets.Length;
        if (_teamIdxField >= n) _teamIdxField = -1;
        if (_posField >= n) _posField = -1;
        if (_ovrField >= n) _ovrField = -1;
        if (_jerseyField >= n) _jerseyField = -1;
        if (_schoolYearField >= n) _schoolYearField = -1;
        if (_traitDevField >= n) _traitDevField = -1;
        if (_firstNameField >= n) _firstNameField = -1;
        if (_lastNameField >= n) _lastNameField = -1;
        if (_injuryStatusField >= n) _injuryStatusField = -1;
        if (_injuryTypeField >= n) _injuryTypeField = -1;
        if (_injurySeverityField >= n) _injurySeverityField = -1;
        if (_totalInjuryDurationField >= n) _totalInjuryDurationField = -1;
        if (_maxInjuryDurationField >= n) _maxInjuryDurationField = -1;
        if (_minInjuryDurationField >= n) _minInjuryDurationField = -1;
        if (_latestInjuryWeekField >= n) _latestInjuryWeekField = -1;
        if (_latestInjuryYearField >= n) _latestInjuryYearField = -1;
        if (_latestInjuryStageField >= n) _latestInjuryStageField = -1;
        if (_wasPreviouslyInjuredField >= n) _wasPreviouslyInjuredField = -1;
        if (_currentYearEndingWeekField >= n) _currentYearEndingWeekField = -1;
        if (_lastYearEndingWeekField >= n) _lastYearEndingWeekField = -1;
    }

    public int FindUserTeamIndex()
    {
        if (_coachTable?.FieldOffsets == null) return -1;
        var offsets = _coachTable.FieldOffsets;
        if (offsets.Length <= 8) return -1;
        for (var i = 0; i < _coachTable.Header.NextRecordToUse; i++)
        {
            var rec = _coachTable.GetRecordBytes(i);
            if (rec == null) break;
            // Coach schema: array pos 0 = IsUserControlled (1 bit), array pos 8 = TeamIndex.
            var isUser = RecordCodec.ReadBits(rec, offsets[0], 1) == 1;
            if (!isUser) continue;
            return RecordCodec.ReadBits(rec, offsets[8], 8);
        }
        return -1;
    }

    public List<PlayerData> GetPlayersByTeam(int teamIndex)
    {
        var list = new List<PlayerData>();
        if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return list;
        var offsets = _playerTable.FieldOffsets;
        var widths = _playerTable.FieldBitWidths;
        if (_teamIdxField < 0 || _teamIdxField >= offsets.Length) return list;

        for (var i = 0; i < _playerTable.Header.NextRecordToUse; i++)
        {
            var rec = _playerTable.GetRecordBytes(i);
            if (rec == null) break;

            var ti = RecordCodec.ReadBits(rec, offsets[_teamIdxField], WidthAt(_teamIdxField));
            if (ti != teamIndex) continue;

            list.Add(new PlayerData
            {
                RecordIndex = i,
                TeamIndex = ti,
                Name = ReadPlayerName(rec, offsets, widths),
                PositionValue = RecordCodec.ReadBits(rec, offsets[_posField], WidthAt(_posField)),
                OverallRating = RecordCodec.ReadBits(rec, offsets[_ovrField], WidthAt(_ovrField)),
                JerseyNum = RecordCodec.ReadBits(rec, offsets[_jerseyField], WidthAt(_jerseyField)),
                SchoolYear = RecordCodec.ReadBits(rec, offsets[_schoolYearField], WidthAt(_schoolYearField)),
                TraitDevelopment = RecordCodec.ReadBits(rec, offsets[_traitDevField], WidthAt(_traitDevField)),
                IsInjured = _injuryStatusField >= 0 && RecordCodec.ReadBits(rec, offsets[_injuryStatusField], WidthAt(_injuryStatusField)) == PlayerTableInfo.InjuryStatusInjured,
            });
        }
        return list;
    }

    public string GetTeamName(int teamIndex)
    {
        if (_teamTable?.FieldOffsets == null || _teamTable.FieldBitWidths == null) return $"Team {teamIndex}";
        var offsets = _teamTable.FieldOffsets;
        var widths = _teamTable.FieldBitWidths;
        // Team table (6311) uses raw offsets: TeamIndex=pos390 (8-bit), and the name fields
        // (DisplayName=pos72, ShortName=pos281, AssetName=pos12) are 32-bit pool pointers.
        const int teamIdxCol = 390, displayNameCol = 72, shortNameCol = 281;
        if (teamIdxCol >= offsets.Length) return $"Team {teamIndex}";
        for (var i = 0; i < _teamTable.Header.NextRecordToUse; i++)
        {
            var rec = _teamTable.GetRecordBytes(i);
            if (rec == null) break;
            var ti = RecordCodec.ReadBits(rec, offsets[teamIdxCol], widths[teamIdxCol]);
            if (ti != teamIndex) continue;
            // Try DisplayName first, then ShortName
            if (displayNameCol < offsets.Length)
            {
                var name = ReadPoolString(rec, offsets[displayNameCol], _teamTable);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            if (shortNameCol < offsets.Length)
            {
                var name = ReadPoolString(rec, offsets[shortNameCol], _teamTable);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return $"Team {teamIndex}";
        }
        return $"Team {teamIndex}";
    }

    // Roster cap enforced by the game. Adding a player to a full roster gets rejected
    // by the game — make room with CutPlayer before stealing into a full roster.
    // Every team is stored at exactly 85 players.
    private const int RosterCap = 85;
    public const int FreeAgentTeamIndex = 255;
    // Array (ASTO) table holding each team's roster list. Team.Roster (member 242)
    // is a ref to (RosterArrayTableId, row); that row is the team's roster.
    private const int RosterArrayTableId = 6097;
    // Team table (6311) raw offsets: TeamIndex=pos390 (master ID), Roster=pos242 (ref).
    private const int TeamMasterIdCol = 390;
    private const int TeamRosterRefCol = 242;

    // Master team ID -> roster array row. Built from the team table's authoritative
    // Roster refs (verified to equal the array majority field-272 row for every real
    // team), with an array-majority fallback for any ID the refs don't cover. Coach
    // TeamIndex and player field-272 are master IDs, NOT array row indices — the two
    // only coincide for the first few alphabetically-ordered teams.
    private Dictionary<int, int> _masterToRow;

    private void BuildMasterToRowMap()
    {
        _masterToRow = new Dictionary<int, int>();
        if (_rosterArray == null) return;

        var rowCount = _rosterArray.Header.RecordCount;

        // Primary: team table Team.Roster refs.
        if (_teamTable?.FieldOffsets != null && _teamTable.FieldBitWidths != null &&
            TeamRosterRefCol < _teamTable.FieldOffsets.Length && TeamMasterIdCol < _teamTable.FieldOffsets.Length)
        {
            var offsets = _teamTable.FieldOffsets;
            var widths = _teamTable.FieldBitWidths;
            for (var p = 0; p < _teamTable.Header.NextRecordToUse; p++)
            {
                var rec = _teamTable.GetRecordBytes(p);
                if (rec == null) break;
                var masterId = RecordCodec.ReadBits(rec, offsets[TeamMasterIdCol], widths[TeamMasterIdCol]);
                if (masterId == FreeAgentTeamIndex) continue;
                var rosterRef = RecordCodec.ReadBits(rec, offsets[TeamRosterRefCol], 32);
                if (((rosterRef >> 17) & 0x7FFF) != RosterArrayTableId) continue;
                var row = rosterRef & 0x1FFFF;
                if (row >= 0 && row < rowCount && !_masterToRow.ContainsKey(masterId))
                    _masterToRow[masterId] = row;
            }
        }

        // Fallback: majority field-272 per array row for master IDs the refs missed.
        if (_playerTable?.FieldOffsets != null && _playerTable.FieldBitWidths != null &&
            _teamIdxField >= 0 && _teamIdxField < _playerTable.FieldOffsets.Length)
        {
            var offsets = _playerTable.FieldOffsets;
            var widths = _playerTable.FieldBitWidths;
            var counts = new Dictionary<int, int>();
            for (var row = 0; row < rowCount; row++)
            {
                counts.Clear();
                var size = _rosterArray.ReadArraySize(row);
                if (size <= 0) continue;
                for (var s = 0; s < size; s++)
                {
                    var pid = (int)(_rosterArray.ReadArrayRowRef(row, s) & 0x1FFFF);
                    if (pid >= _playerTable.Header.NextRecordToUse) continue;
                    var prec = _playerTable.GetRecordBytes(pid);
                    if (prec == null) continue;
                    var t = RecordCodec.ReadBits(prec, offsets[_teamIdxField], widths[_teamIdxField]);
                    counts.TryGetValue(t, out var c);
                    counts[t] = c + 1;
                }
                var best = -1;
                var bestCount = 0;
                foreach (var kv in counts)
                    if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
                if (best >= 0 && best != FreeAgentTeamIndex && !_masterToRow.ContainsKey(best))
                    _masterToRow[best] = row;
            }
        }
    }

    private int RosterRowOf(int masterId) =>
        _masterToRow != null && _masterToRow.TryGetValue(masterId, out var row) ? row : -1;

    public bool CutPlayer(int playerRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_teamIdxField < 0 || _teamIdxField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var oldTeam = RecordCodec.ReadBits(rec, offsets[_teamIdxField], WidthAt(_teamIdxField));
            RemoveFromRoster(oldTeam, playerRecordIndex);
            RecordCodec.WriteBits(rec, offsets[_teamIdxField], WidthAt(_teamIdxField), FreeAgentTeamIndex);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            SyncEditedTables();
            return true;
        }
        catch { return false; }
    }

    // Mirrors UpgradeDevTrait: a single field write on the player record, plus the roster
    // array update so the game's roster stays consistent. This is the mechanism verified
    // to work in-game.
    public bool StealPlayer(int playerRecordIndex, int newTeamIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_teamIdxField < 0 || _teamIdxField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;

            // The game rejects transfers into a full roster; the UI tells the user to cut
            // someone first. Enforce the cap here so the save stays valid.
            var newRow = RosterRowOf(newTeamIndex);
            if (newRow >= 0 && _rosterArray.ReadArraySize(newRow) >= RosterCap)
                return false;

            var oldTeam = RecordCodec.ReadBits(rec, offsets[_teamIdxField], WidthAt(_teamIdxField));
            RemoveFromRoster(oldTeam, playerRecordIndex);
            AddToRoster(newTeamIndex, playerRecordIndex);

            RecordCodec.WriteBits(rec, offsets[_teamIdxField], WidthAt(_teamIdxField), newTeamIndex);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            SyncEditedTables();
            return true;
        }
        catch { return false; }
    }

    // Swap the team field between two player records and swap their entries in the two
    // roster arrays. Both rosters stay at the same size (no cuts, nothing deleted), so
    // the game never trims either side on load.
    public bool TransferPlayer(int playerRecordIndex, int otherPlayerRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_teamIdxField < 0 || _teamIdxField >= offsets.Length) return false;
            var recA = _playerTable.GetRecordBytes(playerRecordIndex);
            var recB = _playerTable.GetRecordBytes(otherPlayerRecordIndex);
            if (recA == null || recB == null) return false;
            var teamA = RecordCodec.ReadBits(recA, offsets[_teamIdxField], WidthAt(_teamIdxField));
            var teamB = RecordCodec.ReadBits(recB, offsets[_teamIdxField], WidthAt(_teamIdxField));

            // Move roster entries: remove each player from their current team's roster,
            // then add them to the other team's (free agency has no roster row). Removing
            // first keeps both rosters at their previous size, so the game never trims
            // either side on load.
            RemoveFromRoster(teamA, playerRecordIndex);
            RemoveFromRoster(teamB, otherPlayerRecordIndex);
            AddToRoster(teamB, playerRecordIndex);
            AddToRoster(teamA, otherPlayerRecordIndex);

            RecordCodec.WriteBits(recA, offsets[_teamIdxField], WidthAt(_teamIdxField), teamB);
            RecordCodec.WriteBits(recB, offsets[_teamIdxField], WidthAt(_teamIdxField), teamA);
            _playerTable.WriteRecordBytes(playerRecordIndex, recA);
            _playerTable.WriteRecordBytes(otherPlayerRecordIndex, recB);
            SyncEditedTables();
            return true;
        }
        catch { return false; }
    }

    private uint PlayerRef(int playerRecordIndex) =>
        (uint)((PlayerTableInfo.TableId << 17) | playerRecordIndex);

    // The ref for a player lives in exactly one roster row; the FA/FCS rows (owned by
    // FreeAgentTeamIndex) are several, so scan every row instead of trusting the row map.
    private void RemoveFromRoster(int masterId, int playerRecordIndex)
    {
        var target = PlayerRef(playerRecordIndex);
        for (var row = 0; row < _rosterArray.Header.RecordCount; row++)
        {
            var size = _rosterArray.ReadArraySize(row);
            var slot = -1;
            for (var s = 0; s < size; s++)
                if (_rosterArray.ReadArrayRowRef(row, s) == target) { slot = s; break; }
            if (slot < 0) continue;
            // Compact the list so the game sees no holes.
            for (var s = slot; s < size - 1; s++)
                _rosterArray.WriteArrayRowRef(row, s, _rosterArray.ReadArrayRowRef(row, s + 1));
            _rosterArray.WriteArrayRowRef(row, size - 1, 0);
            _rosterArray.WriteArraySize(row, size - 1);
            return;
        }
    }

    private void AddToRoster(int masterId, int playerRecordIndex)
    {
        var row = RosterRowOf(masterId);
        if (row < 0) return;
        var slots = _rosterArray.Header.RecordSize / 4;
        var size = _rosterArray.ReadArraySize(row);
        if (size < 0 || size >= slots) return;
        _rosterArray.WriteArrayRowRef(row, size, PlayerRef(playerRecordIndex));
        _rosterArray.WriteArraySize(row, size + 1);
    }

    // Sync order matters: the player table buffer physically contains the roster array's
    // bytes, so it must be copied back first, then the (edited) roster array on top.
    private void SyncEditedTables()
    {
        _dynasty.SyncTable(_playerTable);
        if (_rosterArray != null) _dynasty.SyncTable(_rosterArray);
    }

    public bool UpgradeDevTrait(int playerRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            var widths = _playerTable.FieldBitWidths;
            if (_traitDevField < 0 || _traitDevField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var current = RecordCodec.ReadBits(rec, offsets[_traitDevField], WidthAt(_traitDevField));
            if (current >= 3) return false;
            RecordCodec.WriteBits(rec, offsets[_traitDevField], WidthAt(_traitDevField), current + 1);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return true;
        }
        catch { return false; }
    }

    public bool DowngradeDevTrait(int playerRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_traitDevField < 0 || _traitDevField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var current = RecordCodec.ReadBits(rec, offsets[_traitDevField], WidthAt(_traitDevField));
            if (current <= 0) return false;
            RecordCodec.WriteBits(rec, offsets[_traitDevField], WidthAt(_traitDevField), current - 1);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return true;
        }
        catch { return false; }
    }

    public bool SetPosition(int playerRecordIndex, int positionValue)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_posField < 0 || _posField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var current = RecordCodec.ReadBits(rec, offsets[_posField], WidthAt(_posField));
            if (current == positionValue) return false;
            RecordCodec.WriteBits(rec, offsets[_posField], WidthAt(_posField), positionValue);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return true;
        }
        catch { return false; }
    }

    public bool SetSchoolYear(int playerRecordIndex, int schoolYear)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            if (_schoolYearField < 0 || _schoolYearField >= offsets.Length) return false;
            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var current = RecordCodec.ReadBits(rec, offsets[_schoolYearField], WidthAt(_schoolYearField));
            if (current == schoolYear) return false;
            RecordCodec.WriteBits(rec, offsets[_schoolYearField], WidthAt(_schoolYearField), schoolYear);
            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return true;
        }
        catch { return false; }
    }

    // Clears every injury field back to the healthy baseline. Mirrors the field layout
    // applied by ApplyInjury so a healed player shows no residual flags anywhere.
    public bool HealInjury(int playerRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return false;
            var offsets = _playerTable.FieldOffsets;
            var widths = _playerTable.FieldBitWidths;
            if (_injuryStatusField < 0 || _injuryStatusField >= offsets.Length) return false;

            var rec = _playerTable.GetRecordBytes(playerRecordIndex);
            if (rec == null) return false;
            var targetStatus = RecordCodec.ReadBits(rec, offsets[_injuryStatusField], WidthAt(_injuryStatusField));
            if (targetStatus != PlayerTableInfo.InjuryStatusInjured) return false;

            WriteField(rec, offsets, widths, _injuryStatusField, PlayerTableInfo.InjuryStatusHealthy);
            WriteField(rec, offsets, widths, _injuryTypeField, PlayerTableInfo.InjuryTypeNone);
            WriteField(rec, offsets, widths, _injurySeverityField, PlayerTableInfo.InjurySeverityNone);
            WriteField(rec, offsets, widths, _totalInjuryDurationField, 0);
            WriteField(rec, offsets, widths, _maxInjuryDurationField, 0);
            WriteField(rec, offsets, widths, _minInjuryDurationField, 0);
            WriteField(rec, offsets, widths, _latestInjuryWeekField, 0);
            WriteField(rec, offsets, widths, _latestInjuryYearField, 0);
            WriteField(rec, offsets, widths, _latestInjuryStageField, 0);
            WriteField(rec, offsets, widths, _wasPreviouslyInjuredField, 0);
            WriteField(rec, offsets, widths, _currentYearEndingWeekField, 0);
            WriteField(rec, offsets, widths, _lastYearEndingWeekField, 0);

            _playerTable.WriteRecordBytes(playerRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return true;
        }
        catch { return false; }
    }

    private readonly Random _rng = new();

    // Hard-coded one-game injury profile, byte-verified from a currently-active GameEnding
    // injury in the save (record 5200). InjurySeverity=GameEnding = out one game, back next.
    // F177/F178 (week/year) are read live from the save so the injury anchors correctly in ANY
    // week: the week is anchored to currentWeek+1 (the NEXT game) because the current week's
    // game may already have been played. F183 is the remaining-duration counter the game
    // decrements on each advance and clears at 0 — verified empirically (F183=1 cleared on the
    // first advance, 0 games missed; natural 2-weeks-remaining injuries read F183=2/F280=3/
    // F191=33). So F183=2 anchored to week W+1 keeps the player out for exactly week W+1 and
    // clears him going into week W+2.
    private const int OneGameInjuryStatus = 0;   // F161 Injured
    private const int OneGameInjuryType = 36;    // F162 HandFingerBroken
    private const int OneGameInjurySeverity = 5; // F160 GameEnding
    private const int OneGameTotalDuration = 3;  // F280
    private const int OneGameMaxDuration = 2;    // F183 remaining-duration counter (heals at 0)
    private const int OneGameMinDuration = 33;   // F191 (= +1 in game terms, stored as value+32)
    private const int OneGameLatestWeek = 3;     // F177 fallback (before the +1 anchor)
    private const int OneGameLatestYear = 3;     // F178 fallback
    private const int OneGameStageFlag = 1;      // F176/F284 (LatestInjuryStage + WasPreviouslyInjured)
    private const int OneGameEndingWeek = 30;    // F141/F175

    public int GetCurrentWeek()
    {
        var v = ReadSeasonInt(_seasonWeekTable, PlayerTableInfo.CurrentWeekFieldIdx);
        return v is >= 1 and <= 30 ? v : OneGameLatestWeek;
    }

    public int GetCurrentYear()
    {
        var v = ReadSeasonInt(_seasonYearTable, PlayerTableInfo.CurrentYearFieldIdx);
        return v is >= 1 and <= 10 ? v : OneGameLatestYear;
    }

    private static int ReadSeasonInt(FranchiseTable table, int fieldIdx)
    {
        if (table?.FieldOffsets == null || table.FieldBitWidths == null) return -1;
        if (fieldIdx >= table.FieldOffsets.Length) return -1;
        var rec = table.GetRecordBytes(0);
        if (rec == null) return -1;
        var off = table.FieldOffsets[fieldIdx];
        var w = table.FieldBitWidths[fieldIdx];
        if (off < 0 || w <= 0) return -1;
        return RecordCodec.ReadBits(rec, off, w);
    }

    public (bool Ok, string InjuryDescription) ApplyInjury(int targetRecordIndex)
    {
        try
        {
            if (_playerTable?.FieldOffsets == null || _playerTable.FieldBitWidths == null) return (false, null);
            var offsets = _playerTable.FieldOffsets;
            var widths = _playerTable.FieldBitWidths;
            if (_injuryStatusField < 0 || _injuryStatusField >= offsets.Length) return (false, null);

            var rec = _playerTable.GetRecordBytes(targetRecordIndex);
            if (rec == null) return (false, null);

            var targetStatus = RecordCodec.ReadBits(rec, offsets[_injuryStatusField], WidthAt(_injuryStatusField));
            if (targetStatus == PlayerTableInfo.InjuryStatusInjured) return (true, "already");

            WriteField(rec, offsets, widths, _injuryStatusField, OneGameInjuryStatus);
            WriteField(rec, offsets, widths, _injuryTypeField, OneGameInjuryType);
            WriteField(rec, offsets, widths, _injurySeverityField, OneGameInjurySeverity);
            WriteField(rec, offsets, widths, _totalInjuryDurationField, OneGameTotalDuration);
            WriteField(rec, offsets, widths, _maxInjuryDurationField, OneGameMaxDuration);
            WriteField(rec, offsets, widths, _minInjuryDurationField, OneGameMinDuration);
            WriteField(rec, offsets, widths, _latestInjuryWeekField, GetCurrentWeek() + 1);
            WriteField(rec, offsets, widths, _latestInjuryYearField, GetCurrentYear());
            WriteField(rec, offsets, widths, _latestInjuryStageField, OneGameStageFlag);
            WriteField(rec, offsets, widths, _wasPreviouslyInjuredField, OneGameStageFlag);
            WriteField(rec, offsets, widths, _currentYearEndingWeekField, OneGameEndingWeek);
            WriteField(rec, offsets, widths, _lastYearEndingWeekField, OneGameEndingWeek);

            _playerTable.WriteRecordBytes(targetRecordIndex, rec);
            _dynasty.SyncTable(_playerTable);
            return (true, $"{InjuryTypeNames.GetValueOrDefault(OneGameInjuryType, "Injury")} — Game Ending (2 weeks)");
        }
        catch { return (false, null); }
    }

    private static void WriteField(byte[] rec, int[] offsets, int[] widths, int fieldIdx, int value)
    {
        if (fieldIdx < 0 || fieldIdx >= offsets.Length) return;
        if (offsets[fieldIdx] < 0 || widths[fieldIdx] <= 0) return;
        RecordCodec.WriteBits(rec, offsets[fieldIdx], widths[fieldIdx], value);
    }

    public string DiagnosticInfo()
    {
        if (_dynasty == null) return "No dynasty file loaded";
        var lines = new List<string>();
        var pt = _playerTable;
        lines.Add($"Player table: {(pt != null ? $"found, {pt.FieldOffsets?.Length ?? 0} fields" : "NOT FOUND")}");
        lines.Add($"Team table: {(_teamTable != null ? "found" : "NOT FOUND")}");
        lines.Add($"Coach table: {(_coachTable != null ? "found" : "NOT FOUND")}");
        if (pt?.FieldOffsets != null)
        {
            lines.Add($"Using MFE indices: Team=F{_teamIdxField} Pos=F{_posField} Ovr=F{_ovrField} Jersey=F{_jerseyField} School=F{_schoolYearField} Dev=F{_traitDevField} FN=F{_firstNameField} LN=F{_lastNameField}");
            lines.Add($"Bit widths: Team={WidthAt(_teamIdxField)} Pos={WidthAt(_posField)} Ovr={WidthAt(_ovrField)} Jersey={WidthAt(_jerseyField)} School={WidthAt(_schoolYearField)} Dev={WidthAt(_traitDevField)} FN={WidthAt(_firstNameField)} LN={WidthAt(_lastNameField)}");
            for (var ri = 0; ri < Math.Min(5, pt.Header.NextRecordToUse); ri++)
            {
                var rec = pt.GetRecordBytes(ri);
                if (rec == null) continue;
                var ti = RecordCodec.ReadBits(rec, pt.FieldOffsets[_teamIdxField], WidthAt(_teamIdxField));
                var name = ReadPlayerName(rec, pt.FieldOffsets, pt.FieldBitWidths);
                var pos = RecordCodec.ReadBits(rec, pt.FieldOffsets[_posField], WidthAt(_posField));
                var ovr = RecordCodec.ReadBits(rec, pt.FieldOffsets[_ovrField], WidthAt(_ovrField));
                lines.Add($"  R{ri}: Team={ti} Name=[{name}] Pos={pos} OVR={ovr}");
            }
            var userTeam = FindUserTeamIndex();
            lines.Add($"User team (Coach): {userTeam} -> array row {RosterRowOf(userTeam)}");
        }
        WriteDiagnosticFile();
        return string.Join("\n", lines);
    }

    private void WriteDiagnosticFile()
    {
        if (_dynasty == null) return;
        try
        {
            var lines = new List<string>();
            var pt = _playerTable;
            lines.Add($"Player table: {(pt != null ? $"found, {pt.FieldOffsets?.Length ?? 0} fields" : "NOT FOUND")}");
            lines.Add($"Team table: {(_teamTable != null ? "found" : "NOT FOUND")}");
            lines.Add($"Coach table: {(_coachTable != null ? "found" : "NOT FOUND")}");
            lines.Add($"Using hardcoded MFE indices: Team=F{_teamIdxField} Pos=F{_posField} Ovr=F{_ovrField} Jersey=F{_jerseyField} School=F{_schoolYearField} Dev=F{_traitDevField} FN=F{_firstNameField} LN=F{_lastNameField}");
            lines.Add($"File bit widths: Team={WidthAt(_teamIdxField)} Pos={WidthAt(_posField)} Ovr={WidthAt(_ovrField)} Jersey={WidthAt(_jerseyField)} School={WidthAt(_schoolYearField)} Dev={WidthAt(_traitDevField)} FN={WidthAt(_firstNameField)} LN={WidthAt(_lastNameField)}");
            if (pt?.FieldOffsets != null)
            {
                for (var ri = 0; ri < Math.Min(5, pt.Header.NextRecordToUse); ri++)
                {
                    var rec = pt.GetRecordBytes(ri);
                    if (rec == null) continue;
                    var ti = RecordCodec.ReadBits(rec, pt.FieldOffsets[_teamIdxField], WidthAt(_teamIdxField));
                    var name = ReadPlayerName(rec, pt.FieldOffsets, pt.FieldBitWidths);
                    var pos = RecordCodec.ReadBits(rec, pt.FieldOffsets[_posField], WidthAt(_posField));
                    var ovr = RecordCodec.ReadBits(rec, pt.FieldOffsets[_ovrField], WidthAt(_ovrField));
                    var dev = RecordCodec.ReadBits(rec, pt.FieldOffsets[_traitDevField], WidthAt(_traitDevField));
                    lines.Add($"  R{ri}: Team={ti} Name=[{name}] Pos={pos} OVR={ovr} Dev={dev}");
                }
                var userTeam = FindUserTeamIndex();
                lines.Add($"User team (Coach): {userTeam} -> array row {RosterRowOf(userTeam)}");
                var h = pt.Header;
                lines.Add($"Player header: storeLen={h.TableStoreLength} recCount={h.RecordCount} recWords={h.RecordWords} recCapacity={h.RecordCapacity} members={h.NumMembers} nextUse={h.NextRecordToUse} recSize={h.RecordSize} offStart={h.OffsetStart} t1Start={h.Table1Start} t2Start={h.Table2Start}");
                // Search entire decompressed payload for known player names from CSV
                if (_dynasty?.DecompressedPayload != null)
                {
                    string[] names = { "Omar", "Aarons", "Noah", "Jide", "Christopher", "Duke" };
                    lines.Add("--- Name search in raw payload ---");
                    foreach (var nm in names)
                    {
                        var cnt = 0;
                        var payload = _dynasty.DecompressedPayload;
                        var needle = System.Text.Encoding.ASCII.GetBytes(nm);
                        for (var p = 0; p <= payload.Length - needle.Length; p++)
                        {
                            var ok = true;
                            for (var b = 0; b < needle.Length; b++)
                                if (payload[p + b] != needle[b]) { ok = false; break; }
                            if (ok) cnt++;
                        }
                        lines.Add($"  '{nm}': {cnt} occurrences");
                    }
                    lines.Add("--- End name search ---");
                }
                // List ALL tables found with record counts
                lines.Add("--- All tables ---");
                if (_dynasty?.Tables != null)
                {
                    foreach (var t in _dynasty.Tables)
                        lines.Add($"  {t.Header.Name} id={t.Header.TableId} members={t.Header.NumMembers} recs={t.Header.NextRecordToUse} size={t.Header.RecordSize}");
                }
                lines.Add("--- End tables ---");
                // Raw bytes of table1 region start (first 48 bytes) to look for readable data
                var rawStart = h.Table1Start;
                if (rawStart + 48 <= pt.Data.Length)
                {
                    lines.Add("--- Raw table1 bytes (start of record data) ---");
                    for (var r = 0; r < 48; r += 16)
                    {
                        var hex = BitConverter.ToString(pt.Data, rawStart + r, 16).Replace("-", " ");
                        var asc = "";
                        for (var c = rawStart + r; c < rawStart + r + 16; c++)
                            asc += pt.Data[c] >= 32 && pt.Data[c] < 127 ? (char)pt.Data[c] : '.';
                        lines.Add($"  +{r:X2}: {hex} {asc}");
                    }
                    lines.Add("--- End raw table1 ---");
                }
            }
            // Hex dump first 64 bytes of record 0 + comprehensive field scan
            if (pt?.FieldOffsets != null && pt.FieldBitWidths != null)
            {
                var rec0 = pt.GetRecordBytes(0);
                if (rec0 != null)
                {
                    lines.Add("--- First 64 hex bytes of record 0 ---");
                    for (var r = 0; r < 64; r += 16)
                    {
                        var hex = BitConverter.ToString(rec0, r, Math.Min(16, 64 - r)).Replace("-", " ");
                        lines.Add($"  {r:X4}: {hex}");
                    }
                    lines.Add("--- End hex dump ---");

                    // Scan ALL fields: show offset, width, and raw value for records 0-4
                    var widths = pt.FieldBitWidths;
                    var offsets = pt.FieldOffsets;

                    // Full hex dump + byte-level search for TeamIndex (0xFF) across records
                    lines.Add("--- Full hex dump record 0 (192 bytes) ---");
                    for (var r = 0; r < 192; r += 16)
                    {
                        var hex = BitConverter.ToString(rec0, r, Math.Min(16, 192 - r)).Replace("-", " ");
                        var asc = "";
                        for (var c = r; c < r + 16 && c < 192; c++)
                            asc += rec0[c] >= 32 && rec0[c] < 127 ? (char)rec0[c] : '.';
                        lines.Add($"  {r:X4}: {hex,-47} {asc}");
                    }
                    lines.Add("--- End full hex ---");
                // Search ALL Player records for known names
                lines.Add("--- Searching all player records for names ---");
                var foundAnyName = false;
                for (var ri = 0; ri < pt.Header.NextRecordToUse; ri++)
                {
                    var rec = pt.GetRecordBytes(ri);
                    var ascii = System.Text.Encoding.ASCII.GetString(rec);
                    if (ascii.Contains("Omar") || ascii.Contains("Aarons") || ascii.Contains("Jide"))
                    {
                        foundAnyName = true;
                        var off = ascii.IndexOf("Omar");
                        var off2 = ascii.IndexOf("Aarons");
                        var off3 = ascii.IndexOf("Jide");
                        var marks = new System.Collections.Generic.List<string>();
                        if (off >= 0) marks.Add($"Omar@{off}");
                        if (off2 >= 0) marks.Add($"Aarons@{off2}");
                        if (off3 >= 0) marks.Add($"Jide@{off3}");
                        lines.Add($"  R{ri}: {string.Join(" ", marks)}");
                    }
                }
                if (!foundAnyName)
                    lines.Add("  (no names found in any player record)");
                lines.Add("--- End player name search ---");
                // STRING POOL ANALYSIS: names live after the record region at Table2Start
                lines.Add("--- String pool analysis ---");
                lines.Add($"  pt.Data.Length={pt.Data.Length}  t1Start={pt.Header.Table1Start}  t2Start={pt.Header.Table2Start}");
                var poolOff = pt.Header.Table2Start;
                if (poolOff >= 0 && poolOff + 4096 <= pt.Data.Length)
                {
                    lines.Add("--- String pool first 4096 bytes (hex+ascii) ---");
                    for (var row = 0; row < 4096; row += 16)
                    {
                        var hex = "";
                        var asc = "";
                        for (var b = row; b < Math.Min(row + 16, 4096); b++)
                        {
                            hex += pt.Data[poolOff + b].ToString("X2") + " ";
                            asc += pt.Data[poolOff + b] >= 32 && pt.Data[poolOff + b] < 127 ? (char)pt.Data[poolOff + b] : '.';
                        }
                        lines.Add($"  {row:D5}: {hex,-48} {asc}");
                    }
                    lines.Add("--- End pool dump ---");
                }
                // Find "Generic_" strings in the pool and measure spacing (likely group size)
                var genNeedle = System.Text.Encoding.ASCII.GetBytes("Generic_");
                lines.Add("--- 'Generic_' positions in pool ---");
                var genCount = 0;
                var prevGen = -1;
                var poolLimit = pt.Data.Length - genNeedle.Length;
                for (var p = poolOff; p <= poolLimit; p++)
                {
                    var ok = true;
                    for (var b = 0; b < genNeedle.Length; b++)
                        if (pt.Data[p + b] != genNeedle[b]) { ok = false; break; }
                    if (ok)
                    {
                        var spacing = prevGen >= 0 ? p - prevGen : -1;
                        if (genCount < 40 || spacing != 0)
                            lines.Add($"  Generic_ at pool offset {p} spacingFromPrev={spacing}");
                        genCount++;
                        prevGen = p;
                    }
                }
                lines.Add($"  total Generic_ strings in pool: {genCount}");
                lines.Add("--- End Generic_ ---");
                // Find "Omar" occurrences in pool
                var omarNeedle = System.Text.Encoding.ASCII.GetBytes("Omar");
                var omarPositions = new System.Collections.Generic.List<int>();
                for (var p = poolOff; p <= poolLimit; p++)
                {
                    var ok = true;
                    for (var b = 0; b < omarNeedle.Length; b++)
                        if (pt.Data[p + b] != omarNeedle[b]) { ok = false; break; }
                    if (ok) omarPositions.Add(p);
                }
                lines.Add($"  'Omar' at pool offsets: {string.Join(",", omarPositions)}");
                lines.Add("--- End pool analysis ---");
                // Search records for player ID 30391 (0x76F7) / 30000 as 16-bit LE
                lines.Add("--- Searching records for ID 30391 / 30000 (16-bit LE) ---");
                var foundId = false;
                for (var ri = 0; ri < pt.Header.NextRecordToUse; ri++)
                {
                    var recOff = pt.Header.Table1Start + ri * pt.Header.RecordSize;
                    for (var b = 0; b <= pt.Header.RecordSize - 2; b++)
                    {
                        var v = pt.Data[recOff + b] | (pt.Data[recOff + b + 1] << 8);
                        if (v == 30391 || v == 30000)
                        {
                            lines.Add($"  R{ri} byte {b}: {v} (bytes {pt.Data[recOff + b]:X2} {pt.Data[recOff + b + 1]:X2} {pt.Data[recOff + b + 2]:X2} {pt.Data[recOff + b + 3]:X2})");
                            foundId = true;
                        }
                    }
                }
                if (!foundId) lines.Add("  (no 30391/30000 found)");
                lines.Add("--- End ID search ---");
                }
            }
            if (pt != null && pt.Data != null)
            {
                var binPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "player_table_data.bin");
                System.IO.File.WriteAllBytes(binPath, pt.Data);
                lines.Add($"Wrote player table data ({pt.Data.Length} bytes) to {binPath}");
            }
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pinkslips_diag.txt");
            System.IO.File.WriteAllText(path, string.Join("\n", lines));
        }
        catch { }
    }

    private string ReadPlayerName(byte[] rec, int[] offsets, int[] widths)
    {
        var fn = ReadPoolStringField(rec, offsets, _firstNameField);
        var ln = ReadPoolStringField(rec, offsets, _lastNameField);
        var name = $"{fn} {ln}".Trim();
        return string.IsNullOrWhiteSpace(name) ? $"Player {rec[0]:X2}" : name;
    }

    // Player First/Last names are 32-bit string-pool pointers relative to Table2Start.
    private string ReadPoolStringField(byte[] rec, int[] offsets, int fieldIdx)
    {
        if (fieldIdx < 0 || fieldIdx >= offsets.Length) return "";
        return ReadPoolString(rec, offsets[fieldIdx], _playerTable);
    }

    private static string ReadPoolString(byte[] rec, int bitOffset, FranchiseTable table)
    {
        var ptr = RecordCodec.ReadBits(rec, bitOffset, 32);
        return table.ResolvePoolString(ptr);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}

internal static class PositionNames
{
    // CFB25 PositionE enum (verified from save schema)
    private static readonly string[] Names =
    {
        "QB","HB","FB","WR","TE","LT","LG","C","RG","RT",
        "LE","RE","DT","LOLB","MLB","ROLB","CB","FS","SS","K","P",
        "LS","KR","PR","KOS","3DRB","GAD","PWHB","SLWR","RLE","RRE",
        "RDT","NT","SUBLB","SLCB","HC_CFM","OC_CFM","DC_CFM","Owner_CFM"
    };
    private const int InvalidValue = 63;
    public static string GetValueOrDefault(int key, string fallback) =>
        key == InvalidValue ? "Invalid" :
        key >= 0 && key < Names.Length ? Names[key] : fallback;
    public static (int Value, string Name)[] All =>
        Names.Select((n, i) => (i, n)).ToArray();
}

internal static class InjuryTypeNames
{
    // F162 enum — all values correlated 1:1 against MFE CSV InjuryType column.
    private static readonly Dictionary<int, string> Names = new()
    {
        [5] = "Ankle Achilles Tear",
        [6] = "Ankle Dislocated",
        [7] = "Ankle Dislocated (Several Games)",
        [12] = "Arm Forearm Fracture",
        [13] = "Arm Torn Bicep",
        [14] = "Arm Torn Tricep",
        [16] = "Arm Upper Fracture",
        [18] = "Back Ruptured Disk",
        [19] = "Back Ruptured Disk (Couple)",
        [23] = "Elbow Dislocate",
        [24] = "Elbow Dislocated (Several Games)",
        [25] = "Elbow Fracture",
        [29] = "Foot Broken Toe",
        [30] = "Foot Fracture (Couple Games)",
        [31] = "Foot Fracture",
        [34] = "Foot Stress Fracture",
        [36] = "Hand Finger Broken",
        [38] = "Hand Thumb Broken",
        [39] = "Hand Broken",
        [42] = "Hand Wrist Broken",
        [48] = "Hip Dislocation",
        [49] = "Hip Dislocation (Couple Games)",
        [51] = "Hip Fracture",
        [52] = "Hip Tailbone Broken",
        [57] = "Knee ACL Partial Tear",
        [58] = "Knee Cartilage Tear",
        [59] = "Knee Dislocated",
        [60] = "Knee MCL Partial Tear",
        [61] = "Knee ACL Complete Tear",
        [62] = "Knee PCL Partial Tear",
        [72] = "Leg Groin Pull",
        [73] = "Leg Fibula Broken",
        [75] = "Leg Hamstring Tear",
        [77] = "Leg Quad Tear",
        [78] = "Leg Broken Femur",
        [79] = "Leg Tibia Broken",
        [85] = "Rib Broken Ribs",
        [86] = "Rib Collarbone Broken",
        [87] = "Rib Pectoral Tear",
        [92] = "Shoulder Tear",
        [95] = "Shoulder Tear (Several Games)",
        [98] = "None"
    };
    public static string GetValueOrDefault(int key, string fallback) =>
        Names.TryGetValue(key, out var v) ? v : fallback;
}

internal static class InjurySeverityNames
{
    // F160 enum — all values correlated 1:1 against MFE CSV InjurySeverity column.
    private static readonly Dictionary<int, string> Names = new()
    {
        [5] = "Game Ending",
        [6] = "Couple Games",
        [7] = "Several Games",
        [8] = "Season Ending",
        [255] = "None"
    };
    public static string GetValueOrDefault(int key, string fallback) =>
        Names.TryGetValue(key, out var v) ? v : fallback;
}
