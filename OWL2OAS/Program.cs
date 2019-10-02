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
            EmbeddedResourceLoader.Load(g, "OWL2OAS.rec-core-3.0.rdf, OWL2OAS");
            IUriNode rootOntologyUriNode = g.CreateUriNode(g.BaseUri);
            Ontology rootOntology = new Ontology(rootOntologyUriNode, g);
            foreach (Ontology import in rootOntology.Imports)
            {
                LoadImport(import, g);
            }

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
            foreach (OntologyClass c in g.OwlClasses.Where(oClass => oClass.IsNamed()))
            {
                // Get human-readable label for API (should this be fetched from other metadata property?)
                // TODO: pluralization metadata for clean API?
                string classLabel = c.GetLocalName();

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

                        // If this is a data property with an XSD datatype range
                        if (property.IsDataProperty() && property.Ranges.First().IsXsdDatatype())
                        {
                            // Fall back to string representation for unknown types
                            outputProperty.type = "string";

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

                        if (property.IsObjectProperty())
                        {
                            OntologyClass range = property.Ranges.First();
                            if (range.IsNamed() && g.OwlClasses.Contains(range))
                            {
                                outputProperty.oneOf = new List<Dictionary<string, string>>();
                                outputProperty.oneOf.Add(new Dictionary<string, string> { { "$ref", "#/components/schemas/" + range.GetLocalName() } });
                                outputProperty.oneOf.Add(new Dictionary<string, string> { { "type", "string" }, { "format", "uri" } });
                            }
                            else
                            {
                                // Fall back to string representation for unknown types
                                outputProperty.type = "string";
                                outputProperty.format = "uri";
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

        private static void LoadImport(Ontology importedOntology, OntologyGraph g)
        {
            // Parse and load ontology from its URI
            Uri importedOntologyUri = new Uri(importedOntology.Resource.ToString());
            RdfXmlParser parser = new RdfXmlParser();
            OntologyGraph importedOntologyGraph = new OntologyGraph();
            try
            {
                UriLoader.Cache.Clear();
                UriLoader.Load(importedOntologyGraph, importedOntologyUri, parser);
            }
            catch (RdfParseException e)
            {
                Console.Write(e.Message);
                Console.Write(e.StackTrace);
            }

            // Fetch out the imported ontology's self-described URI from the imported graph
            // This may differ from the URI given by the importing ontology (from which the file was fetched),
            // due to .htaccess redirects, version URIs, etc.
            Uri importedOntologySelfDefinedUri = importedOntologyGraph.BaseUri;

            // Merge imported graph with root ontology graph
            g.Merge(importedOntologyGraph);

            // Set up new ontology metadata object based on self-described imported URI
            Ontology importedOntologyFromSelfDefinition = new Ontology(g.CreateUriNode(importedOntologySelfDefinedUri), g);

            // Traverse import hierarchy
            foreach (Ontology subImport in importedOntologyFromSelfDefinition.Imports)
            {
                LoadImport(subImport, g);
            }
        }
    }
}
