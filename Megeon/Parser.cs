using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Megeon.ParserLib
{
    public class ProductParser
    {
        private HttpClient client;
        private HttpClientHandler handler;

        private readonly string HOST = "http://www.megeon-pribor.ru";

        public Func<List<string>, Task<string>> ConflictResolver { get; set; }
        public string CurrentModel { get; private set; }

        public ProductParser()
        {

        }

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
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("User-Agent", "PostmanRuntime/7.26.8");
            client.DefaultRequestHeaders.Add("HOST", "www.megeon-pribor.ru");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            client.DefaultRequestHeaders.Add("Postman-token", "5b8cf1de-ba9d-48ae-9ca1-91010c72b450");
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

                string selectedTitle = await ConflictResolver(titleToHref.Keys.ToList());
                if (selectedTitle != null)
                {
                    var p = new Product() { Uri = titleToHref[selectedTitle] };
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
}
