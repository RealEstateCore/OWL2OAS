/*
 * RealEstateCore Core Module
 * The documentation below is automatically extracted from an <rdfs:comment> annotation on the ontology RealEstateCore Core Module:<br/><br/>*The REC core module collects the top-level classes and properties that span over or are reused within multiple REC modules.<br/><br/>Note that this module reuses certain classes, properties, and named individuals from other vocabularies, e.g., GeoSPARQL; the copyright conditions on those reused entities are stated in their respective rdfs:comments annotations.*
 *
 * OpenAPI spec version: 3.0
 * 
 *
 * NOTE: This class is auto generated by the swagger code generator program.
 * https://github.com/swagger-api/swagger-codegen.git
 * Do not edit the class manually.
 */

package io.swagger.client.auth;

import io.swagger.client.Pair;

import java.util.Map;
import java.util.List;

public interface Authentication {
    /**
     * Apply authentication settings to header and query params.
     *
     * @param queryParams List of query parameters
     * @param headerParams Map of header parameters
     */
    void applyToParams(List<Pair> queryParams, Map<String, String> headerParams);
}
