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

package io.swagger.client;

@javax.annotation.Generated(value = "io.swagger.codegen.v3.generators.java.JavaClientCodegen", date = "2019-10-16T06:50:30.499Z[GMT]")public class Configuration {
    private static ApiClient defaultApiClient = new ApiClient();

    /**
     * Get the default API client, which would be used when creating API
     * instances without providing an API client.
     *
     * @return Default API client
     */
    public static ApiClient getDefaultApiClient() {
        return defaultApiClient;
    }

    /**
     * Set the default API client, which would be used when creating API
     * instances without providing an API client.
     *
     * @param apiClient API client
     */
    public static void setDefaultApiClient(ApiClient apiClient) {
        defaultApiClient = apiClient;
    }
}
