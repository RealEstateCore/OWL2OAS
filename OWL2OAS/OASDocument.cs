using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class OASDocument
    {
        public readonly string openapi = "3.0.0";
        public Info info;
        public Components components;

        /// <summary>
        /// Initialise the paths block. By default holds only an HTTP GET for the JSON-LD @context endpoint.
        /// </summary>
        public Dictionary<string, Path> paths = new Dictionary<string, Path>
        {
            { "/JsonLdContext", new Path
                {
                    get = new Operation
                    {
                        summary = "Get the JSON-LD @context for this API, i.e., the set of ontologies that were used to generate the API.",
                        responses = new Dictionary<string, Response>
                        {
                            { "200", new Response
                                {
                                    description = "A JSON-LD @context declaration.",
                                    content = new Dictionary<string, Content>
                                    {
                                        { "application/jsonld", new Content
                                            {
                                                Schema = new SchemaReferenceProperty("Context")
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
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted, Alias = "version")]
            public string Version { get; set; }
            public string title;
            public License license;
            public string description;
        }

        public class License
        {
            public string name;
            public string url;
        }

        public class Parameter
        {
            private string _referenceTo;
            [YamlMember(Alias = "$ref")]
            public string ReferenceTo
            {
                get
                {
                    return _referenceTo;
                }
                set
                {
                    _referenceTo = "#/components/parameters/"  + value;
                }
            }

            public enum InFieldValues
            {
                path = 1,
                query = 2,
                header = 3,
                cookie = 4
            }

            public string name;
            [YamlMember(Alias = "in")]
            public InFieldValues InField { get; set; }
            public string description;
            public bool required;
            public Dictionary<string, string> schema;
        }

        public class Components
        {
            public Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter> {
                { "offsetParam", new Parameter
                    {
                        name = "offset",
                        description = "Number of items to skip before returning the results.",
                        InField = Parameter.InFieldValues.query,
                        required = false,
                        schema = new Dictionary<string, string> {
                            { "type", "integer" },
                            { "format", "int32" },
                            { "minimum", "0" },
                            { "default", "0" }
                        }
                    }
                },
                {
                    "limitParam", new Parameter
                    {
                        name = "limit",
                        description = "Maximum number of items to return.",
                        InField = Parameter.InFieldValues.query,
                        required = false,
                        schema = new Dictionary<string, string> {
                            { "type", "integer" },
                            { "format", "int32" },
                            { "minimum", "1" },
                            { "maximum", "100" },
                            { "default", "20" }
                        }
                    }
                }
            };
            public Dictionary<string, Schema> schemas;
        }
        
        public class Schema
        {
            public readonly string type = "object";
            public List<string> required;
            public Dictionary<string, Property> properties;
        }

        public class ObjectProperty: Property
        {
            public new readonly string type = "object";
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
            public string DefaultValue { get; set; }
        }

        public class SchemaReferenceProperty : Property
        {
            public SchemaReferenceProperty(string referenceType)
            {
                Reference = "#/components/schemas/" + referenceType;
            }
            [YamlMember(Alias = "$ref")]
            public string Reference { get; set; }
        }

        public class PropertyItems
        {

        }

        public class Path
        {
            public Operation get;
            public Operation put;
            public Operation post;
            public Operation delete;
        }

        public class Operation
        {
            public string summary;
            public List<Parameter> parameters = new List<Parameter>();
            public Dictionary<string, Response> responses = new Dictionary<string, Response>();
            public List<string> tags = new List<string>();
        }

        public class Response
        {
            public string description { get; set; }
            public Dictionary<string, Content> content;
        }

        public class Content
        {
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted, Alias = "schema")]
            public Property Schema { get; set; }
        }
    }
}
