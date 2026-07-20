using System.Windows;

namespace MegeonParserDesktop;
/// <summary>
/// Interaction logic for ConflictResolverWindow.xaml
/// </summary>
public partial class ConflictResolverWindow : Window
{
    public ConflictResolverWindow()
    {
        InitializeComponent();
    }

    private void btnSelect_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
