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
    public partial class Easy : Page
    {
        //---------------------------------------------------------------------------------------
        List<Button>? PlayerPositionButtons;
        List<Button>? EnemyPositionButtons;

        Random rand = new Random(); // Random number generator for AI ship placement

        int TotalShips = 3; // Total number of ships to be placed in every game
        int PlayerScore;
        int EnemyScore;
        bool shipsPlaced = false; //checking if player has placed their ships before attacking

        //---------------------------------------------------------------------------------------
        //Constructor
        public Easy()
        {
            InitializeComponent(); //WPF initialization
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

            for (int i = 0; i < 16; i++) // 4x4
            {
                Button playerBtn = new Button
                {
                    Style = (Style)FindResource("GridButtonStyle")
                };

                playerBtn.Click += PlayerSetShipClick;
                PlayerGrid.Children.Add(playerBtn);
                PlayerPositionButtons.Add(playerBtn);

                Button enemyBtn = new Button
                {
                    Style = (Style)FindResource("GridButtonStyle")
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
        //---------------------------------------------------------------------------------------
        private void RestartGame()
        {
            //hide restart button
            InitializeGrids();
            for (int i = 0; i < EnemyPositionButtons.Count; i++)
            {
                EnemyPositionButtons[i].IsEnabled = true;
                EnemyPositionButtons[i].Tag = null;
                EnemyPositionButtons[i].Content = null;
            }

            for (int i = 0; i < PlayerPositionButtons.Count; i++)
            {
                PlayerPositionButtons[i].IsEnabled = true;
                PlayerPositionButtons[i].Tag = null;
                PlayerPositionButtons[i].Content = null;
            }
            //Resetting variables to the original value
            TotalShips = 3;
            PlayerScore = 0;
            EnemyScore = 0;
            shipsPlaced = false;

            txtPlayerScore.Content = PlayerScore.ToString();
            txtEnemyScore.Content = EnemyScore.ToString();

            UpdateStatus("⚓ Place your ships!", Brushes.White);

            EnemyLocationPicker();          
        }
        //---------------------------------------------------------------------------------------
        //Ship placement by the player on player grid by clicking on the grid
        private void PlayerSetShipClick(object sender, RoutedEventArgs e)
        {
            
            if (TotalShips > 0)
            {
                var button = (Button)sender;
                button.IsEnabled = false;
                button.Tag = "PlayerShip";
                Debug.WriteLine("player Position: " + button.Name);
                button.Content = new Image
                 {
                    //show ship placed image
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/destroyer.png")),
                    Stretch = Stretch.Uniform
                 };
                TotalShips--;
            }

            //If player has placed all ships, allow attacking
            if (TotalShips == 0)
            {
                shipsPlaced = true;
                Debug.WriteLine("All ships placed! Player can now attack.");
                UpdateStatus("✅ Attack the enemy!", Brushes.LimeGreen);
            }
            else
            {
                UpdateStatus($"⚓ Place your ships! ({TotalShips} left)", Brushes.White);
            }
        }
        //---------------------------------------------------------------------------------------
        //Attacking enemy board by the player on enemy grid by clicking on the grid
        private void AttackClickEvent(object sender, RoutedEventArgs e)
        {
            //checking ship placement flag
            if (!shipsPlaced)
            {
                UpdateStatus("⚠️ Place all your ships first!", Brushes.Red, shake: true);
                return;
            }

            var button = (Button)sender;

            // Disable the button after attack so it cannot be clicked again
            button.IsEnabled = false;

            // Check if this button had an enemy ship
            if ((string?)button.Tag == "EnemyShip")
            {
                // Show hit image
                button.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/hit.jpg")),
                    Stretch = Stretch.Uniform
                };

                PlayerScore++;
                txtPlayerScore.Content = PlayerScore.ToString();
                Debug.WriteLine("HIT! Enemy ship at: " + button.Name);
                UpdateStatus("💥 Player Hit!", Brushes.LimeGreen);

            }
            else
            {
                // Show miss image
                button.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/miss.jpg")),
                    Stretch = Stretch.Uniform
                };

                Debug.WriteLine("MISS at: " + button.Name);
                UpdateStatus("Player Missed!", Brushes.LightBlue);

            }

            EnemyAttackEvent();
            CheckGameOver();
        }
        //---------------------------------------------------------------------------------------
        private void EnemyLocationPicker()
        {
            int shipsToPlace = 3;
            int placed = 0;
            while (placed < shipsToPlace)
            {
                int index = rand.Next(EnemyPositionButtons.Count);

                if (EnemyPositionButtons[index].IsEnabled == true && (string)EnemyPositionButtons[index].Tag == null)
                {
                    EnemyPositionButtons[index].Tag = "EnemyShip";

                    Debug.WriteLine("Enemy Position: " + EnemyPositionButtons[index].Name);

                    placed++;

                }
            }
        }
        //---------------------------------------------------------------------------------------
        private void EnemyAttackEvent()
        {
            // safety check
            if (PlayerPositionButtons.Count == 0) return; 

            int index = rand.Next(PlayerPositionButtons.Count);
            Button targetButton = PlayerPositionButtons[index];

            // retry if already attacked
            if (!targetButton.IsEnabled)
            {
                EnemyAttackEvent(); 
                return;
            }

            if ((string)targetButton.Tag == "PlayerShip")
            {
                targetButton.IsEnabled = false;
                targetButton.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/hit.jpg")),
                    Stretch = Stretch.Uniform
                };

                EnemyScore++;
                txtEnemyScore.Content = EnemyScore.ToString();

                Debug.WriteLine("Enemy HIT at: " + targetButton.Name);
                UpdateStatus("💥 Enemy Hit!", Brushes.Red);
            }
            else
            {
                targetButton.IsEnabled = false;
                targetButton.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/BattleShip;component/miss.jpg")),
                    Stretch = Stretch.Uniform
                };

                Debug.WriteLine("Enemy Miss at: " + targetButton.Name);
                UpdateStatus("Enemy Miss!", Brushes.LightBlue);
            }

            PlayerPositionButtons.RemoveAt(index);
            CheckGameOver();

        }
        //---------------------------------------------------------------------------------------
        private void CheckGameOver()
        {
            int totalShipCells = 3; 

            if (PlayerScore == totalShipCells && EnemyScore == totalShipCells)
            {
                UpdateStatus("🤝 Draw! Both fleets destroyed!", Brushes.Orange);
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