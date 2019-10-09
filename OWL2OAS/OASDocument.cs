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
        public Dictionary<string, Path> paths { get; set; }

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

        public class UriProperty: Property
        {
            public UriProperty(string referenceType)
            {
                oneOf = new List<object>() { new Dictionary<string, string> { { "$ref", "#/components/schemas/" + referenceType } } };
                Schema uriSchema = new Schema();
                uriSchema.properties = new Dictionary<string, Property> { { "@id", new Property() { type = "string" } } };
                uriSchema.required = new List<string> { "@id" };
                oneOf.Add(uriSchema);
            }
            public List<object> oneOf;
        }

        public class Property
        {
            public string type;
            public string format;
            [YamlMember(Alias = "default")]
            public string defaultValue { get; set; }
        }

        public class ReferenceProperty: Property
        {
            public ReferenceProperty(string referenceType)
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
            public Dictionary<string, Response> responses { get; set; }
        }

        public class Get: Verb
        {
            
        }

        public class Response
        {
            public string description { get; set; }
            public Dictionary<string, Content> content { get; set; }
        }

        public class Content
        {
            [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
            public Dictionary<string, string> schema { get; set; }
        }
    }
}
