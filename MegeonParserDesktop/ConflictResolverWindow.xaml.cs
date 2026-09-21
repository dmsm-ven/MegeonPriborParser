using System.Windows;

namespace MegeonParserDesktop;
/// <summary>
/// Interaction logic for ConflictResolverWindow.xaml
/// </summary>
public partial class ConflictResolverWindow : Window
{
    public string SelectedCustomUri => txtCustomUrl.Text;
    public string SelectedProductTitle => lbAvailableVariants.SelectedItem as string;
    public ConflictResolverWindow()
    {
        InitializeComponent();
    }

    private void btnSelect_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void btnCustomUrl_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
