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
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.ToString().Equals(OntologyHelper.OwlDatatypeProperty)).Any();
        }

        public static bool IsObjectProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.ToString().Equals(OntologyHelper.OwlObjectProperty)).Any();
        }

        public static bool IsAnnotationProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.ToString().Equals(OntologyHelper.OwlAnnotationProperty)).Any();
        }

        public static bool IsRdfsDatatype(this OntologyClass oClass)
        {
            return oClass.Types.Where(classType => classType.NodeType == NodeType.Uri).Where(classType => ((UriNode)classType).Uri.ToString().Equals("http://www.w3.org/2000/01/rdf-schema#Datatype")).Any();
        }

        public static bool IsNamed(this OntologyResource resource)
        {
            return resource.Resource.NodeType.Equals(NodeType.Uri);
        }

        public static bool IsLiteral(this INode node)
        {
            return node.NodeType.Equals(NodeType.Literal);
        }

        public static bool IsUri(this INode node)
        {
            return node.NodeType.Equals(NodeType.Uri);
        }

        public static bool IsEnglish(this ILiteralNode node)
        {
            return (node.Language.Equals("en") || node.Language.StartsWith("en-"));
        }

        public static bool HasLanguage(this ILiteralNode node)
        {
            return (!node.Language.Equals(String.Empty));
        }

        public static IEnumerable<INode> GetNodesViaProperty(this OntologyResource resource, INode property)
        {
            return resource.Graph.GetTriplesWithSubjectPredicate(resource.Resource, property).Select(triple => triple.Object);
        }

        public static bool IsXsdDatatype(this OntologyClass oClass)
        {
            if (oClass.Resource.NodeType.Equals(NodeType.Uri))
            {
                return (((UriNode)oClass.Resource).Uri.ToString().StartsWith(XmlSpecsHelper.NamespaceXmlSchema));
            }
            return false;
        }

        public static string GetLocalName(this IUriNode node)
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

        public static string GetLocalName(this OntologyResource resource)
        {
            if (resource.Resource.NodeType.Equals(NodeType.Uri))
            {
                return ((UriNode)resource.Resource).GetLocalName();
            }
            else
            { 
                throw new RdfException(String.Format("{0} is not backed by a named URI node.", resource));
            }
        }
    }
}
