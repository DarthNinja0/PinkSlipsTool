using PinkSlipsTool.Models;

if (args.Length < 2)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  SchemaDump inventory <savefile>          list all tables");
    Console.WriteLine("  SchemaDump players <savefile> [team]     dump players (optionally filtered by team index)");
    Console.WriteLine("  SchemaDump search <savefile> <u16 hex>   find a 16-bit LE value across all table records");
    Console.WriteLine("  SchemaDump scanfields <savefile>        scan player table fields for ID-like columns");
    Console.WriteLine("  SchemaDump team <savefile>               dump team table records (hex)");
    Console.WriteLine("  SchemaDump table <savefile> <id> [n]     dump records of a table by id");
    Console.WriteLine("  SchemaDump teamfields <savefile>        find other team-like fields in player table");
    Console.WriteLine("  SchemaDump roster <savefile>            dump Team.Roster field (pos 242) as references");
    Console.WriteLine("  SchemaDump tableid <savefile> <id>      scan payload for a table id (4-byte BE)");
    Console.WriteLine("  SchemaDump scanmagic <savefile>         find ASTO/SPEX magic tables and their ids");
    Console.WriteLine("  SchemaDump refscan <savefile>           find dense clusters of player refs (0x2130xxxx)");
    Console.WriteLine("  SchemaDump hexrange <savefile> <abs> <len>  dump raw payload bytes at absolute offset");
    Console.WriteLine("  SchemaDump scanarrays <savefile> <start> <end>  enumerate embedded array (ASTO) tables in a byte range");
    Console.WriteLine("  SchemaDump rosterdata <savefile>       decode roster array (id 6097) and cross-check Player.TeamIndex");
    Console.WriteLine("  SchemaDump testroster <savefile> <out> [teamA] [teamB]  exercise cut/steal/transfer, save, and re-validate roster consistency (teams are MASTER IDs, default 0/1)");
    Console.WriteLine("  SchemaDump repairoster <savefile> [out]  fix Player.TeamIndex (field 272) to match the authoritative roster array 6097; array is never touched. Backup + save in place unless out given");
    Console.WriteLine("  SchemaDump arrayhash <savefile>          print MD5 of the roster array (6097) payload for cross-save comparison");
    Console.WriteLine("  SchemaDump checkroster <savefile>        read-only: every roster row's refs must match the owning master ID");
    Console.WriteLine("  SchemaDump teammap <savefile>            dump array-row <-> master-ID <-> team-table mapping and the user's coach team");
    return;
}

var cmd = args[0].ToLowerInvariant();
var path = args[1];

var file = DynastyFile.Load(path);
Console.WriteLine($"Loaded {path}");
Console.WriteLine($"Payload: {file.DecompressedPayload.Length} bytes, {file.Tables.Count} tables\n");

switch (cmd)
{
    case "inventory":
        Inventory(file);
        break;
    case "players":
        var teamFilter = args.Length >= 3 ? int.Parse(args[2]) : -1;
        DumpPlayers(file, teamFilter);
        break;
    case "search":
        var needle = Convert.ToInt32(args[2], 16);
        SearchU16(file, needle);
        break;
    case "scanfields":
        ScanPlayerFields(file);
        break;
    case "team":
        DumpTeam(file);
        break;
    case "table":
        var tid = int.Parse(args[2]);
        var count = args.Length >= 4 ? int.Parse(args[3]) : 5;
        DumpTableById(file, tid, count);
        break;
    case "teamfields":
        FindTeamLikeFields(file);
        break;
    case "roster":
        DumpRosterField(file);
        break;
    case "tableid":
        var targetId = int.Parse(args[2]);
        ScanTableId(file, targetId);
        break;
    case "scanmagic":
        ScanMagic(file);
        break;
    case "refscan":
        ScanPlayerRefClusters(file);
        break;
    case "hexrange":
        var hx = int.Parse(args[2]);
        var hl = int.Parse(args[3]);
        DumpHexRange(file, hx, hl);
        break;
    case "scanarrays":
        var sa = int.Parse(args[2]);
        var se = int.Parse(args[3]);
        ScanArrays(file, sa, se);
        break;
    case "rosterdata":
        var rdStart = args.Length >= 3 ? int.Parse(args[2]) : 0;
        var rdEnd = args.Length >= 4 ? int.Parse(args[3]) : 5;
        DumpRosterData(file, rdStart, rdEnd);
        break;
    case "testroster":
        TestRoster(file, args[2], args.Length >= 4 ? args[3] : null, args.Length >= 5 ? args[4] : null);
        break;
    case "repairoster":
        RepairRoster(file, args.Length >= 3 ? args[2] : null);
        break;
    case "arrayhash":
        ArrayHash(file);
        break;
    case "checkroster":
        CheckRoster(file);
        break;
    case "teammap":
        TeamMap(file);
        break;
    default:
        Console.WriteLine($"Unknown command: {cmd}");
        break;
}

static void Inventory(DynastyFile file)
{
    var i = 0;
    foreach (var t in file.Tables)
    {
        var h = t.Header;
        var rosterish = h.Name.IndexOf("Roster", StringComparison.OrdinalIgnoreCase) >= 0;
        var marker = rosterish ? "  <-- ROSTER?" : "";
        Console.WriteLine($"[{i,3}] {h.Name,-40} id={h.TableId,-6} uid={h.UniqueId,-6} array={h.IsArray,-5} members={h.NumMembers,-5} cap={h.RecordCount,-6} nextUse={h.NextRecordToUse,-6} recSize={h.RecordSize,-5} abs={t.AbsoluteStart,8}{marker}");
        i++;
    }
}

static void DumpPlayers(DynastyFile file, int teamFilter)
{
    var table = file.GetTable(PlayerTableInfo.TableId);
    if (table == null) { Console.WriteLine("Player table not found"); return; }
    var offsets = table.FieldOffsets;
    var widths = table.FieldBitWidths;
    var counts = new Dictionary<int, int>();
    for (var i = 0; i < table.Header.NextRecordToUse; i++)
    {
        var rec = table.GetRecordBytes(i);
        if (rec == null) break;
        var ti = ReadBits(rec, offsets[272], widths[272]);
        counts[ti] = counts.GetValueOrDefault(ti) + 1;
        if (teamFilter >= 0 && ti != teamFilter) continue;
        var fn = ReadPoolString(rec, offsets[146], table);
        var ln = ReadPoolString(rec, offsets[174], table);
        var pos = ReadBits(rec, offsets[3], widths[3]);
        var ovr = ReadBits(rec, offsets[198], widths[198]);
        if (teamFilter >= 0 || i < 20)
            Console.WriteLine($"R{i,5}: team={ti,3} pos={pos,2} ovr={ovr,3} [{fn} {ln}]");
    }
    if (teamFilter < 0)
    {
        Console.WriteLine("\nTeam roster sizes:");
        foreach (var kv in counts.OrderBy(k => k.Key))
            Console.WriteLine($"  team {kv.Key,3}: {kv.Value}");
    }
}

static void SearchU16(DynastyFile file, int needle)
{
    foreach (var t in file.Tables)
    {
        var h = t.Header;
        var hits = new List<int>();
        for (var i = 0; i < h.NextRecordToUse; i++)
        {
            var rec = t.GetRecordBytes(i);
            if (rec == null) break;
            for (var b = 0; b <= rec.Length - 2; b++)
            {
                var v = rec[b] | (rec[b + 1] << 8);
                if (v == needle) { hits.Add(i); break; }
            }
        }
        if (hits.Count > 0)
        {
            var sample = string.Join(",", hits.Take(12));
            var more = hits.Count > 12 ? $", ... ({hits.Count} total)" : "";
            Console.WriteLine($"{h.Name,-40} id={h.TableId} hits={hits.Count,5}  recs=[{sample}{more}]");
        }
    }
}

static void TestRoster(DynastyFile file, string outPath, string teamAArg = null, string teamBArg = null)
{
    var editor = new DynastyEditor(file);
    var arr = file.GetArrayTable(6097);
    if (arr == null) { Console.WriteLine("No roster array — abort"); return; }
    var teamTbl = file.GetTable(6311);
    var to = teamTbl.FieldOffsets;
    var tw = teamTbl.FieldBitWidths;

    // Master ID -> array row via the team table's authoritative Team.Roster ref (pos 242).
    int RowOf(int masterId)
    {
        for (var p = 0; p < teamTbl.Header.NextRecordToUse; p++)
        {
            var rec = teamTbl.GetRecordBytes(p);
            if (rec == null) break;
            var m = RecordCodec.ReadBits(rec, to[390], tw[390]);
            if (m != masterId) continue;
            var rosterRef = RecordCodec.ReadBits(rec, to[242], 32);
            if (((rosterRef >> 17) & 0x7FFF) != 6097) return -1;
            return rosterRef & 0x1FFFF;
        }
        return -1;
    }
    // Given master IDs, the array rows whose field-272 majority is that master ID.
    int[] RowsOfMaster(int masterId)
    {
        var rows = new List<int>();
        for (var t = 0; t < arr.Header.RecordCount; t++)
        {
            var votes = new Dictionary<int, int>();
            var size = arr.ReadArraySize(t);
            for (var i = 0; i < size; i++)
            {
                var refVal = (int)arr.ReadArrayRowRef(t, i);
                var tab = (refVal >> 17) & 0x7FFF;
                var row = refVal & 0x1FFFF;
                if (tab != 4248 || row >= file.GetTable(4248).Header.NextRecordToUse) continue;
                var rec = file.GetTable(4248).GetRecordBytes(row);
                var v = RecordCodec.ReadBits(rec, file.GetTable(4248).FieldOffsets[272], file.GetTable(4248).FieldBitWidths[272]);
                if (v < 0 || v >= arr.Header.RecordCount) continue;
                votes.TryGetValue(v, out var c);
                votes[v] = c + 1;
            }
            var best = -1; var bestCount = 0;
            foreach (var kv in votes)
                if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
            if (best == masterId) rows.Add(t);
        }
        return rows.ToArray();
    }

    var masterA = teamAArg != null ? int.Parse(teamAArg) : 0;
    var masterB = teamBArg != null ? int.Parse(teamBArg) : 1;
    var rowA = RowOf(masterA);
    var rowB = RowOf(masterB);
    Console.WriteLine($"Teams: A=master {masterA} (array row {rowA}), B=master {masterB} (array row {rowB})");

    int SizeOf(int row) => arr.ReadArraySize(row);
    bool RosterContains(int row, int recordIdx)
    {
        var target = (uint)((4248 << 17) | recordIdx);
        for (var i = 0; i < arr.ReadArraySize(row); i++)
            if (arr.ReadArrayRowRef(row, i) == target) return true;
        return false;
    }
    int TeamOf(int recordIdx)
    {
        var player = file.GetTable(4248);
        var rec = player.GetRecordBytes(recordIdx);
        return RecordCodec.ReadBits(rec, player.FieldOffsets[272], player.FieldBitWidths[272]);
    }

    var teamA = editor.GetPlayersByTeam(masterA);
    var teamB = editor.GetPlayersByTeam(masterB);
    if (teamA.Count == 0 || teamB.Count == 0) { Console.WriteLine("A team list empty — pick a cleaner save"); return; }
    Console.WriteLine($"Initial: T{masterA} roster={SizeOf(rowA)} teamList={teamA.Count}  T{masterB} roster={SizeOf(rowB)} teamList={teamB.Count}");

    var victim = teamA[0];
    if (!editor.CutPlayer(victim.RecordIndex)) { Console.WriteLine("  CUT FAILED"); return; }
    Console.WriteLine($"CUT {victim.Name}: TeamIndex={TeamOf(victim.RecordIndex)} T{masterA} roster={SizeOf(rowA)} contains={RosterContains(rowA, victim.RecordIndex)}");

    var stealSrc = teamB[0];
    if (!editor.StealPlayer(stealSrc.RecordIndex, masterA)) { Console.WriteLine("  STEAL FAILED"); return; }
    Console.WriteLine($"STEAL {stealSrc.Name} -> T{masterA}: TeamIndex={TeamOf(stealSrc.RecordIndex)} T{masterA} roster={SizeOf(rowA)} contains={RosterContains(rowA, stealSrc.RecordIndex)} T{masterB} roster={SizeOf(rowB)} contains={RosterContains(rowB, stealSrc.RecordIndex)}");

    var a = editor.GetPlayersByTeam(masterA)[0];
    var b = editor.GetPlayersByTeam(masterB)[0];
    if (!editor.TransferPlayer(a.RecordIndex, b.RecordIndex)) { Console.WriteLine("  TRANSFER FAILED"); return; }
    Console.WriteLine($"TRANSFER {a.Name}(T{masterA}) <-> {b.Name}(T{masterB}): Ateam={TeamOf(a.RecordIndex)} Bteam={TeamOf(b.RecordIndex)}");
    Console.WriteLine($"  T{masterA} contains A={RosterContains(rowA, a.RecordIndex)} contains B={RosterContains(rowA, b.RecordIndex)} roster={SizeOf(rowA)}");
    Console.WriteLine($"  T{masterB} contains A={RosterContains(rowB, a.RecordIndex)} contains B={RosterContains(rowB, b.RecordIndex)} roster={SizeOf(rowB)}");

    file.Save(outPath);
    Console.WriteLine($"Saved to {outPath}");

    var reloaded = DynastyFile.Load(outPath);
    var arr2 = reloaded.GetArrayTable(6097);
    var player2 = reloaded.GetTable(4248);
    var po = player2.FieldOffsets;
    var pw = player2.FieldBitWidths;
    foreach (var m in new[] { masterA, masterB })
    {
        var row = RowsOfMaster(m);
        if (row.Length != 1) { Console.WriteLine($"Reloaded master {m}: expected exactly 1 owner row, found {row.Length}"); continue; }
        var size = arr2.ReadArraySize(row[0]);
        var mismatches = 0;
        for (var i = 0; i < size; i++)
        {
            var refVal = (int)arr2.ReadArrayRowRef(row[0], i);
            var tab = (refVal >> 17) & 0x7FFF;
            var rowIdx = refVal & 0x1FFFF;
            var team = -1;
            if (tab == 4248 && rowIdx < player2.Header.NextRecordToUse)
            {
                var rec = player2.GetRecordBytes(rowIdx);
                team = RecordCodec.ReadBits(rec, po[272], pw[272]);
            }
            if (team != m) mismatches++;
        }
        Console.WriteLine($"Reloaded T{m} (row {row[0]}): rosterSize={size} mismatches={mismatches}");
    }
}

static void TeamMap(DynastyFile file)
{
    var arr = file.GetArrayTable(6097);
    var player = file.GetTable(4248);
    var team = file.GetTable(6311);
    var coach = file.GetTable(4176);
    if (arr == null || player == null) { Console.WriteLine("Required tables missing"); return; }
    var po = player.FieldOffsets;
    var pw = player.FieldBitWidths;
    var teamCol = 272;
    var teamCount = arr.Header.RecordCount;
    var slotsPerRow = arr.Header.RecordSize / 4;

    // masterID per array row = majority field272 among the row's players
    var rowOwner = new int[teamCount];
    Array.Fill(rowOwner, -1);
    for (var t = 0; t < teamCount; t++)
    {
        var votes = new Dictionary<int, int>();
        var size = arr.ReadArraySize(t);
        for (var i = 0; i < size && i < slotsPerRow; i++)
        {
            var refVal = (int)arr.ReadArrayRowRef(t, i);
            var tab = (refVal >> 17) & 0x7FFF;
            var row = refVal & 0x1FFFF;
            if (tab != 4248 || row >= player.Header.NextRecordToUse) continue;
            var rec = player.GetRecordBytes(row);
            var v = RecordCodec.ReadBits(rec, po[teamCol], pw[teamCol]);
            if (v < 0 || v >= teamCount) continue;
            votes.TryGetValue(v, out var c);
            votes[v] = c + 1;
        }
        var best = -1; var bestCount = 0;
        foreach (var kv in votes)
            if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
        rowOwner[t] = best;
    }

    // team table record p's field-390 value (master ID claim) and name (field 72 pool ptr)
    var team390 = new int[teamCount];
    var teamName = new string[teamCount];
    var teamRosterRow = new int[teamCount]; // Team.Roster ref (member 242) -> array row
    Array.Fill(team390, -1);
    Array.Fill(teamRosterRow, -1);
    for (var p = 0; p < teamCount && p < team.Header.NextRecordToUse; p++)
    {
        var rec = team.GetRecordBytes(p);
        if (rec == null) continue;
        var to = team.FieldOffsets;
        var tw = team.FieldBitWidths;
        if (390 < to.Length && to[390] >= 0 && tw[390] > 0)
            team390[p] = RecordCodec.ReadBits(rec, to[390], tw[390]);
        if (72 < to.Length)
            teamName[p] = team.ResolvePoolString(RecordCodec.ReadBits(rec, to[72], 32));
        if (242 < to.Length && to[242] >= 0)
        {
            var rosterRef = RecordCodec.ReadBits(rec, to[242], 32);
            var tab = (rosterRef >> 17) & 0x7FFF;
            var row = rosterRef & 0x1FFFF;
            if (tab == 6097 && row < teamCount) teamRosterRow[p] = row;
        }
    }

    // inverse: master ID -> array row
    var rowByMaster = new Dictionary<int, int>();
    for (var t = 0; t < teamCount; t++)
        if (rowOwner[t] >= 0 && !rowByMaster.ContainsKey(rowOwner[t]))
            rowByMaster[rowOwner[t]] = t;

    // cross-check: team record (master=team390) Roster ref row should equal array-derived row
    var rosterMatch = 0;
    var rosterMismatch = 0;
    for (var p = 0; p < team.Header.NextRecordToUse; p++)
    {
        var m = team390[p];
        if (m < 0 || teamRosterRow[p] < 0) continue;
        var arrRow = rowByMaster.TryGetValue(m, out var r) ? r : -1;
        if (arrRow == teamRosterRow[p]) rosterMatch++;
        else { rosterMismatch++; if (rosterMismatch <= 5) Console.WriteLine($"  ROSTER MISMATCH: team record {p} master={m} rosterRefRow={teamRosterRow[p]} arrayRow={arrRow}"); }
    }
    Console.WriteLine($"Roster-ref cross-check: {rosterMatch} match, {rosterMismatch} mismatch");

    Console.WriteLine($"teamCount={teamCount} teamTableNextUse={team.Header.NextRecordToUse}");
    Console.WriteLine($"{"row",4} {"master",6} {"t390",6} {"name"}");
    for (var t = 0; t < teamCount; t++)
        Console.WriteLine($"{t,4} {rowOwner[t],6} {team390[t],6} {(teamName[t] ?? "")}");
    Console.WriteLine("Inverse (master -> row): " + string.Join(", ", rowByMaster.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}->{kv.Value}")));

    if (coach != null)
    {
        var co = coach.FieldOffsets;
        var cw = coach.FieldBitWidths;
        Console.WriteLine($"\nCoach table nextUse={coach.Header.NextRecordToUse} fields={co?.Length}");
        for (var i = 0; i < coach.Header.NextRecordToUse; i++)
        {
            var rec = coach.GetRecordBytes(i);
            if (rec == null) break;
            var isUser = co[0] >= 0 && RecordCodec.ReadBits(rec, co[0], 1) == 1;
            var teamIdx = co.Length > 8 && co[8] >= 0 && cw[8] > 0 ? RecordCodec.ReadBits(rec, co[8], cw[8]) : -1;
            var rowOfTeam = teamIdx >= 0 && rowByMaster.TryGetValue(teamIdx, out var r) ? r : -1;
            Console.WriteLine($"Coach {i}: user={isUser} TeamIndex={teamIdx} (array row {rowOfTeam})");
        }
    }
}

static void ArrayHash(DynastyFile file)
{
    var arr = file.GetArrayTable(6097);
    if (arr == null) { Console.WriteLine("Roster array (6097) not found"); return; }
    using var md5 = System.Security.Cryptography.MD5.Create();
    var hash = md5.ComputeHash(arr.Data);
    Console.WriteLine($"RosterArray6097 MD5 = {BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}  (extent={arr.Data.Length}, recs={arr.Header.RecordCount})");
}

static void CheckRoster(DynastyFile file)
{
    // Read-only validation: every ref in every roster row must have Player field-272 == the
    // master ID that owns the row (from the team table's authoritative Team.Roster ref, pos 242).
    var arr = file.GetArrayTable(6097);
    var player = file.GetTable(4248);
    var team = file.GetTable(6311);
    if (arr == null || player == null || team == null) { Console.WriteLine("Required tables missing"); return; }
    var to = team.FieldOffsets;
    var tw = team.FieldBitWidths;
    var rowCount = arr.Header.RecordCount;
    var slotsPerRow = arr.Header.RecordSize / 4;

    // Each team record's Roster ref (pos 242) names the array row it owns; the record's
    // field-390 is the master ID that row's players carry (255 for the 5 FCS placeholders).
    var masterByRow = new int[rowCount];
    Array.Fill(masterByRow, -1);
    for (var p = 0; p < team.Header.NextRecordToUse; p++)
    {
        var rec = team.GetRecordBytes(p);
        if (rec == null) break;
        var m = RecordCodec.ReadBits(rec, to[390], tw[390]);
        var rosterRef = RecordCodec.ReadBits(rec, to[242], 32);
        if (((rosterRef >> 17) & 0x7FFF) != 6097) continue;
        var row = rosterRef & 0x1FFFF;
        if (row >= 0 && row < rowCount) masterByRow[row] = m;
    }

    var po = player.FieldOffsets;
    var pw = player.FieldBitWidths;
    var teamCol = 272;
    var totalMismatch = 0;
    var totalRefs = 0;
    for (var r = 0; r < rowCount; r++)
    {
        var owner = masterByRow[r];
        var size = arr.ReadArraySize(r);
        if (owner < 0 && size == 0) continue;
        if (owner < 0 && size > 0) { Console.WriteLine($"  Row {r}: has {size} players but no owning master"); totalMismatch += size; continue; }
        for (var i = 0; i < size && i < slotsPerRow; i++)
        {
            totalRefs++;
            var refVal = (int)arr.ReadArrayRowRef(r, i);
            var tab = (refVal >> 17) & 0x7FFF;
            var rowIdx = refVal & 0x1FFFF;
            var teamVal = -1;
            if (tab == 4248 && rowIdx < player.Header.NextRecordToUse)
            {
                var rec = player.GetRecordBytes(rowIdx);
                teamVal = RecordCodec.ReadBits(rec, po[teamCol], pw[teamCol]);
            }
            if (teamVal != owner)
            {
                totalMismatch++;
                if (totalMismatch <= 10)
                    Console.WriteLine($"  Row {r} (master {owner}) slot {i}: player {rowIdx} field272={teamVal}");
            }
        }
    }
    Console.WriteLine($"Roster check: {totalRefs} refs across {rowCount} rows, {totalMismatch} mismatches");
}

static void RepairRoster(DynastyFile file, string outPath)
{
    // The roster array 6097 is the authoritative roster. Each team's roster lives in the array
    // row its Team.Roster ref (member 242) points at. Player field 272 ("TeamIndex") is the
    // game's master team ID (0-151, 255=free agent), NOT the array row index.
    // Old tool versions edited field 272 only, so field 272 can disagree with the array. This repair
    // makes field 272 consistent with the array and never modifies the array itself.
    var arr = file.GetArrayTable(6097);
    if (arr == null) { Console.WriteLine("Roster array (6097) not found"); return; }
    var player = file.GetTable(4248);
    if (player == null) { Console.WriteLine("Player table (4248) not found"); return; }
    var po = player.FieldOffsets;
    var pw = player.FieldBitWidths;
    var teamCol = 272;
    var teamCount = arr.Header.RecordCount;
    var slotsPerRow = arr.Header.RecordSize / 4;

    // Derive each row's master team ID as the majority field272 among the row's players.
    // rowOwner[row] = -1 when a row has no players or no player with a valid (0-151) TeamIndex.
    var rowOwner = new int[teamCount];
    Array.Fill(rowOwner, -1);
    for (var t = 0; t < teamCount; t++)
    {
        var votes = new Dictionary<int, int>();
        var size = arr.ReadArraySize(t);
        for (var i = 0; i < size && i < slotsPerRow; i++)
        {
            var refVal = (int)arr.ReadArrayRowRef(t, i);
            var tab = (refVal >> 17) & 0x7FFF;
            var row = refVal & 0x1FFFF;
            if (tab != 4248 || row >= player.Header.NextRecordToUse) continue;
            var rec = player.GetRecordBytes(row);
            var team = RecordCodec.ReadBits(rec, po[teamCol], pw[teamCol]);
            if (team < 0 || team >= teamCount) continue;
            votes.TryGetValue(team, out var c);
            votes[team] = c + 1;
        }
        var best = -1;
        var bestCount = 0;
        foreach (var kv in votes)
            if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
        rowOwner[t] = best;
    }

    // Which array row does each player live in?
    var playerRow = new int[player.Header.NextRecordToUse];
    Array.Fill(playerRow, -1);
    for (var t = 0; t < teamCount; t++)
    {
        var size = arr.ReadArraySize(t);
        for (var i = 0; i < size && i < slotsPerRow; i++)
        {
            var refVal = (int)arr.ReadArrayRowRef(t, i);
            var tab = (refVal >> 17) & 0x7FFF;
            var row = refVal & 0x1FFFF;
            if (tab == 4248 && row < player.Header.NextRecordToUse && playerRow[row] == -1)
                playerRow[row] = t;
        }
    }

    // Repair field 272 to the owning row's master ID.
    var fixedCount = 0;
    var mismatchBefore = 0;
    var mismatchAfter = 0;
    for (var ri = 0; ri < player.Header.NextRecordToUse; ri++)
    {
        var rec = player.GetRecordBytes(ri);
        if (rec == null) continue;
        var row = playerRow[ri];
        if (row < 0) continue; // free agents / unrostered players are left alone
        var current = RecordCodec.ReadBits(rec, po[teamCol], pw[teamCol]);
        var expected = rowOwner[row];
        if (expected < 0) continue; // ambiguous row, leave as-is
        if (current != expected)
        {
            mismatchBefore++;
            // rewrite only the TeamIndex field in a copy of the record, then write it back
            var newRec = WriteBits(rec, po[teamCol], pw[teamCol], expected);
            player.WriteRecordBytes(ri, newRec);
            fixedCount++;
        }
        if (RecordCodec.ReadBits(player.GetRecordBytes(ri), po[teamCol], pw[teamCol]) != expected)
            mismatchAfter++;
    }

    Console.WriteLine($"Rows with roster: {teamCount - Array.FindAll(rowOwner, x => x < 0).Length}, rows empty/ambiguous: {Array.FindAll(rowOwner, x => x < 0).Length}");
    Console.WriteLine($"Field272 mismatches vs array: before={mismatchBefore}, fixed={fixedCount}, remaining={mismatchAfter}");
    Console.WriteLine("Note: roster array 6097 is NOT modified.");

    file.SyncTable(player);

    if (outPath == null)
    {
        file.CreateBackup();
        outPath = file.LoadedPath;
        Console.WriteLine($"Backup: {file.BackupPath}");
    }
    file.Save(outPath);
    Console.WriteLine($"Saved: {outPath}");
}

// Rewrite a bit field in a copy of the record and return the new bytes.
static byte[] WriteBits(byte[] rec, int bitOffset, int length, int value)
{
    var result = (byte[])rec.Clone();
    for (var b = 0; b < length; b++)
    {
        var bit = (value >> (length - 1 - b)) & 1;
        var byteIdx = (bitOffset + b) / 8;
        var bitIdx = 7 - ((bitOffset + b) % 8);
        if (bit == 1) result[byteIdx] |= (byte)(1 << bitIdx);
        else result[byteIdx] &= (byte)~(1 << bitIdx);
    }
    return result;
}

static void DumpRosterData(DynastyFile file, int startTeam = 0, int endTeam = 5)
{
    var arr = file.GetArrayTable(6097);
    if (arr == null) { Console.WriteLine("Roster array (6097) not found via GetArrayTable"); return; }
    Console.WriteLine($"Roster array 6097 at abs 0x{arr.AbsoluteStart:X8} extent={arr.Data.Length}");
    Console.WriteLine($"  recCount={arr.Header.RecordCount} recordSize={arr.Header.RecordSize} headerSize=0x{arr.Header.OffsetStart:X} table1Start=0x{arr.Header.Table1Start:X}");

    var sizes = new int[arr.Header.RecordCount];
    for (var i = 0; i < sizes.Length; i++) sizes[i] = arr.ReadArraySize(i);
    Console.WriteLine($"  arraySizes[0..9] = {string.Join(",", sizes.Take(10))}");

    var player = file.GetTable(4248);
    var po = player.FieldOffsets;
    var pw = player.FieldBitWidths;
    var teamCol = 272;
    var mismatches = 0;
    var sampled = 0;
    for (var t = startTeam; t < Math.Min(endTeam, arr.Header.RecordCount); t++)
    {
        var row = new List<string>();
        for (var i = 0; i < sizes[t] && i < 100; i++)
        {
            var refVal = (int)arr.ReadArrayRowRef(t, i);
            var tab = (refVal >> 17) & 0x7FFF;
            var rowIdx = refVal & 0x1FFFF;
            var team = -1;
            if (tab == 4248 && rowIdx < player.Header.NextRecordToUse)
            {
                var rec = player.GetRecordBytes(rowIdx);
                team = RecordCodec.ReadBits(rec, po[teamCol], pw[teamCol]);
                sampled++;
                if (team != t) mismatches++;
            }
            row.Add($"{tab}:{rowIdx}->T{team}");
        }
        Console.WriteLine($"  Team {t} ({sizes[t]} players): {string.Join(" ", row.Take(12))}{(sizes[t] > 12 ? " ..." : "")}");
    }
    Console.WriteLine($"  Cross-check: {sampled} player refs resolved, {mismatches} with TeamIndex != roster row");
}

static void ScanArrays(DynastyFile file, int startAbs, int endAbs)
{
    var payload = file.DecompressedPayload;
    var magic = new byte[] { 0x41, 0x53, 0x54, 0x4F };
    for (var i = startAbs; i < endAbs - 3; i++)
    {
        if (payload[i] != magic[0] || payload[i + 1] != magic[1] ||
            payload[i + 2] != magic[2] || payload[i + 3] != magic[3]) continue;
        var s = i - 0x94;
        if (s < 0) continue;
        var id = (payload[s + 0x80] << 24) | (payload[s + 0x81] << 16) |
                 (payload[s + 0x82] << 8) | payload[s + 0x83];
        var uid = (payload[s + 0x84] << 24) | (payload[s + 0x85] << 16) |
                  (payload[s + 0x86] << 8) | payload[s + 0x87];
        var name = "";
        for (var c = s; c < s + 64 && c < payload.Length && payload[c] != 0; c++)
            name += (char)payload[c];
        var storeLen = (payload[s + 0xA4] << 24) | (payload[s + 0xA5] << 16) |
                       (payload[s + 0xA6] << 8) | payload[s + 0xA7];
        var ho = 0x80 + 40 + storeLen;
        var recCount = (payload[s + ho + 8] << 24) | (payload[s + ho + 9] << 16) |
                       (payload[s + ho + 10] << 8) | payload[s + ho + 11];
        var recWords = (payload[s + ho + 44] << 24) | (payload[s + ho + 45] << 16) |
                       (payload[s + ho + 46] << 8) | payload[s + ho + 47];
        Console.WriteLine($"  ASTO@{i} (0x{i:X8}) start=0x{s:X8} id={id} uid={uid:X8} \"{name}\" recs={recCount} words={recWords}");
    }
}

static void DumpHexRange(DynastyFile file, int abs, int len)
{
    var payload = file.DecompressedPayload;
    len = Math.Min(len, payload.Length - abs);
    for (var r = 0; r < len; r += 16)
    {
        var hex = BitConverter.ToString(payload, abs + r, Math.Min(16, len - r)).Replace("-", " ");
        var asc = "";
        for (var c = r; c < r + 16 && c < len; c++)
            asc += payload[abs + c] >= 32 && payload[abs + c] < 127 ? (char)payload[abs + c] : '.';
        Console.WriteLine($"  {abs + r:X8}: {hex,-47} {asc}");
    }
}

static void ScanPlayerRefClusters(DynastyFile file)
{
    var payload = file.DecompressedPayload;
    // Player table id 4248 -> reference first bytes 21 30 (BE), rows < 0x20000
    var bucket = 4096;
    var counts = new int[payload.Length / bucket + 1];
    for (var i = 0; i < payload.Length - 3; i++)
    {
        if (payload[i] == 0x21 && (payload[i + 1] == 0x30 || payload[i + 1] == 0x31))
        {
            var row = (payload[i + 2] << 8) | payload[i + 3];
            if (row < 0x20000)
                counts[i / bucket]++;
        }
    }
    Console.WriteLine("4KB buckets with >= 8 player refs (0x2130xxxx):");
    for (var b = 0; b < counts.Length; b++)
    {
        if (counts[b] >= 8)
        {
            var start = b * bucket;
            var end = Math.Min(start + bucket, payload.Length);
            var inside = "";
            foreach (var t in file.Tables)
                if (start >= t.AbsoluteStart && start < t.AbsoluteEnd) { inside = $"  inside {t.Header.Name} id={t.Header.TableId} (rel +{start - t.AbsoluteStart})"; break; }
            Console.WriteLine($"  {start,10} (0x{start:X8}) refs={counts[b],4}{inside}");
        }
    }
}

static void ScanMagic(DynastyFile file)
{
    var payload = file.DecompressedPayload;
    var magics = new (byte[] bytes, string name)[]
    {
        (new byte[] { 0x41, 0x53, 0x54, 0x4F }, "ASTO"),
        (new byte[] { 0x53, 0x50, 0x45, 0x58 }, "SPEX"),
    };
    foreach (var m in magics)
    {
        var hits = new List<int>();
        for (var i = 0; i < payload.Length - 3; i++)
        {
            if (payload[i] == m.bytes[0] && payload[i + 1] == m.bytes[1] &&
                payload[i + 2] == m.bytes[2] && payload[i + 3] == m.bytes[3])
                hits.Add(i);
        }
        Console.WriteLine($"{m.name}: {hits.Count} hits");
        foreach (var h in hits.Take(60))
        {
            var start = h - 0x94;
            var tableId = start >= 0 && start + 0x84 < payload.Length
                ? (payload[start + 0x80] << 24) | (payload[start + 0x81] << 16) |
                  (payload[start + 0x82] << 8) | payload[start + 0x83]
                : -1;
            Console.WriteLine($"  magic@{h} (0x{h:X8}) tableStart={start} tableId={tableId}");
        }
    }
}

static void ScanTableId(DynastyFile file, int targetId)
{
    var payload = file.DecompressedPayload;
    var pattern = new byte[] {
        (byte)((targetId >> 24) & 0xFF), (byte)((targetId >> 16) & 0xFF),
        (byte)((targetId >> 8) & 0xFF), (byte)(targetId & 0xFF) };
    var hits = new List<int>();
    for (var i = 0; i < payload.Length - 3; i++)
    {
        if (payload[i] == pattern[0] && payload[i + 1] == pattern[1] &&
            payload[i + 2] == pattern[2] && payload[i + 3] == pattern[3])
            hits.Add(i);
    }
    Console.WriteLine($"Table id {targetId} (0x{targetId:X4}) found at {hits.Count} offsets:");
    foreach (var h in hits.Take(40))
    {
        var rel = -1;
        foreach (var t in file.Tables)
            if (h >= t.AbsoluteStart && h < t.AbsoluteEnd) { rel = h - t.AbsoluteStart; break; }
        var ctx = rel >= 0 ? $"  (inside table at rel +{rel}, table abs {h - rel})" : "";
        Console.WriteLine($"  {h} (0x{h:X8}){ctx}");
    }
}

static void DumpRosterField(DynastyFile file)
{
    foreach (var teamTableId in new[] { 6311, 5292, 5294, 5295, 5296, 5297, 6016, 6017, 6018 })
    {
        var tt = file.GetTable(teamTableId);
        if (tt == null || tt.Header.NumMembers < 243) { Console.WriteLine($"Team table {teamTableId}: not found or members={tt?.Header.NumMembers}"); continue; }
        var rraw = tt.ReadRawOffsetTable();
        var off = rraw[242];
        var firstRefs = new List<string>();
        var nonzero = 0;
        for (var i = 0; i < Math.Min(tt.Header.NextRecordToUse, 10); i++)
        {
            var rec = tt.GetRecordBytes(i);
            if (rec == null) break;
            var bits = ReadBits(rec, off, 32);
            var tableId = (bits >> 17) & 0x7FFF;
            var row = bits & 0x1FFFF;
            if (tableId != 0 || row != 0) nonzero++;
            firstRefs.Add($"(t={tableId},r={row})");
        }
        Console.WriteLine($"Team {teamTableId}: nextUse={tt.Header.NextRecordToUse} roster(242)off={off} first={string.Join(" ", firstRefs.Take(4))} nonzeroIn10={nonzero}");
    }

    var table = file.GetTable(6311);
    if (table == null) { Console.WriteLine("Team table 6311 not found"); return; }
    var raw = table.ReadRawOffsetTable();
    var h = table.Header;
    Console.WriteLine($"Team table 6311: {h.NextRecordToUse} records, members={raw.Length}");
    Console.WriteLine($"  RecordSize={h.RecordSize} RecordCount={h.RecordCount} Table1Length={h.Table1Length} Table2Length={h.Table2Length} Table3Length={h.Table3Length}");
    Console.WriteLine($"  Table1Start={h.Table1Start} Table2Start={h.Table2Start} Table3Start={h.Table3Start} OffsetStart={h.OffsetStart}");

    foreach (var member in new[] { 242, 63, 64 })
    {
        var off = raw[member];
        if (off < 0) continue;
        Console.WriteLine($"\nMember {member}: bitOffset={off} ({off / 8} bytes)");
        var used = 0;
        for (var i = 0; i < table.Header.NextRecordToUse && i < 145; i++)
        {
            var rec = table.GetRecordBytes(i);
            if (rec == null) break;
            var bits = ReadBits(rec, off, 32);
            var tableId = (bits >> 17) & 0x7FFF;
            var row = bits & 0x1FFFF;
            if (tableId != 0 || row != 0) used++;
            if (i < 5)
                Console.WriteLine($"  R{i,3}: ref=(table={tableId}, row={row}) raw=0x{bits:X8}");
        }
        Console.WriteLine($"  ... {used}/{table.Header.NextRecordToUse} records have nonzero reference");
    }
}

static void FindTeamLikeFields(DynastyFile file)
{
    var table = file.GetTable(PlayerTableInfo.TableId);
    if (table == null) { Console.WriteLine("Player table not found"); return; }
    var offsets = table.FieldOffsets;
    var widths = table.FieldBitWidths;
    var n = table.Header.NextRecordToUse;
    var sample = Math.Min(2000, n);

    Console.WriteLine($"Player table: {n} records, {offsets.Length} fields");
    Console.WriteLine("Fields whose values repeat in clusters (team-index-like):\n");

    for (var f = 0; f < offsets.Length; f++)
    {
        var w = widths[f];
        if (w <= 0 || w > 12 || offsets[f] < 0) continue;
        var distinct = new HashSet<int>();
        var min = int.MaxValue; var max = int.MinValue;
        for (var i = 0; i < sample; i++)
        {
            var rec = table.GetRecordBytes(i);
            if (rec == null) break;
            var v = ReadBits(rec, offsets[f], w);
            distinct.Add(v);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        var distinctCount = distinct.Count;
        if (distinctCount == 0) continue;
        var ratio = (double)sample / distinctCount;
        if (distinctCount >= 2 && distinctCount <= 200 && ratio > 5 && max < 1000)
            Console.WriteLine($"  F{f,3}: width={w,3} distinct={distinctCount,5}/{sample} range=[{min},{max}] avgPerVal={ratio:F1}");
    }
}

static void ScanPlayerFields(DynastyFile file)
{
    var table = file.GetTable(PlayerTableInfo.TableId);
    if (table == null) { Console.WriteLine("Player table not found"); return; }
    var offsets = table.FieldOffsets;
    var widths = table.FieldBitWidths;
    var n = table.Header.NextRecordToUse;
    var sample = Math.Min(2000, n);

    Console.WriteLine($"Player table: {n} records, {offsets.Length} fields\n");
    Console.WriteLine("Fields with high value diversity (candidate ID fields):");
    for (var f = 0; f < offsets.Length; f++)
    {
        var w = widths[f];
        if (w <= 0 || offsets[f] < 0) continue;
        var distinct = new HashSet<int>();
        var min = int.MaxValue; var max = int.MinValue;
        for (var i = 0; i < sample; i++)
        {
            var rec = table.GetRecordBytes(i);
            if (rec == null) break;
            var v = ReadBits(rec, offsets[f], w);
            distinct.Add(v);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        var ratio = (double)distinct.Count / sample;
        if (distinct.Count >= Math.Min(50, sample) && max > 1000)
            Console.WriteLine($"  F{f,3}: width={w,3} distinct={distinct.Count,5}/{sample} range=[{min},{max}] ratio={ratio:P0}");
    }

    Console.WriteLine("\nFirst 20 fields (offset/width/value for record 0-2):");
    for (var f = 0; f < 20; f++)
    {
        var vals = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var rec = table.GetRecordBytes(i);
            if (rec == null) break;
            vals.Add(ReadBits(rec, offsets[f], widths[f]).ToString());
        }
        Console.WriteLine($"  F{f,3}: off={offsets[f],5} w={widths[f],3} vals=[{string.Join(",", vals)}]");
    }
}

static void DumpTeam(DynastyFile file)
{
    var table = file.GetTable(6311);
    if (table == null) { Console.WriteLine("Team table not found"); return; }
    var h = table.Header;
    Console.WriteLine($"Team table id=6311: nextUse={h.NextRecordToUse} recSize={h.RecordSize} members={h.NumMembers}");

    for (var i = 0; i < Math.Min(3, h.NextRecordToUse); i++)
    {
        var rec = table.GetRecordBytes(i);
        if (rec == null) break;
        Console.WriteLine($"\n--- Team record {i} ({rec.Length} bytes) ---");
        for (var r = 0; r < rec.Length; r += 16)
        {
            var hex = BitConverter.ToString(rec, r, Math.Min(16, rec.Length - r)).Replace("-", " ");
            var asc = "";
            for (var c = r; c < r + 16 && c < rec.Length; c++)
                asc += rec[c] >= 32 && rec[c] < 127 ? (char)rec[c] : '.';
            Console.WriteLine($"  {r:X4}: {hex,-47} {asc}");
        }
    }

    Console.WriteLine("\nTeam field offsets (raw):");
    var raw = table.ReadRawOffsetTable();
    for (var f = 0; f < raw.Length; f++)
        if (f < 30 || raw[f] > 6000)
            Console.WriteLine($"  M{f,3}: off={raw[f],6}");
}

static void DumpTableById(DynastyFile file, int id, int count)
{
    var table = file.GetTable(id);
    if (table == null) { Console.WriteLine($"Table {id} not found"); return; }
    var h = table.Header;
    Console.WriteLine($"{h.Name} id={id}: nextUse={h.NextRecordToUse} cap={h.RecordCount} recSize={h.RecordSize} members={h.NumMembers} abs={table.AbsoluteStart}");
    for (var i = 0; i < Math.Min(count, h.NextRecordToUse); i++)
    {
        var rec = table.GetRecordBytes(i);
        if (rec == null) break;
        var hex = BitConverter.ToString(rec).Replace("-", " ");
        var asc = "";
        foreach (var c in rec)
            asc += c >= 32 && c < 127 ? (char)c : '.';
        Console.WriteLine($"R{i,3}: {hex}  |{asc}|");
    }
}

static int ReadBits(byte[] rec, int bitOffset, int length)
{
    var value = 0;
    for (var b = 0; b < length; b++)
    {
        var byteIdx = (bitOffset + b) / 8;
        var bitIdx = 7 - ((bitOffset + b) % 8);
        var bit = (rec[byteIdx] >> bitIdx) & 1;
        value = (value << 1) | bit;
    }
    return value;
}

static string ReadPoolString(byte[] rec, int bitOffset, FranchiseTable table)
{
    var ptr = ReadBits(rec, bitOffset, 32);
    return table.ResolvePoolString(ptr);
}
