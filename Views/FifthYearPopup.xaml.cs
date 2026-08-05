using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class FifthYearPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private PlayerData _selectedPlayer;

    public FifthYearPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadRoster();
    }

    private void LoadRoster()
    {
        if (_userTeamIndex < 0)
        {
            HeaderText.Text = "Could not find your team in Coach table";
            StatusText.Text = _editor.DiagnosticInfo();
            return;
        }

        var teamName = _editor.GetTeamName(_userTeamIndex);
        var players = _editor.GetPlayersByTeam(_userTeamIndex);
        PlayerListBox.ItemsSource = players;
        HeaderText.Text = $"YOUR ROSTER — {teamName} ({players.Count} players)";
        CountText.Text = $"{players.Count} players on roster";
        if (players.Count == 0)
            StatusText.Text = "No players found for your team. " + _editor.DiagnosticInfo();
    }

    private static string YearLabel(int schoolYear) => schoolYear switch
    {
        0 => "FR", 1 => "SO", 2 => "JR", 3 => "SR", 4 => "GR",
        _ => $"Y{schoolYear}",
    };

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        if (_selectedPlayer == null) return;

        if (_selectedPlayer.SchoolYear >= 4)
        {
            ApplyButton.IsEnabled = false;
            StatusText.Text = $"{_selectedPlayer.Name} is already a {_selectedPlayer.YearLabel} — cannot grant more years";
        }
        else
        {
            ApplyButton.IsEnabled = true;
            StatusText.Text = $"{_selectedPlayer.Name} ({_selectedPlayer.YearLabel}) → {YearLabel(_selectedPlayer.SchoolYear + 1)}";
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;
        var newYear = player.SchoolYear + 1;

        var result = MessageBox.Show(
            $"Grant {player.Name} ({player.YearLabel} → {YearLabel(newYear)}) an extra year of eligibility?",
            "Confirm Fifth Year", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.SetSchoolYear(player.RecordIndex, newYear))
        {
            StatusText.Text = "Failed — field detection may be incorrect";
            return;
        }

        LoadRoster();
        ApplyButton.IsEnabled = false;
        StatusText.Text = $"FIFTH YEAR: {player.Name} is now a {YearLabel(newYear)}! Save the dynasty file to write the change.";
    }
}
