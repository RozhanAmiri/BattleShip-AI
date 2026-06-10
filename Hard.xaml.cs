using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BattleShip
{
    public partial class Hard : Page
    {
        private List<Button> PreviewCells = new();
        private const int BoardSize = 10;
        private int DragShipLength = 0; //the ship currently being dragged
        private string DragShipName = ""; // the name of the ship currently being dragged (for display purposes)
        private bool ShipVertical = false; // false = horizontal, true = vertical
        private List<int> PlayerShipsToPlace = new() { 5, 3, 3, 2, 2, 1, 1, 1, 1 }; //tracks which ships are still not placed

        //---------------------------------------------------------------------------------------
        private enum ShotState
        {
            Unknown,
            Miss,
            Hit,
            Sunk
        }
        //---------------------------------------------------------------------------------------
        private sealed class Ship
        {
            public string Name { get; }
            public int Length { get; }
            public HashSet<int> Cells { get; }

            public Ship(string name, int length, IEnumerable<int> cells)
            {
                Name = name;
                Length = length;
                Cells = new HashSet<int>(cells);
            }
        }
        //---------------------------------------------------------------------------------------
        private List<Button> PlayerPositionButtons = new();
        private List<Button> EnemyPositionButtons = new();

        private readonly Random rand = new Random();
        private int PlayerScore;
        private int EnemyScore;
        private bool shipsPlaced = false; // checking if player has placed their ships before attacking

        // Hard-mode state
        private ShotState[,] EnemyShots = new ShotState[BoardSize, BoardSize]; // what the enemy knows about the player grid
        private int[,] PlayerShotHeatmap = new int[BoardSize, BoardSize];
        private List<int> RemainingPlayerShipLengths = new() { 5, 3, 3, 2, 2, 1, 1, 1, 1 };
        private List<Ship> EnemyFleet = new();
        private List<Ship> PlayerFleet = new();

        //---------------------------------------------------------------------------------------
        //Constructor
        public Hard()
        {
            InitializeComponent(); //WPF initialization
            InitializeGrids();
            FadeIn();              //Animation
            RestartGame();         //Restart the game for every new round
        }
        //---------------------------------------------------------------------------------------
        //Animations and UI updates
        private void ClearPreview()
        {
            foreach (var btn in PreviewCells)
            {
                if ((string?)btn.Tag != "PlayerShip")
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(37, 76, 84));
                }
            }
            PreviewCells.Clear();
        }
        private void FadeIn()
        {
            DoubleAnimation fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            this.BeginAnimation(OpacityProperty, fadeInAnimation);
        }
        private void AnimateStatusText()
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

            // Start with fade-out, then fade-in
            fadeOut.Completed += (s, e) =>
            {
                GameStatusText.BeginAnimation(OpacityProperty, fadeIn);
            };

            GameStatusText.BeginAnimation(OpacityProperty, fadeOut);
        }
        private void ShakeAndNavigate(Button button, Page targetPage)
        {
            // Ensure transform exists
            if (button.RenderTransform is not TranslateTransform)
            {
                button.RenderTransform = new TranslateTransform();
            }

            var transform = (TranslateTransform)button.RenderTransform;

            DoubleAnimation shakeAnimation = new DoubleAnimation
            {
                From = -15,
                To = 15,
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2),
                Duration = TimeSpan.FromSeconds(0.3),
            };

            shakeAnimation.Completed += (s, e) =>
            {
                transform.X = 0; // reset position

                if (NavigationService != null)
                {
                    NavigationService.Navigate(targetPage);
                }
            };

            transform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
        }
        private void ShakeStatusText()
        {
            DoubleAnimationUsingKeyFrames shake = new DoubleAnimationUsingKeyFrames();
            shake.Duration = TimeSpan.FromMilliseconds(400);

            // Move left/right quickly
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(-10, KeyTime.FromPercent(0.1)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(10, KeyTime.FromPercent(0.2)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(-6, KeyTime.FromPercent(0.3)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(6, KeyTime.FromPercent(0.4)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(-2, KeyTime.FromPercent(0.5)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(2, KeyTime.FromPercent(0.6)));
            shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

            StatusTextTransform.BeginAnimation(TranslateTransform.XProperty, shake);
        }
        private void InitializeGrids()
        {
            PlayerGrid.Children.Clear();
            EnemyGrid.Children.Clear();

            PlayerPositionButtons.Clear();
            EnemyPositionButtons.Clear();

            for (int i = 0; i < BoardSize * BoardSize; i++)
            {
                // PLAYER
                Button playerBtn = new Button
                {
                    Width = 48,
                    Height = 48,
                    Margin = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(37, 76, 84)),
                    Opacity = 0.7,
                    AllowDrop = true,
                    Style = (Style)FindResource("GridButtonStyle"),
                };

                playerBtn.Drop += CellDrop;
                playerBtn.DragOver += CellDragOver;

                PlayerGrid.Children.Add(playerBtn);
                PlayerPositionButtons.Add(playerBtn);

                // ENEMY
                Button enemyBtn = new Button
                {
                    Width = 48,
                    Height = 48,
                    Margin = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(37, 76, 84)),
                    Opacity = 0.7,
                    Style = (Style)FindResource("GridButtonStyle")
                };

                enemyBtn.Click += AttackClickEvent;

                EnemyGrid.Children.Add(enemyBtn);
                EnemyPositionButtons.Add(enemyBtn);
            }
        }
        private void UpdateStatus(string message, Brush color, bool shake = false)
        {
            GameStatusText.Text = message;
            GameStatusText.Foreground = color;
            AnimateStatusText();

            if (shake)
                ShakeStatusText();
        }
        private string GetShipImage(string shipName, int partIndex)
        {
            if (shipName.StartsWith("Carrier"))
                return $"CAR{partIndex + 1}.png";

            if (shipName.StartsWith("Cruiser"))
                return $"CRU{partIndex + 1}.png";

            if (shipName.StartsWith("Submarine"))
                return $"SUB{partIndex + 1}.png";

            if (shipName.StartsWith("Battleship"))
                return $"BS{partIndex + 1}.png";

            if (shipName.StartsWith("Destroyer"))
                return "destroyer.png";

            return "destroyer.png";
        }
        private void UpdateShipButtons()
        {
            if (!PlayerShipsToPlace.Contains(5))
                CarrierBtn.Visibility = Visibility.Collapsed;

            if (!PlayerShipsToPlace.Contains(3))
            {
                // You have TWO ships of length 3
                int count = PlayerShipsToPlace.Count(x => x == 3);
                if (count == 0)
                {
                    CruiserBtn.Visibility = Visibility.Collapsed;
                    SubmarineBtn.Visibility = Visibility.Collapsed;
                }
            }

            if (!PlayerShipsToPlace.Contains(2))
                BattleshipBtn.Visibility = Visibility.Collapsed;

            if (!PlayerShipsToPlace.Contains(1))
                DestroyerBtn.Visibility = Visibility.Collapsed;
        }
        private void UpdateShipUI()
        {
            CarrierCount.Text = $"x{PlayerShipsToPlace.Count(x => x == 5)}";

            int count3 = PlayerShipsToPlace.Count(x => x == 3);
            CruiserCount.Text = $"x{count3}";
            SubmarineCount.Text = $"x{count3}";

            BattleshipCount.Text = $"x{PlayerShipsToPlace.Count(x => x == 2)}";
            DestroyerCount.Text = $"x{PlayerShipsToPlace.Count(x => x == 1)}";
        }
        //---------------------------------------------------------------------------------------
        //Game logic methods
        private void RestartGame()
        {
            // Reset ship buttons
            CarrierBtn.Visibility = Visibility.Visible;
            CruiserBtn.Visibility = Visibility.Visible;
            SubmarineBtn.Visibility = Visibility.Visible;
            BattleshipBtn.Visibility = Visibility.Visible;
            DestroyerBtn.Visibility = Visibility.Visible;
            InitializeGrids();
            // Reset enemy grid
            foreach (var btn in EnemyPositionButtons)
            {
                btn.IsEnabled = true;
                btn.Tag = null;
                btn.Content = null;
            }

            // Reset player grid
            foreach (var btn in PlayerPositionButtons)
            {
                btn.IsEnabled = true;
                btn.Tag = null;
                btn.Content = null;
            }

            //Reset AI memory
            EnemyShots = new ShotState[BoardSize, BoardSize];

            //CRITICAL: update this (you were missing it)
            RemainingPlayerShipLengths = new List<int> {5, 3, 3, 2, 2, 1, 1, 1, 1};

            // Reset fleets
            EnemyFleet.Clear();
            PlayerFleet.Clear();

            // Place enemy ships
            PlaceEnemyFleet();

            //Reset player drag & drop system
            PlayerShipsToPlace = new List<int> {5, 3, 3, 2, 2, 1, 1, 1, 1};

            DragShipLength = 0;
            DragShipName = "";
            ShipVertical = false;

            // Reset scores
            PlayerScore = 0;
            EnemyScore = 0;
            shipsPlaced = false;

            txtPlayerScore.Content = PlayerScore.ToString();
            txtEnemyScore.Content = EnemyScore.ToString();

            UpdateStatus("⚓ Drag and place your ships!", Brushes.White);
            UpdateShipUI();
            UpdateShipButtons();
        }

        //---------------------------------------------------------------------------------------
        //Ship placement by the player on player grid by dragging and dropping ships from the side panel to the grid
        private void ShipDragStart(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (sender is not Button shipButton)
                return;

            if (shipButton.Tag == null)
                return;

            string data = shipButton.Tag.ToString()!; // example: "Carrier:5"
            string[] parts = data.Split(':');

            DragShipName = parts[0];
            DragShipLength = int.Parse(parts[1]);

            DragDrop.DoDragDrop(shipButton, data, DragDropEffects.Move);
        }
        private void RotateShipClick(object sender, RoutedEventArgs e)
        {
            ShipVertical = !ShipVertical;

            string direction = ShipVertical ? "Vertical" : "Horizontal";
            UpdateStatus($"🔄 Ship rotation: {direction}", Brushes.White);
        }
        private bool CanPlacePlayerShip(int r, int c, int length, bool vertical)
        {
            for (int k = 0; k < length; k++)
            {
                int rr = vertical ? r + k : r;
                int cc = vertical ? c : c + k;

                if (!InBounds(rr, cc))
                    return false;

                int idx = RcToIndex(rr, cc);

                if (PlayerPositionButtons[idx].Tag != null)
                    return false;
            }

            return true;
        }
        private void CellDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
                return;

            if (sender is not Button button)
                return;

            string data = e.Data.GetData(DataFormats.StringFormat) as string ?? "";
            string[] parts = data.Split(':');

            if (parts.Length != 2)
                return;

            string shipName = parts[0];

            //SAFE PARSE (prevents crash)
            if (!int.TryParse(parts[1], out int shipLength))
                return;

            int index = PlayerPositionButtons.IndexOf(button);
            if (index < 0)
                return;

            //EXTRA SAFETY (prevents out-of-range crash)
            if (index >= PlayerPositionButtons.Count)
                return;

            var (r, c) = IndexToRc(index);

            // Prevent placing same ship when none left
            if (!PlayerShipsToPlace.Contains(shipLength))
            {
                UpdateStatus("⚠️ That ship is already placed!", Brushes.Orange, shake: true);
                return;
            }

            //Prevent invalid placement
            if (!CanPlacePlayerShip(r, c, shipLength, ShipVertical))
            {
                UpdateStatus("❌ Cannot place ship there!", Brushes.Red, shake: true);
                return;
            }

            // Place the ship
            PlacePlayerShip(shipName, shipLength, r, c, ShipVertical);

            // Remove one instance of this ship length
            PlayerShipsToPlace.Remove(shipLength);

            UpdateShipUI();

            // Update UI (hide used ships)
            UpdateShipButtons();

            // Reset drag state
            DragShipLength = 0;
            DragShipName = "";

            // Check if all ships placed
            if (PlayerShipsToPlace.Count == 0)
            {
                shipsPlaced = true;
                UpdateStatus("✅ All ships placed! Attack the enemy!", Brushes.LimeGreen);
            }
            else
            {
                UpdateStatus($"⚓ Ships left: {string.Join(", ", PlayerShipsToPlace)}", Brushes.White);
            }
            ClearPreview();
        }
        private void PlacePlayerShip(string name, int length, int r, int c, bool vertical)
        {
            var cells = new List<int>(); //track all ship tiles

            for (int k = 0; k < length; k++)
            {
                int rr = vertical ? r + k : r;
                int cc = vertical ? c : c + k;

                int idx = RcToIndex(rr, cc);
                var btn = PlayerPositionButtons[idx];

                btn.Tag = "PlayerShip";
                btn.IsEnabled = true;
                btn.AllowDrop = false;

                //Track this tile
                cells.Add(idx);

                //Get correct split image
                string img = GetShipImage(name, k);

                var image = new Image
                {
                    Source = new BitmapImage(new Uri($"pack://application:,,,/BattleShip;component/{img}")),
                    Stretch = Stretch.Fill,

                    //Rotate if vertical
                    RenderTransform = vertical ? new RotateTransform(90) : null,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                btn.Content = image;
            }

            //ADD SHIP TO PLAYER FLEET (CRITICAL)
            PlayerFleet.Add(new Ship(name, length, cells));

            Debug.WriteLine($"Placed {name} with {length} tiles (tracked in player fleet)");
        }
        private void CellDragOver(object sender, DragEventArgs e)
        {
            ClearPreview();

            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if (sender is not Button button)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            int index = PlayerPositionButtons.IndexOf(button);
            if (index < 0) return;

            var (r, c) = IndexToRc(index);

            bool canPlace = CanPlacePlayerShip(r, c, DragShipLength, ShipVertical);

            for (int k = 0; k < DragShipLength; k++)
            {
                int rr = ShipVertical ? r + k : r;
                int cc = ShipVertical ? c : c + k;

                if (!InBounds(rr, cc)) continue;

                int idx = RcToIndex(rr, cc);
                var btn = PlayerPositionButtons[idx];

                if ((string?)btn.Tag == "PlayerShip") continue;

                btn.Background = canPlace
                    ? Brushes.DarkSeaGreen   // valid
                    : Brushes.IndianRed;     // invalid

                PreviewCells.Add(btn);
            }

            e.Effects = canPlace ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }        
        //---------------------------------------------------------------------------------------
        //Attacking enemy board by the player on enemy grid by clicking on the grid
        private void AttackClickEvent(object sender, RoutedEventArgs e)
        {
            // checking ship placement flag
            if (!shipsPlaced)
            {
                UpdateStatus("⚠️ Place all your ships first!", Brushes.Red, shake: true);
                return;
            }

            var button = (Button)sender;

            // Disable the button after attack so it cannot be clicked again
            button.IsEnabled = false;

            int index = EnemyPositionButtons.IndexOf(button);
            var (r, c) = IndexToRc(index);
            PlayerShotHeatmap[r, c]++;

            // Check if this button had an enemy ship
            if ((string?)button.Tag == "EnemyShip")
            {
                // Show hit image
                button.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/hit.jpg")),
                    Stretch = Stretch.Fill
                };

                Debug.WriteLine("HIT! Enemy ship at: " + button.Name);
                UpdateStatus("💥 Player Hit!", Brushes.LimeGreen);

                //check if ship is fully destroyed
                CheckEnemyShipSunk(index);
            }
            else
            {
                // Show miss image
                button.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/miss.jpg")),
                    Stretch = Stretch.Fill
                };

                Debug.WriteLine("MISS at: " + button.Name);
                UpdateStatus("Player Missed!", Brushes.LightBlue);
            }

            EnemyAttackEvent();
            CheckGameOver();
        }
        private void CheckEnemyShipSunk(int hitIndex)
        {
            foreach (var ship in EnemyFleet.ToList())
            {
                if (!ship.Cells.Contains(hitIndex)) continue;

                bool sunk = ship.Cells.All(idx =>
                {
                    var btn = EnemyPositionButtons[idx];
                    return btn.Content is Image img &&
                           img.Source.ToString().Contains("hit");
                });

                if (sunk)
                {
                    PlayerScore++;
                    txtPlayerScore.Content = PlayerScore.ToString();

                    UpdateStatus($"🔥 You sunk enemy {ship.Name}!", Brushes.LimeGreen);

                    EnemyFleet.Remove(ship);
                    break;
                }
            }
        }
        //---------------------------------------------------------------------------------------
        // Helper methods for coordinate/index conversions and bounds checking
        private static (int r, int c) IndexToRc(int index) => (index / BoardSize, index % BoardSize);
        private static int RcToIndex(int r, int c) => (r * BoardSize) + c;
        private static bool InBounds(int r, int c) => r >= 0 && r < BoardSize && c >= 0 && c < BoardSize;
        //---------------------------------------------------------------------------------------
        // Enemy fleet placement with improved logic for harder difficulty
        private void PlaceEnemyFleet()
        {
            foreach (var btn in EnemyPositionButtons)
            {
                btn.Tag = null;
                btn.Content = null;
            }

            EnemyFleet.Clear();

            // Place ALL ships using smart placement (no adjacency)
            PlaceShipRandom("Carrier", 5);

            PlaceShipRandom("Cruiser", 3);
            PlaceShipRandom("Submarine", 3);

            PlaceShipRandom("Battleship1", 2);
            PlaceShipRandom("Battleship2", 2);

            PlaceShipRandom("Destroyer1", 1);
            PlaceShipRandom("Destroyer2", 1);
            PlaceShipRandom("Destroyer3", 1);
            PlaceShipRandom("Destroyer4", 1);
        }
        private void PlaceShipRandom(string name, int length)
        {
            int tries = 0;

            while (tries++ < 8000)
            {
                bool vertical = rand.Next(2) == 0;

                // 🔥 bias placement away from center
                int r = rand.Next(BoardSize);
                int c = rand.Next(BoardSize);

                // occasionally force edges
                if (rand.Next(100) < 30)
                {
                    int[] edges = { 0, 1, 8, 9 };
                    r = edges[rand.Next(edges.Length)];
                    c = rand.Next(BoardSize);
                }

                if (TryPlaceShipFromStart(name, length, r, c, vertical))
                    return;
            }
        }
        private bool HasAdjacentShip(int r, int c)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    int nr = r + dr;
                    int nc = c + dc;

                    if (!InBounds(nr, nc)) continue;

                    int idx = RcToIndex(nr, nc);

                    if (EnemyPositionButtons[idx].Tag != null)
                        return true;
                }
            }

            return false;
        }
        private bool TryPlaceShipFromStart(string name, int length, int r0, int c0, bool vertical)
        {
            foreach (int sign in new[] { 1, -1 })
            {
                var cells = new List<int>();
                bool ok = true;
                int score = 0;

                for (int k = 0; k < length; k++)
                {
                    int r = r0 + (vertical ? sign * k : 0);
                    int c = c0 + (vertical ? 0 : sign * k);

                    if (!InBounds(r, c))
                    {
                        ok = false;
                        break;
                    }

                    int idx = RcToIndex(r, c);

                    if (EnemyPositionButtons[idx].Tag != null)
                    {
                        ok = false;
                        break;
                    }

                    if (HasAdjacentShip(r, c))
                    {
                        ok = false;
                        break;
                    }

                    // ANTI-HEATMAP: avoid player hotspots
                    score += PlayerShotHeatmap[r, c];

                    // CENTER PENALTY (players love center)
                    if (r >= 3 && r <= 6 && c >= 3 && c <= 6)
                        score += 3;

                    // EDGE BONUS (harder to detect)
                    if (r == 0 || r == 9 || c == 0 || c == 9)
                        score -= 2;

                    cells.Add(idx);
                }

                if (!ok) continue;

                // FINAL DECISION (reject bad placements)
                if (score > rand.Next(15)) continue;

                CommitEnemyShip(name, length, cells);
                return true;
            }

            return false;
        }
        //---------------------------------------------------------------------------------------
        // Enemy attack logic with probability density function (PDF) for harder difficulty
        private void CommitEnemyShip(string name, int length, List<int> cells)
        {
            // Save positions in _enemyFleet for real hit detection
            EnemyFleet.Add(new Ship(name, length, cells));

            foreach (int idx in cells)
            {
                EnemyPositionButtons[idx].Tag = "EnemyShip";
            }

            Debug.WriteLine($"Enemy placed {name} ({length}) at: {string.Join(",", cells)}");
        }
        private void EnemyAttackEvent()
        {
            int targetIndex = ChooseEnemyTargetByPDF();
            if (targetIndex < 0 || targetIndex >= PlayerPositionButtons.Count)
                return;

            var (r, c) = IndexToRc(targetIndex);
            Button targetButton = PlayerPositionButtons[targetIndex];

            //ensure valid target
            int tries = 0;
            while (EnemyShots[r, c] != ShotState.Unknown && tries < 100)
            {
                targetIndex = ChooseEnemyTargetByPDF();

                if (targetIndex < 0 || targetIndex >= PlayerPositionButtons.Count)
                    return;

                (r, c) = IndexToRc(targetIndex);
                targetButton = PlayerPositionButtons[targetIndex];
                tries++;
            }

            if (EnemyShots[r, c] != ShotState.Unknown)
                return;

            targetButton.IsEnabled = false; // safety

            targetButton.IsEnabled = false;

            bool isHit = (string?)targetButton.Tag == "PlayerShip";

            EnemyShots[r, c] = isHit ? ShotState.Hit : ShotState.Miss;

            targetButton.Content = new Image
            {
                Source = new BitmapImage(new Uri(isHit
                    ? "pack://application:,,,/BattleShip;component/hit.jpg"
                    : "pack://application:,,,/BattleShip;component/miss.jpg")),
                Stretch = Stretch.Fill
            };

            if (isHit)
            {
                Debug.WriteLine("Enemy HIT at: " + targetButton.Name);
                UpdateStatus("💥 Enemy Hit!", Brushes.Red);
                CheckPlayerShipSunk(targetIndex);
            }
            else
            {
                Debug.WriteLine("Enemy Miss at: " + targetButton.Name);
                UpdateStatus("Enemy Miss!", Brushes.LightBlue);
            }

            CheckGameOver();
        }
        private void CheckPlayerShipSunk(int hitIndex)
        {
            foreach (var ship in PlayerFleet.ToList())
            {
                if (!ship.Cells.Contains(hitIndex))
                    continue;

                bool sunk = ship.Cells.All(idx =>
                {
                    var (r, c) = IndexToRc(idx);
                    return EnemyShots[r, c] == ShotState.Hit;
                });

                if (sunk)
                {
                    EnemyScore++;
                    txtEnemyScore.Content = EnemyScore.ToString();

                    UpdateStatus($"💀 Enemy sunk your {ship.Name}!", Brushes.Red);

                    // mark this destroyed ship as sunk, not active hit
                    foreach (int idx in ship.Cells)
                    {
                        var (r, c) = IndexToRc(idx);
                        EnemyShots[r, c] = ShotState.Sunk;
                    }

                    RemainingPlayerShipLengths.Remove(ship.Length);
                    PlayerFleet.Remove(ship);

                    break;
                }
            }
        }
        private int ChooseEnemyTargetByPDF()
        {
            int[,] prob = new int[BoardSize, BoardSize];

            // 1) Base probability from all legal placements of all remaining ships
            foreach (int shipLen in RemainingPlayerShipLengths)
            {
                // horizontal
                for (int r = 0; r < BoardSize; r++)
                {
                    for (int c0 = 0; c0 <= BoardSize - shipLen; c0++)
                    {
                        int HitsCovered = 0;
                        bool ok = true;
                        for (int k = 0; k < shipLen; k++)
                        {
                            var s = EnemyShots[r, c0 + k];
                            if (s == ShotState.Miss || s == ShotState.Sunk)
                            {
                                ok = false;
                                break;
                            }

                            if (s == ShotState.Hit)
                                HitsCovered++;
                        }
                        if (!ok) continue;

                        int weight = 1 + (HitsCovered * 6); // bias toward continuing known hits
                        for (int k = 0; k < shipLen; k++)
                        {
                            if (EnemyShots[r, c0 + k] == ShotState.Unknown)
                                prob[r, c0 + k] += weight;
                        }
                    }
                }

                // vertical
                for (int c = 0; c < BoardSize; c++)
                {
                    for (int r0 = 0; r0 <= BoardSize - shipLen; r0++)
                    {
                        int HitsCovered = 0;
                        bool ok = true;
                        for (int k = 0; k < shipLen; k++)
                        {
                            var s = EnemyShots[r0 + k, c];
                            if (s == ShotState.Miss || s == ShotState.Sunk)
                            {
                                ok = false;
                                break;
                            }

                            if (s == ShotState.Hit)
                                HitsCovered++;
                        }
                        if (!ok) continue;

                        int weight = 1 + (HitsCovered * 6);
                        for (int k = 0; k < shipLen; k++)
                        {
                            if (EnemyShots[r0 + k, c] == ShotState.Unknown)
                                prob[r0 + k, c] += weight;
                        }
                    }
                }
            }

            // 2) Local boost: if we have any hits, favor shooting adjacent cells
            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    if (EnemyShots[r, c] != ShotState.Hit) continue;
                    foreach (var (dr, dc) in new (int dr, int dc)[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int nr = r + dr, nc = c + dc;
                        if (!InBounds(nr, nc)) continue;
                        if (EnemyShots[nr, nc] == ShotState.Unknown)
                            prob[nr, nc] += 12;
                    }
                }
            }

            // 3) Pick the highest-probability unknown cell (tie-break randomly)
            int best = -1;
            int BestScore = -1;
            var ties = new List<int>();

            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    if (EnemyShots[r, c] != ShotState.Unknown) continue;
                    int score = prob[r, c];
                    int idx = RcToIndex(r, c);

                    if (score > BestScore)
                    {
                        BestScore = score;
                        ties.Clear();
                        ties.Add(idx);
                        best = idx;
                    }
                    else if (score == BestScore)
                    {
                        ties.Add(idx);
                    }
                }
            }

            if (ties.Count > 0)
                return ties[rand.Next(ties.Count)];

            // Extreme fallback: pick any untargeted button
            var unknown = new List<int>();
            for (int i = 0; i < PlayerPositionButtons.Count; i++)
            {
                var (r, c) = IndexToRc(i);
                if (EnemyShots[r, c] == ShotState.Unknown)
                    unknown.Add(i);
            }
            return unknown.Count == 0 ? -1 : unknown[rand.Next(unknown.Count)];
        }
        //---------------------------------------------------------------------------------------
        // Game over check and handling
        private void CheckGameOver()
        {
            int totalShipCells = 9;

            if (PlayerScore == totalShipCells && EnemyScore == totalShipCells)
            {
                UpdateStatus("🤝 Draw!", Brushes.Orange);
                EndGame();
            }
            else if (PlayerScore == totalShipCells)
            {
                UpdateStatus("🎉 You Win! Enemy fleet destroyed!", Brushes.Green);
                EndGame();
            }
            else if (EnemyScore == totalShipCells)
            {
                UpdateStatus("💀 Game Over! Enemy wins!", Brushes.Red);
                EndGame();
            }
        }
        private void EndGame()
        {
            // disable enemy grid
            foreach (var btn in EnemyPositionButtons)
                btn.IsEnabled = false;

            // disable player grid
            foreach (var btn in PlayerPositionButtons)
                btn.IsEnabled = false;

            Debug.WriteLine("Game Over - No more moves allowed");
        }
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            RestartGame();  //Restart the game for every new round
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShakeAndNavigate(back, new LevelPage());
        }
    }
}