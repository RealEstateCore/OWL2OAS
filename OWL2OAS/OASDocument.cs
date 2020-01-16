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
                },
                {
                    "sortParam", new Parameter
                    {
                        name = "sort",
                        description = "The field and direction to sort results on.",
                        InField = Parameter.InFieldValues.query,
                        required = false,
                        schema = new Dictionary<string, string> {
                            { "$ref", "#/components/schemas/SortingSchema" }
                        },
                        style = "deepObject"
                    }
                }
            };
            public Dictionary<string, Schema> schemas = new Dictionary<string, Schema>
            {
                // Add the default query operator filter schemas
                {"IntegerFilter", new Schema {
                    properties = new Dictionary<string, Property>
                    {
                        {"eq", new Property {type="integer"} },
                        {"lt", new Property {type="integer"} },
                        {"lte", new Property {type="integer"} },
                        {"gt", new Property {type="integer"} },
                        {"gte", new Property {type="integer"} }
                    }
                }},
                {"NumberFilter", new Schema {
                    properties = new Dictionary<string, Property>
                    {
                        {"eq", new Property {type="number"} },
                        {"lt", new Property {type="number"} },
                        {"lte", new Property {type="number"} },
                        {"gt", new Property {type="number"} },
                        {"gte", new Property {type="number"} }
                    }
                }},
                {"StringFilter", new Schema {
                    properties = new Dictionary<string, Property>
                    {
                        {"eq", new Property {type="string"} },
                        {"contains", new Property {type="string"} },
                        {"regex", new Property {type="string"} }
                    }
                }},
                {"DateTimeFilter", new Schema {
                    properties = new Dictionary<string, Property>
                    {
                        {"eq", new Property {type="string", format="date-time"} },
                        {"starting", new Property {type="string", format="date-time"} },
                        {"ending", new Property {type="string", format="date-time"} },
                        {"before", new Property {type="string", format="date-time"} },
                        {"after", new Property {type="string", format="date-time"} },
                        {"latest", new Property {type="boolean" } }
                    }
                }},
                // And the sort operators schema
                {"SortingSchema", new Schema {
                    properties = new Dictionary<string, Property>
                    {
                        {"asc", new Property {type="string"} },
                        {"desc", new Property {type="string"} }
                    }
                }}
            };
        }
        
        public class Schema
        {
            public readonly string type = "object";
            public List<string> required;
            public Dictionary<string, Property> properties;
            public int minProperties;
            public int maxProperties;
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
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted, Alias = "schema")]
            public Property Schema { get; set; }
        }
    }
}
