using System.Collections.ObjectModel;
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

namespace TimelineApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public ObservableCollection<TimelineBlock> Blocks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        var now = DateTime.Now;
        Blocks.Add(new TimelineBlock { Start = now.AddMinutes(1), End = now.AddMinutes(1)});
        Blocks.Add(new TimelineBlock { Start = now.AddMinutes(3), End = now.AddMinutes(3)});
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var start = Blocks[^1].End.AddMinutes(1);
        Blocks.Add(new TimelineBlock { Start = start, End = start.AddMinutes(1)});
    }
}