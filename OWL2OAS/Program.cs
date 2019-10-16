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
            public override string ToString()
            {
                return String.Format("Property:\t<{0}>\nMin:\t{1}\nMax:\t{2}\nExactly:\t{3}",property, min, max, exactly);
            }
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

            // Server block            
            if (args.Length > 0)
            {
                document.servers = new List<Dictionary<string, string>> { new Dictionary<string, string> { { "url", args[0] } } };
            }
            else
            {
                document.servers = new List<Dictionary<string, string>> { new Dictionary<string, string> { { "url", "http://localhost:8080/" } } };
            }

            // Parse OWL classes. For each class, create a schema and a path
            document.components = new OASDocument.Components();
            Dictionary<string, OASDocument.Schema> schemas = new Dictionary<string, OASDocument.Schema>();
            Dictionary<string, OASDocument.Path> paths = new Dictionary<string, OASDocument.Path>();

            // Set context based on the ontology IRI
            //OASDocument.Schema contextSchema = new OASDocument.Schema();
            //contextSchema.properties = new Dictionary<string, OASDocument.Property>();
            OASDocument.Property vocabularyProperty = new OASDocument.Property()
            {
                type = "string",
                format = "uri",
                defaultValue = rootOntologyUriNode.Uri.ToString()
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
            //contextSchema.properties.Add("@context", contextProperty);
            schemas.Add("Context", contextSchema);

            // Iterate over all classes
            foreach (OntologyClass c in g.OwlClasses.Where(oClass => oClass.IsNamed()))
            {
                // Get human-readable label for API (should this be fetched from other metadata property?)
                // TODO: pluralization metadata for clean API?
                string classLabel = c.GetLocalName();

                // Create schema for class and corresponding properties dict
                OASDocument.Schema schema = new OASDocument.Schema();
                schema.properties = new Dictionary<string, OASDocument.Property>();

                // Iterate over superclasses, extract constraints
                Dictionary<IUriNode, PropertyConstraint> constraints = new Dictionary<IUriNode, PropertyConstraint>();
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

                // Add reference to context schema
                schema.properties.Add("@context", new OASDocument.ReferenceProperty("Context"));

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
                foreach (OntologyProperty property in c.IsDomainOf)
                {
                    // We only process (named) object and data properties with singleton ranges.
                    if ((property.IsObjectProperty() || property.IsDataProperty()) && property.Ranges.Count() == 1) {

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
                            if (range.IsNamed() && g.OwlClasses.Contains(range))
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
                schemas.Add(classLabel, schema);

                // Create path for class
                OASDocument.Path path = new OASDocument.Path();
                paths.Add("/" + classLabel, path);

                // Create each of the HTTP methods
                // TODO: PUT, PATCH, etc
                // TODO: filtering, parameters, etc
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

                // TODO: wrap responses in pagination?
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
