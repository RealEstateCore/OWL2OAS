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

                // Create patch schema (alternate schema for the PATCH operation)
                OASDocument.Schema patchSchema = new OASDocument.Schema();
                patchSchema.properties = new Dictionary<string, OASDocument.Property>();
                patchSchema.minProperties = 2;
                patchSchema.maxProperties = 2;

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
                schema.properties.Add("@context", new OASDocument.SchemaReferenceProperty("Context"));
                patchSchema.properties.Add("@context", new OASDocument.SchemaReferenceProperty("Context"));

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

                // @context is mandatory
                schema.required = new List<string>() { "@context" };
                patchSchema.required = new List<string>() { "@context" };

                // Label is an option for all entries
                OASDocument.Property labelProperty = new OASDocument.Property();
                labelProperty.type = "string";
                schema.properties.Add("label", labelProperty);
                patchSchema.properties.Add("label", labelProperty);

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

                            // If range is named, check if it is an XSD type that can be parsed into 
                            // an OAS type and format (note: not all XSD types are covered)
                            if (property.Ranges.First().IsNamed()) { 
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
                                PropertyCardinalityConstraints pc = constraints[propertyNode];
                                if (pc.min != 0)
                                    arrayProperty.minItems = pc.min;
                                if (pc.max != 0)
                                    arrayProperty.maxItems = pc.max;
                                if (pc.exactly != 0)
                                    arrayProperty.maxItems = arrayProperty.minItems = pc.exactly;
                            }
                            schema.properties.Add(propertyLabel, arrayProperty);
                            patchSchema.properties.Add(propertyLabel, arrayProperty);
                        }
                        else
                        {
                            // This is a single-valued property, assign it w/o the array
                            schema.properties.Add(propertyLabel, outputProperty);
                            patchSchema.properties.Add(propertyLabel, outputProperty);
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
                document.components.schemas.Add(classLabel + "-PATCH", patchSchema);
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
                    patch = GeneratePatchToIdOperation(classLabel),
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
                Dictionary<string, string> propertySchema;
                if (filterSchema.Length > 0)
                {
                    string filterSchemaReference = string.Format("#/components/schemas/{0}", filterSchema);
                    propertySchema = new Dictionary<string, string>
                    {
                        { "$ref", filterSchemaReference }
                    };
                }
                else
                {
                    propertySchema = new Dictionary<string, string>
                    {
                        { "type", propertyType }
                    };

                    if (propertyFormat.Length > 0)
                    {
                        propertySchema.Add("format", propertyFormat);
                    }
                }

                OASDocument.Parameter parameter = new OASDocument.Parameter
                {
                    name = propertyLabel,
                    description = string.Format("Filter value on property '{0}'.", propertyLabel),
                    required = false,
                    schema = propertySchema,
                    InField = OASDocument.Parameter.InFieldValues.query
                };

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

        private static OASDocument.Operation GeneratePatchToIdOperation(string classLabel)
        {
            OASDocument.Operation patchOperation = new OASDocument.Operation();
            patchOperation.summary = string.Format("Update a single property on a specific '{0}' object.", classLabel);
            patchOperation.tags.Add(classLabel);

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
                name = "patch",
                description = "A single JSON key-value pair (plus @context), indicating the property to update and its new value. Note that the Swagger UI does not properly show the size constraint on this parameter; but the underlying OpenAPI Specification document does.",
                InField = OASDocument.Parameter.InFieldValues.header,
                required = true,
                schema = new Dictionary<string, string> {
                            { "$ref", "#/components/schemas/" + HttpUtility.UrlEncode(classLabel + "-PATCH") }
                        }
            };
            patchOperation.parameters.Add(idParameter);
            patchOperation.parameters.Add(bodyParameter);

            // Create each of the HTTP response types
            OASDocument.Response response = new OASDocument.Response();
            response.description = "Entity was updated successfully (new representation returned).";
            patchOperation.responses.Add("200", response);

            response.content = new Dictionary<string, OASDocument.Content>();
            OASDocument.Content content = new OASDocument.Content();
            response.content.Add("application/jsonld", content);

            // Response is per previously defined schema
            content.Schema = new OASDocument.SchemaReferenceProperty(HttpUtility.UrlEncode(classLabel));

            return patchOperation;
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
                return int.Parse(minCardinalities.LiteralNodes().First().Value);
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
                return int.Parse(exactCardinalities.LiteralNodes().First().Value);
            }

            IEnumerable<INode> exactQualifiedCardinalities = restriction.GetNodesViaProperty(qualifiedCardinality);
            if (exactQualifiedCardinalities.LiteralNodes().Count() == 1 &&
                exactQualifiedCardinalities.LiteralNodes().First().IsInteger())
            {
                IEnumerable<IUriNode> qualifierClasses = restriction.GetNodesViaProperty(onClass).UriNodes();
                if (qualifierClasses.Count() == 1 && qualifierClasses.First().Uri.Equals(VocabularyHelper.OWL.Thing)) { 
                    return int.Parse(exactQualifiedCardinalities.LiteralNodes().First().Value);
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
                return int.Parse(maxCardinalities.LiteralNodes().First().Value);
            }

            IEnumerable<INode> maxQualifiedCardinalities = restriction.GetNodesViaProperty(maxQualifiedCardinality);
            if (maxQualifiedCardinalities.LiteralNodes().Count() == 1 &&
                maxQualifiedCardinalities.LiteralNodes().First().IsInteger())
            {
                IEnumerable<IUriNode> qualifierClasses = restriction.GetNodesViaProperty(onClass).UriNodes();
                if (qualifierClasses.Count() == 1 && qualifierClasses.First().Uri.Equals(VocabularyHelper.OWL.Thing))
                {
                    return int.Parse(maxQualifiedCardinalities.LiteralNodes().First().Value);
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

                // Only proceed if we have not seen this fetched ontology before, otherwise we risk 
                // unecessary fetches and computation, and possibly import loops.
                // Note that importedOntologies uses a custom comparer from DotNetRdfExtensions, 
                // since the Ontology class does not implement IComparable
                if (!importedOntologies.Contains(importedOntologyFromFetchedGraph))
                {
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