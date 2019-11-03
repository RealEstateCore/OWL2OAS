
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
  -n, --no-imports    Sets program to not follow owl:Imports declarations.

  -s, --server        (Default: http://localhost:8080/) The server URL (where
                      presumably an API implementation is running).

  -f, --file-path     Required. The path to the on-disk root ontology file to
                      translate.

  -u, --uri-path      Required. The URI of the root ontology file to translate.

  --help              Display this help screen.

  --version           Display version information.
```

## Examples

See the [examples directory](tree/master/OWL2OAS/examples).
