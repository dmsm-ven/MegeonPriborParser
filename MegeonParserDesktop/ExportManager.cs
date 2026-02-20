using MegeonParserDesktop.Models;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Xml;

namespace MegeonParserDesktop;

public class ExportManager
{
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ProductParser parser;
    public static readonly string HOST = "http://www.megeon-pribor.ru";
    private readonly string _storageFile;
    private readonly ExportFormatter formatter;
    private readonly ResourceDownloader resourceDownloader = new();
    private List<Product> products;

    public string CurrentModel => parser.CurrentModel;
    public int START_ETK_ID
    {
        get => formatter.START_ETK_ID;
        set
        {
            formatter.START_ETK_ID = value;
        }
    }

    public ExportManager(string storageFile, string credentials)
    {
        jsonOptions = new JsonSerializerOptions()
        {
            WriteIndented = true
        };
        parser = new ProductParser();
        _storageFile = storageFile;
        Load();

        formatter = new ExportFormatter(products, credentials);
    }

    public async Task Parse(IProgress<double> indicator)
    {

        string downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        string etkModelsFile = Path.Combine(downloadFolder, "oc_product.csv");
        string megeonModelsFile = Path.Combine(downloadFolder, "yml_catalog.xml");

        if (!File.Exists(etkModelsFile) || !File.Exists(megeonModelsFile))
        {
            throw new FileNotFoundException("Не найден файл xml/csv");
        }

        var allMegeonModels = new XmlDocument();
        allMegeonModels.Load(megeonModelsFile);

        HashSet<string> megeonModels = allMegeonModels.DocumentElement
            .LastChild.LastChild
            .SelectNodes(".//offer")
            .OfType<XmlElement>()
            .Select(offer => offer.ChildNodes.OfType<XmlNode>().FirstOrDefault(n => n.Name == "model").InnerText.Trim())
            .ToHashSet();

        var etkModels = File.ReadAllLines(etkModelsFile)
            .Select(line => line.Trim('"'))
            .ToArray();

        var modelsToParse = megeonModels.Except(etkModels).ToArray();
        Clipboard.SetText(string.Join(Environment.NewLine, modelsToParse));

        if (modelsToParse.Length > 0)
        {
            var res = MessageBox.Show($"Найдено: {modelsToParse.Length} новинок. Запустить парсинг ?", "Информация", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.No)
            {
                return;
            }

            var data = await parser.Parse(modelsToParse, indicator);

            products = data;
            Save(products);
            Load();
        }
        else
        {
            MessageBox.Show("Новинок не найден");
        }
    }

    public string GetGeneralExport()
    {
        return formatter.GetGeneralExport();
    }

    public string GetAdditionalImagesExport()
    {
        return formatter.GetAdditionalImagesExport();
    }

    public async Task DownloadResources(string folder, IProgress<double> indicator)
    {
        await resourceDownloader.DownloadResources(this.products, folder, indicator);
    }

    internal string GetPdfSql()
    {
        return formatter.GetPdfSql();
    }

    internal string GetDescUpdateSql()
    {
        return formatter.GetDescUpdateSql();
    }

    private void Save(List<Product> productsToSave)
    {
        if (productsToSave != null)
        {
            var str = JsonSerializer.Serialize(productsToSave, jsonOptions);
            File.WriteAllText(_storageFile, str);
        }
    }
    private void Load()
    {
        if (File.Exists(_storageFile))
        {
            var str = File.ReadAllText(_storageFile);
            products = JsonSerializer.Deserialize<List<Product>>(str, jsonOptions);
        }
    }

    internal async Task<string> GetDimensionsSql(bool getAll)
    {
        return await formatter.GetDimensionsSql(getAll);
    }
}
