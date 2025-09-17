using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using ItemInterpreter.Data;

namespace ItemInterpreter.Loaders
{
    public static class ItemXmlLoader
    {
        public static List<ItemDefinition> Load(string path)
        {
            var doc = XDocument.Load(path);
            var items = new List<ItemDefinition>();

            foreach (var section in doc.Descendants("Section"))
            {
                int sectionIndex = int.Parse(section.Attribute("Index")?.Value ?? "0");
                string sectionName = section.Attribute("Name")?.Value ?? $"Section {sectionIndex}";

                foreach (var item in section.Elements("Item"))
                {
                    var def = new ItemDefinition
                    {
                        Section = sectionIndex,
                        SectionName = sectionName,
                        Index = int.Parse(item.Attribute("Index")?.Value ?? "0"),
                        Name = item.Attribute("Name")?.Value ?? "Unknown",
                        Slot = int.Parse(item.Attribute("Slot")?.Value ?? "-1"),
                        Width = int.Parse(item.Attribute("Width")?.Value ?? "1"),
                        Height = int.Parse(item.Attribute("Height")?.Value ?? "1"),
                        Type = int.Parse(item.Attribute("Type")?.Value ?? "0"),
                        Excellent = item.Attribute("Option")?.Value == "1",
                        CanBeSold = item.Attribute("SellToNPC")?.Value == "1",
                        CanBeStored = item.Attribute("StoreWarehouse")?.Value == "1",
                        Repairable = item.Attribute("Repair")?.Value == "1",
                        Requirements = new Dictionary<string, int>
                        {
                            ["Level"] = int.Parse(item.Attribute("ReqLevel")?.Value ?? "0"),
                            ["Strength"] = int.Parse(item.Attribute("ReqStrength")?.Value ?? "0"),
                            ["Dexterity"] = int.Parse(item.Attribute("ReqDexterity")?.Value ?? "0"),
                            ["Energy"] = int.Parse(item.Attribute("ReqEnergy")?.Value ?? "0"),
                            ["Vitality"] = int.Parse(item.Attribute("ReqVitality")?.Value ?? "0"),
                            ["Command"] = int.Parse(item.Attribute("ReqCommand")?.Value ?? "0")
                        }
                    };

                    items.Add(def);
                }
            }


            return items;
        }
    }
}
