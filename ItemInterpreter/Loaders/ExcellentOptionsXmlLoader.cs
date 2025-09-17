using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ItemInterpreter.Data;

namespace ItemInterpreter.Loaders
{
    public static class ExcellentOptionsXmlLoader
    {
        public static List<ExcellentOptionDefinition> Load(string path)
        {
            var doc = XDocument.Load(path);
            var options = new List<ExcellentOptionDefinition>();

            foreach (var group in doc.Descendants("Item"))
            {
                int itemType = int.Parse(group.Attribute("Cat")?.Value ?? "0");
                int index = int.Parse(group.Attribute("Index")?.Value ?? "0");

                foreach (var option in group.Elements("Option"))
                {
                    options.Add(new ExcellentOptionDefinition
                    {
                        ItemType = itemType,
                        Index = index,
                        Kind = int.Parse(option.Attribute("Index")?.Value ?? "0"),
                        Name = option.Attribute("Text")?.Value ?? "Unknown Option"
                    });
                }
            }

            return options;
        }
    }
}
