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

        public struct License
        {
            public string name;
        }

        public struct Components
        {
            public List<Schema> schemas;
        }
      
        public struct Schema
        {
            public string title;
        }

        public struct Paths
        {
            public Paths[] paths;
        }
    }
}
