using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Parsing;

namespace OWL2OAS
{
    /// <summary>
    /// Various extensions to DotNetRdf, particularly relating to the <c>VDS.RDF.Ontology</c> functionality.
    /// </summary>
    public static class DotNetRdfExtensions
    {
        #region Shared
        /// <summary>
        /// Custom comparer for OntologyResource objects, that simply
        /// defers to comparison of nested INodes.
        /// </summary>
        class OntologyResourceComparer : IEqualityComparer<OntologyResource>
        {
            public bool Equals(OntologyResource x, OntologyResource y)
            {
                return x.Resource == y.Resource;
            }

            public int GetHashCode(OntologyResource obj)
            {
                return obj.Resource.GetHashCode();
            }
        }
        #endregion

        #region INode/ILiteralNode/IUriNode extensions
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
            return node.Language.Equals("en") || node.Language.StartsWith("en-", StringComparison.Ordinal);
        }

        public static bool HasLanguage(this ILiteralNode node)
        {
            return (!node.Language.Equals(String.Empty));
        }

        public static bool IsInteger(this ILiteralNode node)
        {
            string datatype = node.DataType.ToString();
            return datatype.StartsWith(XmlSpecsHelper.NamespaceXmlSchema, StringComparison.Ordinal)
                && (datatype.EndsWith("Integer", StringComparison.Ordinal) || datatype.EndsWith("Int", StringComparison.Ordinal));
        }

        
        public static bool IsDataProperty(this INode propertyNode)
        {
            IGraph graph = propertyNode.Graph;
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode dataProperty = graph.CreateUriNode(new Uri(OntologyHelper.OwlDatatypeProperty));
            return graph.ContainsTriple(new Triple(propertyNode, rdfType, dataProperty));
        }

        public static bool IsObjectProperty(this INode propertyNode)
        {
            IGraph graph = propertyNode.Graph;
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode objectProperty = graph.CreateUriNode(new Uri(OntologyHelper.OwlObjectProperty));
            return graph.ContainsTriple(new Triple(propertyNode, rdfType, objectProperty));
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

        public static string GetLocalName(this IUriNode node)
        {
            if (node.Uri.Fragment.Length > 0)
            {
                return node.Uri.Fragment.Trim('#');
            }
            return Path.GetFileName(node.Uri.AbsolutePath);
        }

        public static string GetNamespaceName(this IUriNode node)
        {
            if (node.Uri.Fragment.Length > 0)
            {
                return node.Uri.GetLeftPart(UriPartial.Path);
            }
            string link = node.Uri.GetLeftPart(UriPartial.Path);
            return link.Substring(0, link.LastIndexOf("/", StringComparison.Ordinal) + 1);
        }
        #endregion

        #region OntologyGraph extensions
        public static bool HasBaseUriOntology(this OntologyGraph graph)
        {
            IUriNode baseUriNode = graph.CreateUriNode(graph.BaseUri);
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode owlOntology = graph.CreateUriNode(new Uri(OntologyHelper.OwlOntology));
            return graph.ContainsTriple(new Triple(baseUriNode, rdfType, owlOntology));
        }

        public static Ontology GetBaseUriOntology(this OntologyGraph graph)
        {
            IUriNode ontologyUriNode = graph.CreateUriNode(graph.BaseUri);
            return new Ontology(ontologyUriNode, graph);
        }

        /// <summary>
        /// Gets all owl:Ontology nodes declared in the graph, packaged as Ontology objects.
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static IEnumerable<Ontology> GetOntologies(this OntologyGraph graph)
        {
            IUriNode rdfType = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfType));
            IUriNode owlOntology = graph.CreateUriNode(new Uri(OntologyHelper.OwlOntology));
            IEnumerable<IUriNode> ontologyNodes = graph.GetTriplesWithPredicateObject(rdfType, owlOntology)
                .Select(triple => triple.Subject)
                .UriNodes();
            return ontologyNodes.Select(node => new Ontology(node, graph));
        }

        /// <summary>
        /// Returns the "main" owl:Ontology declared in the the graph. Will
        /// return the owl:Ontology whose identifier matches the RDF graph base
        /// URI; if no such owl:Ontology is present, will return the first declared
        /// owl:Ontology; if there are none, will throw an RdfException.
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        public static Ontology GetOntology(this OntologyGraph graph)
        {
            if (graph.HasBaseUriOntology())
            {
                return graph.GetBaseUriOntology();
            }
            IEnumerable<Ontology> graphOntologies = graph.GetOntologies();
            if (graphOntologies.Any())
            {
                return graphOntologies.First();
            }
            throw new RdfException(string.Format("The graph {0} doesn't contain any owl:Ontology declarations.", graph));
        }
        #endregion

        #region OntologyResource extensions
        public static bool IsNamed(this OntologyResource ontResource)
        {
            return ontResource.Resource.IsUri();
        }

        public static Uri GetIri(this OntologyResource resource)
        {
            if (!resource.IsNamed())
            {
                throw new RdfException(string.Format("Ontology resource {0} does not have an IRI.", resource));
            }

            return ((UriNode)resource.Resource).Uri;
        }

        // TODO this method and the one above seem to overlap; simplify if possible
        public static string GetLocalName(this OntologyResource resource)
        {
            if (resource.IsNamed())
            {
                return ((UriNode)resource.Resource).GetLocalName();
            }
            throw new RdfException(string.Format("{0} is not backed by a named URI node.", resource));
        }

        public static IEnumerable<INode> GetNodesViaProperty(this OntologyResource resource, INode property)
        {
            return resource.Graph.GetTriplesWithSubjectPredicate(resource.Resource, property).Select(triple => triple.Object);
        }

        public static bool IsDeprecated(this OntologyResource resource)
        {
            IUriNode deprecated = resource.Graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#deprecated"));
            return resource.GetNodesViaProperty(deprecated).LiteralNodes().Any(node => node.Value == "true");
        }

        public static OntologyGraph OntologyGraph(this OntologyResource resource)
        {
            // TODO: Check if this is potentially explosive design
            return resource.Graph as OntologyGraph;
        }
        #endregion

        #region Ontology extensions
        public static bool HasVersionIri(this Ontology ontology)
        {
            IUriNode versionIri = ontology.Graph.CreateUriNode(VocabularyHelper.OWL.versionIRI);
            return ontology.GetNodesViaProperty(versionIri).UriNodes().Any();
        }

        public static Uri GetVersionIri(this Ontology ontology)
        {
            if (!ontology.HasVersionIri())
            {
                throw new RdfException(string.Format("Ontology {0} does not have an owl:versionIRI annotation", ontology));
            }

            IUriNode versionIri = ontology.Graph.CreateUriNode(VocabularyHelper.OWL.versionIRI);
            return ontology.GetNodesViaProperty(versionIri).UriNodes().First().Uri;
        }

        /// <summary>
        /// Gets the version IRI of the ontology, if it is defined, or the ontology
        /// IRI if it is not. If neither is defined (i.e. the ontology is anonymous),
        /// throws an exception.
        /// </summary>
        /// <param name="ontology"></param>
        /// <returns></returns>
        public static Uri GetVersionOrOntologyIri(this Ontology ontology)
        {
            if (ontology.HasVersionIri())
            {
                return ontology.GetVersionIri();
            }
            return ontology.GetIri();
        }

        /// <summary>
        /// Gets a short name representation for an ontology, based on the last segment
        /// of the ontology IRI or (in the case of anonymous ontologies) the ontology hash.
        /// Useful for qname prefixes.
        /// </summary>
        /// <param name="ontology"></param>
        /// <returns></returns>
        public static string GetShortName(this Ontology ontology)
        {
            // Fallback way of getting a persistent short identifier in the
            // (unlikely?) case that we are dealing w/ an anonymous ontology
            if (!ontology.IsNamed())
            {
                return ontology.GetHashCode().ToString();
            }

            // This is a simple string handling thing
            string ontologyUriString = ontology.GetIri().ToString();

            // Trim any occurences of entity separation characters
            if (ontologyUriString.EndsWith("/", StringComparison.Ordinal) || ontologyUriString.EndsWith("#", StringComparison.Ordinal))
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
        #endregion

        #region OntologyClass extensions
        public static bool IsRdfsDatatype(this OntologyClass oClass)
        {
            return oClass.Types.UriNodes().Any(classType => classType.Uri.Equals(VocabularyHelper.RDFS.Datatype));
        }

        public static bool IsRestriction(this OntologyClass oClass)
        {
            return oClass.Types.UriNodes().Any(classType => classType.Uri.Equals(VocabularyHelper.OWL.Restriction));
        }

        public static bool IsXsdDatatype(this OntologyClass oClass)
        {
            if (oClass.IsNamed())
            {
                return oClass.GetIri().ToString().StartsWith(XmlSpecsHelper.NamespaceXmlSchema, StringComparison.Ordinal);
            }
            return false;
        }

        public static IEnumerable<OntologyProperty> IsScopedDomainOf(this OntologyClass cls)
        {
            OntologyGraph graph = cls.Graph as OntologyGraph;
            IUriNode onProperty = graph.CreateUriNode(new Uri("http://www.w3.org/2002/07/owl#onProperty"));
            IEnumerable<IUriNode> propertyNodes = cls.SuperClasses.Where(superClass => superClass.IsRestriction())
                .SelectMany(restriction => restriction.GetNodesViaProperty(onProperty)).UriNodes();
            return propertyNodes.SelectMany(node => graph.OwlProperties.Where(oProperty => oProperty.Resource.Equals(node)));
        }

        public static IEnumerable<OntologyProperty> IsExhaustiveDomainOf(this OntologyClass oClass)
        {
            IEnumerable<OntologyProperty> directDomainProperties = oClass.IsDomainOf;
            IEnumerable<OntologyProperty> indirectDomainProperties = oClass.SuperClasses.SelectMany(cls => cls.IsDomainOf);
            IEnumerable<OntologyProperty> scopedDomainProperties = oClass.IsScopedDomainOf();
            IEnumerable<OntologyProperty> allDomainProperties = directDomainProperties.Union(indirectDomainProperties).Union(scopedDomainProperties);
            return allDomainProperties.Distinct(new OntologyResourceComparer()).Select(ontResource => ontResource as OntologyProperty);
        }
        #endregion

        #region OntologyProperty extensions
        public static bool IsObjectProperty(this OntologyProperty property)
        {
            return property.Types.UriNodes().Any(propertyType => propertyType.Uri.ToString().Equals(OntologyHelper.OwlObjectProperty));
        }

        public static bool IsDataProperty(this OntologyProperty property)
        {
            return property.Types.UriNodes().Any(propertyType => propertyType.Uri.ToString().Equals(OntologyHelper.OwlDatatypeProperty));
        }

        public static bool IsAnnotationProperty(this OntologyProperty property)
        {
            return property.Types.UriNodes().Any(propertyType => propertyType.Uri.ToString().Equals(OntologyHelper.OwlAnnotationProperty));
        }

        public static bool IsFunctional(this OntologyProperty property)
        {
            return property.Types.UriNodes().Any(propertyType => propertyType.Uri.Equals(VocabularyHelper.OWL.FunctionalProperty));
        }

        public static IEnumerable<OntologyProperty> DataProperties(this IEnumerable<OntologyProperty> properties)
        {
            return properties.Where(property => property.IsDataProperty());
        }

        public static IEnumerable<OntologyProperty> ObjectProperties(this IEnumerable<OntologyProperty> properties)
        {
            return properties.Where(property => property.IsObjectProperty());
        }
        #endregion
    }
}
