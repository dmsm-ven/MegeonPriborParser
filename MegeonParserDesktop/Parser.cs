using HtmlAgilityPack;
using MegeonParserDesktop.Models;
using System.IO;
using System.Net;
using System.Net.Http;

namespace MegeonParserDesktop;

public class ProductParser
{
    private HttpClient client;
    private HttpClientHandler handler;

    private readonly string HOST = "http://www.megeon-pribor.ru";
    public string CurrentModel { get; private set; }

    private HttpClient CreateHttpClient()
    {
        handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("HOST", "www.megeon-pribor.ru");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        client.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
        return client;
    }

    public async Task<List<Product>> Parse(IEnumerable<string> modelsToParse,
        IProgress<double> indicator)
    {
        var list = new List<Product>();

        int total = modelsToParse.Count();
        int current = 0;

        foreach (var model in modelsToParse)
        {
            CurrentModel = model;
            var product = await SearchAndParse(model);

            if (product == null && model.EndsWith("с поверкой"))
            {
                product = await SearchAndParse(model.Replace("с поверкой", string.Empty));
            }

            if (product != null)
            {
                product.Model = CurrentModel;
                list.Add(product);
            }
            else
            {
                File.WriteAllLines("errors.txt", new[] { model });
            }

            indicator?.Report((double)++current / total);
        }

        return list;
    }

    private async Task<Product> SearchAndParse(string model)
    {
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("setsearchdata", "1"),
            new KeyValuePair<string, string>("category_id", "0"),
            new KeyValuePair<string, string>("search_type", "exact"),
            new KeyValuePair<string, string>("search", model),
        });

        using (var client = CreateHttpClient())
        {
            await client.GetStringAsync("https://www.megeon-pribor.ru/");

            IEnumerable<Cookie> responseCookies = handler.CookieContainer.GetCookies(new Uri("http://www.megeon-pribor.ru")).Cast<Cookie>();
            foreach (var c in responseCookies)
            {
                var x = c;
            }


            var response = await client.PostAsync("https://www.megeon-pribor.ru/katalog/search/result", formContent);
            string responseString = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(responseString);

            Product p = await FindValidProduct(model, doc);
            return p;
        }
    }

    private async Task<Product> FindValidProduct(string model, HtmlDocument doc)
    {
        if (doc.DocumentNode.SelectSingleNode("//div[@class='jshop_list_product']//div[@class='name']/a") == null) { return null; }

        var results = doc.DocumentNode
            .SelectNodes("//div[@class='jshop_list_product']//div[@class='name']/a")
            .ToArray();

        if (results.Length == 1)
        {
            var p = new Product() { Uri = HOST + results[0].GetAttributeValue("href", null) };
            await ParseProductDetails(p);
            return p;
        }
        else
        {
            Dictionary<string, string> titleToHref = results
                .ToDictionary(a => a.InnerText.TrimHtml(), a => HOST + a.GetAttributeValue("href", null));

            throw new NotImplementedException();
            //string selectedTitle = await ConflictResolver(titleToHref.Keys.ToList());
            //if (selectedTitle != null)
            //{
            //    var p = new Product() { Uri = titleToHref[selectedTitle] };
            //    await ParseProductDetails(p);
            //    return p;
            //}
        }

        return null;
    }

    private async Task ParseProductDetails(Product product)
    {
        var doc = await GetDocument(product.Uri);
        product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText;
        product.DescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[@class='jshop_prod_description']")?.InnerHtml;
        product.TechDescriptionMarkup = doc.DocumentNode.SelectSingleNode("//div[@class='jshop_prod_description_table']")?.InnerHtml;

        if (doc.DocumentNode.SelectSingleNode("//span[@id='list_product_image_thumb']//img[@class='image_thumb']/..") != null)
        {
            var images = doc.DocumentNode
                .SelectNodes("//span[@id='list_product_image_thumb']//img[@class='image_thumb']/..")
                .Select(a => a.GetAttributeValue("href", string.Empty))
                .Distinct()
                .ToList();

            if (images.Count > 2)
            {
                var first = images.First();
                images.RemoveAt(0);
                images.Insert(1, first);
            }

            product.Images.AddRange(images);
        }

        if (doc.DocumentNode.SelectSingleNode("//div[@class='list_user_guige']//a[contains(@href, '.pdf')]/span/..") != null)
        {
            var pdfs = doc.DocumentNode
                .SelectNodes("//div[@class='list_user_guige']//a[contains(@href, '.pdf')]/span/..")
                .Select(a => new Pdf()
                {
                    Uri = a.GetAttributeValue("href", null),
                    Name = a.SelectSingleNode("./span")?.InnerText.TrimHtml()
                })
                .ToList();
            product.Instructions.AddRange(pdfs);

        }
    }

    private async Task<HtmlDocument> GetDocument(string uri)
    {
        try
        {
            var doc = new HtmlDocument();

            var str = await client.GetStringAsync(uri);

            doc.LoadHtml(str);
            return doc;
        }
        catch
        {
            return null;
        }

    }
}
