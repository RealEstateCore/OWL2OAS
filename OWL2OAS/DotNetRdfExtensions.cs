using System;
using System.Collections.Generic;
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

        // TODO: The below check isn't really right, is it?
        public static bool IsDatatype(this OntologyClass oClass)
        {
            if (oClass.Resource.NodeType.Equals(NodeType.Uri))
            {
                if (((UriNode)oClass.Resource).Uri.ToString().Contains(XmlSpecsHelper.NamespaceXmlSchema))
                {
                    return true;
                }
            }
            return false;
        }

        // TODO: This is crap.
        public static Uri GetDataRange(this OntologyProperty property)
        {
            return property.Ranges.Where(range => range.IsDatatype()).Select(rangeClass => ((UriNode)rangeClass.Resource).Uri).DefaultIfEmpty(new Uri(RdfSpecsHelper.RdfXmlLiteral)).FirstOrDefault();
        }
    }
}
