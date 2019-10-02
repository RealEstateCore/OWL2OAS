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
        // Dictionary mapping some common XSD data types to corresponding OSA data types and formats, see
        // https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.0.md#dataTypeFormat
        static readonly Dictionary<string, (string, string)> xsdOsaMappings = new Dictionary<string, (string, string)>
        {
            {"boolean",("boolean","") },
            {"byte",("string","byte") },
            {"base64Binary",("string","byte") },
            {"dateTime",("string","date-time") },
            {"dateTimeStamp",("string","date-time") },
            {"double",("number","double") },
            {"float",("number","float") },
            {"int",("integer","int32") },
            {"integer",("integer","int32") },
            {"long",("integer","int64") },
            {"string",("string","") },
        };

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

            // Iterate over all leaf classes
            foreach (OntologyClass c in g.OwlClasses.Where(oClass => oClass.IsBottomClass))
            {
                // Get human-readable label for API (should this be fetched from other metadata property?)
                // TODO: pluralization metadata for clean API?
                string classLabel = GetLabel(c, "en");

                // Create schema for class
                OASDocument.Schema schema = new OASDocument.Schema();

                // TODO: parse superclasses for any restrictions defining max/min constraints, to be used inside loop
                schema.properties = new Dictionary<string, OASDocument.Property>();

                // Add id and label for all entries
                OASDocument.Property idProperty = new OASDocument.Property();
                idProperty.type = "string";
                schema.properties.Add("id", idProperty);
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);

                foreach (OntologyProperty property in c.IsDomainOf)
                {
                    // We only process (named) object and data properties with singleton ranges.
                    if ((property.IsObjectProperty() || property.IsDataProperty()) && property.Ranges.Count() == 1) {

                        OASDocument.Property outputProperty = new OASDocument.Property();
                        string propertyLocalName = ((UriNode)property.Resource).GetLocalName();

                        // Default to string representation for unknown types
                        outputProperty.type = "string";

                        if (property.Ranges.First().IsXsdDatatype())
                        {
                            // Parse XSD type into OAS type and format
                            string rangeXsdType = ((UriNode)property.Ranges.First().Resource).GetLocalName();

                            // Look through well-known XSD type mapping (note: not all XSD types are covered)
                            if (xsdOsaMappings.ContainsKey(rangeXsdType))
                            {
                                outputProperty.type = xsdOsaMappings[rangeXsdType].Item1;
                                string format = xsdOsaMappings[rangeXsdType].Item2;
                                if (format.Length > 0) {
                                    outputProperty.format = format;
                                }
                            }
                        }

                        // TODO: add max/min constraints here
                        schema.properties.Add(propertyLocalName, outputProperty);
                    }
                }
                // TODO: figure out which properties that have min 1 constraint; use to populate below
                schema.required = new List<string> { "id" };

                /**/
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
