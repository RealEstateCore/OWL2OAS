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

package io.swagger.client.model;

import java.util.Objects;
import java.util.Arrays;
import com.google.gson.TypeAdapter;
import com.google.gson.annotations.JsonAdapter;
import com.google.gson.annotations.SerializedName;
import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonWriter;
import io.swagger.client.model.BuildingComponent;
import io.swagger.client.model.Context;
import io.swagger.client.model.GeoReferenceOrigo;
import io.swagger.v3.oas.annotations.media.Schema;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
/**
 * Building
 */

@javax.annotation.Generated(value = "io.swagger.codegen.v3.generators.java.JavaClientCodegen", date = "2019-10-16T06:50:30.499Z[GMT]")
public class Building {
  @SerializedName("@context")
  private Context _atContext = null;

  @SerializedName("@id")
  private String _atId = null;

  @SerializedName("@type")
  private String _atType = "Building";

  @SerializedName("label")
  private String label = null;

  @SerializedName("hasBuildingComponent")
  private List<BuildingComponent> hasBuildingComponent = null;

  @SerializedName("hasGeoReferenceOrigo")
  private GeoReferenceOrigo hasGeoReferenceOrigo = null;

  public Building _atContext(Context _atContext) {
    this._atContext = _atContext;
    return this;
  }

   /**
   * Get _atContext
   * @return _atContext
  **/
  @Schema(description = "")
  public Context getAtContext() {
    return _atContext;
  }

  public void setAtContext(Context _atContext) {
    this._atContext = _atContext;
  }

  public Building _atId(String _atId) {
    this._atId = _atId;
    return this;
  }

   /**
   * Get _atId
   * @return _atId
  **/
  @Schema(description = "")
  public String getAtId() {
    return _atId;
  }

  public void setAtId(String _atId) {
    this._atId = _atId;
  }

  public Building _atType(String _atType) {
    this._atType = _atType;
    return this;
  }

   /**
   * Get _atType
   * @return _atType
  **/
  @Schema(description = "")
  public String getAtType() {
    return _atType;
  }

  public void setAtType(String _atType) {
    this._atType = _atType;
  }

  public Building label(String label) {
    this.label = label;
    return this;
  }

   /**
   * Get label
   * @return label
  **/
  @Schema(description = "")
  public String getLabel() {
    return label;
  }

  public void setLabel(String label) {
    this.label = label;
  }

  public Building hasBuildingComponent(List<BuildingComponent> hasBuildingComponent) {
    this.hasBuildingComponent = hasBuildingComponent;
    return this;
  }

  public Building addHasBuildingComponentItem(BuildingComponent hasBuildingComponentItem) {
    if (this.hasBuildingComponent == null) {
      this.hasBuildingComponent = new ArrayList<BuildingComponent>();
    }
    this.hasBuildingComponent.add(hasBuildingComponentItem);
    return this;
  }

   /**
   * Get hasBuildingComponent
   * @return hasBuildingComponent
  **/
  @Schema(description = "")
  public List<BuildingComponent> getHasBuildingComponent() {
    return hasBuildingComponent;
  }

  public void setHasBuildingComponent(List<BuildingComponent> hasBuildingComponent) {
    this.hasBuildingComponent = hasBuildingComponent;
  }

  public Building hasGeoReferenceOrigo(GeoReferenceOrigo hasGeoReferenceOrigo) {
    this.hasGeoReferenceOrigo = hasGeoReferenceOrigo;
    return this;
  }

   /**
   * Get hasGeoReferenceOrigo
   * @return hasGeoReferenceOrigo
  **/
  @Schema(description = "")
  public GeoReferenceOrigo getHasGeoReferenceOrigo() {
    return hasGeoReferenceOrigo;
  }

  public void setHasGeoReferenceOrigo(GeoReferenceOrigo hasGeoReferenceOrigo) {
    this.hasGeoReferenceOrigo = hasGeoReferenceOrigo;
  }


  @Override
  public boolean equals(java.lang.Object o) {
    if (this == o) {
      return true;
    }
    if (o == null || getClass() != o.getClass()) {
      return false;
    }
    Building building = (Building) o;
    return Objects.equals(this._atContext, building._atContext) &&
        Objects.equals(this._atId, building._atId) &&
        Objects.equals(this._atType, building._atType) &&
        Objects.equals(this.label, building.label) &&
        Objects.equals(this.hasBuildingComponent, building.hasBuildingComponent) &&
        Objects.equals(this.hasGeoReferenceOrigo, building.hasGeoReferenceOrigo);
  }

  @Override
  public int hashCode() {
    return Objects.hash(_atContext, _atId, _atType, label, hasBuildingComponent, hasGeoReferenceOrigo);
  }


  @Override
  public String toString() {
    StringBuilder sb = new StringBuilder();
    sb.append("class Building {\n");
    
    sb.append("    _atContext: ").append(toIndentedString(_atContext)).append("\n");
    sb.append("    _atId: ").append(toIndentedString(_atId)).append("\n");
    sb.append("    _atType: ").append(toIndentedString(_atType)).append("\n");
    sb.append("    label: ").append(toIndentedString(label)).append("\n");
    sb.append("    hasBuildingComponent: ").append(toIndentedString(hasBuildingComponent)).append("\n");
    sb.append("    hasGeoReferenceOrigo: ").append(toIndentedString(hasGeoReferenceOrigo)).append("\n");
    sb.append("}");
    return sb.toString();
  }

  /**
   * Convert the given object to string with each line indented by 4 spaces
   * (except the first line).
   */
  private String toIndentedString(java.lang.Object o) {
    if (o == null) {
      return "null";
    }
    return o.toString().replace("\n", "\n    ");
  }

}
