using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class OASDocument
    {
        public readonly string openapi = "3.0.2";
        public Info info;
        public Components components;

        public Dictionary<string, Path> paths;
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
            public Schema schema;
            public string style;
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
                        schema = new PrimitiveSchema {
                            type = "integer",
                            format = "int32",
                            minimum = 0,
                            DefaultValue = "0"
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
                        schema = new PrimitiveSchema {
                            type = "integer",
                            format = "int32",
                            minimum = 0,
                            maximum = 100,
                            DefaultValue = "20"
                        }
                    }
                },
                {
                    "sortParam", new Parameter
                    {
                        name = "sort",
                        description = "The field and direction to sort results on.",
                        InField = Parameter.InFieldValues.query,
                        required = false,
                        schema = new ReferenceSchema("SortingSchema"),
                        style = "deepObject"
                    }
                }
            };
            public Dictionary<string, Schema> schemas = new Dictionary<string, Schema>
            {
                // Add Hydra Colletions wrapper schema
                {"HydraCollectionWrapper",
                    new ComplexSchema
                    {
                        required = new List<string>
                        {
                            "@context",
                            "@type",
                            "member"
                        },
                        properties = new Dictionary<string, OASDocument.Schema>
                        {
                            {"@context", new PrimitiveSchema { type="string", format = "uri", DefaultValue="http://www.w3.org/ns/hydra/context.jsonld"} },
                            {"@type", new PrimitiveSchema { type="string", DefaultValue="Collection"} },
                            {"totalItems", new PrimitiveSchema { type="integer" } },
                        }
                    }
                },
                // Add the default query operator filter schemas
                {"IntegerFilter", new ComplexSchema {
                    properties = new Dictionary<string, Schema>
                    {
                        {"eq", new PrimitiveSchema {type="integer"} },
                        {"lt", new PrimitiveSchema {type="integer"} },
                        {"lte", new PrimitiveSchema {type="integer"} },
                        {"gt", new PrimitiveSchema {type="integer"} },
                        {"gte", new PrimitiveSchema {type="integer"} }
                    }
                }},
                {"NumberFilter", new ComplexSchema {
                    properties = new Dictionary<string, Schema>
                    {
                        {"eq", new PrimitiveSchema {type="number"} },
                        {"lt", new PrimitiveSchema {type="number"} },
                        {"lte", new PrimitiveSchema {type="number"} },
                        {"gt", new PrimitiveSchema {type="number"} },
                        {"gte", new PrimitiveSchema {type="number"} }
                    }
                }},
                {"StringFilter", new ComplexSchema {
                    properties = new Dictionary<string, Schema>
                    {
                        {"eq", new PrimitiveSchema {type="string"} },
                        {"contains", new PrimitiveSchema {type="string"} },
                        {"regex", new PrimitiveSchema {type="string"} }
                    }
                }},
                {"DateTimeFilter", new ComplexSchema {
                    properties = new Dictionary<string, Schema>
                    {
                        {"eq", new PrimitiveSchema {type="string", format="date-time"} },
                        {"starting", new PrimitiveSchema {type="string", format="date-time"} },
                        {"ending", new PrimitiveSchema {type="string", format="date-time"} },
                        {"before", new PrimitiveSchema {type="string", format="date-time"} },
                        {"after", new PrimitiveSchema {type="string", format="date-time"} },
                        {"latest", new PrimitiveSchema {type="boolean" } }
                    }
                }},
                // And the sort operators schema
                {"SortingSchema", new ComplexSchema {
                    properties = new Dictionary<string, Schema>
                    {
                        {"asc", new PrimitiveSchema {type="string"} },
                        {"desc", new PrimitiveSchema {type="string"} }
                    }
                }}
            };
        }
        
        public class Schema
        {

        }

        public class ComplexSchema: Schema
        {
            public readonly string type = "object";
            public List<string> required;
            public Dictionary<string, Schema> properties;
            public int maxProperties;
            public int minProperties;
        }

        public class PrimitiveSchema: Schema
        {
            public string type;
            public string format;
            public int minimum;
            public int maximum;
            [YamlMember(Alias = "default")]
            public string DefaultValue { get; set; }
            [YamlMember(Alias = "enum")]
            public string[] Enumeration { get; set; }
        }

        public class ReferenceSchema: Schema
        {
            public ReferenceSchema(string referenceType)
            {
                Reference = "#/components/schemas/" + referenceType;
            }
            [YamlMember(Alias = "$ref")]
            public string Reference { get; set; }
        }

        public class AllOfSchema: Schema
        {
            public Schema[] allOf;
        }

        public class ArraySchema: Schema
        {
            public readonly string type = "array";
            public Schema items;
            public int maxItems;
            public int minItems;
        }

        public class Path
        {
            public Operation get;
            public Operation put;
            public Operation patch;
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
            public Schema schema;
        }
    }
}
