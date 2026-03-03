# Territory Game - Complete Codebase Explanation

## 1. Project Architecture Overview

The project has 6 source files organized in a **code-behind pattern** (UI defined in AXAML, logic in matching .cs files):

```
Program.cs            --> Entry point, boots Avalonia
App.axaml / .cs       --> Application shell, creates the MainWindow
Player.cs             --> Data models (Player + LeaderboardEntry)
MainWindow.axaml      --> UI layout (what you see)
MainWindow.axaml.cs   --> All game logic (what happens when you click)
```

The flow is: `Program.Main()` --> `App` --> `MainWindow`.

---

## 2. Program.cs (Entry Point)

```
Line 12: Main() is the starting point of the entire application.
Line 13: StartWithClassicDesktopLifetime means "run as a normal desktop window app"
```

`[STAThread]` (line 11) tells Windows to use a Single-Threaded Apartment -- required for GUI apps because UI elements can only be accessed from one thread.

`BuildAvaloniaApp()` (line 16-20) configures:
- `Configure<App>()` -- use our `App` class as the root
- `UsePlatformDetect()` -- automatically detect Windows/Mac/Linux
- `WithInterFont()` -- include the Inter font family
- `LogToTrace()` -- send debug logs to the trace output

---

## 3. App.axaml + App.axaml.cs (Application Shell)

**App.axaml** -- Declares the application and picks the visual theme:
- Line 4: `RequestedThemeVariant="Default"` -- follows system dark/light mode
- Line 8: `<FluentTheme />` -- uses Microsoft's Fluent design style (rounded buttons, modern look)

**App.axaml.cs** -- Two lifecycle methods:
- `Initialize()` (line 9-12): Calls `AvaloniaXamlLoader.Load(this)` which parses the AXAML markup and creates the objects it describes. This is how the AXAML "becomes real."
- `OnFrameworkInitializationCompleted()` (line 14-22): Once Avalonia is ready, it creates `new MainWindow()` and assigns it as the app's main window. The `is` keyword checks that we're running as a desktop app (not mobile/web).

---

## 4. Player.cs (Data Models)

Two simple classes with **auto-properties** (`{ get; set; }`):

**Player** -- represents one player in the game:

| Property | Type | Purpose |
|---|---|---|
| `Number` | `int` | 1-based ID, matches what's stored in the board array (1, 2, 3...) |
| `Name` | `string` | Display name like "Alice" |
| `Color` | `Color` | Avalonia color used for painting their cells |
| `IsBot` | `bool` | If true, the AI plays for them |
| `IsEliminated` | `bool` | Set to true when they can't make any more moves |

**LeaderboardEntry** -- one row in the leaderboard history:

| Property | Purpose |
|---|---|
| `WinnerName` | Who won (or "Draw") |
| `BoardSize` | e.g., "6x6" |
| `PlayerCount` | How many players were in the game |
| `Date` | When the game ended |

The `= ""` on string properties is a **default value** -- avoids null warnings since the project has `<Nullable>enable</Nullable>`.

---

## 5. MainWindow.axaml (UI Layout)

This is the visual structure. The root is a **Grid** with 3 rows and 2 columns:

```
Line 9: RowDefinitions="Auto, *, Auto"  ColumnDefinitions="70*, 30*"
         -----  --  -----               ----    ----
         Header  |  Footer              Board   Right panel
              Board area                (70%)   (30%)
              (fills remaining space)
```

- `Auto` = size to fit content
- `*` = take all remaining space
- `70*` vs `30*` = a 70/30 proportional split

**Header** (lines 12-15): A `TextBlock` named `HeaderText`. The `x:Name="HeaderText"` is critical -- it generates a C# field so the code-behind can access it (e.g., `HeaderText.Text = "Blue to move"`). `Grid.ColumnSpan="2"` makes it stretch across both columns.

**Game Board** (lines 18-20): A `Border` (thick black outline) containing an empty `Grid` named `GameBoard`. This grid starts empty -- the cells are created dynamically in C# code by `BuildBoard()`.

**Right Panel** (lines 23-65): A `ScrollViewer` wrapping a `StackPanel` (vertical stack of elements):
- 4 Buttons with `Click="OnPlayClick"` etc. -- these are **event handlers**. When clicked, Avalonia calls the matching method in the .cs file.
- `ScoresPanel` -- empty StackPanel, filled dynamically with player scores
- `LeaderboardPanel` -- empty StackPanel, filled dynamically with past results

**Footer** (lines 68-71): Shows board dimensions like "6x6".

---

## 6. MainWindow.axaml.cs (All Game Logic)

This is the big file. Each section explained below:

### 6a. Fields (lines 17-40)

```csharp
private static readonly (string Name, Color Color)[] AvailableColors = { ... };
```
This is a **tuple array** -- each element holds a name and a color together. `static readonly` means it's shared across all instances and set once. These are the 8 color choices in the setup dialog.

```csharp
private int[,] _board = null!;
```
`int[,]` is a **2D array**. `_board[row, col]` stores 0 (empty), 1 (player 1's piece), 2 (player 2's piece), etc. The `null!` tells the compiler "I know this is null now but I'll set it in the constructor, don't warn me."

```csharp
private Border[,] _cells = null!;
```
Parallel 2D array storing the UI elements. When a player claims cell `[2,3]`, we update `_cells[2,3].Background` to their color.

```csharp
private int _gameGeneration;
```
Incremented every time a new game starts. Used by the bot to cancel pending moves from a previous game (explained in the bot section).

### 6b. Constructor (lines 42-52)

```csharp
InitializeComponent();    // Parses the AXAML, creates all the named elements
Width = 700; Height = 550;
_board = new int[_rows, _cols];  // 6x6 array, all zeros
_cells = new Border[_rows, _cols];
LoadLeaderboard();        // Read leaderboard.txt from disk
BuildBoard();             // Create the grid cells
UpdateLeaderboardUI();    // Show leaderboard entries in the panel
```

### 6c. BuildBoard() (lines 56-87) -- Dynamic Grid Creation

This is the most important UI method. Instead of hardcoding 36 borders in AXAML, we create them in code so the board size can change.

```csharp
GameBoard.RowDefinitions.Clear();   // Remove old rows
GameBoard.ColumnDefinitions.Clear();
GameBoard.Children.Clear();         // Remove old cells
```

Then it adds `_rows` row definitions and `_cols` column definitions, each with `GridLength.Star` (equal sizing).

The nested loop (lines 68-84) creates one `Border` per cell:
```csharp
int row = r, col = c;  // IMPORTANT: capture the loop variables
border.PointerPressed += (_, _) => OnCellClick(row, col);
```
This is a **lambda/closure**. Without `int row = r`, all lambdas would share the same `r` variable and by the time you click, `r` would be its final loop value. Capturing into `row` freezes the value.

`Grid.SetRow(border, r)` and `Grid.SetColumn(border, c)` are **attached properties** -- they tell the parent Grid which cell this border belongs to.

### 6d. CellBrush() (lines 89-94)

Takes a board value (0, 1, 2...) and returns the matching color brush. Uses LINQ's `FirstOrDefault` to find the player whose `Number` matches. Returns white for empty cells or if no players exist yet.

### 6e. UpdateHeader() (lines 96-107)

Changes the header text and color based on the current state. When `_gameActive` is false, shows "Press Play to start". Otherwise shows the current player's name in their color.

### 6f. UpdateScores() (lines 109-143)

Clears the ScoresPanel and rebuilds it. For each player, it:
1. Counts their cells by scanning the entire board (lines 116-119)
2. Creates a horizontal StackPanel with a colored square + text showing "Name: count"
3. Shows "(out)" for eliminated players, "[Bot]" for bots, and gray text for eliminated ones

### 6g. OnCellClick() (lines 147-154) -- Input Validation

Four guard clauses (early returns):
1. `!_gameActive` -- game hasn't started or is over
2. `_players[_currentPlayerIndex].IsBot` -- it's the bot's turn, ignore human clicks
3. `_board[row, col] != 0` -- cell already taken
4. `!IsValidMove(...)` -- not adjacent to player's territory

If all checks pass, calls `MakeMove()`.

### 6h. MakeMove() (lines 156-162)

Writes the player's number into `_board`, paints the cell, then calls `AfterMove()` to handle turn switching.

### 6i. AfterMove() (lines 164-199) -- Turn Switching and Elimination

This is the most complex method. After someone places a piece:

1. **Check if board is full** (line 166) --> draw
2. **Find the next player who can move** (lines 172-197):
   - Uses modular arithmetic `(next + 1) % _players.Count` to wrap around the player list
   - Skips eliminated players
   - If a player has legal moves, it's their turn -- update UI and trigger bot if needed
   - If a player has NO legal moves, **eliminate them** (`IsEliminated = true`)
   - After eliminating, check if only 1 player remains -- they win
3. If the loop exhausts all players without finding one who can move, it's a draw

### 6j. Game Rules (lines 219-271)

**IsValidMove(row, col, playerNum)** -- Three checks:
1. Cell must be empty (`_board[row, col] != 0`)
2. If player has no pieces yet (first move), any empty cell is valid
3. Otherwise, must be adjacent to one of their existing pieces

**IsAdjacentTo(row, col, playerNum)** -- Checks all 8 neighbors using a double loop with offsets `dr` and `dc` from -1 to +1:
```
(-1,-1) (-1,0) (-1,+1)
( 0,-1)  CELL  ( 0,+1)
(+1,-1) (+1,0) (+1,+1)
```
`if (dr == 0 && dc == 0) continue` skips the cell itself. The bounds check `nr >= 0 && nr < _rows && nc >= 0 && nc < _cols` prevents array-out-of-bounds at edges.

**HasLegalMoves(playerNum)** -- Scans every cell. If the player has no pieces yet, any empty cell counts. Otherwise, only empty cells adjacent to their territory count.

**IsBoardFull()** -- Returns false as soon as it finds any cell with value 0.

### 6k. Bot AI (lines 273-309)

**ScheduleBotMove()** -- `async void` because it's fire-and-forget (no one awaits it):

```csharp
int gen = _gameGeneration;
await Task.Delay(400);
if (gen != _gameGeneration || !_gameActive) return;
```
The **generation check** is the key safety mechanism. If the user clicks Play during the 400ms delay, `_gameGeneration` increments and the old bot's move is discarded. Without this, a bot from a previous game could make a move on the new board.

The AI strategy (lines 284-294):
1. Find all valid moves
2. Score each move by `CountEmptyNeighbors` -- how many empty cells surround that position
3. Pick the highest-scoring move (greedy -- tries to expand into open areas)
4. `ThenBy(_ => rng.Next())` adds randomness to break ties so the bot isn't predictable

**CountEmptyNeighbors()** -- Same 8-neighbor loop as `IsAdjacentTo`, but counts empty cells instead of player cells.

### 6l. OnPlayClick() (lines 313-329)

Opens the setup dialog and waits for it (`await`). If the user cancels, `players` is null and we return. Otherwise:
- `_gameGeneration++` -- invalidates any pending bot moves
- Resets the board to all zeros
- Sets player 0 as current
- If player 0 is a bot, triggers `ScheduleBotMove()`

### 6m. OnSaveClick() (lines 331-363) -- Save File

Uses Avalonia's `StorageProvider.SaveFilePickerAsync()` -- a platform-native "Save As" dialog.

The save format has two parts:
- **Line 1**: `height width` (e.g., `6 6`)
- **Line 2**: all board values in row-major order (e.g., `0 0 1 2 0 0 ...`)
- **Lines 3+** (extended): player count, current player index, then one line per player with `Name|Color|IsBot|IsEliminated` separated by `|`

`stream.SetLength(0)` truncates the file if it already existed and was longer than our new data.

### 6n. OnLoadClick() (lines 365-492) -- Load File

This is the longest method. It:
1. Opens a file picker dialog
2. Reads line 1 to get dimensions
3. Reads line 2 to fill the board array
4. **Tries** to read line 3+ for extended player info (names, colors, bots)
5. If no extended data exists (basic 2-line file), creates default players and infers whose turn it is by counting pieces: if player 1 has more pieces than player 2, it's player 2's turn, and vice versa
6. After loading, verifies the game state is playable -- if the current player can't move, scans for someone who can

`Color.Parse(pParts[1])` parses a hex color string like `"#FF0000FF"` back into an Avalonia `Color` object.

### 6o. OnSizeClick() (lines 494-541) -- Size Dialog

Creates a popup `Window` entirely in C# (no AXAML file needed). Uses `ShowDialog(this)` which makes it **modal** -- the main window is blocked until this dialog closes.

The `bool confirmed` pattern: set to false initially, set to true when OK is clicked. After the dialog closes, check the flag to know if the user confirmed or just closed the window.

Validates input: rows and cols must be between 2 and 20.

### 6p. ShowSetupDialog() (lines 545-667) -- Player Setup

The most UI-heavy method. Builds a configuration window in code:
- ComboBox for player count (2-6)
- 6 player rows, each with name TextBox, color ComboBox, and Bot checkbox
- Only the first N rows are visible (controlled by `IsVisible`)

```csharp
countCombo.SelectionChanged += (_, _) => { ... }
```
When the player count changes, this **event handler** shows/hides the appropriate rows.

The tuple list `playerRows` stores references to each row's controls so we can read their values when OK is clicked:
```csharp
var (name, color, bot, _) = playerRows[i];  // tuple deconstruction
```

Returns `null` if cancelled, or a `List<Player>` if confirmed.

### 6q. Leaderboard (lines 669-750)

**LoadLeaderboard()** -- Reads `leaderboard.txt` line by line. Each line is `WinnerName|BoardSize|PlayerCount|Date`, split by `|`. Wrapped in `try/catch` to handle corrupt files gracefully.

**SaveLeaderboard()** -- Converts each entry back to a `|`-separated string and writes all lines. Uses LINQ's `Select()` to transform each entry.

**AddLeaderboardEntry()** -- Inserts at position 0 (newest first), caps at 20 entries, saves to disk, and refreshes the UI.

**UpdateLeaderboardUI()** -- Clears and rebuilds the panel with `TextBlock` elements showing numbered results.

---

## 7. Key Concepts Your Professor Might Ask About

### "What is `partial` in `partial class MainWindow`?"
The class is split across two files -- the AXAML-generated code (hidden, auto-generated by the compiler) and your `.cs` file. `partial` lets them merge into one class.

### "What is `async/await`?"
Used for operations that take time (file dialogs, delays). `await` pauses the method without freezing the UI. The method resumes when the operation completes. All async methods return `Task` (or `void` for event handlers).

### "Why `async void` for button handlers but `async Task` for the setup dialog?"
Event handlers must be `void` (Avalonia requires it). But `ShowSetupDialog` is called from code, not from AXAML, so it can return `Task<List<Player>?>` and be properly awaited.

### "How does the board data structure work?"
`int[,]` is a 2D array. `_board[r,c] = 0` means empty, `1` means player 1, etc. The parallel `_cells[r,c]` array holds the UI `Border` objects so we can change their color when a move is made.

### "How does the bot know when to move?"
After each move, `AfterMove()` checks if the next player is a bot. If so, it calls `ScheduleBotMove()`, which waits 400ms then picks the best move. The chain continues: `MakeMove` -> `AfterMove` -> `ScheduleBotMove` -> `MakeMove` -> ... until it's a human's turn or the game ends.

### "How is the save file backward-compatible?"
A basic file has exactly 2 lines (dimensions + board). The load code tries to read line 3 -- if it's not there, it falls back to creating 2 default players. This means you can load files from the basic spec and from the extended format.

### "What design pattern is used?"
Code-behind pattern. The UI is declared in AXAML, and the logic lives in the matching `.cs` file. Elements are connected via `x:Name` (generates C# fields) and `Click="MethodName"` (wires events to handlers).

### "Why create the grid dynamically instead of in AXAML?"
Because the board size can change (via the Size button or loading a file). A static AXAML grid would be fixed at one size. Dynamic creation with `BuildBoard()` lets us rebuild the grid for any dimensions.

### "What is the lambda capture issue on line 78?"
```csharp
int row = r, col = c;
border.PointerPressed += (_, _) => OnCellClick(row, col);
```
Without the local copies, all closures would capture the loop variable `r` by reference. By the time a cell is clicked, `r` would equal `_rows` (its final value after the loop ends). Creating `int row = r` captures the current value.

### "How does elimination work with more than 2 players?"
When it becomes a player's turn and they have no legal moves (all adjacent cells occupied), they are marked `IsEliminated = true`. The game continues with the remaining players. `AfterMove()` loops through players using modular arithmetic, skipping eliminated ones, until it finds one who can move or determines only one player remains.

---

## 8. Save File Format

### Basic format (matches assignment spec):
```
4 3
0 0 2 0 0 1 2 0 1 1 2 0
```
- Line 1: `height width` (rows columns)
- Line 2: board values in row-major order (row 0 left-to-right, then row 1, etc.)
- Values: 0 = empty, 1 = player 1, 2 = player 2

### Extended format (adds player info):
```
4 3
0 0 2 0 0 1 2 0 1 1 2 0
2 1
Alice|#FF0000FF|False|False
Bob|#FFFF0000|True|False
```
- Line 3: `num_players current_player_index(1-based)`
- Lines 4+: `Name|Color|IsBot|IsEliminated` per player

### Leaderboard file (leaderboard.txt):
```
Alice|6x6|2|2026-03-03 14:30
Draw|8x8|3|2026-03-03 14:15
```
- Each line: `WinnerName|BoardSize|PlayerCount|Date`
- Newest entries first, max 20 entries

---

## 9. Game Flow Diagram

```
User clicks PLAY
    |
    v
ShowSetupDialog() opens
    |
    v
User configures players, clicks "Start Game"
    |
    v
Board reset, BuildBoard(), player 0 starts
    |
    v
+---> Is current player a bot?
|         |
|     YES: ScheduleBotMove() -- wait 400ms, pick best move, call MakeMove()
|     NO:  Wait for human click
|         |
|         v
|     OnCellClick(row, col)
|         |
|     Validates: game active? human turn? cell empty? valid move?
|         |
|         v
|     MakeMove(row, col) -- write to _board, paint cell
|         |
|         v
|     AfterMove()
|         |
|     Board full? --> EndGame(null) = DRAW
|         |
|     Find next non-eliminated player with legal moves
|         |
|     No one can move? --> EndGame(last remaining) = WIN
|         |
|     Found someone --> _currentPlayerIndex = that player
|         |
+----<----+
```

---

## 10. Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| UI Framework | Avalonia | 11.2.1 |
| Theme | FluentTheme | 11.2.1 |
| Runtime | .NET | 9.0 |
| Language | C# | 13 (latest) |
| Pattern | Code-behind | - |
| Output | WinExe (desktop) | - |
