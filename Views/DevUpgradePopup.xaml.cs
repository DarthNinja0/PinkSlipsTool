using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class DevUpgradePopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private readonly bool _downgrade;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;

    public DevUpgradePopup(DynastyFile dynasty, bool downgrade = false)
    {
        InitializeComponent();
        _downgrade = downgrade;
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        if (_downgrade)
        {
            Title = "Downgrade Dev Trait";
            InstructionText.Text = "Select a player to DOWNGRADE their development trait (penalty):";
            UpgradeButton.Content = "DOWNGRADE DEV TRAIT";
        }
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
        _players = _editor.GetPlayersByTeam(_userTeamIndex);
        PlayerListBox.ItemsSource = _players;
        HeaderText.Text = $"YOUR ROSTER — {teamName} ({_players.Count} players)";
        CountText.Text = $"{_players.Count} players on roster";
        if (_players.Count == 0)
            StatusText.Text = "No players found for your team. " + _editor.DiagnosticInfo();
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(SortBox, PlayerListBox, _players);

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        if (_selectedPlayer == null) return;

        if (_downgrade)
        {
            if (_selectedPlayer.TraitDevelopment <= 0)
            {
                UpgradeButton.IsEnabled = false;
                StatusText.Text = $"{_selectedPlayer.Name} is already Normal — cannot downgrade further";
            }
            else
            {
                UpgradeButton.IsEnabled = true;
                var newTrait = _selectedPlayer.TraitDevelopment - 1;
                var newTraitName = newTrait switch { 0 => "Normal", 1 => "Impact", 2 => "Star", _ => "?" };
                StatusText.Text = $"{_selectedPlayer.Name} ({_selectedPlayer.Position})  {_selectedPlayer.DevTrait} → {newTraitName}";
            }
            return;
        }

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

    private void UpgradeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;

        if (_downgrade)
        {
            var newTrait = player.TraitDevelopment - 1;
            var newTraitName = newTrait switch { 0 => "Normal", 1 => "Impact", 2 => "Star", _ => "?" };

            var result = MessageBox.Show(
                $"Downgrade {player.Name}'s dev trait from {player.DevTrait} to {newTraitName}?",
                "Confirm Downgrade", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            if (!_editor.DowngradeDevTrait(player.RecordIndex))
            {
                StatusText.Text = "Downgrade failed — field detection may be incorrect";
                return;
            }

            LoadRoster();
            UpgradeButton.IsEnabled = false;
            StatusText.Text = $"DOWNGRADED: {player.Name} is now {newTraitName}!";
            return;
        }

        var newTraitUp = player.TraitDevelopment + 1;
        var newTraitUpName = newTraitUp switch { 1 => "Impact", 2 => "Star", 3 => "Elite", _ => "?" };

        var resultUp = MessageBox.Show(
            $"Upgrade {player.Name}'s dev trait from {player.DevTrait} to {newTraitUpName}?",
            "Confirm Upgrade", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (resultUp != MessageBoxResult.Yes) return;

        if (!_editor.UpgradeDevTrait(player.RecordIndex))
        {
            StatusText.Text = "Upgrade failed — field detection may be incorrect";
            return;
        }

        // Refresh the list to show updated trait (this clears the selection, so use the
        // captured values from before the reload)
        LoadRoster();
        UpgradeButton.IsEnabled = false;
        StatusText.Text = $"UPGRADED: {player.Name} is now {newTraitUpName}!";
    }
}
