using Megeon.ParserLib;
using System;
using System.IO;
using System.Windows;

namespace Megeon
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Manager manager;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (o, e) =>
            {
                string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(downloadsFolder))
                {
                    throw new DirectoryNotFoundException(downloadsFolder);
                }
                txtDownloadFolderPath.Text = downloadsFolder;

                manager = new Manager("products.json");

                manager.ConfigureConflictResolver(async (ev) =>
                {
                    return null;


                    var vm = new SearchConflictResolverViewModel()
                    {
                        Items = ev,
                        ModelToFind = manager.CurrentModel
                    };

                    var resolverWindow = new SearchConflictResolverDialog() { DataContext = vm };

                    resolverWindow.ShowDialog();

                    return vm.SelectedItem;
                });
            };
            Closed += (o, e) =>
            {
                Properties.Settings.Default.saved_resource_folder = (txtDownloadFolderPath.Text ?? string.Empty);
                Properties.Settings.Default.Save();
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
            IdNullCheck();

            string data = manager.GetGeneralExport();
            Clipboard.SetText(data);
        }

        private void btnAdditionalImages_Click(object sender, RoutedEventArgs e)
        {
            IdNullCheck();

            string data = manager.GetAdditionalImagesExport();
            Clipboard.SetText(data);
        }

        private void btnFillDescriptions_Click(object sender, RoutedEventArgs e)
        {
            IdNullCheck();

            var ofd = new Microsoft.Win32.OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                manager.FillDescription(ofd.FileName);
            }
        }

        private void btnGetDescriptionUpdate_Click(object sender, RoutedEventArgs e)
        {
            IdNullCheck();

            string data = manager.GetDescUpdateSql();
            Clipboard.SetText(data);
        }

        private void btnCategorSql_Click(object sender, RoutedEventArgs e)
        {
            IdNullCheck();

            string data = manager.GetCategoryMapSql();
            Clipboard.SetText(data);
        }

        private void IdNullCheck()
        {
            if (manager.START_ETK_ID == 0)
            {
                var d = new StartIdInputForm();
                if (d.ShowDialog() == true)
                {
                    manager.START_ETK_ID = d.START_ID;
                    return;
                }
                else
                {
                    MessageBox.Show("ID НЕ УКАЗАН");
                }
            }
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
}
