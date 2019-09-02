using System;
using System.Collections.Generic;
using System.Text;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load ontology graph
            OntologyGraph g = new OntologyGraph();
            FileLoader.Load(g, args[0]);

            // Create OAS object
            OASDocument document = new OASDocument();

            OASDocument.Info info = new OASDocument.Info();
            info.title = "RealEstateCore API";
            info.version = "\"3.1\"";
            OASDocument.License license = new OASDocument.License();
            license.name = "MIT";
            info.license = license;
            document.info = info;

            OASDocument.Components components = new OASDocument.Components();
            Dictionary<string, OASDocument.Schema> schemas = new Dictionary<string, OASDocument.Schema>();
            foreach (OntologyClass c in g.OwlClasses)
            {
                string classLabel = GetLabel(c, "en");
                OASDocument.Schema schema = new OASDocument.Schema();
                schemas.Add(classLabel, schema);
            }
            components.schemas = schemas;
            document.components = components;

            Dictionary<string, OASDocument.Path> paths = new Dictionary<string, OASDocument.Path>();
            foreach (OntologyClass c in g.OwlClasses)
            {
                string classLabel = GetLabel(c, "en");
                OASDocument.Path path = new OASDocument.Path();
                OASDocument.Get get = new OASDocument.Get();
                Dictionary<string, OASDocument.Response> responses = new Dictionary<string, OASDocument.Response>();
                OASDocument.Response response = new OASDocument.Response();
                response.description = "A paged array of '" + classLabel + "' objects.";
                responses.Add("200", response);
                get.summary = "Get all '" + classLabel + "' objects.";
                get.responses = responses;
                path.get = get;
                paths.Add("/" + classLabel, path);
            }
            document.paths = paths;

            DumpAsYaml(document);
            
            // Keep window open during debug
            Console.WriteLine("Press any key to quit.");
            Console.ReadKey();
        }

        private static void DumpAsYaml(object data)
        {
            var stringBuilder = new StringBuilder();
            var serializer = new Serializer();
            stringBuilder.AppendLine(serializer.Serialize(data));
            Console.WriteLine(stringBuilder);
            Console.WriteLine("");
        }

        private static string GetLabel(OntologyResource ontologyResource, string language)
        {
            foreach (ILiteralNode label in ontologyResource.Label)
            {
                if (label.Language == language)
                {
                    return label.Value;
                }
            }
            return ontologyResource.Resource.ToString();
        }
    }
}
