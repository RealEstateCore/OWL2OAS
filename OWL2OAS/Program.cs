using System;
using System.Collections.Generic;
using System.Linq;
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
            //FileLoader.Load(g, args[0]);
            EmbeddedResourceLoader.Load(g, "OWL2OAS.rec-core.rdf, OWL2OAS");

            // Create OAS object
            OASDocument document = new OASDocument();

            // Create OAS Info header
            OASDocument.Info info = new OASDocument.Info();
            info.title = "RealEstateCore API";
            info.version = "3.1";
            OASDocument.License license = new OASDocument.License();
            license.name = "MIT";
            info.license = license;
            document.info = info;

            // Parse OWL classes. For each class, create a schema and a path
            document.components = new OASDocument.Components();
            Dictionary<string, OASDocument.Schema> schemas = new Dictionary<string, OASDocument.Schema>();
            Dictionary<string, OASDocument.Path> paths = new Dictionary<string, OASDocument.Path>();
            foreach (OntologyClass c in g.OwlClasses)
            {
                // Get human-readable label for API (should this be fetched from other metadata property?)
                // TODO: pluralization metadata for clean API?
                string classLabel = GetLabel(c, "en");

                // Create schema for class
                OASDocument.Schema schema = new OASDocument.Schema();

                foreach (OntologyProperty property in c.IsDomainOf)
                {
                    // This is an extraordinarily convoluted way of checking for object property type. 
                    if (property.IsObjectProperty()) {
                        //property.Ranges.fi
                    }
                }

                schema.required = new List<string> { "id", "label" };
                schema.properties = new Dictionary<string, OASDocument.Property>();
                OASDocument.Property idProperty = new OASDocument.Property();
                idProperty.type = "string";
                schema.properties.Add("id", idProperty);
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);
                schemas.Add(classLabel, schema);

                // Create path for class
                OASDocument.Path path = new OASDocument.Path();
                paths.Add("/" + classLabel, path);

                // Create each of the HTTP methods
                // TODO: PUT, PATCH, etc
                path.get = new OASDocument.Get();
                path.get.summary = "Get all '" + classLabel + "' objects.";
                path.get.responses = new Dictionary<string, OASDocument.Response>();
                
                // Create each of the HTTP response types
                OASDocument.Response response = new OASDocument.Response();
                response.description = "A paged array of '" + classLabel + "' objects.";
                path.get.responses.Add("200", response);

                response.content = new Dictionary<string, OASDocument.Content>();
                OASDocument.Content content = new OASDocument.Content();
                response.content.Add("application/jsonld", content);

                content.schema = new Dictionary<string, string>();
                content.schema.Add("$ref", "#/components/schemas/" + classLabel);
            }
            document.components.schemas = schemas;
            document.paths = paths;

            DumpAsYaml(document);
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
                    return label.Value.Replace(" ", "");
                }
            }
            return ontologyResource.Resource.ToString();
        }
    }
}
