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
        public Paths paths { get; set; }

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

        public class Paths
        {
            public Paths[] paths;
        }
    }
}
