using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace IO.Swagger.Model {

  /// <summary>
  /// 
  /// </summary>
  [DataContract]
  public class BuildingComponent {
    /// <summary>
    /// Gets or Sets Context
    /// </summary>
    [DataMember(Name="@context", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "@context")]
    public Context Context { get; set; }

    /// <summary>
    /// Gets or Sets Id
    /// </summary>
    [DataMember(Name="@id", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "@id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or Sets Type
    /// </summary>
    [DataMember(Name="@type", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "@type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or Sets Rdfslabel
    /// </summary>
    [DataMember(Name="rdfs:label", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "rdfs:label")]
    public string Rdfslabel { get; set; }

    /// <summary>
    /// Gets or Sets ContainsMountedDevice
    /// </summary>
    [DataMember(Name="containsMountedDevice", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "containsMountedDevice")]
    public List<Device> ContainsMountedDevice { get; set; }

    /// <summary>
    /// Gets or Sets HasSubBuildingComponent
    /// </summary>
    [DataMember(Name="hasSubBuildingComponent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasSubBuildingComponent")]
    public List<BuildingComponent> HasSubBuildingComponent { get; set; }

    /// <summary>
    /// Gets or Sets HasSuperBuildingComponent
    /// </summary>
    [DataMember(Name="hasSuperBuildingComponent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasSuperBuildingComponent")]
    public List<BuildingComponent> HasSuperBuildingComponent { get; set; }

    /// <summary>
    /// Gets or Sets IsPartOfBuilding
    /// </summary>
    [DataMember(Name="isPartOfBuilding", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "isPartOfBuilding")]
    public List<Building> IsPartOfBuilding { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      var sb = new StringBuilder();
      sb.Append("class BuildingComponent {\n");
      sb.Append("  Context: ").Append(Context).Append("\n");
      sb.Append("  Id: ").Append(Id).Append("\n");
      sb.Append("  Type: ").Append(Type).Append("\n");
      sb.Append("  Rdfslabel: ").Append(Rdfslabel).Append("\n");
      sb.Append("  ContainsMountedDevice: ").Append(ContainsMountedDevice).Append("\n");
      sb.Append("  HasSubBuildingComponent: ").Append(HasSubBuildingComponent).Append("\n");
      sb.Append("  HasSuperBuildingComponent: ").Append(HasSuperBuildingComponent).Append("\n");
      sb.Append("  IsPartOfBuilding: ").Append(IsPartOfBuilding).Append("\n");
      sb.Append("}\n");
      return sb.ToString();
    }

    /// <summary>
    /// Get the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public string ToJson() {
      return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

}
}
