using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace BattleShip
{
    public partial class LevelPage : Page
    {
        public LevelPage()
        {
            InitializeComponent();
            DoubleAnimation fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            LevelGrid.BeginAnimation(OpacityProperty, fadeInAnimation);
        }
        private void ShakeAndNavigate(Button button, Page targetPage)
        {
            DoubleAnimation shakeAnimation = new DoubleAnimation
            {
                From = -15,
                To = 15,
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3),
                Duration = TimeSpan.FromSeconds(0.3),
            };

            shakeAnimation.Completed += (s, e) =>
            {
                if (NavigationService != null)
                {
                    NavigationService.Navigate(targetPage);
                }
            };

            // Start the animation
            button.RenderTransform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
        }
        private void easy_Click(object sender, RoutedEventArgs e)
        {
            easy.Visibility = Visibility.Collapsed;
            medium.Visibility = Visibility.Collapsed;
            hard.Visibility = Visibility.Collapsed;

            ShakeAndNavigate(easy, new Easy());
        }

        private void medium_Click(object sender, RoutedEventArgs e)
        {
            easy.Visibility = Visibility.Collapsed;
            medium.Visibility = Visibility.Collapsed;
            hard.Visibility = Visibility.Collapsed;

            ShakeAndNavigate(medium, new Medium());
        }

        private void hard_Click(object sender, RoutedEventArgs e)
        {
            easy.Visibility = Visibility.Collapsed;
            medium.Visibility = Visibility.Collapsed;
            hard.Visibility = Visibility.Collapsed;

            ShakeAndNavigate(hard, new Hard());
        }
    }
}
