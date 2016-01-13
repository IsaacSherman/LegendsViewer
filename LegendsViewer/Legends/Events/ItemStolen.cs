using System;
using System.Collections.Generic;
using System.Linq;
using LegendsViewer.Legends.Parser;

namespace LegendsViewer.Legends.Events
{
    public class ItemStolen : WorldEvent
    {
        public int StructureID { get; set; }
        public Structure Structure { get; set; }
        public string ItemType { get; set; }
        public int ItemSubType { get; set; }
        public string Material { get; set; }
        public int MaterialTypeID { get; set; }
        public int MaterialIndex { get; set; }
        public HistoricalFigure Thief { get; set; }
        public Entity Entity { get; set; }
        public Site Site { get; set; }
        public Site ReturnSite { get; set; }

        public ItemStolen(List<Property> properties, World world)
            : base(properties, world)
        {
            ItemType = "UNKNOWN ITEM";
            Material = "UNKNOWN MATERIAL";
            foreach (Property property in properties)
            {
                switch (property.Name)
                {
                    case "histfig": Thief = world.GetHistoricalFigure(Convert.ToInt32(property.Value)); break;
                    case "site_id": Site = world.GetSite(Convert.ToInt32(property.Value)); break;
                    case "entity": Entity = world.GetEntity(Convert.ToInt32(property.Value)); break;
                    case "item_type": ItemType = property.Value.Replace("_", " "); break;
                    case "mat": Material = property.Value; break;
                    case "item_subtype": ItemSubType = Convert.ToInt32(property.Value); break;
                    case "mattype": MaterialIndex = Convert.ToInt32(property.Value); break;
                    case "matindex": ItemSubType = Convert.ToInt32(property.Value); break;
                    case "site": if (Site == null) { Site = world.GetSite(Convert.ToInt32(property.Value)); } else property.Known = true; break;
                    case "structure": StructureID = Convert.ToInt32(property.Value); break;
                }
            }
            if (Site != null)
            {
                Structure = Site.Structures.FirstOrDefault(structure => structure.ID == StructureID);
            }
            Thief.AddEvent(this);
            Site.AddEvent(this);
            Entity.AddEvent(this);
            Structure.AddEvent(this);
        }
        public override string Print(bool path = true, DwarfObject pov = null)
        {
            string eventString = this.GetYearTime();
            eventString += " a ";
            eventString += Material + " " + ItemType;
            eventString += " was stolen from ";
            if (Site != null)
            {
                eventString += Site.ToLink(path, pov);
            }
            else
            {
                eventString += "UNKNOWN SITE";
            }
            eventString += " by ";
            if (Thief != null)
            {
                eventString += Thief.ToLink(path, pov);
            }
            else
            {
                eventString += "UNKNOWN HISTORICAL FIGURE";
            }

            if (ReturnSite != null)
            {
                eventString += " and brought to " + ReturnSite.ToLink();
            }

            eventString += ". ";
            eventString += PrintParentCollection(path, pov);
            return eventString;
        }
    }
}