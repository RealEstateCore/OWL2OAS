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
    }
}
