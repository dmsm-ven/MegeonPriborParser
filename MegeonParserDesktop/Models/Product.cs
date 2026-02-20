using System.Text.Json.Serialization;

namespace MegeonParserDesktop.Models;

public class Product
{
    public string Name { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string DescriptionMarkup { get; set; } = string.Empty;
    public string TechDescriptionMarkup { get; set; } = string.Empty;
    public string Breadcrumbs { get; set; } = string.Empty;

    [JsonIgnore]
    public string ProductType => Name.Replace(Model, string.Empty).TrimHtml();

    public List<string> Images { get; set; } = new List<string>();
    public List<Pdf> Instructions { get; set; } = new List<Pdf>();
}
