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
            [Option('s', "server", Default = "http://localhost:8080/", Required = false, HelpText = "The server URL (where presumably an API implementation is running).")]
            public string Server { get; set; }
            [Option('f', "file-path", Required = true, HelpText = "The path to the on-disk root ontology file to translate.", SetName = "fileOntology")]
            public string FilePath { get; set; }
            [Option('u', "uri-path", Required = true, HelpText = "The URI of the root ontology file to translate.", SetName = "uriOntology")]
            public string UriPath { get; set; }
        }

        /// <summary>
        /// The ontology being parsed.
        /// </summary>
        private static Ontology rootOntology;

        /// <summary>
        /// Set of transitively imported child ontologies.
        /// </summary>
        private static readonly HashSet<Ontology> importedOntologies = new HashSet<Ontology>();

        // Various configuration fields
        private static string _server;
        private static bool _noImports;
        private static bool _localOntology;
        private static string _ontologyPath;

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
            {"string",("string","") }
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

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       _noImports = o.NoImports;
                       _server = o.Server;
                       if (o.FilePath != null)
                       {
                           _localOntology = true;
                           _ontologyPath = o.FilePath;
                       }
                       else
                       {
                           _localOntology = false;
                           _ontologyPath = o.UriPath;
                       }
                   })
                   .WithNotParsed<Options>((errs) =>
                   {
                       Environment.Exit(1);
                   });

            // Clear cache from any prior runs
            UriLoader.Cache.Clear();

            // Load ontology graph from local or remote path
            OntologyGraph rootOntologyGraph = new OntologyGraph();
            if (_localOntology)
            {
                FileLoader.Load(rootOntologyGraph, _ontologyPath);
            }
            else
            {
                UriLoader.Load(rootOntologyGraph, new Uri(_ontologyPath));
            }

            // Get the main ontology defined in the graph.
            rootOntology = rootOntologyGraph.GetOntology();

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

            // Create the OAS Info header
            document.info = GenerateDocumentInfo();

            // Server block
            document.servers = new List<Dictionary<string, string>> { new Dictionary<string, string> { { "url", _server } } };

            // Set up components/schemas structure.
            document.components = new OASDocument.Components();
            document.components.schemas = new Dictionary<string, OASDocument.Schema>();

            // Generate and add the Context schema
            document.components.schemas.Add("Context", GenerateContextSchema());

            // Parse OWL classes.For each class, create a schema and a path
            GenerateClassSchemas(rootOntologyGraph, document);
            GenerateClassPaths(rootOntologyGraph, document);

            // Also parse classes in imports
            foreach (Ontology importedOntology in importedOntologies)
            {
                GenerateClassSchemas(importedOntology.Graph as OntologyGraph, document);
                GenerateClassPaths(importedOntology.Graph as OntologyGraph, document);
            }

            DumpAsYaml(document);
        }

        private static OASDocument.Schema GenerateContextSchema()
        {
            // Set @context/@vocab based on the ontology IRI
            OASDocument.Property vocabularyProperty = new OASDocument.Property
            {
                type = "string",
                format = "uri",
                DefaultValue = rootOntology.GetVersionOrOntologyIri().ToString()
            };
            // Set @context/@base (default data namespace)
            OASDocument.Property baseNamespaceProperty = new OASDocument.Property
            {
                type = "string",
                format = "uri"
            };
            // Hardcoded rdfs:label for @context
            OASDocument.Property labelContextProperty = new OASDocument.Property
            {
                type = "string",
                format = "uri",
                DefaultValue = VocabularyHelper.RDFS.label.ToString()
            };
            // Mash it all together into a @context block
            OASDocument.Schema contextSchema = new OASDocument.Schema
            {
                required = new List<string> { "@vocab", "@base", "label" },
                properties = new Dictionary<string, OASDocument.Property> {
                    { "@vocab", vocabularyProperty },
                    { "@base", baseNamespaceProperty },
                    { "label", labelContextProperty }
                }
            };
            // Add each imported ontology to the @context
            foreach (Ontology importedOntology in importedOntologies)
            {
                OASDocument.Property importedVocabularyProperty = new OASDocument.Property
                {
                    type = "string",
                    format = "uri",
                    DefaultValue = importedOntology.GetVersionOrOntologyIri().ToString()
                };
                contextSchema.properties.Add(importedOntology.GetShortName(), importedVocabularyProperty);
                contextSchema.required.Add(importedOntology.GetShortName());
            }

            return contextSchema;
        }

        private static OASDocument.Info GenerateDocumentInfo()
        {
            OASDocument.Info docInfo = new OASDocument.Info();

            OntologyGraph rootOntologyGraph = rootOntology.OntologyGraph();

            // Check for mandatory components (dc:title, version info, cc:license).
            IUriNode dcTitle = rootOntologyGraph.CreateUriNode(VocabularyHelper.DC.title);
            if (!rootOntology.GetNodesViaProperty(dcTitle).LiteralNodes().Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <dc:title> annotation.", rootOntology));
            }
            if (!rootOntology.VersionInfo.Any())
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <owl:versionInfo> annotation.", rootOntology));
            }
            IUriNode ccLicense = rootOntologyGraph.CreateUriNode(VocabularyHelper.CC.license);
            if (!rootOntology.GetNodesViaProperty(ccLicense).Any(objNode => objNode.IsLiteral() || objNode.IsUri()))
            {
                throw new RdfException(string.Format("Ontology <{0}> does not have an <cc:license> annotation that is a URI or literal.", rootOntology));
            }
            docInfo.title = rootOntology.GetNodesViaProperty(dcTitle).LiteralNodes().OrderBy(title => title.HasLanguage()).First().Value;
            docInfo.Version = rootOntology.VersionInfo.OrderBy(versionInfo => versionInfo.HasLanguage()).First().Value;
            docInfo.license = new OASDocument.License();
            INode licenseNode = rootOntology.GetNodesViaProperty(ccLicense).OrderBy(node => node.NodeType).First();
            if (licenseNode.IsUri())
            {
                docInfo.license.name = ((UriNode)licenseNode).GetLocalName();
                docInfo.license.url = ((UriNode)licenseNode).Uri.ToString();
            }
            else
            {
                docInfo.license.name = ((LiteralNode)licenseNode).Value;
            }

            // Non-mandatory info components, e.g., rdfs:comment
            IUriNode dcDescription = rootOntologyGraph.CreateUriNode(VocabularyHelper.DC.description);
            if (rootOntology.GetNodesViaProperty(dcDescription).LiteralNodes().Any())
            {
                string ontologyDescription = rootOntology.GetNodesViaProperty(dcDescription).LiteralNodes().OrderBy(description => description.HasLanguage()).First().Value.Trim().Replace("\r\n", "\n").Replace("\n", "<br/>");
                docInfo.description = string.Format("The documentation below is automatically extracted from a <dc:description> annotation on the ontology {0}:<br/><br/>*{1}*", rootOntology, ontologyDescription);
            }

            return docInfo;
        }

        private static string GetKeyNameForResource(OntologyGraph graph, OntologyResource cls)
        {
            if (graph.Equals(rootOntology.Graph))
            {
                return cls.GetLocalName();
            }
            string prefix = importedOntologies.First(ontology => ontology.Graph.Equals(graph)).GetShortName();
            string localName = cls.GetLocalName();
            return string.Format("{0}:{1}", prefix, localName);
        }

        private static void GenerateClassSchemas(OntologyGraph graph, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass oClass in graph.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(graph, oClass);

                // Create schema for class and corresponding properties dict
                OASDocument.Schema schema = new OASDocument.Schema();
                schema.properties = new Dictionary<string, OASDocument.Property>();

                // Iterate over superclasses, extract constraints
                Dictionary<IUriNode, PropertyConstraint> constraints = new Dictionary<IUriNode, PropertyConstraint>();
                foreach (OntologyClass superClass in oClass.SuperClasses)
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
                    DefaultValue = oClass.GetLocalName()
                };
                schema.properties.Add("@type", typeProperty);

                // Label is an option for all entries
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);

                // Todo: refactor, break out majority of the foor loop into own method for clarity
                IEnumerable<OntologyProperty> allProperties = oClass.IsExhaustiveDomainOf();
                foreach (OntologyProperty property in allProperties.Where(prop => !prop.IsDeprecated()))
                {
                    // We only process (named) object and data properties with singleton ranges.
                    if ((property.IsObjectProperty() || property.IsDataProperty()) && property.Ranges.Count() == 1)
                    {
                        // Used for lookups against constraints dict
                        UriNode propertyNode = ((UriNode)property.Resource);

                        // Used to allocate property to schema.properties dictionary
                        string propertyLabel = GetKeyNameForResource(graph, property);

                        // The return value: a property block to be added to the output document
                        OASDocument.Property outputProperty;

                        // Check if multiple values are allowed for this property. By default they are.
                        bool propertyAllowsMultipleValues = true
                            && (!constraints.ContainsKey(propertyNode) || !constraints[propertyNode].MaxOne())
                            && !property.IsFunctional();

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
                                OASDocument.Property nestedIdProperty = new OASDocument.Property
                                {
                                    type = "string"
                                };
                                OASDocument.Property nestedTypeProperty = new OASDocument.Property
                                {
                                    type = "string",
                                    DefaultValue = range.GetLocalName()
                                };
                                uriProperty = new OASDocument.ObjectProperty
                                {
                                    properties = new Dictionary<string, OASDocument.Property> {
                                        { "@id", nestedIdProperty },
                                        { "@type", nestedTypeProperty }
                                    },
                                    required = new List<string> { "@id" }
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
                            schema.properties.Add(propertyLabel, arrayProperty);
                        }
                        else
                        {
                            // This is a single-valued property, assign it w/o the array
                            schema.properties.Add(propertyLabel, outputProperty);
                        }

                        // Tag any min 1 or exactly 1 properties as required
                        if (constraints.ContainsKey(propertyNode) && constraints[propertyNode].IsRequired())
                        {
                            if (schema.required == null)
                                schema.required = new List<string>();
                            schema.required.Add(propertyLabel);
                        }
                    }
                }
                document.components.schemas.Add(classLabel, schema);
            }
        }

        private static void GenerateClassPaths(OntologyGraph graph, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass oClass in graph.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(graph, oClass);

                // Create paths and corresponding operations for class
                document.paths.Add(string.Format("/{0}", classLabel), new OASDocument.Path
                {
                    get = GenerateGetEntitiesOperation(classLabel, oClass),
                    post = GeneratePostEntityOperation(classLabel)
                });
                document.paths.Add(string.Format("/{0}/{{id}}", classLabel), new OASDocument.Path
                {
                    get = GenerateGetEntityByIdOperation(classLabel),
                    put = GeneratePutToIdOperation(classLabel),
                    delete = GenerateDeleteByIdOperation(classLabel)
                });
            }
        }

        private static OASDocument.Operation GenerateDeleteByIdOperation(string classLabel)
        {
            OASDocument.Operation deleteOperation = new OASDocument.Operation();
            deleteOperation.summary = string.Format("Delete a '{0}' object.", classLabel);
            deleteOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = string.Format("Id of '{0}' to delete.", classLabel),
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new Dictionary<string, string> {
                            { "type", "string" }
                        }
            };
            deleteOperation.parameters.Add(idParameter);

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = string.Format("'{0}' entity was successfully deleted.", classLabel);
            deleteOperation.responses.Add("default", response);

            return deleteOperation;
        }

        private static OASDocument.Operation GeneratePostEntityOperation(string classLabel)
        {
            OASDocument.Operation postOperation = new OASDocument.Operation();
            postOperation.summary = string.Format("Create a new '{0}' object.", classLabel);
            postOperation.tags.Add(classLabel);

            OASDocument.Parameter bodyParameter = new OASDocument.Parameter
            {
                name = "entity",
                description = string.Format("New '{0}' entity that is to be added.", classLabel),
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new Dictionary<string, string> {
                            { "$ref", "#/components/schemas/" + HttpUtility.UrlEncode(classLabel) }
                        }
            };
            postOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = "Entity was successfully created (new representation returned).";
            postOperation.responses.Add("200", response);

            response.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content = new OASDocument.Content();
            response.content.Add("application/jsonld", content);

            // Response is per previously defined schema
            content.Schema = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel));

            return postOperation;
        }

        private static OASDocument.Operation GenerateGetEntityByIdOperation(string classLabel)
        {
            OASDocument.Operation getOperation = new OASDocument.Operation();
            getOperation.summary = string.Format("Get a specific '{0}' object.", classLabel);
            getOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = string.Format("Id of '{0}' to return.", classLabel),
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new Dictionary<string, string> {
                            { "type", "string" }
                        }
            };
            getOperation.parameters.Add(idParameter);

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = string.Format("A '{0}' object.", classLabel);
            getOperation.responses.Add("200", response);

            response.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content = new OASDocument.Content();
            response.content.Add("application/jsonld", content);

            // Response is per previously defined schema
            content.Schema = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel));

            return getOperation;
        }

        private static OASDocument.Operation GenerateGetEntitiesOperation(string classLabel, OntologyClass oClass)
        {

            // Create Get
            OASDocument.Operation getOperation = new OASDocument.Operation();
            getOperation.summary = "Get '" + classLabel + "' entities.";
            getOperation.tags.Add(classLabel);

            // Add pagination parameters
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "offsetParam" });
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "limitParam" });

            // Add parameters for each field that can be expressed on this class
            foreach (OntologyProperty property in oClass.IsExhaustiveDomainOf()
                .Where(property => property.IsDataProperty() || property.IsObjectProperty())
                .Where(property => property.Ranges.Count() == 1)
                .Where(property => !property.IsDeprecated()))
            {
                string propertyLabel = GetKeyNameForResource(property.OntologyGraph(), property);

                // Fall back to string representation and no format for object properties
                // abd data properties w/ unknown types
                string propertyType = "string";
                string propertyFormat = "";

                // Parse XSD type into OAS type and format (note: not all XSD types are covered)
                string rangeXsdType = ((UriNode)property.Ranges.First().Resource).GetLocalName();
                
                if (xsdOsaMappings.ContainsKey(rangeXsdType))
                {
                    propertyType = xsdOsaMappings[rangeXsdType].Item1;
                    propertyFormat = xsdOsaMappings[rangeXsdType].Item2;
                }

                OASDocument.Parameter parameter = new OASDocument.Parameter
                {
                    name = propertyLabel,
                    description = string.Format("Filter value on property '{0}'.", propertyLabel),
                    required = false,
                    schema = new Dictionary<string, string>
                    {
                        { "type", propertyType }
                    },
                    InField = OASDocument.Parameter.InFieldValues.query
                };

                if (propertyFormat.Length > 0)
                {
                    parameter.schema.Add("format", propertyFormat);
                }

                getOperation.parameters.Add(parameter);
            }

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = "An array of '" + classLabel + "' objects.";
            getOperation.responses.Add("200", response);

            response.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content = new OASDocument.Content();
            response.content.Add("application/jsonld", content);

            // Wrap responses in array
            content.Schema = new OASDocument.ArrayProperty
            {
                items = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel))
            };

            // Return
            return getOperation;
        }

        private static OASDocument.Operation GeneratePutToIdOperation(string classLabel)
        {
            OASDocument.Operation putOperation = new OASDocument.Operation();
            putOperation.summary = string.Format("Update an existing '{0}' entity.", classLabel);
            putOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = string.Format("Id of '{0}' to update.", classLabel),
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new Dictionary<string, string> {
                            { "type", "string" }
                        }
            };
            OASDocument.Parameter bodyParameter = new OASDocument.Parameter
            {
                name = "entity",
                description = string.Format("Updated data for '{0}' entity.", classLabel),
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new Dictionary<string, string> {
                            { "$ref", "#/components/schemas/" + HttpUtility.UrlEncode(classLabel) }
                        }
            };
            putOperation.parameters.Add(idParameter);
            putOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = "Entity was updated successfully (new representation returned).";
            putOperation.responses.Add("200", response);

            response.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content = new OASDocument.Content();
            response.content.Add("application/jsonld", content);

            // Response is per previously defined schema
            content.Schema = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel));

            return putOperation;
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
            IUriNode onProperty = graph.CreateUriNode(VocabularyHelper.OWL.onProperty);
            IUriNode cardinality = graph.CreateUriNode(VocabularyHelper.OWL.cardinality);
            IUriNode qualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.qualifiedCardinality);
            IUriNode someValuesFrom = graph.CreateUriNode(VocabularyHelper.OWL.someValuesFrom);
            IUriNode minCardinality = graph.CreateUriNode(VocabularyHelper.OWL.minCardinality);
            IUriNode minQualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.minQualifiedCardinality);
            IUriNode maxCardinality = graph.CreateUriNode(VocabularyHelper.OWL.maxCardinality);
            IUriNode maxQualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.maxQualifiedCardinality);

            if (restriction.GetNodesViaProperty(onProperty).UriNodes().Count(node => node.IsOntologyProperty()) == 1)
            {
                PropertyConstraint pc = new PropertyConstraint();
                IUriNode restrictionPropertyNode = restriction.GetNodesViaProperty(onProperty).UriNodes().First(node => node.IsOntologyProperty());
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
            // We only deal with named ontologies
            if (importedOntology.IsNamed()) {

                // Parse and load ontology from its URI
                Uri importedOntologyUri = ((IUriNode)importedOntology.Resource).Uri;
                OntologyGraph importedOntologyGraph = new OntologyGraph();

                try
                {
                    UriLoader.Load(importedOntologyGraph, importedOntologyUri);
                }
                catch (RdfParseException e)
                {
                    Console.Write(e.Message);
                    Console.Write(e.StackTrace);
                }

                // Only proceed if we have not seen this graph before, otherwise we
                // risk unecessary fetches and computation, and possibly import loops.
                // TODO: Replace the below with proper equals() comparisson on the Ontology
                if (!importedOntologies.Select(ontology => ontology.Graph).Contains(importedOntologyGraph)) { 

                    // Set up a new ontology metadata object from the imported ontology graph,
                    // add it to the global imports collection and traverse its import hierarchy
                    // transitively (if we haven't imported it before).
                    // Note that this ontology IRI often differs from the URI given by
                    // the importing ontology above (from which the file was fetched),
                    // due to .htaccess redirects, version URIs, etc.
                    Ontology importedOntologyFromSelfDefinition = importedOntologyGraph.GetOntology();
                
                    importedOntologies.Add(importedOntologyFromSelfDefinition);
                    foreach (Ontology subImport in importedOntologyFromSelfDefinition.Imports)
                    {
                        LoadImport(subImport);
                    }
                }
            }
        }
    }
}