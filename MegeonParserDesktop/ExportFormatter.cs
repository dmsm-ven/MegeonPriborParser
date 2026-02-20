using MegeonParserDesktop.Models;
using SlugGenerator;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace MegeonParserDesktop;

public class ExportFormatter
{
    private readonly List<Product> products;
    private readonly Dictionary<string, ProductDimensions> modelDimension;
    private readonly string credentials;

    public int START_ETK_ID { get; set; }

    private const string manufacturer_ftp_path = "megeon";
    private const string manufacturer = "Мегеон";

    public ExportFormatter(List<Product> products, string credentials)
    {
        this.products = products == null ?
            Enumerable.Empty<Product>().ToList() :
            products.OrderBy(m => m.Model).ToList();
        modelDimension = new Dictionary<string, ProductDimensions>();
        this.credentials = credentials;
    }

    public string GetGeneralExport()
    {
        var sb = new StringBuilder();

        int id = START_ETK_ID;
        foreach (var product in products)
        {
            string mainImage = GetMainImage(product);

            string caption = $"{product.Model} {product.ProductType}";
            string keyword = caption.GenerateSlug();
            string meta_title = $"{caption} купить в Санкт-Петербурге";
            string meta_desc = $"{meta_title} с доставкой по России";

            sb
                .AppendTab(id.ToString())   //product_id
                .AppendTab(product.Name)   //name(ru)
                .AppendTab(string.Empty)   //categories
                .AppendTab(product.Model)   //sku
                .AppendTab(string.Empty)   //upc
                .AppendTab(string.Empty)   //ean
                .AppendTab(string.Empty)   //jan
                .AppendTab(string.Empty)   //isbn
                .AppendTab(string.Empty)   //mpn
                .AppendTab(string.Empty)   //location
                .AppendTab("0")   //quantity
                .AppendTab(product.Model)   //model
                .AppendTab(manufacturer)   //manufacturer
                .AppendTab(mainImage)   //image_name
                .AppendTab("yes")   //shipping
                .AppendTab("0")   //price
                .AppendTab("0")   //points
                .AppendTab(string.Empty)   //date_added
                .AppendTab(string.Empty)   //date_modified
                .AppendTab(string.Empty)   //date_available
                .AppendTab("0")   //weight
                .AppendTab("kg")   //weight_unit
                .AppendTab("0")    //length
                .AppendTab("0")    //width
                .AppendTab("0")    //height
                .AppendTab("mm")   //length_unit
                .AppendTab("true")   //status
                .AppendTab("0")   //tax_class_id
                .AppendTab(keyword)   //seo_keyword
                .AppendTab(string.Empty)   //description(ru)
                .AppendTab("0")   //category_show(ru)
                .AppendTab("0")   //main_product(ru)
                .AppendTab(meta_title)   //meta_title(ru)
                .AppendTab(meta_desc)   //meta_description(ru)
                .AppendTab(string.Empty)   //meta_keywords(ru)
                .AppendTab("10")   //stock_status_id
                .AppendTab("0,1,2,3,4,5,6,7,8")   //store_ids
                .AppendTab("0:,1:,2:,3:,4:,5:,6:,7:,8:")   //layout
                .AppendTab(string.Empty)   //related_ids
                .AppendTab(string.Empty)   //adjacent_ids
                .AppendTab(string.Empty)   //tags(ru)
                .AppendTab("1")   //sort_order
                .AppendTab("true")   //subtract
                .AppendLine("1"); //minimum

            id++;

        }

        var result = sb.ToString();
        return result;
    }

    public string GetAdditionalImagesExport()
    {
        var sb = new StringBuilder();

        int id = START_ETK_ID;
        foreach (var product in products)
        {
            int sort_order = 0;
            foreach (var image in product.Images.Skip(1))
            {
                string imagePath = $"catalog/{manufacturer_ftp_path}/products/" + image.CreateMD5() + ".jpg";
                sb.AppendLine($"{id}\t{imagePath}\t{sort_order++}\t{product.Model}");
            }

            id++;
        }

        var result = sb.ToString();
        return result;
    }

    private string GetMainImage(Product product)
    {
        var firstImage = product.Images.FirstOrDefault();
        if (firstImage != null)
        {
            return $"catalog/{manufacturer_ftp_path}/products/" + firstImage.CreateMD5() + ".jpg";
        }
        else
        {
            return "catalog/placeholder.jpg";
        }
    }

    private string BuildDescription(Product product)
    {
        var sb = new StringBuilder();


        if (Regex.IsMatch(product.DescriptionMarkup, "<img(.*?)>"))
        {
            product.DescriptionMarkup = Regex.Replace(product.DescriptionMarkup, "<img(.*?)>", string.Empty);
        }

        if (Regex.IsMatch(product.DescriptionMarkup, "<a(.*?)>(.*?)</a>"))
        {
            product.DescriptionMarkup = Regex.Replace(product.DescriptionMarkup, "<a(.*?)>(.*?)</a>", "<strong>$2</strong>");
        }

        if (Regex.IsMatch(product.DescriptionMarkup, "<iframe>"))
        {

        }

        sb.Append(product.DescriptionMarkup ?? string.Empty);
        sb.Append(product.TechDescriptionMarkup ?? string.Empty);

        var result = sb.ToString().Replace("\r", string.Empty).TrimHtml();

        //сделать форматирование OL списка поставки, пример как должно быть
        //....
        //<p><strong>Комплект поставки:</strong></p>
        //<ol>
        //<li>Тахометр МЕГЕОН 18011- 1 шт.</li>
        //<li>Батарея 9 В тип 6F22 (Крона) - 1 шт.</li>
        //<li>Сумка для переноски и хранения - 1 шт.</li>
        //<li>Светоотражающая клейкая лента - 2 шт.</li>
        //<li>Гарантийный талон - 1 экз.</li>
        //<li>Руководство по эксплуатации/паспорт – 1 экз.</li>
        //</ol>
        //....


        ReplaceComplectation(ref result);

        result = Regex.Replace(result, "<ul style=\"text-indent: 15px;\">", "<ul>");
        result = Regex.Replace(result, "<table(.*?)>", "<div class=\"table-responsive\"><table class=\"table\">");
        result = Regex.Replace(result, "</table>", "</table></div>");
        result = Regex.Replace(result, "<p> *</?p>", "");

        return result;
    }

    // Заменяем обычную разметку на список <ol> распарсив его
    private void ReplaceComplectation(ref string result)
    {
        var chStr = "<p><strong>Комплект поставки:</strong></p>";
        var oldLength = result.Length;
        var newResult = result.Replace(chStr, "<p><strong>Комплект поставки:</strong></p>\n<ol>");

        if (oldLength < newResult.Length)
        {
            var startTagIndex = newResult.IndexOf(chStr) + chStr.Length + 1;
            var nextTagIndex = newResult.IndexOf("<p>", startTagIndex);

            if (nextTagIndex > startTagIndex && nextTagIndex >= 1 && nextTagIndex < newResult.Length)
            {
                newResult = newResult.Insert(nextTagIndex, "</ol>\n");

                var list = newResult.Substring(startTagIndex, nextTagIndex - startTagIndex + "</ol>\n".Length);
                var replacedList = Regex.Replace(list, @"(\d+)\. (.*)", "<li>$2</li>");

                newResult = newResult.Replace(list, replacedList);
            }
        }

        result = newResult;
    }

    internal string GetPdfSql()
    {
        string yandexDiskFolder = "https://disk.yandex.ru/d/V1KNVJO3SY3ROw/megeon";

        var sb = new StringBuilder("INSERT INTO `oc_product_pdf`(`product_id`, `name`, `path`) VALUES ");

        foreach (var p in products)
        {
            string product_id = $"(SELECT product_id FROM oc_product WHERE model = '{p.Model}')";

            foreach (var pdf in p.Instructions)
            {
                string path = $"{yandexDiskFolder}/{pdf.Uri.CreateMD5() + ".pdf"}";

                sb.AppendLine($"({product_id}, '{pdf.Name}', '{path}'),");
            }
        }

        var result = sb.ToString().Trim('\r', '\n', '\t', ' ', ',') + ";";

        return result;

    }

    internal string GetDescUpdateSql()
    {
        var sb = new StringBuilder();

        sb.AppendLine("UPDATE oc_product_description");
        sb.AppendLine("JOIN oc_product ON oc_product.product_id = oc_product_description.product_id");
        sb.AppendLine("SET oc_product_description.description = case oc_product.model");

        foreach (var product in products)
        {
            string escapedDesc = HttpUtility.HtmlEncode(BuildDescription(product));
            sb.AppendLine($"WHEN '{product.Model}' THEN '{escapedDesc}'");
        }

        sb.AppendLine("ELSE ''");
        sb.AppendLine("END");

        sb.AppendLine($"WHERE oc_product.product_id >= {START_ETK_ID}");

        var result = sb.ToString();
        return result;
    }

    internal async Task<string> GetDimensionsSql(bool getAll)
    {
        await LoadXml();

        var sb = new StringBuilder();
        if (getAll)
        {
            foreach (var kvp in modelDimension)
            {
                var dim = kvp.Value;
                string product_id = $"(SELECT product_id FROM oc_product WHERE model = '{kvp.Key}')";

                sb.AppendLine($"UPDATE IGNORE oc_product SET length = {dim.Length}, width = {dim.Width}, height = {dim.Height}, weight = {dim.Weight} WHERE product_id = {product_id};");
            }
        }
        else
        {
            foreach (var p in products)
            {
                if (modelDimension.TryGetValue(p.Model, out var dim))
                {
                    string product_id = $"(SELECT product_id FROM oc_product WHERE model = '{p.Model}')";

                    sb.AppendLine($"UPDATE IGNORE oc_product SET length = {dim.Length}, width = {dim.Width}, height = {dim.Height}, weight = {dim.Weight} WHERE product_id = {product_id};");
                }
            }
        }

        var result = sb.ToString().Trim('\r', '\n', '\t', ' ', ',') + ";";

        return result;
    }

    private async Task LoadXml()
    {
        await Task.CompletedTask;
        var client = new HttpClient();
        string param = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", param);
        var xmlData = await client.GetStringAsync("http://megeon-pribor.ru/dealers/catalog/yml_catalog.xml");
        var doc = XDocument.Parse(xmlData);

        modelDimension.Clear();

        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

        foreach (var offer in doc.Descendants("offer"))
        {
            //<model>МЕГЕОН 11012</model>
            var model = offer.Descendants("model").FirstOrDefault()?.Value ?? string.Empty;

            if (!string.IsNullOrEmpty(model) && !modelDimension.ContainsKey(model))
            {
                var paramNodes = offer.Descendants("param");

                var length = paramNodes.FirstOrDefault(n => n.Attributes().FirstOrDefault(a => a.Name == "name").Value == "Глубина").Value ?? "0";
                var width = paramNodes.FirstOrDefault(n => n.Attributes().FirstOrDefault(a => a.Name == "name").Value == "Ширина").Value ?? "0";
                var height = paramNodes.FirstOrDefault(n => n.Attributes().FirstOrDefault(a => a.Name == "name").Value == "Высота").Value ?? "0";
                var weight = paramNodes.FirstOrDefault(n => n.Attributes().FirstOrDefault(a => a.Name == "name").Value == "Вес").Value ?? "0";

                var dim = new ProductDimensions()
                {
                    Length = Math.Round(decimal.Parse(length) * 1000, 0).ToString("F0"),
                    Width = Math.Round(decimal.Parse(width) * 1000, 0).ToString("F0"),
                    Height = Math.Round(decimal.Parse(height) * 1000, 0).ToString("F0"),
                    Weight = Math.Round(decimal.Parse(weight), 2).ToString("F2"),
                };
                modelDimension.Add(model, dim);

                //<param name="Глубина">0.22</param>
                //<param name="Ширина">0.12</param>
                //<param name="Высота">0.041</param>
                //<param name="Вес">0.22</param>
            }
        }
    }
}
