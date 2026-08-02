using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class DevUpgradePopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private PlayerData _selectedPlayer;

    public DevUpgradePopup(DynastyFile dynasty)
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

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        if (_selectedPlayer != null)
        {
            if (_selectedPlayer.TraitDevelopment >= 3)
            {
                UpgradeButton.IsEnabled = false;
                StatusText.Text = $"{_selectedPlayer.Name} is already Elite — cannot upgrade further";
            }
            else
            {
                UpgradeButton.IsEnabled = true;
                StatusText.Text = $"{_selectedPlayer.Name} ({_selectedPlayer.Position})  {_selectedPlayer.DevTrait} → {_selectedPlayer.TraitDevelopment + 1 switch {1=>"Impact",2=>"Star",3=>"Elite",_=>"?"}}";
            }
        }
    }

    private void UpgradeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;
        var newTrait = player.TraitDevelopment + 1;
        var newTraitName = newTrait switch { 1 => "Impact", 2 => "Star", 3 => "Elite", _ => "?" };

        var result = MessageBox.Show(
            $"Upgrade {player.Name}'s dev trait from {player.DevTrait} to {newTraitName}?",
            "Confirm Upgrade", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.UpgradeDevTrait(player.RecordIndex))
        {
            StatusText.Text = "Upgrade failed — field detection may be incorrect";
            return;
        }

        // Refresh the list to show updated trait (this clears the selection, so use the
        // captured values from before the reload)
        LoadRoster();
        UpgradeButton.IsEnabled = false;
        StatusText.Text = $"UPGRADED: {player.Name} is now {newTraitName}!";
    }
}
