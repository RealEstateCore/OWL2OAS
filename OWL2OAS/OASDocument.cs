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

        public class ArrayProperty: Property
        {
            public new string type = "array";
            public List<Property> items;
            public int maxItems;
            public int minItems;
        }

        public class Property
        {
            public string type;
            public string format;
            public List<Dictionary<string, string>> oneOf;
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
