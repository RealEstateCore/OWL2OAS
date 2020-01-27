using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
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
        /// Custom comparer for Ontology objects, based on W3C OWL2 specification for version IRIs.
        /// See https://www.w3.org/TR/owl2-syntax/#Ontology_IRI_and_Version_IRI
        /// </summary>
        class OntologyComparer : IEqualityComparer<Ontology>
        {

            public bool Equals(Ontology x, Ontology y)
            {
                return
                    !x.HasVersionIri() && !y.HasVersionIri() && (x.GetIri() == y.GetIri()) ||
                    x.HasVersionIri() && y.HasVersionIri() && (x.GetIri() == y.GetIri()) && (x.GetVersionIri() == y.GetVersionIri());
            }

            // Method borrowed from https://stackoverflow.com/a/263416
            public int GetHashCode(Ontology x)
            {
                // Generate partial hashes from identify-carrying fields, i.e., ontology IRI 
                // and version IRI; if no version IRI exists, default to partial hash of 0.
                int oidHash = x.GetIri().GetHashCode();
                int vidHash = x.HasVersionIri() ? x.GetVersionIri().GetHashCode() : 0;

                // 
                int hash = 23;
                hash = hash * 37 + oidHash;
                hash = hash * 37 + vidHash;
                return hash;
            }
        }

        /// <summary>
        /// The ontology being parsed.
        /// </summary>
        private static Ontology rootOntology;

        /// <summary>
        /// Set of transitively imported child ontologies.
        /// </summary>
        private static readonly HashSet<Ontology> importedOntologies = new HashSet<Ontology>(new OntologyComparer());

        private static Dictionary<string, HashSet<string>> requiredPropertiesForEachClass = new Dictionary<string, HashSet<string>>();

        private static Dictionary<Uri, string> namespacePrefixes = new Dictionary<Uri, string>();

        // Used in string handling etc
        private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

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
        public struct PropertyCardinalityConstraints
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
                   .WithParsed(o =>
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
                   .WithNotParsed((errs) =>
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

            // Generate and add the Context schema
            document.components.schemas.Add("Context", GenerateContextSchema());

            // Generate and add the Loaded Ontologies path
            document.paths = new Dictionary<string, OASDocument.Path>
            {
                { "/LoadedOntologies", GenerateLoadedOntologiesPath() }
            };

            // Parse OWL classes.For each class, create a schema and a path
            GenerateAtomicClassSchemas(rootOntologyGraph, document);
            GenerateClassPaths(rootOntologyGraph, document);

            // Also parse classes in imports
            foreach (Ontology importedOntology in importedOntologies)
            {
                GenerateAtomicClassSchemas(importedOntology.Graph as OntologyGraph, document);
                GenerateClassPaths(importedOntology.Graph as OntologyGraph, document);
            }

            // Dispose all open graphs
            rootOntologyGraph.Dispose();

            DumpAsYaml(document);
        }

        private static OASDocument.Path GenerateLoadedOntologiesPath()
        {
            OASDocument.Path loadedOntologiesPath = new OASDocument.Path
            {
                get = new OASDocument.Operation
                {
                    summary = "Get the set of ontologies that were imported by the root ontology when the API was generated.",
                    responses = new Dictionary<string, OASDocument.Response>
                        {
                            { "200", new OASDocument.Response
                                {
                                    description = "A list of ontologies used to generate this API. Note that while the prefix names used here correspond with the ones given in the JSON-LD @context for the supported data types, the prefix mapping in the API " +
                                    "is based on the Ontology IRIs given in those @context blocks, which may differ from the values given here (which give priority to version IRIs).",
                                    content = new Dictionary<string, OASDocument.Content>
                                    {
                                        { "application/json", new OASDocument.Content
                                            {
                                                schema = GenerateLoadedOntologiesSchema()
                                            }
                                        }
                                    }
                                }
                            }
                        }
                }
            };
            return loadedOntologiesPath;
        }

        private static OASDocument.Schema GenerateLoadedOntologiesSchema()
        {
            OASDocument.ComplexSchema loadedOntologiesSchema = new OASDocument.ComplexSchema
            {
                properties = new Dictionary<string, OASDocument.Schema>(),
                required = new List<string>()
            };

            // Add each imported ontology to the @context
            foreach (Ontology importedOntology in importedOntologies)
            {
                OASDocument.PrimitiveSchema importedOntologySchema = new OASDocument.PrimitiveSchema
                {
                    type = "string",
                    format = "uri",
                    Enumeration = new string[] { importedOntology.GetVersionOrOntologyIri().ToString() }
                };

                // Fetch shortname as generated at import load time
                string ontologyShortname = namespacePrefixes[importedOntology.GetIri()];
                loadedOntologiesSchema.properties.Add(ontologyShortname, importedOntologySchema);
                loadedOntologiesSchema.required.Add(ontologyShortname);
            }

            return loadedOntologiesSchema;
        }

        private static OASDocument.Schema GenerateContextSchema()
        {
            // Set @context/@vocab based on the ontology IRI
            OASDocument.PrimitiveSchema vocabularySchema = new OASDocument.PrimitiveSchema
            {
                type = "string",
                format = "uri",
                DefaultValue = rootOntology.GetIri().ToString()
            };
            // Set @context/@base (default data namespace)
            OASDocument.PrimitiveSchema baseNamespaceSchema = new OASDocument.PrimitiveSchema
            {
                type = "string",
                format = "uri"
            };
            // Hardcoded rdfs:label for @context
            OASDocument.PrimitiveSchema labelContextSchema = new OASDocument.PrimitiveSchema
            {
                type = "string",
                format = "uri",
                DefaultValue = VocabularyHelper.RDFS.label.ToString()
            };
            // Mash it all together into a @context block
            OASDocument.ComplexSchema contextSchema = new OASDocument.ComplexSchema
            {
                required = new List<string> { "@vocab", "@base", "label" },
                properties = new Dictionary<string, OASDocument.Schema> {
                    { "@vocab", vocabularySchema },
                    { "@base", baseNamespaceSchema },
                    { "label", labelContextSchema }
                }
            };
            // Add each prefix to the @context (sorting by shortname, e.g., dictionary value, for usability)
            List<KeyValuePair<Uri,string>> prefixMappingsList = namespacePrefixes.ToList();
            prefixMappingsList.Sort((pair1, pair2) => string.CompareOrdinal(pair1.Value, pair2.Value));
            foreach (KeyValuePair<Uri, string> prefixMapping in prefixMappingsList)
            {
                OASDocument.PrimitiveSchema importedVocabularySchema = new OASDocument.PrimitiveSchema
                {
                    type = "string",
                    format = "uri",
                    DefaultValue = prefixMapping.Key.ToString()
                };
                contextSchema.properties.Add(prefixMapping.Value, importedVocabularySchema);
                contextSchema.required.Add(prefixMapping.Value);
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
                throw new RdfException($"Ontology <{rootOntology}> does not have an <dc:title> annotation.");
            }
            if (!rootOntology.VersionInfo.Any())
            {
                throw new RdfException($"Ontology <{rootOntology}> does not have an <owl:versionInfo> annotation.");
            }
            IUriNode ccLicense = rootOntologyGraph.CreateUriNode(VocabularyHelper.CC.license);
            if (!rootOntology.GetNodesViaProperty(ccLicense).Any(objNode => objNode.IsLiteral() || objNode.IsUri()))
            {
                throw new RdfException($"Ontology <{rootOntology}> does not have an <cc:license> annotation that is a URI or literal.");
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
                ILiteralNode ontologyDescriptionLiteral = rootOntology.GetNodesViaProperty(dcDescription).LiteralNodes().OrderBy(description => description.HasLanguage()).First();
                string ontologyDescription = ontologyDescriptionLiteral.Value.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br/>", StringComparison.Ordinal);
                docInfo.description = $"The documentation below is automatically extracted from a <dc:description> annotation on the ontology {rootOntology}:<br/><br/>*{ontologyDescription}*";
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
            return $"{prefix}:{localName}";
        }

        private static void GenerateAtomicClassSchemas(OntologyGraph graph, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass oClass in graph.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(graph, oClass);

                // Create schema for class and corresponding properties dict
                OASDocument.ComplexSchema schema = new OASDocument.ComplexSchema();
                schema.properties = new Dictionary<string, OASDocument.Schema>();

                // Set up the required properties set, used in subsequently generating HTTP operations
                requiredPropertiesForEachClass.Add(classLabel, new HashSet<string>());

                // Iterate over superclasses, extract cardinality constraints from OWL restrictions
                // This dictionary maps properties to the found constraints
                Dictionary<IUriNode, PropertyCardinalityConstraints> constraints = new Dictionary<IUriNode, PropertyCardinalityConstraints>();
                foreach (OntologyClass superClass in oClass.SuperClasses)
                {
                    if (superClass.IsRestriction())
                    {
                        // Only proceed if the property restriction is well-formed, i.e., it actually has a property
                        if (superClass.HasRestrictionProperty()) 
                        {
                            IUriNode constraintProperty = superClass.GetRestrictionProperty();

                            // Extract prior constraints for property, if we've found any before;
                            // otherwise create new constraints struct
                            PropertyCardinalityConstraints pc;
                            if (constraints.ContainsKey(constraintProperty))
                            {
                                pc = constraints[constraintProperty];
                            }
                            else
                            {
                                pc = new PropertyCardinalityConstraints();
                            }

                            // Extract cardinality from restriction class (in a well-formed ontology, 
                            // only one of the below will be non-zero for each restriction)
                            int min = GetMinCardinality(superClass);
                            int exactly = GetExactCardinality(superClass);
                            int max = GetMaxCardinality(superClass);

                            // Update prior constraints with new values, if they are non-zero
                            if (min != 0)
                                pc.min = min;
                            if (exactly != 0)
                                pc.exactly = exactly;
                            if (max != 0)
                                pc.max = max;

                            // Put the constraint back on the constraints dictionary
                            constraints[constraintProperty] = pc;
                        }
                    }
                }

                // Add reference to context schema
                schema.properties.Add("@context", new OASDocument.ReferenceSchema("Context"));

                // Add @id for all entries
                OASDocument.PrimitiveSchema idSchema = new OASDocument.PrimitiveSchema();
                idSchema.type = "string";
                schema.properties.Add("@id", idSchema);

                // Add @type for all entries
                OASDocument.PrimitiveSchema typeSchema = new OASDocument.PrimitiveSchema
                {
                    type = "string",
                    DefaultValue = GetPrefixedOrFullName(oClass)
                };
                schema.properties.Add("@type", typeSchema);

                // @context is mandatory
                schema.required = new List<string>() { "@context" };

                // Label is an option for all entries
                OASDocument.PrimitiveSchema labelSchema = new OASDocument.PrimitiveSchema();
                labelSchema.type = "string";
                schema.properties.Add("label", labelSchema);

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
                        OASDocument.Schema outputSchema;

                        // Check if multiple values are allowed for this property. By default they are.
                        bool propertyAllowsMultipleValues = true
                            && (!constraints.ContainsKey(propertyNode) || !constraints[propertyNode].MaxOne())
                            && !property.IsFunctional();

                        // If this is a data property
                        if (property.IsDataProperty())
                        {
                            // Set up the (possibly later on nested) property block
                            OASDocument.PrimitiveSchema dataPropertySchema = new OASDocument.PrimitiveSchema();

                            // Fall back to string representation for unknown types
                            dataPropertySchema.type = "string";

                            // If range is named, check if it is an XSD type that can be parsed into 
                            // an OAS type and format (note: not all XSD types are covered)
                            if (property.Ranges.First().IsNamed()) { 
                                string rangeXsdType = ((UriNode)property.Ranges.First().Resource).GetLocalName();
                                if (xsdOsaMappings.ContainsKey(rangeXsdType))
                                {
                                    dataPropertySchema.type = xsdOsaMappings[rangeXsdType].Item1;
                                    string format = xsdOsaMappings[rangeXsdType].Item2;
                                    if (format.Length > 0)
                                    {
                                        dataPropertySchema.format = format;
                                    }
                                }
                            }

                            // Assign return value
                            outputSchema = dataPropertySchema;
                        }
                        else
                        {
                            // This is an Object property
                            // Set up the (possibly later on nested) property block
                            OASDocument.Schema uriPropertySchema;

                            // Set the type of the property; locally defined named classes can be either URI or full schema representation
                            OntologyClass range = property.Ranges.First();
                            if (range.IsNamed())
                            {
                                uriPropertySchema = new OASDocument.ComplexSchema
                                {
                                    properties = new Dictionary<string, OASDocument.Schema> {
                                        { "@id", new OASDocument.PrimitiveSchema { type = "string" } },
                                        { "@type", new OASDocument.PrimitiveSchema { type = "string", DefaultValue = GetPrefixedOrFullName(range) } }
                                    },
                                    required = new List<string> { "@id" }
                                };
                            }
                            else
                            {
                                // Fall back to string representation (for more complex anoymous OWL constructs, e.g., intersections etc)
                                uriPropertySchema = new OASDocument.PrimitiveSchema
                                {
                                    type = "string"
                                };
                            }

                            outputSchema = uriPropertySchema;
                        }

                        // If this field allows multiple values (as is the default), wrap it in an array
                        if (propertyAllowsMultipleValues)
                        {
                            OASDocument.ArraySchema propertyArraySchema = new OASDocument.ArraySchema();
                            propertyArraySchema.items = outputSchema;
                            // Assign constraints on the array, if any
                            if (constraints.ContainsKey(propertyNode))
                            {
                                PropertyCardinalityConstraints pc = constraints[propertyNode];
                                if (pc.min != 0)
                                    propertyArraySchema.minItems = pc.min;
                                if (pc.max != 0)
                                    propertyArraySchema.maxItems = pc.max;
                                if (pc.exactly != 0)
                                    propertyArraySchema.maxItems = propertyArraySchema.minItems = pc.exactly;
                            }
                            schema.properties.Add(propertyLabel, propertyArraySchema);
                        }
                        else
                        {
                            // This is a single-valued property, assign it w/o the array
                            schema.properties.Add(propertyLabel, outputSchema);
                        }

                        // Tag any min 1 or exactly 1 properties as required
                        if (constraints.ContainsKey(propertyNode) && constraints[propertyNode].IsRequired())
                        {
                            requiredPropertiesForEachClass[classLabel].Add(propertyLabel);
                        }
                    }
                }
                document.components.schemas.Add(classLabel, schema);
            }
        }

        private static string GetPrefixedOrFullName(OntologyResource resource)
        {
            if (!resource.IsNamed())
            {
                throw new RdfException($"Resource '{resource.ToString()}' is anonymous.");
            }
            // Fall back to full URI name, in case qname cannot be generated
            string resourceName = resource.GetIri().ToString();
            Uri resourceNamespace = resource.GetNamespace();
            if (namespacePrefixes.ContainsKey(resourceNamespace))
            {
                resourceName = $"{namespacePrefixes[resourceNamespace]}:{resource.GetLocalName()}";
            }
            return resourceName;
        }

        private static void GenerateClassPaths(OntologyGraph graph, OASDocument document)
        {
            // Iterate over all classes
            foreach (OntologyClass oClass in graph.OwlClasses.Where(oClass => oClass.IsNamed() && !oClass.IsDeprecated()))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(graph, oClass);

                // Create paths and corresponding operations for class
                document.paths.Add($"/{classLabel}", new OASDocument.Path
                {
                    get = GenerateGetEntitiesOperation(classLabel, oClass),
                    post = GeneratePostEntityOperation(classLabel)
                });
                document.paths.Add($"/{classLabel}/{{id}}", new OASDocument.Path
                {
                    get = GenerateGetEntityByIdOperation(classLabel),
                    patch = GeneratePatchToIdOperation(classLabel),
                    put = GeneratePutToIdOperation(classLabel),
                    delete = GenerateDeleteByIdOperation(classLabel)
                });
            }
        }

        private static OASDocument.Operation GenerateDeleteByIdOperation(string classLabel)
        {
            OASDocument.Operation deleteOperation = new OASDocument.Operation();
            deleteOperation.summary = $"Delete a '{classLabel}' object.";
            deleteOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to delete.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema {
                    type = "string"
                }
            };
            deleteOperation.parameters.Add(idParameter);

            // Create each of the HTTP response types
            OASDocument.Response response404 = new OASDocument.Response();
            response404.description = $"An object of type '{classLabel}' with the specified ID was not found.";
            deleteOperation.responses.Add("404", response404);

            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            deleteOperation.responses.Add("500", response500);

            OASDocument.Response response200 = new OASDocument.Response();
            response200.description = $"'{classLabel}' entity was successfully deleted.";
            deleteOperation.responses.Add("200", response200);

            return deleteOperation;
        }

        private static OASDocument.Operation GeneratePostEntityOperation(string classLabel)
        {
            OASDocument.Operation postOperation = new OASDocument.Operation();
            postOperation.summary = $"Create a new '{classLabel}' object.";
            postOperation.tags.Add(classLabel);

            OASDocument.Parameter bodyParameter = new OASDocument.Parameter
            {
                name = "entity",
                description = $"New '{classLabel}' entity that is to be added.",
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new OASDocument.ReferenceSchema(HttpUtility.UrlEncode(classLabel))
            };
            postOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            postOperation.responses.Add("500", response500);

            OASDocument.Response response400 = new OASDocument.Response();
            response400.description = "Bad Request";
            postOperation.responses.Add("400", response400);

            OASDocument.Response response201 = new OASDocument.Response();
            response201.description = "Entity was successfully created (new representation returned).";
            postOperation.responses.Add("201", response201);

            response201.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content201 = new OASDocument.Content();
            response201.content.Add("application/jsonld", content201);

            // Response is per previously defined schema
            content201.schema = MergeAtomicSchemaWithRequiredProperties(classLabel);

            return postOperation;
        }

        private static OASDocument.Operation GenerateGetEntityByIdOperation(string classLabel)
        {
            OASDocument.Operation getOperation = new OASDocument.Operation();
            getOperation.summary = $"Get a specific '{classLabel}' object.";
            getOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to return.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema
                {
                    type = "string"
                }
            };
            getOperation.parameters.Add(idParameter);

            // Create each of the HTTP response types
            OASDocument.Response response404 = new OASDocument.Response();
            response404.description = $"An object of type '{classLabel}' with the specified ID was not found.";
            getOperation.responses.Add("404", response404);

            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            getOperation.responses.Add("500", response500);

            OASDocument.Response response200 = new OASDocument.Response();
            response200.description = $"A '{classLabel}' object.";
            getOperation.responses.Add("200", response200);

            response200.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content200 = new OASDocument.Content();
            response200.content.Add("application/jsonld", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithRequiredProperties(classLabel);

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

            // Add sort param
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "sortParam" });

            // Add parameters for each property field that can be expressed on this class
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

                // If range is named, check if it is an XSD type that can be parsed into 
                // an OAS type and format (note: not all XSD types are covered)
                if (property.Ranges.First().IsNamed())
                {
                    string rangeXsdType = ((UriNode)property.Ranges.First().Resource).GetLocalName();
                    if (xsdOsaMappings.ContainsKey(rangeXsdType))
                    {
                        propertyType = xsdOsaMappings[rangeXsdType].Item1;
                        propertyFormat = xsdOsaMappings[rangeXsdType].Item2;
                    }
                }

                // Select a filter schema to use for parameter formats where it is applicable
                string filterSchema = "";
                switch (propertyType)
                {
                    case "string":
                        switch (propertyFormat)
                        {
                            case "date-time":
                                filterSchema = "DateTimeFilter";
                                break;

                            default:
                                filterSchema = "StringFilter";
                                break;
                        }
                        break;

                    case "integer":
                        filterSchema = "IntegerFilter";
                        break;

                    case "number":
                        filterSchema = "NumberFilter";
                        break;
                }

                // Base the property schema on the filter, if one was selected above
                // Otherwise, just do a simple type-based schema, possibly with format if one was found
                OASDocument.Schema propertySchema;
                if (filterSchema.Length > 0)
                {
                    propertySchema = new OASDocument.ReferenceSchema(filterSchema);
                }
                else
                {
                    if (propertyFormat.Length > 0)
                    {
                        propertySchema = new OASDocument.PrimitiveSchema
                        {
                            type = propertyType,
                            format = propertyFormat
                        };
                    }
                    else {
                        propertySchema = new OASDocument.PrimitiveSchema
                        {
                            type = propertyType
                        };
                    }
                }

                OASDocument.Parameter parameter = new OASDocument.Parameter
                {
                    name = propertyLabel,
                    description = $"Filter value on property '{propertyLabel}'.",
                    required = false,
                    schema = propertySchema,
                    InField = OASDocument.Parameter.InFieldValues.query
                };

                if (filterSchema.Length > 0)
                {
                    parameter.style = "deepObject";
                }

                getOperation.parameters.Add(parameter);
            }

            // Create each of the HTTP response types
            OASDocument.Response response400 = new OASDocument.Response();
            response400.description = "Bad Request";
            getOperation.responses.Add("400", response400);

            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            getOperation.responses.Add("500", response500);

            OASDocument.Response response200 = new OASDocument.Response();
            response200.description = "An array of '" + classLabel + "' objects.";
            getOperation.responses.Add("200", response200);

            response200.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content200 = new OASDocument.Content();
            response200.content.Add("application/jsonld", content200);

            // Generate schema with required fields propped on via allOf (if any required fields exist)
            OASDocument.Schema classSchemaWithRequiredProperties = MergeAtomicSchemaWithRequiredProperties(classLabel);

            // Generate wrapper Hydra schema (https://www.hydra-cg.com/spec/latest/core/)
            OASDocument.Schema hydraSchema = new OASDocument.AllOfSchema
            {
                allOf = new OASDocument.Schema[]
                {
                    new OASDocument.ReferenceSchema("HydraCollectionWrapper"),
                    new OASDocument.ComplexSchema
                    {
                        properties = new Dictionary<string, OASDocument.Schema>
                        {
                            {"member", new OASDocument.ArraySchema  {
                                items = classSchemaWithRequiredProperties
                            } }
                        }
                    }
                }
            };

            // Wrap responses in array
            content200.schema = hydraSchema;

            // Return
            return getOperation;
        }

        private static OASDocument.Schema MergeAtomicSchemaWithRequiredProperties(string classLabel)
        {
            OASDocument.Schema itemSchema;
            if (requiredPropertiesForEachClass[classLabel].Count == 0)
            {
                itemSchema = new OASDocument.ReferenceSchema(HttpUtility.UrlEncode(classLabel));
            }
            else
            {
                itemSchema = new OASDocument.AllOfSchema
                {
                    allOf = new OASDocument.Schema[] {
                        new OASDocument.ReferenceSchema(HttpUtility.UrlEncode(classLabel)),
                        new OASDocument.ComplexSchema {
                            required = requiredPropertiesForEachClass[classLabel].ToList()
                        }
                    }
                };
            }
            return itemSchema;
        }

        private static OASDocument.Operation GeneratePatchToIdOperation(string classLabel)
        {
            OASDocument.Operation patchOperation = new OASDocument.Operation();
            patchOperation.summary = $"Update a single property on a specific '{classLabel}' object.";
            patchOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to update.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema {
                    type = "string"
                }
            };
            OASDocument.Parameter bodyParameter = new OASDocument.Parameter
            {
                name = "patch",
                description = "A single JSON key-value pair (plus @context), indicating the property to update and its new value. Note that the Swagger UI does not properly show the size constraint on this parameter; but the underlying OpenAPI Specification document does.",
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new OASDocument.AllOfSchema
                {
                    allOf = new OASDocument.Schema[] {
                        new OASDocument.ReferenceSchema(HttpUtility.UrlEncode(classLabel)),
                        new OASDocument.ComplexSchema {
                            required =  new List<string> { "@context" },
                            minProperties = 2,
                            maxProperties = 2
                        }
                    }
                }
            };
            patchOperation.parameters.Add(idParameter);
            patchOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response400 = new OASDocument.Response();
            response400.description = "Bad Request";
            patchOperation.responses.Add("400", response400);

            OASDocument.Response response404 = new OASDocument.Response();
            response404.description = $"An object of type '{classLabel}' with the specified ID was not found.";
            patchOperation.responses.Add("404", response404);

            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            patchOperation.responses.Add("500", response500);

            OASDocument.Response response200 = new OASDocument.Response();
            response200.description = "Entity was updated successfully (new representation returned).";
            patchOperation.responses.Add("200", response200);

            response200.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content200 = new OASDocument.Content();
            response200.content.Add("application/jsonld", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithRequiredProperties(classLabel);

            return patchOperation;
        }

        private static OASDocument.Operation GeneratePutToIdOperation(string classLabel)
        {
            OASDocument.Operation putOperation = new OASDocument.Operation();
            putOperation.summary = $"Update an existing '{classLabel}' entity.";
            putOperation.tags.Add(classLabel);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to update.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema {
                    type = "string"
                }
            };
            OASDocument.Parameter bodyParameter = new OASDocument.Parameter
            {
                name = "entity",
                description = $"Updated data for '{classLabel}' entity.",
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new OASDocument.ReferenceSchema(HttpUtility.UrlEncode(classLabel))
            };
            putOperation.parameters.Add(idParameter);
            putOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response400 = new OASDocument.Response();
            response400.description = "Bad Request";
            putOperation.responses.Add("400", response400);

            OASDocument.Response response404 = new OASDocument.Response();
            response404.description = $"An object of type '{classLabel}' with the specified ID was not found.";
            putOperation.responses.Add("404", response404);

            OASDocument.Response response500 = new OASDocument.Response();
            response500.description = "Internal Server Error";
            putOperation.responses.Add("500", response500);

            OASDocument.Response response200 = new OASDocument.Response();
            response200.description = "Entity was updated successfully (new representation returned).";
            putOperation.responses.Add("200", response200);

            response200.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content200 = new OASDocument.Content();
            response200.content.Add("application/jsonld", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithRequiredProperties(classLabel);

            return putOperation;
        }

        private static void DumpAsYaml(object data)
        {
            var stringBuilder = new StringBuilder();
            var serializerBuilder = new SerializerBuilder().DisableAliases();
            var serializer = serializerBuilder.Build();
            stringBuilder.AppendLine(serializer.Serialize(data));
            Console.WriteLine(stringBuilder);
            Console.WriteLine("");
        }

        private static int GetMinCardinality(OntologyClass restriction)
        {
            OntologyGraph graph = restriction.Graph as OntologyGraph;
            IUriNode someValuesFrom = graph.CreateUriNode(VocabularyHelper.OWL.someValuesFrom);
            IUriNode minCardinality = graph.CreateUriNode(VocabularyHelper.OWL.minCardinality);
            IUriNode minQualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.minQualifiedCardinality);

            IEnumerable<INode> minCardinalities = restriction.GetNodesViaProperty(minCardinality).Union(restriction.GetNodesViaProperty(minQualifiedCardinality));
            if (minCardinalities.LiteralNodes().Count() == 1 &&
                minCardinalities.LiteralNodes().First().IsInteger())
            {
                return int.Parse(minCardinalities.LiteralNodes().First().Value, invariantCulture);
            }

            if (restriction.GetNodesViaProperty(someValuesFrom).Count() == 1)
            {
                return 1;
            }

            return 0;
        }

        private static int GetExactCardinality(OntologyClass restriction)
        {
            OntologyGraph graph = restriction.Graph as OntologyGraph;
            IUriNode cardinality = graph.CreateUriNode(VocabularyHelper.OWL.cardinality);
            IUriNode qualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.qualifiedCardinality);
            IUriNode onClass = graph.CreateUriNode(VocabularyHelper.OWL.onClass);

            IEnumerable<INode> exactCardinalities = restriction.GetNodesViaProperty(cardinality);
            if (exactCardinalities.LiteralNodes().Count() == 1 &&
                exactCardinalities.LiteralNodes().First().IsInteger())
            {
                return int.Parse(exactCardinalities.LiteralNodes().First().Value, invariantCulture);
            }

            IEnumerable<INode> exactQualifiedCardinalities = restriction.GetNodesViaProperty(qualifiedCardinality);
            if (exactQualifiedCardinalities.LiteralNodes().Count() == 1 &&
                exactQualifiedCardinalities.LiteralNodes().First().IsInteger())
            {
                IEnumerable<IUriNode> qualifierClasses = restriction.GetNodesViaProperty(onClass).UriNodes();
                if (qualifierClasses.Count() == 1 && qualifierClasses.First().Uri.Equals(VocabularyHelper.OWL.Thing)) { 
                    return int.Parse(exactQualifiedCardinalities.LiteralNodes().First().Value, invariantCulture);
                }
            }

            return 0;
        }

        private static int GetMaxCardinality(OntologyClass restriction)
        {
            OntologyGraph graph = restriction.Graph as OntologyGraph;
            IUriNode maxCardinality = graph.CreateUriNode(VocabularyHelper.OWL.maxCardinality);
            IUriNode maxQualifiedCardinality = graph.CreateUriNode(VocabularyHelper.OWL.maxQualifiedCardinality);
            IUriNode onClass = graph.CreateUriNode(VocabularyHelper.OWL.onClass);

            IEnumerable<INode> maxCardinalities = restriction.GetNodesViaProperty(maxCardinality);
            if (maxCardinalities.LiteralNodes().Count() == 1 &&
                maxCardinalities.LiteralNodes().First().IsInteger())
            {
                return int.Parse(maxCardinalities.LiteralNodes().First().Value, invariantCulture);
            }

            IEnumerable<INode> maxQualifiedCardinalities = restriction.GetNodesViaProperty(maxQualifiedCardinality);
            if (maxQualifiedCardinalities.LiteralNodes().Count() == 1 &&
                maxQualifiedCardinalities.LiteralNodes().First().IsInteger())
            {
                IEnumerable<IUriNode> qualifierClasses = restriction.GetNodesViaProperty(onClass).UriNodes();
                if (qualifierClasses.Count() == 1 && qualifierClasses.First().Uri.Equals(VocabularyHelper.OWL.Thing))
                {
                    return int.Parse(maxQualifiedCardinalities.LiteralNodes().First().Value, invariantCulture);
                }
            }

            return 0;
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

                // Parse and load ontology from the stated import URI
                Uri importUri = importedOntology.GetIri();

                //Uri importedOntologyUri = ((IUriNode)importedOntology.Resource).Uri;
                OntologyGraph fetchedOntologyGraph = new OntologyGraph();

                try
                {
                    UriLoader.Load(fetchedOntologyGraph, importUri);
                }
                catch (RdfParseException e)
                {
                    Console.Write(e.Message);
                    Console.Write(e.StackTrace);
                }

                // Set up a new ontology metadata object from the retrieved ontology graph.
                // This is needed since this ontology's self-defined IRI or version IRI often 
                // differs from the IRI through which it was imported (i.e., importedOntology in 
                // this method's signature), due to .htaccess redirects, version URIs, etc.
                Ontology importedOntologyFromFetchedGraph = fetchedOntologyGraph.GetOntology();

                // Only proceed if the retrieved ontology has an IRI
                if (importedOntologyFromFetchedGraph.IsNamed())
                { 
                    // Only proceed if we have not seen this fetched ontology before, otherwise we risk 
                    // unecessary fetches and computation, and possibly import loops.
                    // Note that importedOntologies uses a custom comparer from DotNetRdfExtensions, 
                    // since the Ontology class does not implement IComparable
                    if (!importedOntologies.Contains(importedOntologyFromFetchedGraph))
                    {
                        // Add the fetched ontology to the namespace prefix index
                        // (tacking on _1, _2, etc. to the shortname if it exists since before, 
                        // since we need all prefix names to be unique).
                        string importedOntologyShortname = importedOntologyFromFetchedGraph.GetShortName();
                        int i = 1;
                        while (namespacePrefixes.ContainsValue(importedOntologyShortname))
                        {
                            importedOntologyShortname = importedOntologyShortname.Split('_')[0] + "_" + i;
                            i++;
                        }
                        namespacePrefixes.Add(importedOntologyFromFetchedGraph.GetIri(), importedOntologyShortname);

                        // Add imported ontology to the global imports collection and traverse its 
                        // import hierarchy transitively
                        importedOntologies.Add(importedOntologyFromFetchedGraph);
                        foreach (Ontology subImport in importedOntologyFromFetchedGraph.Imports)
                        {
                            LoadImport(subImport);
                        }
                    }
                }
            }
        }
    }
}