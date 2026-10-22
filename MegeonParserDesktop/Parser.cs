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

    private readonly string HOST = "https://megeon-pribor.ru";
    public string CurrentModel { get; private set; }

    public Func<string, string[], string> conflictResolver { get; set; }

    private HttpClient CreateHttpClient()
    {
        handler = new HttpClientHandler()
        {
            // Редиректы обрабатываем вручную: при автоматическом 301/302 POST превращается в GET
            // и тело формы теряется -> сайт отдает дефолтный список товаров.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        return client;
    }

    // GET с ручным следованием редиректам
    private async Task<string> GetStringFollowingRedirects(HttpClient c, string url)
    {
        for (int i = 0; i < 5; i++)
        {
            using var resp = await c.GetAsync(url);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location != null)
            {
                url = new Uri(new Uri(url), resp.Headers.Location).ToString();
                continue;
            }
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        throw new InvalidOperationException("Слишком много редиректов: " + url);
    }

    // POST с сохранением метода и тела при редиректах (301/302/303/307/308)
    private async Task<string> PostFormFollowingRedirects(HttpClient c, string url, IEnumerable<KeyValuePair<string, string>> form)
    {
        for (int i = 0; i < 5; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Referrer = new Uri(HOST + "/");
            req.Headers.Add("Origin", HOST);

            using var resp = await c.SendAsync(req);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location != null)
            {
                url = new Uri(new Uri(url), resp.Headers.Location).ToString();
                continue;
            }
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        throw new InvalidOperationException("Слишком много редиректов: " + url);
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
        var form = new[]
        {
            new KeyValuePair<string, string>("setsearchdata", "1"),
            new KeyValuePair<string, string>("category_id", "0"),
            new KeyValuePair<string, string>("search_type", "exact"),
            new KeyValuePair<string, string>("search", model),
        };

        using (var client = CreateHttpClient())
        {
            // получаем сессионные cookie сайта (PHPSESSID и т.п.)
            await GetStringFollowingRedirects(client, HOST + "/");

            string responseString = await PostFormFollowingRedirects(client, HOST + "/katalog/search/result", form);

            var doc = new HtmlDocument();
            doc.LoadHtml(responseString);

            try { File.WriteAllText("last_search_response.html", responseString); } catch { }

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

            var fullMatch = titleToHref.Keys.Count(title => title.Contains(model));

            if (fullMatch == 1)
            {
                var title = titleToHref.Keys.Single(title => title.Contains(model));
                var p = new Product() { Uri = titleToHref[title] };
                await ParseProductDetails(p);
                return p;
            }
            else
            {
                string selectedTitle = conflictResolver.Invoke(model, titleToHref.Keys.ToArray());

                var p = new Product();

                if (selectedTitle.StartsWith("https"))
                {
                    p.Uri = selectedTitle;
                }
                else if (!string.IsNullOrWhiteSpace(selectedTitle))
                {
                    p.Uri = titleToHref[selectedTitle];
                }
                await ParseProductDetails(p);
                return p;
            }
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

            var str = await GetStringFollowingRedirects(client, uri);

            doc.LoadHtml(str);
            return doc;
        }
        catch
        {
            return null;
        }

    }
}
