using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace XmlFunctionalDiff
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: XmlDiff <file1.xml> <file2.xml>");
                return;
            }

            Console.WriteLine("Compairing files...");
            Console.WriteLine($"\tFile 1: {args[0]}");
            Console.WriteLine($"\tFile 2: {args[1]}");

            XDocument doc1 = _loadAndNormalize(args[0]);
            XDocument doc2 = _loadAndNormalize(args[1]);

            List<string> diffs = new List<string>();
            _compareElements(doc1.Root, doc2.Root, "/" + doc1.Root.Name, diffs);

            if (diffs.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("XML files are functionally equivalent.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("XML files are functionally different.");
                Console.ResetColor();
                Console.WriteLine();
                foreach (string diff in diffs)
                {
                    Console.WriteLine(diff);
                }
            }

            Console.ReadKey();
        }

        private static XDocument _loadAndNormalize(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true
            };

            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                XDocument doc = XDocument.Load(reader);
                XElement normalizedRoot = _normalizeElement(doc.Root);
                return new XDocument(normalizedRoot);
            }
        }

        static XElement _normalizeElement(XElement element)
        {
            IEnumerable<XAttribute> attributes = element.Attributes()
                .OrderBy((XAttribute a) => a.Name.ToString());

            IEnumerable<XElement> childElements = element.Elements()
                .Select(_normalizeElement)
                .OrderBy((XElement e) => e.Name + e.ToString(SaveOptions.DisableFormatting));

            IEnumerable<XText> textNodes = element.Nodes()
                .OfType<XText>()
                .Select((XText t) => new XText(t.Value.Trim()));

            return new XElement(
                element.Name,
                attributes,
                childElements,
                textNodes
            );
        }

        private static void _compareElements(
            XElement a,
            XElement b,
            string path,
            List<string> diffs
            )
        {
            if (a.Name != b.Name)
            {
                diffs.Add($"{path}{Environment.NewLine}  Element name differs: {a.Name} vs {b.Name}{Environment.NewLine}");
                return;
            }

            Dictionary<XName, string> attributesA = a.Attributes().ToDictionary(
                (XAttribute x) => x.Name,
                (XAttribute x) => x.Value
                );

            Dictionary<XName, string> attributesB = b.Attributes().ToDictionary(
                (XAttribute x) => x.Name,
                (XAttribute x) => x.Value
                );

            foreach (XName name in attributesA.Keys.Union(attributesB.Keys))
            {
                attributesA.TryGetValue(name, out string valA);
                attributesB.TryGetValue(name, out string valB);

                if (valA != valB)
                {
                    diffs.Add($"{path}/@{name}{Environment.NewLine}  File1: {(valA ?? "<missing>")}{Environment.NewLine}  File2: {(valB ?? "<missing>")}{Environment.NewLine}");
                }
            }

            bool hasChildElements = a.Elements().Any() || b.Elements().Any();
            if (!hasChildElements)
            {
                string textA = a.Value.Trim();
                string textB = b.Value.Trim();

                if (textA != textB)
                {
                    diffs.Add($"{path}{Environment.NewLine}  Text differs:{Environment.NewLine}  File1: {textA}{Environment.NewLine}  File2: {textB}{Environment.NewLine}");
                }
            }

            Dictionary<string, List<XElement>> childrenA = a.Elements()
                .GroupBy(_elementKey)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            Dictionary<string, List<XElement>> childrenB = b.Elements()
                .GroupBy(_elementKey)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            foreach (string key in childrenA.Keys.Union(childrenB.Keys))
            {
                if(!childrenA.TryGetValue(key, out List<XElement> listA))
                {
                    diffs.Add($"{path}/{key}{Environment.NewLine}  Present in File2, missing in File1{Environment.NewLine}");
                    continue;
                }

                if(!childrenB.TryGetValue(key, out List<XElement> listB))
                {
                    diffs.Add($"{path}/{key}{Environment.NewLine}  Present in File1, missing in File2{Environment.NewLine}");
                    continue;
                }

                int max = Math.Max(listA.Count, listB.Count);

                for (int i = 0; i < max; i++)
                {
                    if (i >= listA.Count || i >= listB.Count)
                    {
                        diffs.Add($"{path}/{key}{Environment.NewLine}  Element count differs{Environment.NewLine}");
                        continue;
                    }

                    _compareElements(
                        a: listA[i],
                        b: listB[i],
                        path: $"{path}/{key}",
                        diffs: diffs
                        );
                }
            }
        }

        static string _elementKey(XElement e)
        {
            XAttribute keyAttr = e.Attribute("key") ?? e.Attribute("name") ?? e.Attribute("id");

            if (keyAttr != null)
            {
                return $"{e.Name}[@{keyAttr.Name}='{keyAttr.Value}']";
            }

            return e.Name.ToString();
        }
    }
}
