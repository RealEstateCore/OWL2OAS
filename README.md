
# The OWL2OAS Converter

**Author:** [Karl Hammar](https://karlhammar.com)

This is a converter for translating [OWL ontologies](https://www.w3.org/TR/owl2-overview/) into
[OpenAPI Specification](https://swagger.io/specification/) documents. OWL is a formal language for expressing concepts and their
relations on the web, based on description logic. OpenAPI Specification is a standard for describing REST endpoints and operations.
This tool generates REST endpoints for the classes (i.e., concepts) declared in an ontology, and generates [JSON-LD](https://json-ld.org)
schemas for those classes based on the object and data properties that they are linked to in the ontology (either via
rdfs:domain/rdfs:range, or via property restrictions).

## Usage

The generated OAS document is printed to stdout.

To translate a local file, use the `-f`option, as follows:

```
./OWL2OAS -f /path/to/ontology.rdf
```

To translate a remote file, use the `-u` option, as follows:
```
./OWL2OAS -u http://example.com/path/to/ontology.rdf
```

## Options

```
  -c, --ClassInclusionPolicy       (Default: DefaultInclude) Whether to include
                                   all classes by default (overridden by
                                   o2o:included annotation). Valid options:
                                   DefaultInclude or DefaultExclude.

  -p, --PropertyInclusionPolicy    (Default: DefaultInclude) Whether to include
                                   all properties by default (overridden by
                                   o2o:included annotation). Valid options:
                                   DefaultInclude or DefaultExclude.

  -n, --no-imports                 Sets program to not follow owl:Imports
                                   declarations.

  -s, --server                     (Default: http://localhost:8080/) The server
                                   URL (where presumably an API implementation
                                   is running).

  -f, --file-path                  Required. The path to the on-disk root
                                   ontology file to translate.

  -u, --uri-path                   Required. The URI of the root ontology file
                                   to translate.

  --help                           Display this help screen.

  --version                        Display version information.
```

## Entity inclusion policy

By default we include all classes and properties found in the parsed ontologies. If you want to specifically exclude some entity from the generated output, apply the `https://karlhammar.com/owl2oas/o2o.owl#included` annotation property on it, with the boolean value `false`. If you want to reverse this inclusion policy, for classes or properties, use the command line options `--ClassInclusionPolicy` or `--PropertyInclusionPolicy` with the value `DefaultExclude`; then, *only* the entities of that kind which have been tagged with `https://karlhammar.com/owl2oas/o2o.owl#included` and the value `true` will be included.

## OWL Entity Equivalence 

We do not support equivalence between classes or between properties. Strange things will probably happen if you run this tool over ontologies that depend on equivalence axioms. Feel free to try it out though!

## Examples

See the [examples directory](https://github.com/hammar/OWL2OAS/tree/master/OWL2OAS/examples).
