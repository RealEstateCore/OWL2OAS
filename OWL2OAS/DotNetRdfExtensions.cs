using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDS.RDF;
using VDS.RDF.Ontology;

namespace OWL2OAS
{
    public static class DotNetRdfExtensions
    {
        public static bool isDataProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlDatatypeProperty)).Any();
        }

        public static bool isObjectProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlObjectProperty)).Any();
        }

        public static bool isAnnotationProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.Equals(OntologyHelper.OwlAnnotationProperty)).Any();
        }
    }
}
