using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace Megeon.ParserLib
{
    public class Manager
    {
        private readonly JsonSerializerSettings settings;
        private readonly ProductParser parser;
        public static readonly string HOST = "http://www.megeon-pribor.ru";
        private readonly string _storageFile;
        private readonly HttpClient client;
        private readonly Formatter formatter;
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

        public Manager(string storageFile)
        {
            settings = new JsonSerializerSettings()
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };
            parser = new ProductParser();
            _storageFile = storageFile;
            Load();

            formatter = new Formatter(products);

            client = new HttpClient(new HttpClientHandler() { CookieContainer = new CookieContainer(), AllowAutoRedirect = true });
            client.Timeout = TimeSpan.FromMinutes(3);
        }

        public void ConfigureConflictResolver(Func<List<string>, Task<string>> resolver)
        {
            parser.ConflictResolver = resolver;
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

        public void FillDescription(string fileName)
        {
            formatter.FillDescription(fileName);
        }


        public async Task DownloadResources(string folder, IProgress<double> indicator)
        {
            int total = products.Count;
            int current = 0;

            foreach (var p in products)
            {
                await DownloadProductResources(folder, p);

                indicator?.Report((double)++current / total);
            }
        }

        private async Task DownloadProductResources(string folder, Product product)
        {
            if (product.Images != null)
            {
                foreach (var image in product.Images)
                {
                    string localPath = Path.Combine(folder, "products", image.CreateMD5() + Path.GetExtension(image));

                    if (!File.Exists(localPath))
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        try
                        {
                            await (new WebClient().DownloadFileTaskAsync(image, localPath));
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }
            }

            if (product.Instructions != null)
            {
                foreach (var pdf in product.Instructions)
                {
                    string localPath = Path.Combine(folder, "pdf", pdf.Uri.CreateMD5() + Path.GetExtension(pdf.Uri));

                    if (!File.Exists(localPath))
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        try
                        {
                            await (new WebClient().DownloadFileTaskAsync(pdf.Uri, localPath));
                        }
                        catch
                        {
                            throw;
                        }
                    }
                }
            }
        }

        internal string GetPdfSql()
        {
            return formatter.GetPdfSql();
        }

        internal string GetCategoryMapSql()
        {
            return formatter.GetCategoryMapSql();
        }

        internal string GetDescUpdateSql()
        {
            return formatter.GetDescUpdateSql();
        }

        private void Save(List<Product> productsToSave)
        {
            if (productsToSave != null)
            {
                var str = JsonConvert.SerializeObject(productsToSave, settings);
                File.WriteAllText(_storageFile, str);
            }
        }

        private void Load()
        {
            if (File.Exists(_storageFile))
            {
                var str = File.ReadAllText(_storageFile);
                products = JsonConvert.DeserializeObject<List<Product>>(str, settings);
            }
        }

        internal async Task<string> GetDimensionsSql(bool getAll)
        {
            return await formatter.GetDimensionsSql(getAll);
        }
    }
}
