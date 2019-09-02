using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace OWL2OAS
{
    class OASDocument
    {
        public string openapi { get { return "3.0.0"; } }
        public Info info { get; set; }
        public Components components { get; set; }
        public Dictionary<string, Path> paths { get; set; }

        public struct Info
        {
            public string version;
            public string title;
            public License license;
        }

        public class License
        {
            public string name;
        }

        public class Components
        {
            public Dictionary<string, Schema> schemas { get; set; }
        }
        
        public class Schema
        {
            public string type { get { return "object";  } }
        }

        public class Path
        {
            public Get get { get; set; }
        }

        public class Get
        {
            public string summary;
            public Dictionary<string, Response> responses { get; set; }
        }

        public class Response
        {
            public string description { get; set; }
        }
    }
}
