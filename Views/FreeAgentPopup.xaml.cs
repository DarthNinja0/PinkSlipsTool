using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class FreeAgentPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private PlayerData _selectedPlayer;
    private List<PlayerData> _freeAgents;

    public FreeAgentPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadFreeAgents();
    }

    private void LoadFreeAgents()
    {
        if (_userTeamIndex < 0)
        {
            HeaderText.Text = "Could not find your team in Coach table";
            StatusText.Text = _editor.DiagnosticInfo();
            return;
        }

        _freeAgents = _editor.GetPlayersByTeam(DynastyEditor.FreeAgentTeamIndex);
        FaListBox.ItemsSource = _freeAgents;
        HeaderText.Text = $"FREE AGENTS ({_freeAgents.Count} players)";
        CountText.Text = $"{_freeAgents.Count} free agents";
        if (_freeAgents.Count == 0)
            StatusText.Text = "No free agents found. " + _editor.DiagnosticInfo();
    }

    private void FaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = FaListBox.SelectedItem as PlayerData;
        if (_selectedPlayer == null) return;

        var rosterSize = _editor.GetPlayersByTeam(_userTeamIndex).Count;
        if (_selectedPlayer.TeamIndex != DynastyEditor.FreeAgentTeamIndex)
        {
            SignButton.IsEnabled = false;
            StatusText.Text = $"{_selectedPlayer.Name} is not a free agent";
        }
        else if (rosterSize >= 85)
        {
            SignButton.IsEnabled = false;
            StatusText.Text = $"Your roster is FULL ({rosterSize}/85) — cut someone first";
        }
        else
        {
            SignButton.IsEnabled = true;
            StatusText.Text = $"Ready to sign: {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)";
        }
    }

    private void SignButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;

        var result = MessageBox.Show(
            $"Sign {player.Name} ({player.Position}, {player.OverallRating} OVR) to your team?",
            "Confirm Sign", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.StealPlayer(player.RecordIndex, _userTeamIndex))
        {
            StatusText.Text = "Sign failed — your roster may be full (85-player cap)";
            return;
        }

        StatusText.Text = $"SIGNED: {player.Name} added to your team! Save the dynasty file to write the change.";
        SignButton.IsEnabled = false;
        LoadFreeAgents();
    }
}
