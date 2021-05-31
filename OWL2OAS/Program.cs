using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class Program
    {
        public enum EntityInclusionPolicy
        {
            DefaultInclude,
            DefaultExclude
        }

        public class Options
        {
            [Option('c', "ClassInclusionPolicy", Default = EntityInclusionPolicy.DefaultInclude, HelpText = "Whether to include all classes by default (overridden by o2o:included annotation). Valid options: DefaultInclude or DefaultExclude.")]
            public EntityInclusionPolicy ClassInclusionPolicy { get; set; }
            [Option('p', "PropertyInclusionPolicy", Default = EntityInclusionPolicy.DefaultInclude, HelpText = "Whether to include all properties by default (overridden by o2o:included annotation). Valid options: DefaultInclude or DefaultExclude.")]
            public EntityInclusionPolicy PropertyInclusionPolicy { get; set; }
            [Option('n', "no-imports", Required = false, HelpText = "Sets program to not follow owl:Imports declarations.")]
            public bool NoImports { get; set; }
            [Option('s', "server", Default = "http://localhost:8080/", Required = false, HelpText = "The server URL (where presumably an API implementation is running).")]
            public string Server { get; set; }
            [Option('f', "file-path", Required = true, HelpText = "The path to the on-disk root ontology file to translate.", SetName = "fileOntology")]
            public string FilePath { get; set; }
            [Option('u', "uri-path", Required = true, HelpText = "The URI of the root ontology file to translate.", SetName = "uriOntology")]
            public string UriPath { get; set; }
            [Option('o', "outputPath", Required = true, HelpText = "The path at which to put the generated OAS file.")]
            public string OutputPath { get; set; }
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
        /// The joint ontology graph into which all imported ontologies are merged
        /// and upon which this tool subsequently operates.
        /// </summary>
        private static readonly OntologyGraph _ontologyGraph = new OntologyGraph();

        /// <summary>
        /// The generated output OAS document.
        /// </summary>
        private static OASDocument _document;

        /// <summary>
        /// Set of transitively imported child ontologies.
        /// </summary>
        private static readonly HashSet<Ontology> importedOntologies = new HashSet<Ontology>(new OntologyComparer());

        private static readonly Dictionary<string, HashSet<string>> requiredPropertiesForEachClass = new Dictionary<string, HashSet<string>>();

        private static readonly Dictionary<Uri, string> namespacePrefixes = new Dictionary<Uri, string>();

        // Used in string handling etc
        private static readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;

        // Various configuration fields
        private static string _server;
        private static bool _noImports;
        private static bool _localOntology;
        private static string _ontologyPath;
        private static bool _defaultIncludeClasses;
        private static bool _defaultIncludeProperties;
        private static string _outputPath;

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
        /// Checks whether an ontology class or property should be included in the output specification, based on a) the run time 
        /// "ClassInclusionPolicy" and "PropertyInclusionPolicy" options, and b) any explicit annotations, if present, on the resource
        /// in question using the https://karlhammar.com/owl2oas/o2o.owl#included annotation property.
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>True iff the resource is annotated with included=true OR the relevant entity inclusion policy is DefaultInclude AND there is no included=false annotation on the entity.</returns>
        private static bool IsIncluded(OntologyResource resource)
        {
            // Do not include deprecated entities
            if (resource.IsDeprecated())
            {
                return false;
            }

            // Check which inclusion policy applies; also, if resource is not class or property, throw exception right away
            bool includeByDefault;
            switch (resource)
            {
                case OntologyClass oClass:
                    includeByDefault = _defaultIncludeClasses;
                    break;
                case OntologyProperty oProp:
                    includeByDefault = _defaultIncludeProperties;
                    break;
                default:
                    throw new RdfException($"Resource {resource} is neither an OntologyClass nor OntologyResource.");
            }

            // Check for existence of an o2o:included annotation; if so return based on this
            IUriNode includeAnnotationProperty = rootOntology.OntologyGraph().CreateUriNode(VocabularyHelper.O2O.included);
            IEnumerable<ILiteralNode> includeAnnotationValues = resource.GetNodesViaProperty(includeAnnotationProperty).LiteralNodes();
            if (includeAnnotationValues.Count() == 1)
            {
                bool resourceIncluded = includeAnnotationValues.First().AsValuedNode().AsBoolean();
                return resourceIncluded;
            }

            // If no annotation, go by the default policy
            return includeByDefault;
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       _noImports = o.NoImports;
                       _server = o.Server;
                       _outputPath = o.OutputPath;
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
                       _defaultIncludeClasses = (o.ClassInclusionPolicy == EntityInclusionPolicy.DefaultInclude ? true : false);
                       _defaultIncludeProperties = (o.PropertyInclusionPolicy == EntityInclusionPolicy.DefaultInclude ? true : false);
                   })
                   .WithNotParsed((errs) =>
                   {
                       Environment.Exit(1);
                   });

            // Clear cache from any prior runs
            UriLoader.Cache.Clear();

            // Load ontology graph from local or remote path
            if (_localOntology)
            {
                FileLoader.Load(_ontologyGraph, _ontologyPath);
            }
            else
            {
                UriLoader.Load(_ontologyGraph, new Uri(_ontologyPath));
            }

            // Get the main ontology defined in the graph.
            rootOntology = _ontologyGraph.GetOntology();

            // If configured for it, parse owl:Imports transitively
            if (!_noImports)
            {
                foreach (Ontology import in rootOntology.Imports)
                {
                    LoadImport(import);
                }
            }

            // Create OAS object, create OAS info header, server block, (empty) components/schemas structure, and LoadedOntologies endpoint
            _document = new OASDocument
            {
                info = GenerateDocumentInfo(),
                servers = new List<Dictionary<string, string>> { new Dictionary<string, string> { { "url", _server } } },
                components = new OASDocument.Components(),
                paths = new Dictionary<string, OASDocument.Path>
                {
                    { "/LoadedOntologies", GenerateLoadedOntologiesPath() }
                }
            };

            // Parse OWL classes.For each class, create a schema and a path
            GenerateAtomicClassSchemas();
            GenerateClassPaths();

            // Generate and add the Context schema
            // This is done after class schemas are generated, in case the former causes new prefixes to be added
            // to the context
            _document.components.schemas.Add("Context", GenerateContextSchema());

            // Dispose all open graphs
            _ontologyGraph.Dispose();

            // Dump output as YAML
            var stringBuilder = new StringBuilder();
            var serializerBuilder = new SerializerBuilder().DisableAliases();
            var serializer = serializerBuilder.Build();
            stringBuilder.AppendLine(serializer.Serialize(_document));
            File.WriteAllText(_outputPath, stringBuilder.ToString());
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

            OASDocument.PrimitiveSchema rootOntologySchema = new OASDocument.PrimitiveSchema
            {
                type = "string",
                format = "uri",
                Enumeration = new string[] { rootOntology.GetVersionOrOntologyIri().ToString() }
            };
            loadedOntologiesSchema.properties.Add("", rootOntologySchema);
            loadedOntologiesSchema.required.Add("");

            // Add each imported ontology to the loaded ontologies schema
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
            // Hardcoded Hydra
            OASDocument.PrimitiveSchema hydraSchema = new OASDocument.PrimitiveSchema
            {
                type = "string",
                format = "uri",
                DefaultValue = "http://www.w3.org/ns/hydra/core#"
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
                required = new List<string> { "@vocab", "@base", "hydra" },
                properties = new Dictionary<string, OASDocument.Schema> {
                    { "@vocab", vocabularySchema },
                    { "@base", baseNamespaceSchema },
                    { "hydra", hydraSchema },
                    { "label", labelContextSchema }
                }
            };
            // Add each prefix to the @context (sorting by shortname, e.g., dictionary value, for usability)
            List<KeyValuePair<Uri, string>> prefixMappingsList = namespacePrefixes.ToList();
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

        private static string GetKeyNameForResource(OntologyResource resource)
        {
            if (!resource.IsNamed())
            {
                throw new RdfException($"Could not generate key name for OntologyResource '{resource}'; resource is anonymous.");
            }

            Uri resourceNamespace = resource.GetNamespace();

            // If the concept is defined in the root ontology (which is default @vocab), return the local identifier
            if (resourceNamespace.Equals(rootOntology.GetIri()))
            {
                return resource.GetLocalName();
            }

            // If the resource is in an ontology that has been parsed and thus added to known namespace prefixes, return prefixed qname
            if (namespacePrefixes.ContainsKey(resourceNamespace))
            {
                return $"{namespacePrefixes[resourceNamespace]}:{resource.GetLocalName()}";
            }

            // Fallback option -- add this thing to the namespace mapper and return it
            char[] trimChars = { '#', '/' };
            string namespaceShortName = resourceNamespace.ToString().Trim(trimChars).Split('/').Last();
            namespacePrefixes[resourceNamespace] = namespaceShortName;
            return $"{namespacePrefixes[resourceNamespace]}:{resource.GetLocalName()}";
        }

        private static string GetEndpointName(OntologyResource cls)
        {
            IUriNode endpoint = _ontologyGraph.CreateUriNode(VocabularyHelper.O2O.endpoint);
            IEnumerable<ILiteralNode> endpoints = cls.GetNodesViaProperty(endpoint).LiteralNodes();
            if (endpoints.Any())
            {
                return endpoints.First().AsValuedNode().AsString();
            }
            return GetKeyNameForResource(cls);
        }

        private static void GenerateAtomicClassSchemas()
        {
            // Iterate over all non-deprecated classes that are either explicitly included, or that have subclasses that are included
            // The latter is to ensure that schema subsumption using allOf works
            foreach (OntologyClass oClass in _ontologyGraph.OwlClasses.Where(oClass => oClass.IsNamed() &&
                !oClass.IsDeprecated() &&
                (IsIncluded(oClass) || oClass.SubClasses.Any(subClass => IsIncluded(subClass)))))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(oClass);

                // Create schema for class and corresponding properties dict
                OASDocument.ComplexSchema schema = new OASDocument.ComplexSchema();
                schema.properties = new Dictionary<string, OASDocument.Schema>();

                // Set up the required properties set, used in subsequently generating HTTP operations
                requiredPropertiesForEachClass.Add(classLabel, new HashSet<string>());

                // Add @id for all entries
                OASDocument.PrimitiveSchema idSchema = new OASDocument.PrimitiveSchema();
                idSchema.type = "string";
                schema.properties.Add("@id", idSchema);

                // Add @type for all entries
                OASDocument.PrimitiveSchema typeSchema = new OASDocument.PrimitiveSchema
                {
                    type = "string",
                    DefaultValue = GetKeyNameForResource(oClass)
                };
                schema.properties.Add("@type", typeSchema);

                // @context is mandatory
                schema.required = new List<string>() { "@context" };

                // Label is an option for all entries
                OASDocument.PrimitiveSchema labelSchema = new OASDocument.PrimitiveSchema();
                labelSchema.type = "string";
                schema.properties.Add("label", labelSchema);

                // Iterate over all (local) relationships and add them as properties
                foreach (Relationship relationship in oClass.GetRelationships().Where(relationship =>
                    IsIncluded(relationship.Property) &&
                    (relationship.Property.IsObjectProperty() || relationship.Property.IsDataProperty()) &&
                    !relationship.Property.IsDeprecated() &&
                    !relationship.Target.IsDeprecated() &&
                    !relationship.Target.IsOwlThing() &&
                    !relationship.Target.IsRdfsLiteral()
                ))
                {
                    OntologyClass range = relationship.Target;// property.Ranges.First();
                    OntologyProperty property = relationship.Property;

                    // Used for lookups against constraints dict
                    UriNode propertyNode = ((UriNode)property.Resource);

                    // Used to allocate property to schema.properties dictionary
                    string propertyLabel = GetKeyNameForResource(property);

                    // The return value: a property block to be added to the output document
                    OASDocument.Schema outputSchema;

                    // Check if multiple values are allowed for this property. By default they are.
                    //bool propertyAllowsMultipleValues = !property.IsFunctional();

                    // If this is a data property
                    if (property.IsDataProperty())
                    {
                        // Set up the (possibly later on nested) property block
                        OASDocument.PrimitiveSchema dataPropertySchema = new OASDocument.PrimitiveSchema();

                        // Fall back to string representation for unknown types
                        dataPropertySchema.type = "string";

                        // Check that range is an XSD type that can be parsed into 
                        // an OAS type and format (note: not all XSD types are covered)
                        if (range.IsXsdDatatype() || range.IsSimpleXsdWrapper()) {
                            string rangeXsdType = "";
                            if (range.IsXsdDatatype()) {
                                rangeXsdType = ((UriNode)range.Resource).GetLocalName();
                            }
                            else {
                                rangeXsdType = range.EquivalentClasses.First().GetUriNode().GetLocalName();
                            }
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
                        uriPropertySchema = new OASDocument.ComplexSchema
                        {
                            properties = new Dictionary<string, OASDocument.Schema> {
                                        { "@id", new OASDocument.PrimitiveSchema { type = "string" } },
                                        { "@type", new OASDocument.PrimitiveSchema { type = "string", DefaultValue = GetKeyNameForResource(range) } }
                                    },
                            required = new List<string> { "@id" }
                        };

                        outputSchema = uriPropertySchema;
                    }

                    if (relationship.MinimumCount > 0) { 
                        requiredPropertiesForEachClass[classLabel].Add(propertyLabel);
                    }


                    if (property.IsFunctional() || relationship.ExactCount == 1)
                    {
                        schema.properties[propertyLabel] = outputSchema;
                    }
                    else
                    {
                        OASDocument.ArraySchema propertyArraySchema = new OASDocument.ArraySchema();
                        propertyArraySchema.items = outputSchema;
                        if (relationship.MinimumCount.HasValue)
                        {
                            propertyArraySchema.minItems = relationship.MinimumCount.Value;
                        }
                        if (relationship.MaximumCount.HasValue)
                        {
                            propertyArraySchema.maxItems = relationship.MaximumCount.Value;
                        }
                        schema.properties[propertyLabel] = propertyArraySchema;

                    }
                }

                IUriNode rdfsSubClassOf = _ontologyGraph.CreateUriNode(VocabularyHelper.RDFS.subClassOf);
                IEnumerable<OntologyClass> namedSuperClasses = oClass.DirectSuperClasses.Where(superClass =>
                    superClass.IsNamed() &&
                    !superClass.IsOwlThing() &&
                    !superClass.IsDeprecated() &&
                    !PropertyAssertionIsDeprecated(oClass.GetUriNode(), rdfsSubClassOf, superClass.GetUriNode())
                );
                if (namedSuperClasses.Any())
                {
                    OASDocument.AllOfSchema inheritanceSchema = new OASDocument.AllOfSchema();
                    OASDocument.Schema[] superClassSchemaReferences = namedSuperClasses.Select(superClass => new OASDocument.ReferenceSchema(GetKeyNameForResource(superClass))).ToArray();
                    inheritanceSchema.allOf = new OASDocument.Schema[namedSuperClasses.Count() + 1];
                    for (int i = 0; i < namedSuperClasses.Count(); i++)
                    {
                        inheritanceSchema.allOf[i] = superClassSchemaReferences[i];
                    }
                    inheritanceSchema.allOf[namedSuperClasses.Count()] = schema;
                    _document.components.schemas.Add(classLabel.Replace(":", "_", StringComparison.Ordinal), inheritanceSchema);
                }
                else
                {
                    _document.components.schemas.Add(classLabel.Replace(":", "_", StringComparison.Ordinal), schema);
                }
            }
        }

        // TODO: move this into the DotNetRdfExtensions class
        private static bool PropertyAssertionIsDeprecated(INode subj, IUriNode pred, INode obj)
        {
            IUriNode owlAnnotatedSource = _ontologyGraph.CreateUriNode(VocabularyHelper.OWL.annotatedSource);
            IUriNode owlAnnotatedProperty = _ontologyGraph.CreateUriNode(VocabularyHelper.OWL.annotatedProperty);
            IUriNode owlAnnotatedTarget = _ontologyGraph.CreateUriNode(VocabularyHelper.OWL.annotatedTarget);
            IUriNode owlDeprecated = _ontologyGraph.CreateUriNode(VocabularyHelper.OWL.deprecated);

            IEnumerable<INode> axiomAnnotations = _ontologyGraph.Nodes
                .Where(node => _ontologyGraph.ContainsTriple(new Triple(node, owlAnnotatedSource, subj)))
                .Where(node => _ontologyGraph.ContainsTriple(new Triple(node, owlAnnotatedProperty, pred)))
                .Where(node => _ontologyGraph.ContainsTriple(new Triple(node, owlAnnotatedTarget, obj)));

            foreach (INode axiomAnnotation in axiomAnnotations)
            {
                foreach (Triple deprecationAssertion in _ontologyGraph.GetTriplesWithSubjectPredicate(axiomAnnotation, owlDeprecated).Where(trip => trip.Object.NodeType == NodeType.Literal))
                {
                    IValuedNode deprecationValue = deprecationAssertion.Object.AsValuedNode();
                    try
                    {
                        if (deprecationValue.AsBoolean())
                        {
                            return true;
                        }
                    }
                    catch
                    {

                    }
                }
            }
            return false;
        }

            private static void GenerateClassPaths()
        {
            // Iterate over all classes
            foreach (OntologyClass oClass in _ontologyGraph.OwlClasses.Where(oClass => oClass.IsNamed() && IsIncluded(oClass)))
            {
                // Get key name for API
                string classLabel = GetKeyNameForResource(oClass);
                string endpointName = GetEndpointName(oClass);

                // Create paths and corresponding operations for class
                _document.paths.Add($"/{endpointName}", new OASDocument.Path
                {
                    get = GenerateGetEntitiesOperation(endpointName, classLabel, oClass),
                    post = GeneratePostEntityOperation(endpointName, classLabel)
                });
                _document.paths.Add($"/{endpointName}/{{id}}", new OASDocument.Path
                {
                    get = GenerateGetEntityByIdOperation(endpointName, classLabel),
                    patch = GeneratePatchToIdOperation(endpointName, classLabel),
                    put = GeneratePutToIdOperation(endpointName, classLabel),
                    delete = GenerateDeleteByIdOperation(endpointName, classLabel)
                });
            }
        }

        private static OASDocument.Operation GenerateDeleteByIdOperation(string endpointName, string classLabel)
        {
            OASDocument.Operation deleteOperation = new OASDocument.Operation();
            deleteOperation.summary = $"Delete a '{classLabel}' object.";
            deleteOperation.tags.Add(endpointName);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to delete.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema
                {
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

        private static OASDocument.Operation GeneratePostEntityOperation(string endpointName, string classLabel)
        {
            OASDocument.Operation postOperation = new OASDocument.Operation();
            postOperation.summary = $"Create a new '{classLabel}' object.";
            postOperation.tags.Add(endpointName);

            // Create request body
            OASDocument.RequestBody body = new OASDocument.RequestBody
            {
                description = $"New '{classLabel}' entity that is to be added.",
                required = true,
                content = new Dictionary<string, OASDocument.Content>
                {
                    {
                        "application/ld+json", new OASDocument.Content
                        {
                            schema = MergeAtomicSchemaWithContext(classLabel)
                        }
                    }
                }
            };
            postOperation.requestBody = body;

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
            response201.content.Add("application/ld+json", content201);

            // Response is per previously defined schema
            content201.schema = MergeAtomicSchemaWithContextAndRequiredProperties(classLabel);

            return postOperation;
        }

        private static OASDocument.Operation GenerateGetEntityByIdOperation(string endpointName, string classLabel)
        {
            OASDocument.Operation getOperation = new OASDocument.Operation();
            getOperation.summary = $"Get a specific '{classLabel}' object.";
            getOperation.tags.Add(endpointName);

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
            response200.content.Add("application/ld+json", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithContextAndRequiredProperties(classLabel);

            return getOperation;
        }

        private static OASDocument.Operation GenerateGetEntitiesOperation(string endpointName, string classLabel, OntologyClass oClass)
        {

            // Create Get
            OASDocument.Operation getOperation = new OASDocument.Operation();
            getOperation.summary = "Get '" + classLabel + "' entities.";
            getOperation.tags.Add(endpointName);

            // Add pagination parameters
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "pageParam" });
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "sizeParam" });

            // Add sort param
            getOperation.parameters.Add(new OASDocument.Parameter { ReferenceTo = "sortParam" });

            // Add parameters for each property field that can be expressed on this class
            foreach (OntologyProperty property in oClass.IsExhaustiveDomainOfUniques()
            .Where(property => property.IsDataProperty() || property.IsObjectProperty())
            .Where(property => property.Ranges.Count() == 1)
            .Where(property => IsIncluded(property)))
            {
                string propertyLabel = GetKeyNameForResource(property);

                // Fall back to string representation and no format for object properties
                // abd data properties w/ unknown types
                string propertyType = "string";
                string propertyFormat = "";

                // Check that range is an XSD type that can be parsed into 
                // an OAS type and format (note: not all XSD types are covered)
                OntologyClass range = property.Ranges.First();
                if (range.IsNamed()) {
                    if (range.IsXsdDatatype() || range.IsSimpleXsdWrapper()) {
                        string rangeXsdType = "";
                        if (range.IsXsdDatatype()) {
                            rangeXsdType = ((UriNode)range.Resource).GetLocalName();
                        }
                        else {
                            rangeXsdType = range.EquivalentClasses.First().GetUriNode().GetLocalName();
                        }
                        if (xsdOsaMappings.ContainsKey(rangeXsdType))
                        {
                            propertyType = xsdOsaMappings[rangeXsdType].Item1;
                            string format = xsdOsaMappings[rangeXsdType].Item2;
                            if (format.Length > 0)
                            {
                                propertyFormat = format;
                            }
                        }
                    }
                }

                // Select a filter schema to use for parameter formats where it is applicable
                string filterSchema = "";
                switch (propertyType)
                {
                    case "string":
                        filterSchema = propertyFormat switch
                        {
                            "date-time" => "DateTimeFilter",
                            _ => "StringFilter",
                        };
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
                    else
                    {
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
            response200.content.Add("application/ld+json", content200);

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
                            {"hydra:member", new OASDocument.ArraySchema  {
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
                itemSchema = new OASDocument.ReferenceSchema(classLabel);
            }
            else
            {
                itemSchema = new OASDocument.AllOfSchema
                {
                    allOf = new OASDocument.Schema[] {
                        new OASDocument.ReferenceSchema(classLabel),
                        new OASDocument.ComplexSchema {
                            required = requiredPropertiesForEachClass[classLabel].ToList()
                        }
                    }
                };
            }
            return itemSchema;
        }

        private static OASDocument.Schema MergeAtomicSchemaWithContext(string classLabel)
        {
            OASDocument.AllOfSchema itemSchema = new OASDocument.AllOfSchema();
            OASDocument.ReferenceSchema classSchema = new OASDocument.ReferenceSchema(classLabel);
            OASDocument.ReferenceSchema contextReferenceSchema = new OASDocument.ReferenceSchema("Context");
            OASDocument.ComplexSchema contextPropertySchema = new OASDocument.ComplexSchema
            {
                required = new List<string> { "@context" },
                properties = new Dictionary<string, OASDocument.Schema>() { { "@context", contextReferenceSchema } }
            };

            // Otherwise merge only with context
            itemSchema.allOf = new OASDocument.Schema[]
            {
                contextPropertySchema,
                classSchema
            };
            return itemSchema;
        }

        private static OASDocument.Schema MergeAtomicSchemaWithContextAndRequiredProperties(string classLabel)
        {
            OASDocument.AllOfSchema itemSchema = new OASDocument.AllOfSchema();
            OASDocument.ReferenceSchema classSchema = new OASDocument.ReferenceSchema(classLabel);
            OASDocument.ReferenceSchema contextReferenceSchema = new OASDocument.ReferenceSchema("Context");
            OASDocument.ComplexSchema contextPropertySchema = new OASDocument.ComplexSchema
            {
                required = new List<string> { "@context" },
                properties = new Dictionary<string, OASDocument.Schema>() { { "@context", contextReferenceSchema } }
            };

            // If there are required properties, merge them also
            if (requiredPropertiesForEachClass[classLabel].Count != 0)
            {
                OASDocument.ComplexSchema requiredPropertiesSchema = new OASDocument.ComplexSchema { required = requiredPropertiesForEachClass[classLabel].ToList() };
                itemSchema.allOf = new OASDocument.Schema[]
                {
                    contextPropertySchema,
                    classSchema,
                    requiredPropertiesSchema
                };
                return itemSchema;
            }

            // Otherwise merge only with context
            itemSchema.allOf = new OASDocument.Schema[]
            {
                contextPropertySchema,
                classSchema
            };
            return itemSchema;
        }

        private static OASDocument.Operation GeneratePatchToIdOperation(string endpointName, string classLabel)
        {
            OASDocument.Operation patchOperation = new OASDocument.Operation();
            patchOperation.summary = $"Update a single property on a specific '{classLabel}' object.";
            patchOperation.tags.Add(endpointName);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to update.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema
                {
                    type = "string"
                }
            };
            patchOperation.parameters.Add(idParameter);

            // Create patch schema
            OASDocument.ReferenceSchema contextReferenceSchema = new OASDocument.ReferenceSchema("Context");
            OASDocument.ComplexSchema contextPropertySchema = new OASDocument.ComplexSchema
            {
                required = new List<string> { "@context" },
                properties = new Dictionary<string, OASDocument.Schema>() { { "@context", contextReferenceSchema } }
            };

            OASDocument.Schema patchSchema = new OASDocument.AllOfSchema
            {
                allOf = new OASDocument.Schema[] {
                        contextPropertySchema,
                        new OASDocument.ReferenceSchema(classLabel),
                        new OASDocument.ComplexSchema {
                            minProperties = 2,
                            maxProperties = 2
                        }
                    }
            };

            // Add request body
            OASDocument.RequestBody body = new OASDocument.RequestBody
            {
                description = "A single JSON key-value pair (plus @context), indicating the property to update and its new value. Note that the Swagger UI does not properly show the size constraint on this parameter; but the underlying OpenAPI Specification document does.",
                required = true,
                content = new Dictionary<string, OASDocument.Content>
                {
                    {
                        "application/ld+json", new OASDocument.Content
                        {
                            schema = patchSchema
                        }
                    }
                }
            };
            patchOperation.requestBody = body;

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
            response200.content.Add("application/ld+json", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithContextAndRequiredProperties(classLabel);

            return patchOperation;
        }

        private static OASDocument.Operation GeneratePutToIdOperation(string endpointName, string classLabel)
        {
            OASDocument.Operation putOperation = new OASDocument.Operation();
            putOperation.summary = $"Update an existing '{classLabel}' entity.";
            putOperation.tags.Add(endpointName);

            // Add the ID parameter
            OASDocument.Parameter idParameter = new OASDocument.Parameter
            {
                name = "id",
                description = $"Id of '{classLabel}' to update.",
                InField = OASDocument.Parameter.InFieldValues.path,
                required = true,
                schema = new OASDocument.PrimitiveSchema
                {
                    type = "string"
                }
            };
            putOperation.parameters.Add(idParameter);

            // Add request body
            OASDocument.RequestBody body = new OASDocument.RequestBody
            {
                description = $"Updated data for '{classLabel}' entity.",
                required = true,
                content = new Dictionary<string, OASDocument.Content>
                {
                    {
                        "application/ld+json", new OASDocument.Content
                        {
                            schema = MergeAtomicSchemaWithContext(classLabel)
                        }
                    }
                }
            };
            putOperation.requestBody = body;

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
            response200.content.Add("application/ld+json", content200);

            // Response is per previously defined schema
            content200.schema = MergeAtomicSchemaWithContextAndRequiredProperties(classLabel);

            return putOperation;
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
                if (qualifierClasses.Count() == 1 && qualifierClasses.First().Uri.Equals(VocabularyHelper.OWL.Thing))
                {
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
            if (importedOntology.IsNamed())
            {

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

                        // Merge the fetch graph with the joint ontology graph the tool operates on
                        _ontologyGraph.Merge(fetchedOntologyGraph);

                        // Add imported ontology to the global imports collection and traverse its 
                        // import hierarchy transitively
                        importedOntologies.Add(importedOntologyFromFetchedGraph);
                        foreach (Ontology subImport in importedOntologyFromFetchedGraph.Imports)
                        {
                            LoadImport(subImport);
                        }
                    }
                }

                // Dispose graph before returning
                fetchedOntologyGraph.Dispose();
            }
        }
    }
}