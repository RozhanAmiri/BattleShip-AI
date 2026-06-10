using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BattleShip
{
    public partial class Medium : Page
    {
        //---------------------------------------------------------------------------------------
        private const int BoardSize = 6;

        private List<Button> PlayerPositionButtons = new();
        private List<Button> EnemyPositionButtons = new();
        private List<Button> previewCells = new();

        private readonly Random rand = new Random();
        private HashSet<int> enemyShotIndexes = new();
        private HashSet<int> playerShotIndexes = new();

        private Dictionary<int, int> playerShipHealth = new();
        private int playerShipIdCounter = 0;

        private int PlayerScore;
        private int EnemyScore;
        private bool shipsPlaced = false;

        private int DragShipLength = 0;
        private string DragShipName = "";
        private bool ShipVertical = false;

        // Medium ships: 4, 3, 3, 1, 1
        private Dictionary<string, int> PlayerShipsToPlace = new()
        {
            { "Battleship", 1 },
            { "Cruiser", 1 },
            { "Submarine", 1 },
            { "Destroyer", 2 }
        };

        // Hunt + target AI memory
        private Point? firstHit = null;
        private Point? secondHit = null;
        private List<Button> targetQueue = new();
        //---------------------------------------------------------------------------------------
        //Constructor
        public Medium()
        {
            InitializeComponent(); //WPF initialization
            InitializeGrids();   //Initialize the player and enemy grids with buttons
            FadeIn();        //Animation
            RestartGame();  //Restart the game for every new round
        }
        //---------------------------------------------------------------------------------------
        private void InitializeGrids()
        {
            PlayerGrid.Children.Clear();
            EnemyGrid.Children.Clear();

            PlayerPositionButtons = new List<Button>();
            EnemyPositionButtons = new List<Button>();

            for (int i = 0; i < BoardSize * BoardSize; i++)
            {
                int index = i;

                // PLAYER
                Button playerBtn = new Button
                {
                    Style = (Style)FindResource("GridButtonStyle"),
                    Tag = null,
                    AllowDrop = true
                };

                playerBtn.Drop += CellDrop;
                playerBtn.DragOver += CellDragOver;

                PlayerGrid.Children.Add(playerBtn);
                PlayerPositionButtons.Add(playerBtn);

                // ENEMY
                Button enemyBtn = new Button
                {
                    Style = (Style)FindResource("GridButtonStyle"),
                    Tag = null
                };

                enemyBtn.Click += AttackClickEvent;

                EnemyGrid.Children.Add(enemyBtn);
                EnemyPositionButtons.Add(enemyBtn);
            }
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
        private void UpdateStatus(string message, Brush color, bool shake = false)
        {
            GameStatusText.Text = message;
            GameStatusText.Foreground = color;
            AnimateStatusText();

            if (shake)
                ShakeStatusText();
        }
        private void ClearPreview()
        {
            foreach (var cell in previewCells)
            {
                cell.ClearValue(Button.BackgroundProperty);
            }
            previewCells.Clear();
        }
        //---------------------------------------------------------------------------------------
        private void RestartGame()
        {
            PlayerShipsToPlace = new Dictionary<string, int>
            {
                { "Battleship", 1 },
                { "Cruiser", 1 },
                { "Submarine", 1 },
                { "Destroyer", 2 }
            };

            playerShipHealth.Clear();
            playerShipIdCounter = 0;

            EnemyShipHealth.Clear();
            EnemyShipIdCounter = 0;

            enemyShotIndexes.Clear();
            playerShotIndexes.Clear();

            targetQueue.Clear();
            firstHit = null;
            secondHit = null;

            foreach (var btn in EnemyPositionButtons)
            {
                btn.IsEnabled = true;
                btn.Tag = null;
                btn.Content = null;
                btn.ClearValue(Button.BackgroundProperty);
            }

            foreach (var btn in PlayerPositionButtons)
            {
                btn.IsEnabled = true;
                btn.Tag = null;
                btn.Content = null;
                btn.ClearValue(Button.BackgroundProperty);
            }

            foreach (var btn in FleetPanel.Children.OfType<Button>())
            {
                btn.IsEnabled = true;
                btn.Opacity = 1;
            }

            PlayerScore = 0;
            EnemyScore = 0;
            shipsPlaced = false;
            ShipVertical = false;
            DragShipLength = 0;
            DragShipName = "";

            txtPlayerScore.Content = "0";
            txtEnemyScore.Content = "0";

            UpdateStatus("⚓ Place your ships!", Brushes.White);

            EnemyLocationPicker();
        }
        //---------------------------------------------------------------------------------------
        //Ship placement by the player on player grid by clicking on the grid
        private Dictionary<int, int> PlayerShipHealth = new();
        private int PlayerShipIdCounter = 0;
        private void ShipDragStart(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (sender is not Button shipButton)
                return;

            if (shipButton.Tag == null)
                return;

            string data = shipButton.Tag.ToString()!;
            var parts = data.Split(':');

            if (parts.Length != 2)
                return;

            string shipName = parts[0];

            if (!int.TryParse(parts[1], out int length))
                return;

            // ✅ SET DRAG VALUES
            DragShipName = shipName;
            DragShipLength = length;

            // 🔥 IMPORTANT: only start drag when actually moving
            DragDrop.DoDragDrop(shipButton, data, DragDropEffects.Move);
        }
        private bool CanPlaceShip(int index, int length, bool vertical)
        {
            if (index < 0) return false;

            int r = index / BoardSize;
            int c = index % BoardSize;

            for (int k = 0; k < length; k++)
            {
                int rr = vertical ? r + k : r;
                int cc = vertical ? c : c + k;

                // ❌ Out of bounds
                if (rr >= BoardSize || cc >= BoardSize)
                    return false;

                int idx = rr * BoardSize + cc;
                var cell = PlayerPositionButtons[idx];

                // ❌ Only block REAL ships
                if (cell.Tag != null && cell.Tag.ToString()!.StartsWith("PlayerShip"))
                    return false;
            }

            return true;
        }
        private void CellDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
                return;

            if (sender is not Button button)
                return;

            if (DragShipLength <= 0)
                return;

            ClearPreview();

            int startIndex = PlayerPositionButtons.IndexOf(button);
            if (startIndex < 0) return;

            int r = startIndex / BoardSize;
            int c = startIndex % BoardSize;

            bool canPlace = true;

            for (int k = 0; k < DragShipLength; k++)
            {
                int rr = ShipVertical ? r + k : r;
                int cc = ShipVertical ? c : c + k;

                if (rr >= BoardSize || cc >= BoardSize)
                {
                    canPlace = false;
                    break;
                }

                int idx = rr * BoardSize + cc;
                var cell = PlayerPositionButtons[idx];

                previewCells.Add(cell);

                if (cell.Tag != null && cell.Tag.ToString()!.StartsWith("PlayerShip"))
                    canPlace = false;
            }

            foreach (var cell in previewCells)
            {
                cell.Background = canPlace ? Brushes.DarkSeaGreen : Brushes.IndianRed;
            }
        }
        private void CellDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.StringFormat))
                return;

            if (sender is not Button button)
                return;

            int index = PlayerPositionButtons.IndexOf(button);

            if (index < 0)
            {
                UpdateStatus("❌ Invalid drop!", Brushes.Red, true);
                return;
            }

            if (DragShipLength <= 0 || string.IsNullOrEmpty(DragShipName))
            {
                UpdateStatus("❌ Drag error!", Brushes.Red, true);
                return;
            }

            int length = DragShipLength;

            // 🚨 Check if ship still available
            if (!PlayerShipsToPlace.ContainsKey(DragShipName) ||
                PlayerShipsToPlace[DragShipName] <= 0)
            {
                UpdateStatus("⚠️ No more of this ship!", Brushes.Orange, true);
                return;
            }

            ClearPreview();

            if (!CanPlaceShip(index, length, ShipVertical))
            {
                UpdateStatus("❌ Invalid placement!", Brushes.Red, true);
                return;
            }

            // ✅ PLACE SHIP
            PlaceShip(index, length);

            // decrease count
            PlayerShipsToPlace[DragShipName]--;

            // disable ship button if finished
            foreach (var btn in FleetPanel.Children.OfType<Button>())
            {
                if (btn.Tag == null)
                    continue;

                string btnShipName = btn.Tag.ToString()!.Split(':')[0];

                if (btnShipName == DragShipName && PlayerShipsToPlace[DragShipName] == 0)
                {
                    btn.IsEnabled = false;
                    btn.Opacity = 0.4;
                }
            }

            // reset drag
            DragShipLength = 0;
            DragShipName = "";

            // count ships left correctly
            int shipsLeft = PlayerShipsToPlace.Values.Sum();

            if (shipsLeft == 0)
            {
                shipsPlaced = true;

                UpdateStatus(
                    "⚔️ BATTLE MODE! FIRE AT WILL!",
                    Brushes.Gold,
                    true
                );
            }
            else
            {
                UpdateStatus($"⚓ Ships left: {shipsLeft}", Brushes.White);
            }
        }
        private void RotateShipClick(object sender, RoutedEventArgs e)
        {
            ShipVertical = !ShipVertical;

            UpdateStatus(
                ShipVertical ? "🔄 Vertical" : "➡️ Horizontal",
                Brushes.LightBlue
            );
        }
        private void PlaceShip(int index, int length)
        {
            int r = index / BoardSize;
            int c = index % BoardSize;

            int shipId = playerShipIdCounter++;
            playerShipHealth[shipId] = length;

            string prefix = DragShipName.ToLower() switch
            {
                "battleship" => "BS1",
                "cruiser" => "CRU",
                "submarine" => "SUB",
                "destroyer" => "destroyer",
                _ => "destroyer"
            };

            for (int k = 0; k < length; k++)
            {
                int rr = ShipVertical ? r + k : r;
                int cc = ShipVertical ? c : c + k;

                int idx = rr * BoardSize + cc;
                var btn = PlayerPositionButtons[idx];

                btn.Tag = $"PlayerShip:{shipId}";

                string imagePath;

                if (length == 1)
                {
                    imagePath = "destroyer.png";
                }
                else if (prefix == "BS1")
                {
                    imagePath = $"BS1{k + 1}.png";
                }
                else
                {
                    imagePath = $"{prefix}{k + 1}.png";
                }

                var img = new Image
                {
                    Source = new BitmapImage(new Uri($"pack://application:,,,/BattleShip;component/{imagePath}")),
                    Stretch = Stretch.Fill,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                if (ShipVertical)
                    img.RenderTransform = new RotateTransform(90);

                btn.Content = img;
            }
        }
        //---------------------------------------------------------------------------------------
        //Attacking enemy board by the player on enemy grid by clicking on the grid
        private void AttackClickEvent(object sender, RoutedEventArgs e)
        {
            if (!shipsPlaced)
            {
                UpdateStatus("⚠️ Place all your ships first!", Brushes.Red, true);
                return;
            }

            var button = (Button)sender;
            button.IsEnabled = false;

            if (button.Tag != null && button.Tag.ToString()!.StartsWith("EnemyShip"))
            {
                string[] parts = button.Tag.ToString()!.Split(':');
                int shipId = int.Parse(parts[1]);

                EnemyShipHealth[shipId]--;

                button.Content = new Image
                {
                    Source = new BitmapImage(
                        new Uri("pack://application:,,,/BattleShip;component/hit.jpg")),
                    Stretch = Stretch.Uniform
                };

                if (EnemyShipHealth[shipId] == 0)
                {
                    PlayerScore++;
                    txtPlayerScore.Content = PlayerScore.ToString();

                    UpdateStatus("🔥 Ship Destroyed!", Brushes.Gold);
                }
                else
                {
                    UpdateStatus("💥 Hit!", Brushes.LimeGreen);
                }
            }
            else
            {
                button.Content = new Image
                {
                    Source = new BitmapImage(
                        new Uri("pack://application:,,,/BattleShip;component/miss.jpg")),
                    Stretch = Stretch.Uniform
                };

                UpdateStatus("Player Missed!", Brushes.LightBlue);
            }

            EnemyAttackEvent();
            CheckGameOver();
        }
        //---------------------------------------------------------------------------------------
        private Dictionary<int, int> EnemyShipHealth = new();
        private int EnemyShipIdCounter = 0;
        private void EnemyLocationPicker()
        {
            EnemyShipHealth.Clear();
            EnemyShipIdCounter = 0;

            List<int> ships = new() { 4, 3, 3, 1, 1 };

            foreach (int length in ships)
            {
                bool placed = false;

                while (!placed)
                {
                    int index = rand.Next(EnemyPositionButtons.Count);
                    bool vertical = rand.Next(2) == 0;

                    if (!CanPlaceEnemyShip(index, length, vertical))
                        continue;

                    int shipId = EnemyShipIdCounter++;
                    EnemyShipHealth[shipId] = length;

                    PlaceEnemyShip(index, length, vertical, shipId);

                    placed = true;
                }
            }
        }
        private bool CanPlaceEnemyShip(int index, int length, bool vertical)
        {
            int r = index / BoardSize;
            int c = index % BoardSize;

            for (int k = 0; k < length; k++)
            {
                int rr = vertical ? r + k : r;
                int cc = vertical ? c : c + k;

                // ❌ out of bounds
                if (rr >= BoardSize || cc >= BoardSize)
                    return false;

                // check surrounding 8 directions (including itself)
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int nr = rr + dr;
                        int nc = cc + dc;

                        if (nr < 0 || nc < 0 || nr >= BoardSize || nc >= BoardSize)
                            continue;

                        int neighborIndex = nr * BoardSize + nc;

                        if (EnemyPositionButtons[neighborIndex].Tag != null)
                            return false;
                    }
                }
            }

            return true;
        }
        private void PlaceEnemyShip(int index, int length, bool vertical, int shipId)
        {
            int r = index / BoardSize;
            int c = index % BoardSize;

            for (int k = 0; k < length; k++)
            {
                int rr = vertical ? r + k : r;
                int cc = vertical ? c : c + k;

                int idx = rr * BoardSize + cc;

                // store ship ID
                EnemyPositionButtons[idx].Tag = $"EnemyShip:{shipId}";
            }
        }
        //---------------------------------------------------------------------------------------
        // HUNT MODE → TARGET MODE AI ATTACK ALGORITHM
        //---------------------------------------------------------------------------------------
        private void EnemyAttackEvent()
        {
            if (enemyShotIndexes.Count >= PlayerPositionButtons.Count)
                return;

            while (targetQueue.Count > 0)
            {
                Button targeted = targetQueue[0];
                targetQueue.RemoveAt(0);

                int idx = PlayerPositionButtons.IndexOf(targeted);

                if (idx >= 0 && !enemyShotIndexes.Contains(idx))
                {
                    ExecuteEnemyShot(targeted);
                    return;
                }
            }

            var possibleTargets = PlayerPositionButtons.Select((b, index) => new
                {
                    Button = b,
                    Index = index,
                    Row = index / BoardSize,
                    Col = index % BoardSize,
                    Random = rand.Next()
                })
                .Where(x => !enemyShotIndexes.Contains(x.Index)).OrderBy(x => (x.Row + x.Col) % 2 == 0 ? 0 : 1).ThenBy(x => x.Random).ToList();

            if (possibleTargets.Count > 0)
                ExecuteEnemyShot(possibleTargets[0].Button);
        }
        //---------------------------------------------------------------------------------------
        // EXECUTES THE SHOT + BUILDS TARGET MODE LOGIC
        private void ExecuteEnemyShot(Button targetButton)
        {
            int targetIndex = PlayerPositionButtons.IndexOf(targetButton);

            if (targetIndex < 0 || enemyShotIndexes.Contains(targetIndex))
                return;

            enemyShotIndexes.Add(targetIndex);

            bool isHit = targetButton.Tag != null &&
                         targetButton.Tag.ToString()!.StartsWith("PlayerShip");

            if (!isHit)
            {
                targetButton.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/miss.jpg")),
                    Stretch = Stretch.Uniform
                };

                UpdateStatus("Enemy Miss!", Brushes.LightBlue);
                CheckGameOver();
                return;
            }

            string[] parts = targetButton.Tag.ToString()!.Split(':');
            int shipId = int.Parse(parts[1]);

            playerShipHealth[shipId]--;

            targetButton.Content = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/hit.jpg")),
                Stretch = Stretch.Uniform
            };

            Point hitPoint = ButtonToPoint(targetButton);

            if (playerShipHealth[shipId] == 0)
            {
                EnemyScore++;
                txtEnemyScore.Content = EnemyScore.ToString();

                UpdateStatus("💀 Enemy destroyed your ship!", Brushes.Red);

                firstHit = null;
                secondHit = null;
                targetQueue.Clear();
            }
            else
            {
                UpdateStatus("💥 Enemy Hit!", Brushes.Red);

                if (firstHit == null)
                {
                    firstHit = hitPoint;
                    EnqueueAdjacent(hitPoint);
                }
                else if (secondHit == null)
                {
                    secondHit = hitPoint;
                    DetermineShipOrientation(firstHit.Value, secondHit.Value);
                }
                else
                {
                    ContinueLinearTarget(firstHit.Value, hitPoint);
                }
            }

            CheckGameOver();
        }
        //---------------------------------------------------------------------------------------
        // CONVERT BUTTON NAME TO GRID COORDINATE
        private Point ButtonToPoint(Button b)
        {
            int index = PlayerPositionButtons.IndexOf(b);
            int row = index / BoardSize;
            int col = index % BoardSize;
            return new Point(row, col);
        }
        //---------------------------------------------------------------------------------------
        // ADD SURROUNDING CELLS FOR TARGET MODE
        private void EnqueueAdjacent(Point p)
        {
            TryQueue(p.X - 1, p.Y);
            TryQueue(p.X + 1, p.Y);
            TryQueue(p.X, p.Y - 1);
            TryQueue(p.X, p.Y + 1);
        }
        //---------------------------------------------------------------------------------------
        private void TryQueue(double r, double c)
        {
            int rr = (int)r;
            int cc = (int)c;

            if (rr < 0 || cc < 0 || rr >= BoardSize || cc >= BoardSize)
                return;

            int index = rr * BoardSize + cc;

            if (enemyShotIndexes.Contains(index))
                return;

            Button b = PlayerPositionButtons[index];

            if (!targetQueue.Contains(b))
                targetQueue.Add(b);
        }
        //---------------------------------------------------------------------------------------
        // DETERMINE ORIENTATION AND REFINE TARGETS
        private void DetermineShipOrientation(Point p1, Point p2)
        {
            bool horizontal = p1.X == p2.X;

            if (horizontal)
            {
                // continue left/right
                ContinueLinearTarget(p1, p2);
            }
            else
            {
                // continue up/down
                ContinueLinearTarget(p1, p2);
            }
        }
        //---------------------------------------------------------------------------------------
        private void ContinueLinearTarget(Point p1, Point p2)
        {
            int dx = Math.Sign(p2.X - p1.X);
            int dy = Math.Sign(p2.Y - p1.Y);

            TryQueue(p2.X + dx, p2.Y + dy);
            TryQueue(p1.X - dx, p1.Y - dy);
        }
        //---------------------------------------------------------------------------------------
        private void CheckGameOver()
        {
            int totalShips = 5;

            if (PlayerScore == totalShips && EnemyScore == totalShips)
            {
                UpdateStatus("🤝 Draw!", Brushes.Orange);
                EndGame();
            }
            else if (PlayerScore == totalShips)
            {
                UpdateStatus("🎉 You Win! Enemy fleet destroyed!", Brushes.Green);
                EndGame();
            }
            else if (EnemyScore == totalShips)
            {
                UpdateStatus("💀 Game Over! Enemy wins!", Brushes.Red);
                EndGame();
            }
        }
        //---------------------------------------------------------------------------------------
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
        //---------------------------------------------------------------------------------------
        private void Restart_Click(object sender, RoutedEventArgs e)
        {
            RestartGame();  //Restart the game for every new round
        }
        //---------------------------------------------------------------------------------------
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShakeAndNavigate(back, new LevelPage());
        }
    }
}