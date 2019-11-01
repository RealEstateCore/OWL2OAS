using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using CommandLine;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class Program
    {
        public class Options
        {
            [Option('n', "no-imports", Required = false, HelpText = "Sets program to not follow owl:Imports declarations.")]
            public bool NoImports { get; set; }
            [Option('s', "server", Default = "http://localhost:8080/", Required = false, HelpText = "Sets the server URL (where presumably an API implementation is running).")]
            public string Server { get; set; }
        }

        /// <summary>
        /// The ontology being parsed.
        /// </summary>
        private static Ontology rootOntology;

        /// <summary>
        /// Set of transitively imported child ontologies.
        /// </summary>
        private static readonly HashSet<Ontology> importedOntologies = new HashSet<Ontology>();

        private static string _server;
        private static bool _noImports;

        /// <summary>
        /// Dictionary mapping some common XSD data types to corresponding OSA data types and formats, see
        /// https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.0.md#dataTypeFormat
        /// </summary>
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

        /// <summary>
        /// A struct representing cardinality constraints on a property.
        /// </summary>
        public struct PropertyConstraint
        {
            public IUriNode property;
            public int min;
            public int max;
            public int exactly;
            public bool AllowsMultiple()
            {
                return !MaxOne();
            }
            public bool MaxOne()
            {
                return (exactly == 1 || max == 1);
            }
            public bool IsRequired()
            {
                return (min == 1 || exactly == 1);
            }
        }

        /// <summary>
        /// Custom comparer for OntologyResource objects, that simply
        /// defers to comparison of nested INodes.
        /// </summary>
        class OntologyResourceComparer : IEqualityComparer<OntologyResource>
        {
            public bool Equals(OntologyResource x, OntologyResource y)
            {
                return x.Resource == y.Resource;
            }

            public int GetHashCode(OntologyResource obj)
            {
                return obj.Resource.GetHashCode();
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       _noImports = o.NoImports;
                       _server = o.Server;
                   });

            // Load ontology graph
            OntologyGraph rootOntologyGraph = new OntologyGraph();
            //FileLoader.Load(g, args[0]);
            EmbeddedResourceLoader.Load(rootOntologyGraph, "OWL2OAS.rec-core-3.0.rdf, OWL2OAS");
            IUriNode rootOntologyUriNode = rootOntologyGraph.CreateUriNode(rootOntologyGraph.BaseUri);
            rootOntology = new Ontology(rootOntologyUriNode, rootOntologyGraph);

            // If configured for it, parse owl:Imports
            if (!_noImports)
            {
                foreach (Ontology import in rootOntology.Imports)
                {
                    LoadImport(import);
                }
            }

            // Create OAS object
            OASDocument document = new OASDocument();

            // TODO: Refactor, break out construction of info block into own method for clarity
            // Create OAS Info header
            document.info = new OASDocument.Info();

            // Check for mandatory components (dc:title, version info, cc:license).
            IUriNode dcTitle = rootOntologyGraph.CreateUriNode(new Uri("http://purl.org/dc/elements/1.1/title"));
            if (!rootOntology.GetLiteralNodesViaProperty(dcTitle).Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <dc:title> annotation.", rootOntology));
            }
            if (!rootOntology.VersionInfo.Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <owl:versionInfo> annotation.", rootOntology));
            }
            IUriNode ccLicense = rootOntologyGraph.CreateUriNode(new Uri("http://creativecommons.org/ns#license"));
            if (!rootOntology.GetNodesViaProperty(ccLicense).Where(objNode => objNode.IsLiteral() || objNode.IsUri()).Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <cc:license> annotation that is a URI or literal.", rootOntology));
            }

            document.info.title = rootOntology.GetLiteralNodesViaProperty(dcTitle).OrderBy(title => title.HasLanguage()).First().Value;
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
            IUriNode dcDescription = rootOntologyGraph.CreateUriNode(new Uri("http://purl.org/dc/elements/1.1/description"));
            if (rootOntology.GetLiteralNodesViaProperty(dcDescription).Any())
            {
                string ontologyDescription = rootOntology.GetLiteralNodesViaProperty(dcDescription).OrderBy(description => description.HasLanguage()).First().Value.Trim().Replace("\r\n", "\n").Replace("\n", "<br/>");
                document.info.description = string.Format("The documentation below is automatically extracted from a <dc:description> annotation on the ontology {0}:<br/><br/>*{1}*", rootOntology, ontologyDescription);
            }

            // Server block
            document.servers = new List<Dictionary<string, string>> { new Dictionary<string, string> { { "url", _server } } };

            // Parse OWL classes. For each class, create a schema and a path
            document.components = new OASDocument.Components();
            Dictionary<string, OASDocument.Schema> schemas = new Dictionary<string, OASDocument.Schema>();
            document.paths = new Dictionary<string, OASDocument.Path>();

            // Set context based on the ontology IRI
            OASDocument.Property vocabularyProperty = new OASDocument.Property()
            {
                type = "string",
                format = "uri",
                defaultValue = rootOntology.GetVersionOrOntologyIri().ToString()
            };
            OASDocument.Property baseNamespaceProperty = new OASDocument.Property()
            {
                type = "string",
                format = "uri"
            };
            OASDocument.Property labelContextProperty = new OASDocument.Property()
            {
                type = "string",
                format = "uri",
                defaultValue = "http://www.w3.org/2000/01/rdf-schema#label"
            };
            OASDocument.Schema contextSchema = new OASDocument.Schema()
            {
                required = new List<string> { "@vocab", "@base", "label" },
                properties = new Dictionary<string, OASDocument.Property> {
                    { "@vocab", vocabularyProperty },
                    { "@base", baseNamespaceProperty },
                    { "label", labelContextProperty }
                }
            };
            // Add each imported ontology to the context
            foreach (Ontology importedOntology in importedOntologies)
            {
                OASDocument.Property importedVocabularyProperty = new OASDocument.Property()
                {
                    type = "string",
                    format = "uri",
                    defaultValue = importedOntology.GetVersionOrOntologyIri().ToString()
                };
                contextSchema.properties.Add(importedOntology.GetShortName(), importedVocabularyProperty);
                contextSchema.required.Add(importedOntology.GetShortName());
            }
            schemas.Add("Context", contextSchema);
            document.components.schemas = schemas;

            GenerateClassSchemas(rootOntologyGraph, document);
            GenerateClassPaths(rootOntologyGraph, document);

            foreach (Ontology importedOntology in importedOntologies)
            {
                GenerateClassSchemas(importedOntology.Graph as OntologyGraph, document);
                GenerateClassPaths(importedOntology.Graph as OntologyGraph, document);
            }

            DumpAsYaml(document);
        }

        private static string GetKeyNameForClass(OntologyGraph graph, OntologyClass cls)
        {
            if (graph.Equals(rootOntology.Graph))
            {
                return cls.GetLocalName();
            }
            else
            {
                string prefix = importedOntologies.First(ontology => ontology.Graph.Equals(graph)).GetShortName();
                string localName = cls.GetLocalName();
                return string.Format("{0}:{1}", prefix, localName);
            }
        }

        private static void GenerateClassSchemas(OntologyGraph graph, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass c in graph.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForClass(graph, c);

                // Create schema for class and corresponding properties dict
                OASDocument.Schema schema = new OASDocument.Schema();
                schema.properties = new Dictionary<string, OASDocument.Property>();

                // Iterate over superclasses, extract constraints
                Dictionary<IUriNode, PropertyConstraint> constraints = new Dictionary<IUriNode, PropertyConstraint>();
                foreach (OntologyClass superClass in c.SuperClasses)
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

                // Add reference to context schema
                schema.properties.Add("@context", new OASDocument.SchemaReferenceProperty("Context"));

                // Add @id for all entries
                OASDocument.Property idProperty = new OASDocument.Property();
                idProperty.type = "string";
                schema.properties.Add("@id", idProperty);

                // Add @type for all entries
                OASDocument.Property typeProperty = new OASDocument.Property
                {
                    type = "string",
                    defaultValue = c.GetLocalName()
                };
                schema.properties.Add("@type", typeProperty);

                // Label is an option for all entries
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);

                // Todo: refactor, break out majority of the foor loop into own method for clarity
                IEnumerable<OntologyProperty> directDomainProperties = c.IsDomainOf;
                IEnumerable<OntologyProperty> indirectDomainProperties = c.SuperClasses.SelectMany(cls => cls.IsDomainOf);
                IEnumerable<OntologyProperty> scopedDomainProperties = c.IsScopedDomainOf();
                IEnumerable<OntologyProperty> allProperties = directDomainProperties.Union(indirectDomainProperties).Union(scopedDomainProperties);
                foreach (OntologyProperty property in allProperties.Distinct(new OntologyResourceComparer()).Where(prop => !prop.IsDeprecated()))
                {
                    // We only process (named) object and data properties with singleton ranges.
                    if ((property.IsObjectProperty() || property.IsDataProperty()) && property.Ranges.Count() == 1)
                    {

                        // Used for lookups against constraints dict
                        UriNode propertyNode = ((UriNode)property.Resource);

                        // Used to allocate property to schema.properties dictionary
                        string propertyLocalName = propertyNode.GetLocalName();

                        // The return value: a property block to be added to the output document
                        OASDocument.Property outputProperty;

                        // Check if multiple values are allowed for this property. By default they are.
                        bool propertyAllowsMultipleValues = true;
                        if ((constraints.ContainsKey(propertyNode) && constraints[propertyNode].MaxOne()) || property.IsFunctional())
                        {
                            propertyAllowsMultipleValues = false;
                        }

                        // If this is a data property
                        if (property.IsDataProperty())
                        {
                            // Set up the (possibly later on nested) property block
                            OASDocument.Property dataProperty = new OASDocument.Property();

                            // Fall back to string representation for unknown types
                            dataProperty.type = "string";

                            // Parse XSD type into OAS type and format (note: not all XSD types are covered)
                            string rangeXsdType = ((UriNode)property.Ranges.First().Resource).GetLocalName();
                            if (xsdOsaMappings.ContainsKey(rangeXsdType))
                            {
                                dataProperty.type = xsdOsaMappings[rangeXsdType].Item1;
                                string format = xsdOsaMappings[rangeXsdType].Item2;
                                if (format.Length > 0)
                                {
                                    dataProperty.format = format;
                                }
                            }

                            // Assign return value
                            outputProperty = dataProperty;
                        }
                        else
                        {
                            // This is an Object property
                            // Set up the (possibly later on nested) property block
                            OASDocument.Property uriProperty;

                            // Set the type of the property; locally defined named classes can be either URI or full schema representation
                            OntologyClass range = property.Ranges.First();
                            if (range.IsNamed() && graph.OwlClasses.Contains(range))
                            {
                                OASDocument.Property nestedIdProperty = new OASDocument.Property()
                                {
                                    type = "string"
                                };
                                OASDocument.Property nestedTypeProperty = new OASDocument.Property()
                                {
                                    type = "string",
                                    defaultValue = range.GetLocalName()
                                };
                                uriProperty = new OASDocument.ObjectProperty()
                                {
                                    properties = new Dictionary<string, OASDocument.Property>() {
                                        { "@id", nestedIdProperty },
                                        { "@type", nestedTypeProperty }
                                    },
                                    required = new List<string>() { "@id" }
                                };
                            }
                            else
                            {
                                // Fall back to string representation
                                uriProperty = new OASDocument.Property();
                                uriProperty.type = "string";
                            }

                            outputProperty = uriProperty;
                        }

                        // If this field allows multiple values (as is the default), wrap it in an array
                        if (propertyAllowsMultipleValues)
                        {
                            OASDocument.ArrayProperty arrayProperty = new OASDocument.ArrayProperty();
                            arrayProperty.items = outputProperty;
                            // Assign constraints on the array, if any
                            if (constraints.ContainsKey(propertyNode))
                            {
                                PropertyConstraint pc = constraints[propertyNode];
                                if (pc.min != 0)
                                    arrayProperty.minItems = pc.min;
                                if (pc.max != 0)
                                    arrayProperty.maxItems = pc.max;
                                if (pc.exactly != 0)
                                    arrayProperty.maxItems = arrayProperty.minItems = pc.exactly;
                            }
                            schema.properties.Add(propertyLocalName, arrayProperty);
                        }
                        else
                        {
                            // This is a single-valued property, assign it w/o the array
                            schema.properties.Add(propertyLocalName, outputProperty);
                        }

                        // Tag any min 1 or exactly 1 properties as required
                        if (constraints.ContainsKey(propertyNode) && constraints[propertyNode].IsRequired())
                        {
                            if (schema.required == null)
                                schema.required = new List<string>();
                            schema.required.Add(propertyLocalName);
                        }
                    }
                }
                document.components.schemas.Add(classLabel, schema);
            }
        }

        // TODO: move boilerplate code below into OASDocument
        private static void GenerateClassPaths(OntologyGraph g, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass c in g.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForClass(g, c);

                // Create path for class
                OASDocument.Path path = new OASDocument.Path();
                document.paths.Add("/" + classLabel, path);

                // Create each of the HTTP methods
                // TODO: PUT, PATCH, etc
                // TODO: filtering, parameters, etc
                path.get = new OASDocument.Get();
                path.get.summary = "Get '" + classLabel + "' objects.";

                path.get.responses = new Dictionary<string, OASDocument.Response>();

                // Create each of the HTTP response types
                OASDocument.Response response = new OASDocument.Response();
                response.description = "An array of '" + classLabel + "' objects.";
                path.get.responses.Add("200", response);

                response.content = new Dictionary<string, OASDocument.Content>();
                OASDocument.Content content = new OASDocument.Content();
                response.content.Add("application/jsonld", content);

                // Wrap responses in pagination
                content.schema = new OASDocument.ArrayProperty()
                {
                    items = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel))
                };
            }
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
            IUriNode qualifiedCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#qualifiedCardinality"));
            IUriNode someValuesFrom = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#someValuesFrom"));
            IUriNode minCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#minCardinality"));
            IUriNode minQualifiedCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#minQualifiedCardinality"));
            IUriNode maxCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#maxCardinality"));
            IUriNode maxQualifiedCardinality = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#maxQualifiedCardinality"));

            if (restriction.GetNodesViaProperty(onProperty).UriNodes().Where(node => node.IsOntologyProperty()).Count() == 1)
            {
                PropertyConstraint pc = new PropertyConstraint();
                IUriNode restrictionPropertyNode = restriction.GetNodesViaProperty(onProperty).UriNodes().Where(node => node.IsOntologyProperty()).First();
                pc.property = restrictionPropertyNode;

                IEnumerable<INode> exactCardinalities = restriction.GetNodesViaProperty(cardinality).Union(restriction.GetNodesViaProperty(qualifiedCardinality));
                if (exactCardinalities.LiteralNodes().Count() == 1 &&
                    exactCardinalities.LiteralNodes().First().IsInteger())
                {
                    pc.exactly = int.Parse(exactCardinalities.LiteralNodes().First().Value);
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

        /// <summary>
        /// Loads imported ontologies transitively. Each imported ontology is added
        /// to the static set <c>importedOntologies</c>.
        /// </summary>
        /// <param name="importedOntology">The ontology to import.</param>
        private static void LoadImport(Ontology importedOntology)
        {
            // We only deal with names ontologies
            if (importedOntology.Resource.IsUri()) {

                // Parse and load ontology from its URI
                Uri importedOntologyUri = ((IUriNode)importedOntology.Resource).Uri;
                OntologyGraph importedOntologyGraph = new OntologyGraph();

                try
                {
                    UriLoader.Cache.Clear();
                    UriLoader.Load(importedOntologyGraph, importedOntologyUri);
                }
                catch (RdfParseException e)
                {
                    Console.Write(e.Message);
                    Console.Write(e.StackTrace);
                }

                // Fetch out the imported ontology's URI from the imported graph's base URI.
                // Note that this often differs from the URI given by the importing ontology
                // (from which the file was fetched), due to .htaccess redirects, version URIs, etc.
                Uri importedOntologyGraphBaseUri = importedOntologyGraph.BaseUri;

                // Add ontology to prefix mappings used to generate contexts etc
                string prefix = importedOntology.GetShortName();
                //prefixMappings[importedOntologyGraphBaseUri] = prefix;

                // Add imported graph to list of imports for later processing
                //importedGraphs.Add(importedOntologyGraph);
                
                // Set up a new ontology metadata object, add it to the global imports collection
                // and traverse its import hierarchy transitively
                Ontology importedOntologyFromSelfDefinition = new Ontology(importedOntologyGraph.CreateUriNode(importedOntologyGraphBaseUri), importedOntologyGraph);
                importedOntologies.Add(importedOntologyFromSelfDefinition);
                foreach (Ontology subImport in importedOntologyFromSelfDefinition.Imports)
                {
                    LoadImport(subImport);
                }
            }
        }
    }
}