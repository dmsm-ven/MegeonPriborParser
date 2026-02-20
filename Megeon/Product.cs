using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Megeon.ParserLib
{
    public class Product
    {
        public string Name { get; set; }
        public string Uri { get; set; }
        public string Model { get; set; }
        public string DescriptionMarkup { get; set; }
        public string TechDescriptionMarkup { get; set; }
        public string Breadcrumbs { get; set; }

        [JsonIgnore]
        public string ProductType
        {
            get
            {
                return Name.Replace(Model, String.Empty).TrimHtml();
            }
        }

        public List<string> Images { get; set; } = new List<string>();
        public List<Pdf> Instructions { get; set; } = new List<Pdf>();
    }

    public class Pdf
    {
        public string Name { get; set; }
        public string Uri { get; set; }
    }
}
