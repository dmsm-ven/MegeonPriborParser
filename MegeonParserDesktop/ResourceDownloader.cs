using MegeonParserDesktop.Models;
using System.IO;
using System.Net;
using System.Net.Http;

namespace MegeonParserDesktop;

internal class ResourceDownloader
{
    private readonly HttpClient client;
    public ResourceDownloader()
    {

        client = new HttpClient(new HttpClientHandler() { CookieContainer = new CookieContainer(), AllowAutoRedirect = true });
        client.Timeout = TimeSpan.FromMinutes(3);
    }
    internal async Task DownloadResources(IReadOnlyList<Product> products, string folder, IProgress<double> indicator)
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
}
