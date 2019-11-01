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

        public static IEnumerable<OntologyProperty> IsScopedDomainOf(this OntologyClass cls)
        {
            OntologyGraph graph = cls.Graph as OntologyGraph;
            IUriNode onProperty = cls.Graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#onProperty"));
            IEnumerable<IUriNode> propertyNodes = cls.SuperClasses.Where(superClass => superClass.IsRestriction())
                .SelectMany(restriction => restriction.GetNodesViaProperty(onProperty)).UriNodes();
            return propertyNodes.SelectMany(node => graph.OwlProperties.Where(oProperty => oProperty.Resource.Equals(node)));
        }

        public static bool IsDeprecated(this OntologyResource resource)
        {
            IUriNode deprecated = resource.Graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#deprecated"));
            return resource.GetNodesViaProperty(deprecated).Where(node => node.IsLiteral() && (node as ILiteralNode).Value == "true").Any();
        }

        public static bool IsDataProperty(this INode propertyNode)
        {
            IGraph graph = propertyNode.Graph;
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode dataProperty = graph.CreateUriNode(new Uri(OntologyHelper.OwlDatatypeProperty));
            return graph.ContainsTriple(new Triple(propertyNode, rdfType, dataProperty));
        }

        public static bool IsObjectProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.ToString().Equals(OntologyHelper.OwlObjectProperty)).Any();
        }

        public static bool IsObjectProperty(this INode propertyNode)
        {
            IGraph graph = propertyNode.Graph;
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode objectProperty = graph.CreateUriNode(new Uri(OntologyHelper.OwlObjectProperty));
            return graph.ContainsTriple(new Triple(propertyNode, rdfType, objectProperty));
        }

        public static bool IsAnnotationProperty(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.NodeType == NodeType.Uri).Where(propertyType => ((UriNode)propertyType).Uri.ToString().Equals(OntologyHelper.OwlAnnotationProperty)).Any();
        }

        public static bool IsAnnotationProperty(this INode propertyNode)
        {
            IGraph graph = propertyNode.Graph;
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode annotationProperty = graph.CreateUriNode(new Uri(OntologyHelper.OwlAnnotationProperty));
            return graph.ContainsTriple(new Triple(propertyNode, rdfType, annotationProperty));
        }

        public static bool IsOntologyProperty(this INode node)
        {
            return node.IsAnnotationProperty() || node.IsDataProperty() || node.IsObjectProperty();
        }

        public static bool IsRdfsDatatype(this OntologyClass oClass)
        {
            return oClass.Types.Where(classType => classType.NodeType == NodeType.Uri).Where(classType => ((UriNode)classType).Uri.ToString().Equals("http://www.w3.org/2000/01/rdf-schema#Datatype")).Any();
        }

        public static bool IsRestriction(this OntologyClass oClass)
        {
            return oClass.Types.Where(classType => classType.NodeType == NodeType.Uri).Where(classType => ((UriNode)classType).Uri.ToString().Equals("http://www.w3.org/2002/07/owl#Restriction")).Any();
        }

        public static bool IsFunctional(this OntologyProperty property)
        {
            return property.Types.Where(propertyType => propertyType.IsUri() && ((UriNode)propertyType).Uri.ToString().Equals("http://www.w3.org/2002/07/owl#FunctionalProperty")).Any();
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

        public static bool IsInteger(this ILiteralNode node)
        {
            string datatype = node.DataType.ToString();
            return (datatype.StartsWith(XmlSpecsHelper.NamespaceXmlSchema) && (datatype.EndsWith("Integer") ||datatype.EndsWith("Int")));
        }

        public static IEnumerable<INode> GetNodesViaProperty(this OntologyResource resource, INode property)
        {
            return resource.Graph.GetTriplesWithSubjectPredicate(resource.Resource, property).Select(triple => triple.Object);
        }

        public static IEnumerable<ILiteralNode> GetLiteralNodesViaProperty(this OntologyResource resource, INode property)
        {
            return resource.GetNodesViaProperty(property).Where(node => node.NodeType == NodeType.Literal).Select(node => node as ILiteralNode);
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

        public static string GetNamespaceName(this IUriNode node)
        {
            if (node.Uri.Fragment.Length > 0)
            {
                return node.Uri.GetLeftPart(UriPartial.Path);
            }
            else
            {
                string link = node.Uri.GetLeftPart(UriPartial.Path);
                return link.Substring(0, link.LastIndexOf("/") + 1);
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

        public static string GetOntologyShortName(this Ontology ontology)
        {
            // Fallback way of getting a persistent short identifier in the
            // (unlikely?) case that we are dealing w/ an anonymous ontology
            if (!ontology.IsNamed())
            {
                return ontology.GetHashCode().ToString();
            }

            // This is a simple string handling thing
            string ontologyUriString = ((IUriNode)ontology.Resource).Uri.ToString();

            // Trim any occurences of entity separation characters
            if (ontologyUriString.EndsWith("/") || ontologyUriString.EndsWith("#"))
            {
                char[] trimChars = { '/', '#' };
                ontologyUriString = ontologyUriString.Trim(trimChars);
            }

            // Get the last bit of the string
            ontologyUriString = ontologyUriString.Substring(ontologyUriString.LastIndexOf('/') + 1);

            // If the string contains dots, treat them as file ending delimiter and get rid of them
            // one at a time
            while (ontologyUriString.Contains('.'))
            {
                ontologyUriString = ontologyUriString.Substring(0, ontologyUriString.LastIndexOf('.'));
            }

            return ontologyUriString;
        }
    }
}
