using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace AvaloniaExercisesSolution;

public partial class MainWindow : Window
{
    private static readonly (string Name, Color Color)[] AvailableColors =
    {
        ("Blue", Colors.Blue),
        ("Red", Colors.Red),
        ("Green", Colors.Green),
        ("Orange", Colors.Orange),
        ("Purple", Colors.Purple),
        ("Cyan", Colors.Cyan),
        ("HotPink", Colors.HotPink),
        ("Gold", Colors.Gold),
    };

    private static readonly string LeaderboardPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "leaderboard.txt");

    private int _rows = 6;
    private int _cols = 6;
    private int[,] _board = null!;
    private Border[,] _cells = null!;
    private List<Player> _players = new();
    private int _currentPlayerIndex;
    private bool _gameActive;
    private int _gameGeneration;
    private List<LeaderboardEntry> _leaderboard = new();

    public MainWindow()
    {
        InitializeComponent();
        Width = 700;
        Height = 550;
        _board = new int[_rows, _cols];
        _cells = new Border[_rows, _cols];
        LoadLeaderboard();
        BuildBoard();
        UpdateLeaderboardUI();
    }

    // ───────── Board rendering ─────────

    private void BuildBoard()
    {
        GameBoard.RowDefinitions.Clear();
        GameBoard.ColumnDefinitions.Clear();
        GameBoard.Children.Clear();

        for (int r = 0; r < _rows; r++)
            GameBoard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        for (int c = 0; c < _cols; c++)
            GameBoard.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        _cells = new Border[_rows, _cols];
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var border = new Border
                {
                    Background = CellBrush(_board[r, c]),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                };
                int row = r, col = c;
                border.PointerPressed += (_, _) => OnCellClick(row, col);
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                GameBoard.Children.Add(border);
                _cells[r, c] = border;
            }
        }
        FooterText.Text = $"{_rows}x{_cols}";
    }

    private IBrush CellBrush(int value)
    {
        if (value == 0 || _players.Count == 0) return Brushes.White;
        var player = _players.FirstOrDefault(p => p.Number == value);
        return player != null ? new SolidColorBrush(player.Color) : Brushes.White;
    }

    private void UpdateHeader()
    {
        if (!_gameActive || _players.Count == 0)
        {
            HeaderText.Text = "Press Play to start";
            HeaderText.Foreground = Brushes.Black;
            return;
        }
        var p = _players[_currentPlayerIndex];
        HeaderText.Text = $"{p.Name} to move";
        HeaderText.Foreground = new SolidColorBrush(p.Color);
    }

    private void UpdateScores()
    {
        ScoresPanel.Children.Clear();
        if (_players.Count == 0) return;

        foreach (var p in _players)
        {
            int count = 0;
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_board[r, c] == p.Number) count++;

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };
            row.Children.Add(new Border
            {
                Width = 14, Height = 14,
                Background = new SolidColorBrush(p.Color),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var text = p.IsEliminated ? $"{p.Name}: {count} (out)" : $"{p.Name}: {count}";
            if (p.IsBot) text += " [Bot]";
            row.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = p.IsEliminated ? Brushes.Gray : Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            });
            ScoresPanel.Children.Add(row);
        }
    }

    // ───────── Cell click & move logic ─────────

    private void OnCellClick(int row, int col)
    {
        if (!_gameActive) return;
        if (_players[_currentPlayerIndex].IsBot) return;
        if (_board[row, col] != 0) return;
        if (!IsValidMove(row, col, _players[_currentPlayerIndex].Number)) return;
        MakeMove(row, col);
    }

    private void MakeMove(int row, int col)
    {
        var player = _players[_currentPlayerIndex];
        _board[row, col] = player.Number;
        _cells[row, col].Background = new SolidColorBrush(player.Color);
        AfterMove();
    }

    private void AfterMove()
    {
        if (IsBoardFull())
        {
            EndGame(null);
            return;
        }

        int checkedCount = 0;
        int next = _currentPlayerIndex;
        while (checkedCount < _players.Count)
        {
            next = (next + 1) % _players.Count;
            checkedCount++;
            if (_players[next].IsEliminated) continue;

            if (HasLegalMoves(_players[next].Number))
            {
                _currentPlayerIndex = next;
                UpdateHeader();
                UpdateScores();
                if (_players[_currentPlayerIndex].IsBot)
                    ScheduleBotMove();
                return;
            }

            _players[next].IsEliminated = true;
            var remaining = _players.Where(p => !p.IsEliminated).ToList();
            if (remaining.Count <= 1)
            {
                EndGame(remaining.Count == 1 ? remaining[0] : null);
                return;
            }
        }
        EndGame(null);
    }

    private void EndGame(Player? winner)
    {
        _gameActive = false;
        UpdateScores();
        if (winner != null)
        {
            HeaderText.Text = $"{winner.Name} wins!";
            HeaderText.Foreground = new SolidColorBrush(winner.Color);
            AddLeaderboardEntry(winner.Name);
        }
        else
        {
            HeaderText.Text = "Draw!";
            HeaderText.Foreground = Brushes.Black;
            AddLeaderboardEntry("Draw");
        }
    }

    // ───────── Game rules ─────────

    private bool IsValidMove(int row, int col, int playerNum)
    {
        if (_board[row, col] != 0) return false;
        if (!PlayerHasPieces(playerNum)) return true;
        return IsAdjacentTo(row, col, playerNum);
    }

    private bool PlayerHasPieces(int playerNum)
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (_board[r, c] == playerNum) return true;
        return false;
    }

    private bool IsAdjacentTo(int row, int col, int playerNum)
    {
        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++)
        {
            if (dr == 0 && dc == 0) continue;
            int nr = row + dr, nc = col + dc;
            if (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols && _board[nr, nc] == playerNum)
                return true;
        }
        return false;
    }

    private bool HasLegalMoves(int playerNum)
    {
        if (!PlayerHasPieces(playerNum))
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_board[r, c] == 0) return true;
            return false;
        }
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (_board[r, c] == 0 && IsAdjacentTo(r, c, playerNum))
                    return true;
        return false;
    }

    private bool IsBoardFull()
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (_board[r, c] == 0) return false;
        return true;
    }

    // ───────── Bot AI ─────────

    private async void ScheduleBotMove()
    {
        int gen = _gameGeneration;
        await Task.Delay(400);
        if (gen != _gameGeneration || !_gameActive) return;

        var player = _players[_currentPlayerIndex];
        if (!player.IsBot || player.IsEliminated) return;

        var moves = new List<(int Row, int Col, int Score)>();
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (IsValidMove(r, c, player.Number))
                    moves.Add((r, c, CountEmptyNeighbors(r, c)));

        if (moves.Count == 0) return;

        var rng = new Random();
        var best = moves.OrderByDescending(m => m.Score).ThenBy(_ => rng.Next()).First();
        MakeMove(best.Row, best.Col);
    }

    private int CountEmptyNeighbors(int row, int col)
    {
        int count = 0;
        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++)
        {
            if (dr == 0 && dc == 0) continue;
            int nr = row + dr, nc = col + dc;
            if (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols && _board[nr, nc] == 0)
                count++;
        }
        return count;
    }

    // ───────── Button handlers ─────────

    private async void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        var players = await ShowSetupDialog();
        if (players == null) return;

        _gameGeneration++;
        _players = players;
        _board = new int[_rows, _cols];
        _currentPlayerIndex = 0;
        _gameActive = true;
        BuildBoard();
        UpdateHeader();
        UpdateScores();

        if (_players[0].IsBot)
            ScheduleBotMove();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Game",
            SuggestedFileName = "game.save.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });
        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);

        await writer.WriteLineAsync($"{_rows} {_cols}");

        var values = new string[_rows * _cols];
        int idx = 0;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                values[idx++] = _board[r, c].ToString();
        await writer.WriteLineAsync(string.Join(" ", values));

        if (_players.Count > 0)
        {
            await writer.WriteLineAsync($"{_players.Count} {_currentPlayerIndex + 1}");
            foreach (var p in _players)
                await writer.WriteLineAsync($"{p.Name}|{p.Color}|{p.IsBot}|{p.IsEliminated}");
        }
    }

    private async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Game",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });
        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);

        var firstLine = await reader.ReadLineAsync();
        if (firstLine == null) return;
        var dims = firstLine.Trim().Split(' ');
        if (dims.Length < 2) return;
        if (!int.TryParse(dims[0], out int rows) || !int.TryParse(dims[1], out int cols)) return;

        var secondLine = await reader.ReadLineAsync();
        if (secondLine == null) return;
        var vals = secondLine.Trim().Split(' ');
        if (vals.Length < rows * cols) return;

        _rows = rows;
        _cols = cols;
        _board = new int[_rows, _cols];
        int idx = 0;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (int.TryParse(vals[idx++], out int val))
                    _board[r, c] = val;

        // Try reading extended player info
        _players = new List<Player>();
        var thirdLine = await reader.ReadLineAsync();
        if (thirdLine != null)
        {
            var parts = thirdLine.Trim().Split(' ');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out int numPlayers) &&
                int.TryParse(parts[1], out int currentIdx))
            {
                _currentPlayerIndex = currentIdx - 1;
                for (int i = 0; i < numPlayers; i++)
                {
                    var playerLine = await reader.ReadLineAsync();
                    if (playerLine == null) break;
                    var pParts = playerLine.Split('|');
                    if (pParts.Length < 4) break;
                    _players.Add(new Player
                    {
                        Number = i + 1,
                        Name = pParts[0],
                        Color = Color.Parse(pParts[1]),
                        IsBot = bool.Parse(pParts[2]),
                        IsEliminated = bool.Parse(pParts[3]),
                    });
                }
            }
        }

        // If no extended data, create default 2 players
        if (_players.Count == 0)
        {
            int maxPlayer = 0;
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    if (_board[r, c] > maxPlayer) maxPlayer = _board[r, c];

            int numP = Math.Max(2, maxPlayer);
            for (int i = 0; i < numP; i++)
            {
                _players.Add(new Player
                {
                    Number = i + 1,
                    Name = $"Player {i + 1}",
                    Color = AvailableColors[i % AvailableColors.Length].Color,
                    IsBot = false,
                });
            }
            int count1 = 0, count2 = 0;
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                {
                    if (_board[r, c] == 1) count1++;
                    else if (_board[r, c] == 2) count2++;
                }
            _currentPlayerIndex = count1 > count2 ? 1 : 0;
        }

        _gameGeneration++;
        _gameActive = true;
        BuildBoard();

        if (IsBoardFull())
        {
            EndGame(null);
            return;
        }

        // Make sure current player can move; find someone who can if not
        bool found = false;
        for (int i = 0; i < _players.Count; i++)
        {
            int pi = (_currentPlayerIndex + i) % _players.Count;
            if (!_players[pi].IsEliminated && HasLegalMoves(_players[pi].Number))
            {
                _currentPlayerIndex = pi;
                found = true;
                break;
            }
        }

        if (!found)
        {
            EndGame(null);
            return;
        }

        UpdateHeader();
        UpdateScores();
        if (_players[_currentPlayerIndex].IsBot)
            ScheduleBotMove();
    }

    private async void OnSizeClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Board Size",
            Width = 250,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var rowsBox = new TextBox { Text = _rows.ToString(), Margin = new Thickness(5) };
        var colsBox = new TextBox { Text = _cols.ToString(), Margin = new Thickness(5) };
        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0),
        };

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Rows:", Margin = new Thickness(5, 5, 5, 0) });
        panel.Children.Add(rowsBox);
        panel.Children.Add(new TextBlock { Text = "Columns:", Margin = new Thickness(5, 10, 5, 0) });
        panel.Children.Add(colsBox);
        panel.Children.Add(okButton);
        dialog.Content = panel;

        bool confirmed = false;
        okButton.Click += (_, _) => { confirmed = true; dialog.Close(); };
        await dialog.ShowDialog(this);

        if (confirmed &&
            int.TryParse(rowsBox.Text, out int newRows) &&
            int.TryParse(colsBox.Text, out int newCols) &&
            newRows >= 2 && newCols >= 2 && newRows <= 20 && newCols <= 20)
        {
            _rows = newRows;
            _cols = newCols;
            _board = new int[_rows, _cols];
            _gameGeneration++;
            _gameActive = false;
            BuildBoard();
            UpdateHeader();
            UpdateScores();
        }
    }

    // ───────── Setup dialog ─────────

    private async Task<List<Player>?> ShowSetupDialog()
    {
        var dialog = new Window
        {
            Title = "Game Setup",
            Width = 420,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        var mainPanel = new StackPanel { Margin = new Thickness(15), Spacing = 8 };

        // Player count selector
        var countPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        countPanel.Children.Add(new TextBlock
        {
            Text = "Number of players:",
            VerticalAlignment = VerticalAlignment.Center,
        });
        var countCombo = new ComboBox { Width = 60 };
        for (int i = 2; i <= 6; i++)
            countCombo.Items.Add(i);
        countCombo.SelectedIndex = 0;
        countPanel.Children.Add(countCombo);
        mainPanel.Children.Add(countPanel);

        // Player config rows
        var playersPanel = new StackPanel { Spacing = 5, Margin = new Thickness(0, 10, 0, 0) };
        var playerRows = new List<(TextBox Name, ComboBox Color, CheckBox Bot, StackPanel Row)>();

        for (int i = 0; i < 6; i++)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                IsVisible = i < 2,
            };

            row.Children.Add(new TextBlock
            {
                Text = $"{i + 1}.",
                Width = 20,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var nameBox = new TextBox
            {
                Text = $"Player {i + 1}",
                Width = 120,
            };
            row.Children.Add(nameBox);

            var colorCombo = new ComboBox { Width = 100 };
            foreach (var (name, _) in AvailableColors)
                colorCombo.Items.Add(name);
            colorCombo.SelectedIndex = i % AvailableColors.Length;
            row.Children.Add(colorCombo);

            var botCheck = new CheckBox
            {
                Content = "Bot",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
            };
            row.Children.Add(botCheck);

            playersPanel.Children.Add(row);
            playerRows.Add((nameBox, colorCombo, botCheck, row));
        }
        mainPanel.Children.Add(playersPanel);

        // Update visibility when count changes
        countCombo.SelectionChanged += (_, _) =>
        {
            if (countCombo.SelectedItem is int count)
            {
                for (int i = 0; i < 6; i++)
                    playerRows[i].Row.IsVisible = i < count;
            }
        };

        // OK / Cancel buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 15,
            Margin = new Thickness(0, 20, 0, 0),
        };
        var okButton = new Button { Content = "Start Game", Width = 100 };
        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        mainPanel.Children.Add(buttonPanel);

        dialog.Content = mainPanel;

        bool confirmed = false;
        okButton.Click += (_, _) => { confirmed = true; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);

        if (!confirmed) return null;

        int playerCount = countCombo.SelectedItem is int pc ? pc : 2;
        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            var (name, color, bot, _) = playerRows[i];
            int colorIdx = color.SelectedIndex >= 0 ? color.SelectedIndex : i;
            players.Add(new Player
            {
                Number = i + 1,
                Name = string.IsNullOrWhiteSpace(name.Text) ? $"Player {i + 1}" : name.Text,
                Color = AvailableColors[colorIdx % AvailableColors.Length].Color,
                IsBot = bot.IsChecked == true,
            });
        }
        return players;
    }

    // ───────── Leaderboard ─────────

    private void LoadLeaderboard()
    {
        _leaderboard = new List<LeaderboardEntry>();
        if (!File.Exists(LeaderboardPath)) return;

        try
        {
            foreach (var line in File.ReadAllLines(LeaderboardPath))
            {
                var parts = line.Split('|');
                if (parts.Length < 4) continue;
                _leaderboard.Add(new LeaderboardEntry
                {
                    WinnerName = parts[0],
                    BoardSize = parts[1],
                    PlayerCount = int.TryParse(parts[2], out int pc2) ? pc2 : 2,
                    Date = parts[3],
                });
            }
        }
        catch { /* ignore corrupt file */ }
    }

    private void SaveLeaderboard()
    {
        try
        {
            var lines = _leaderboard.Select(e =>
                $"{e.WinnerName}|{e.BoardSize}|{e.PlayerCount}|{e.Date}");
            File.WriteAllLines(LeaderboardPath, lines);
        }
        catch { /* ignore write errors */ }
    }

    private void AddLeaderboardEntry(string winnerName)
    {
        _leaderboard.Insert(0, new LeaderboardEntry
        {
            WinnerName = winnerName,
            BoardSize = $"{_rows}x{_cols}",
            PlayerCount = _players.Count,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        });

        if (_leaderboard.Count > 20)
            _leaderboard.RemoveRange(20, _leaderboard.Count - 20);

        SaveLeaderboard();
        UpdateLeaderboardUI();
    }

    private void UpdateLeaderboardUI()
    {
        LeaderboardPanel.Children.Clear();

        if (_leaderboard.Count == 0)
        {
            LeaderboardPanel.Children.Add(new TextBlock
            {
                Text = "No games played yet",
                Foreground = Brushes.Gray,
                FontSize = 12,
                Margin = new Thickness(0, 5),
            });
            return;
        }

        for (int i = 0; i < _leaderboard.Count; i++)
        {
            var entry = _leaderboard[i];
            LeaderboardPanel.Children.Add(new TextBlock
            {
                Text = $"{i + 1}. {entry.WinnerName} ({entry.BoardSize}, {entry.PlayerCount}p)",
                FontSize = 11,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 1),
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }
}
