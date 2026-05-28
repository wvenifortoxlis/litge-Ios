using System.Xml.Linq;

namespace LitGe
{
    public class TableOfContentReader : ITableOfContentReader
    {
        private static readonly XNamespace NS = "http://www.daisy.org/z3986/2005/ncx/";

        private static void ReadChapters(XElement element, IList<Chapter> result)
        {
            IEnumerable<XElement> nodes = element.Descendants(NS + "navPoint");
            foreach (XElement node in nodes)
            {
                Chapter chapter = new Chapter();
                int playOrder = 0;
                if (!Int32.TryParse(node.Attribute("playOrder").Value, out playOrder))
                    continue;
                chapter.PlayOrder = playOrder;
                chapter.Source = node.Element(NS + "content").Attribute("src").Value;
                chapter.Title = node.Element(NS + "navLabel").Element(NS + "text").Value;
                if (node.Parent != null && node.Parent.Attributes().Any(a => a.Name == "playOrder"))
                {
                    int parentOrder = 0;
                    XElement? currentNode = node.Parent;
                    while (currentNode != null)
                    {
                        if (!currentNode.Attributes().Any(a => a.Name == "playOrder"))
                        {
                            currentNode = currentNode.Parent;
                            continue;
                        }
                        if (
                            Int32.TryParse(
                                currentNode.Attribute("playOrder").Value,
                                out parentOrder
                            )
                        )
                            break;
                        currentNode = currentNode.Parent;
                    }
                    if (parentOrder != 0)
                    {
                        IEnumerable<Chapter> flattened = result.Flatten(x => x.Chapters);
                        Chapter parent = flattened.Single(c => c.PlayOrder == parentOrder);
                        chapter.Parent = parent;
                        parent.Chapters.Add(chapter);
                    }
                    else
                    {
                        result.Add(chapter);
                    }
                }
                else
                    result.Add(chapter);
            }
        }

        /// <summary>
        /// Parses epub table of content.
        /// </summary>
        /// <param name="stream">The stream containing TOC buffer.</param>
        /// <returns>A list of parsed TOCs.</returns>
        public IEnumerable<Chapter> Parse(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                //var str = reader.ReadToEnd();
                XDocument doc = XDocument.Load(stream);
                List<Chapter> result = new List<Chapter>(30);
                ReadChapters((doc.FirstNode.NextNode as XElement).Element(NS + "navMap"), result);
                return result;
            }
        }
    }
}
