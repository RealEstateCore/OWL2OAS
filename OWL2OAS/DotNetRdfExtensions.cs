using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;

namespace OWL2OAS
{
    public static class DotNetRdfExtensions
    {
        public static bool IsDataProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlDatatypeProperty)).Any();
        }

        public static bool IsObjectProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlObjectProperty)).Any();
        }

        public static bool IsAnnotationProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlAnnotationProperty)).Any();
        }

        public static bool IsRdfsDatatype(this OntologyClass oClass)
        {
            return oClass.Types.Where(classType => classType.NodeType == NodeType.Uri).Where(classType => ((UriNode)classType).Uri.Equals("http://www.w3.org/2000/01/rdf-schema#Datatype")).Any();
        }

        public static bool IsXsdDatatype(this OntologyClass oClass)
        {
            if (oClass.Resource.NodeType.Equals(NodeType.Uri))
            {
                return (((UriNode)oClass.Resource).Uri.ToString().StartsWith(XmlSpecsHelper.NamespaceXmlSchema));
            }
            return false;
        }

        public static string GetLocalName(this UriNode node)
        {
            if (node.Uri.Fragment.Length > 0)
            {
                return node.Uri.Fragment.Trim('#');
            }
            else
            {
                return Path.GetFileName(node.Uri.AbsolutePath);
            }
        }
    }
}
