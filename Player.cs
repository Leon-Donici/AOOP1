using Avalonia.Media;

namespace AvaloniaExercisesSolution;

public class Player
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public Color Color { get; set; }
    public bool IsBot { get; set; }
    public bool IsEliminated { get; set; }
}

public class LeaderboardEntry
{
    public string WinnerName { get; set; } = "";
    public string BoardSize { get; set; } = "";
    public int PlayerCount { get; set; }
    public string Date { get; set; } = "";
}
