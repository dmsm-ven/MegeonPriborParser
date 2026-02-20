using System.IO;
using System.Windows;

namespace MegeonParserDesktop;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private ExportManager manager;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (o, e) =>
        {
            txtDownloadFolderPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var credentials = File.ReadAllText("price_list_credentials.txt");
            manager = new ExportManager("products.json", credentials);
        };
    }

    private async void btnParse_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await manager.Parse(new Progress<double>(v => pbIndicator.Value = v));
            IsEnabled = true;
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void btnDownloadResources_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        if (!Directory.Exists(txtDownloadFolderPath.Text))
        {
            if (string.IsNullOrWhiteSpace(txtDownloadFolderPath.Text))
            {
                MessageBox.Show("Папка для загрузки не выбрана");
            }
            else
            {
                Directory.CreateDirectory(txtDownloadFolderPath.Text);
            }

        }

        try
        {
            await manager.DownloadResources(txtDownloadFolderPath.Text, new Progress<double>(v => pbIndicator.Value = v));
            IsEnabled = true;
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }


    }

    private void btnExportGeneral_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetGeneralExport();
        Clipboard.SetText(data);
    }

    private void btnAdditionalImages_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetAdditionalImagesExport();
        Clipboard.SetText(data);
    }

    private void btnGetDescriptionUpdate_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetDescUpdateSql();
        Clipboard.SetText(data);
    }

    private void btnPdf_Click(object sender, RoutedEventArgs e)
    {
        string data = manager.GetPdfSql();
        Clipboard.SetText(data);
    }

    private async void btnDimensions_Click(object sender, RoutedEventArgs e)
    {
        btnDimensions.IsEnabled = false;
        try
        {
            string data = await manager.GetDimensionsSql(getAll: false);
            Clipboard.SetText(data);
        }
        finally
        {
            btnDimensions.IsEnabled = true;
        }
    }
}