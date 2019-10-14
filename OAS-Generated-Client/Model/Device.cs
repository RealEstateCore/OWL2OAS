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
  public class Device {
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
    /// Gets or Sets AssociatedWithEvent
    /// </summary>
    [DataMember(Name="associatedWithEvent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "associatedWithEvent")]
    public List<Event> AssociatedWithEvent { get; set; }

    /// <summary>
    /// Gets or Sets DeviceMeasurementUnit
    /// </summary>
    [DataMember(Name="deviceMeasurementUnit", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deviceMeasurementUnit")]
    public List<MeasurementUnit> DeviceMeasurementUnit { get; set; }

    /// <summary>
    /// Gets or Sets DeviceQuantityKind
    /// </summary>
    [DataMember(Name="deviceQuantityKind", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "deviceQuantityKind")]
    public List<QuantityKind> DeviceQuantityKind { get; set; }

    /// <summary>
    /// Gets or Sets HasSubDevice
    /// </summary>
    [DataMember(Name="hasSubDevice", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasSubDevice")]
    public List<Device> HasSubDevice { get; set; }

    /// <summary>
    /// Gets or Sets HasSuperDevice
    /// </summary>
    [DataMember(Name="hasSuperDevice", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasSuperDevice")]
    public Device HasSuperDevice { get; set; }

    /// <summary>
    /// Gets or Sets IsMountedInBuildingComponent
    /// </summary>
    [DataMember(Name="isMountedInBuildingComponent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "isMountedInBuildingComponent")]
    public BuildingComponent IsMountedInBuildingComponent { get; set; }

    /// <summary>
    /// Gets or Sets ServesBuilding
    /// </summary>
    [DataMember(Name="servesBuilding", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "servesBuilding")]
    public List<Building> ServesBuilding { get; set; }

    /// <summary>
    /// Gets or Sets ServesBuildingComponent
    /// </summary>
    [DataMember(Name="servesBuildingComponent", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "servesBuildingComponent")]
    public List<BuildingComponent> ServesBuildingComponent { get; set; }

    /// <summary>
    /// Gets or Sets ServesDevice
    /// </summary>
    [DataMember(Name="servesDevice", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "servesDevice")]
    public List<Device> ServesDevice { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      var sb = new StringBuilder();
      sb.Append("class Device {\n");
      sb.Append("  Context: ").Append(Context).Append("\n");
      sb.Append("  Id: ").Append(Id).Append("\n");
      sb.Append("  Type: ").Append(Type).Append("\n");
      sb.Append("  Rdfslabel: ").Append(Rdfslabel).Append("\n");
      sb.Append("  AssociatedWithEvent: ").Append(AssociatedWithEvent).Append("\n");
      sb.Append("  DeviceMeasurementUnit: ").Append(DeviceMeasurementUnit).Append("\n");
      sb.Append("  DeviceQuantityKind: ").Append(DeviceQuantityKind).Append("\n");
      sb.Append("  HasSubDevice: ").Append(HasSubDevice).Append("\n");
      sb.Append("  HasSuperDevice: ").Append(HasSuperDevice).Append("\n");
      sb.Append("  IsMountedInBuildingComponent: ").Append(IsMountedInBuildingComponent).Append("\n");
      sb.Append("  ServesBuilding: ").Append(ServesBuilding).Append("\n");
      sb.Append("  ServesBuildingComponent: ").Append(ServesBuildingComponent).Append("\n");
      sb.Append("  ServesDevice: ").Append(ServesDevice).Append("\n");
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
