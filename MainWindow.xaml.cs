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

namespace BattleShip;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    private void OpenGame_Click(object sender, RoutedEventArgs e)
    {
        //Shake animation for button styling
        DoubleAnimation shakeAnimation = new DoubleAnimation
        {
            From = -15,
            To = 15,
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3), //Shaking 3 times
            Duration = TimeSpan.FromSeconds(10),
        };

        Play.RenderTransform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);

        //Navigate to new page
        Play.Visibility = Visibility.Collapsed;
        Main.Navigate(new LevelPage());

    }
}