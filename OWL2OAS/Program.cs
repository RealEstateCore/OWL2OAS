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

        public struct PropertyConstraint
        {
            public OntologyProperty property;
            public int min;
            public int max;
            public int exactly;
        }

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

            // TODO: Refactor, break out construction of info block into own method for clarity
            // Create OAS Info header
            document.info = new OASDocument.Info();

            // Check for mandatory components (label, version info, cc:license).
            if (!rootOntology.Label.Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <rdfs:label> annotation.", rootOntology));   
            }
            if (!rootOntology.VersionInfo.Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <owl:versionInfo> annotation.", rootOntology));
            }
            IUriNode ccLicense = g.CreateUriNode(new Uri("http://creativecommons.org/ns#license"));
            if (!rootOntology.GetNodesViaProperty(ccLicense).Where(objNode => objNode.IsLiteral() || objNode.IsUri()).Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <cc:license> annotation that is a URI or literal.", rootOntology));
            }

            document.info.title = rootOntology.Label.OrderBy(label => label.HasLanguage()).First().Value;
            document.info.version = rootOntology.VersionInfo.OrderBy(versionInfo => versionInfo.HasLanguage()).First().Value;
            document.info.license = new OASDocument.License();
            INode licenseNode = rootOntology.GetNodesViaProperty(ccLicense).OrderBy(node => node.NodeType).First();
            if (licenseNode.IsUri())
            {
                document.info.license.name = ((UriNode)licenseNode).GetLocalName();
                document.info.license.url = ((UriNode)licenseNode).Uri.ToString();
            }
            else
            {
                document.info.license.name = ((LiteralNode)licenseNode).Value;
            }

            // Non-mandatory info components, e.g., rdfs:comment
            if (rootOntology.Comment.Any())
            {
                string ontologyComment = rootOntology.Comment.OrderBy(comment => comment.HasLanguage()).First().Value.Trim().Replace("\r\n","\n").Replace("\n", "<br/>");
                document.info.description = string.Format("The documentation below is automatically extracted from an <rdfs:comment> annotation on the ontology {0}:<br/><br/>*{1}*", rootOntology, ontologyComment);
            }

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


                // Iterate over superclasses, extract constraints
                Dictionary<OntologyProperty, PropertyConstraint> constraints = new Dictionary<OntologyProperty, PropertyConstraint>();
                foreach (OntologyClass superClass in c.DirectSuperClasses)
                {
                    if (superClass.IsRestriction())
                    {
                        PropertyConstraint? constraint = ExtractRestriction(superClass);
                        if (constraint.HasValue)
                        {
                            constraints.Add(constraint.Value.property, constraint.Value);
                        }
                    }
                }

                // Add id and label for all entries
                OASDocument.Property idProperty = new OASDocument.Property();
                idProperty.type = "string";
                schema.properties.Add("id", idProperty);
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);

                // Todo: refactor, break out majority of the foor loop into own method for clarity
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

        private static PropertyConstraint? ExtractRestriction(OntologyClass restriction)
        {
            OntologyGraph graph = restriction.Graph as OntologyGraph;
            IUriNode onProperty = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#onProperty"));
            IUriNode cardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#cardinality"));
            IUriNode someValuesFrom = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#someValuesFrom"));
            IUriNode minCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#minCardinality"));
            IUriNode minQualifiedCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#minQualifiedCardinality"));
            IUriNode maxCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#maxCardinality"));
            IUriNode maxQualifiedCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#maxQualifiedCardinality"));

            if (restriction.GetNodesViaProperty(onProperty).UriNodes().Where(node => node.IsOntologyProperty()).Count() == 1)
            {
                PropertyConstraint pc = new PropertyConstraint();
                IUriNode restrictionPropertyNode = restriction.GetNodesViaProperty(onProperty).UriNodes().Where(node => node.IsOntologyProperty()).First();
                pc.property = graph.CreateOntologyProperty(restrictionPropertyNode);

                if (restriction.GetNodesViaProperty(cardinality).LiteralNodes().Count() == 1 &&
                    restriction.GetNodesViaProperty(cardinality).LiteralNodes().First().IsInteger())
                {
                    pc.exactly = int.Parse(restriction.GetNodesViaProperty(cardinality).LiteralNodes().First().Value);
                    return pc;
                }

                if (restriction.GetNodesViaProperty(someValuesFrom).Count() == 1)
                {
                    pc.min = 1;
                }

                IEnumerable <INode> minCardinalities = restriction.GetNodesViaProperty(minCardinality).Union(restriction.GetNodesViaProperty(minQualifiedCardinality));
                if (minCardinalities.LiteralNodes().Count() == 1 &&
                    minCardinalities.LiteralNodes().First().IsInteger())
                {
                    pc.min = int.Parse(minCardinalities.LiteralNodes().First().Value);
                }

                IEnumerable<INode> maxCardinalities = restriction.GetNodesViaProperty(maxCardinality).Union(restriction.GetNodesViaProperty(maxQualifiedCardinality));
                if (maxCardinalities.LiteralNodes().Count() == 1 &&
                    maxCardinalities.LiteralNodes().First().IsInteger())
                {
                    pc.max = int.Parse(maxCardinalities.LiteralNodes().First().Value);
                }
                return pc;
            }
            return null;
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
