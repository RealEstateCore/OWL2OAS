using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class OASDocument
    {
        public string openapi { get { return "3.0.0"; } }
        public Info info { get; set; }
        public Components components { get; set; }

        /// <summary>
        /// Initialise the paths block. By default holds only an HTTP GET for the JSON-LD @context endpoint.
        /// </summary>
        public Dictionary<string, Path> paths = new Dictionary<string, Path>()
        {
            { "/JsonLdContext", new Path()
                {
                    get = new Get()
                    {
                        // Reset default pagination parameters b/c this endpoint will not need pagination
                        parameters = new List<ParameterReferenceProperty>(),
                        summary = "Get the JSON-LD @context for this API, i.e., the set of ontologies that were used to generate the API.",
                        responses = new Dictionary<string, Response>()
                        {
                            { "200", new Response()
                                {
                                    description = "A JSON-LD @context declaration.",
                                    content = new Dictionary<string, Content>()
                                    {
                                        { "application/jsonld", new Content()
                                            {
                                                schema = new SchemaReferenceProperty("Context")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
        public List<Dictionary<string, string>> servers { get; set; }

        public class Info
        {
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
            public string version { get; set; }
            public string title { get; set; }
            public License license { get; set; }
            public string description;
        }

        public class License
        {
            public string name;
            public string url;
        }

        public class Components
        {
            public struct Parameter
            {
                public string name;
                [YamlMember(Alias = "in")]
                public string inField { get { return "query"; } }
                public string description;
                public bool required { get { return false; } }
                public Dictionary<string, string> schema;
            }
            public Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter> {
                { "offsetParam", new Parameter()
                    {
                        name = "offset",
                        description = "Number of items to skip before returning the results.",
                        schema = new Dictionary<string, string> {
                            { "type", "integer" },
                            { "format", "int32" },
                            { "minimum", "0" },
                            { "default", "0" },
                        }
                    }
                },
                {
                    "limitParam", new Parameter()
                    {
                        name = "limit",
                        description = "Maximum number of items to return.",
                        schema = new Dictionary<string, string> {
                            { "type", "integer" },
                            { "format", "int32" },
                            { "minimum", "1" },
                            { "maximum", "100" },
                            { "default", "20" },
                        }
                    }
                }
            };
            public Dictionary<string, Schema> schemas { get; set; }
        }
        
        public class Schema
        {
            public string type { get { return "object";  } }
            public List<string> required;
            public Dictionary<string, Property> properties;
        }

        public class ObjectProperty: Property
        {
            public new string type { get { return "object"; } }
            public Dictionary<string, Property> properties;
            public List<string> required;
        }

        public class ArrayProperty: Property
        {
            public new string type = "array";
            public Property items;
            public int maxItems;
            public int minItems;
        }

        public class Property
        {
            public string type;
            public string format;
            [YamlMember(Alias = "default")]
            public string defaultValue { get; set; }
        }

        public class ParameterReferenceProperty: Property
        {
            public ParameterReferenceProperty(string referenceType)
            {
                reference = "#/components/parameters/" + referenceType;
            }
            [YamlMember(Alias = "$ref")]
            public string reference { get; set; }
        }

        public class SchemaReferenceProperty : Property
        {
            public SchemaReferenceProperty(string referenceType)
            {
                reference = "#/components/schemas/" + referenceType;
            }
            [YamlMember(Alias = "$ref")]
            public string reference { get; set; }
        }

        public class PropertyItems
        {

        }

        public class Path
        {
            public Get get { get; set; }
        }

        public class Verb
        {
            public string summary;
            public List<ParameterReferenceProperty> parameters;
            public Dictionary<string, Response> responses { get; set; }
        }

        public class Get: Verb
        {
            public Get()
            {
                parameters = new List<ParameterReferenceProperty>();
                parameters.Add(new OASDocument.ParameterReferenceProperty("offsetParam"));
                parameters.Add(new OASDocument.ParameterReferenceProperty("limitParam"));
            }
        }

        public class Response
        {
            public string description { get; set; }
            public Dictionary<string, Content> content { get; set; }
        }

        public class Content
        {
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
            public Property schema { get; set; }
        }
    }
}
