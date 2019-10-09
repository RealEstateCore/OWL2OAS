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
  public class Event {
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
    /// Gets or Sets EventMeasurementUnit
    /// </summary>
    [DataMember(Name="eventMeasurementUnit", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "eventMeasurementUnit")]
    public List<MeasurementUnit> EventMeasurementUnit { get; set; }

    /// <summary>
    /// Gets or Sets EventQuantityKind
    /// </summary>
    [DataMember(Name="eventQuantityKind", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "eventQuantityKind")]
    public List<QuantityKind> EventQuantityKind { get; set; }

    /// <summary>
    /// Gets or Sets HasCreatedTime
    /// </summary>
    [DataMember(Name="hasCreatedTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasCreatedTime")]
    public List<DateTime?> HasCreatedTime { get; set; }

    /// <summary>
    /// Gets or Sets HasDeletedTime
    /// </summary>
    [DataMember(Name="hasDeletedTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasDeletedTime")]
    public List<DateTime?> HasDeletedTime { get; set; }

    /// <summary>
    /// Gets or Sets HasDuration
    /// </summary>
    [DataMember(Name="hasDuration", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasDuration")]
    public List<double?> HasDuration { get; set; }

    /// <summary>
    /// Gets or Sets HasObservationTime
    /// </summary>
    [DataMember(Name="hasObservationTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasObservationTime")]
    public List<DateTime?> HasObservationTime { get; set; }

    /// <summary>
    /// Gets or Sets HasPointInTime
    /// </summary>
    [DataMember(Name="hasPointInTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasPointInTime")]
    public List<DateTime?> HasPointInTime { get; set; }

    /// <summary>
    /// Gets or Sets HasReadTime
    /// </summary>
    [DataMember(Name="hasReadTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasReadTime")]
    public List<DateTime?> HasReadTime { get; set; }

    /// <summary>
    /// Gets or Sets HasStartTime
    /// </summary>
    [DataMember(Name="hasStartTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasStartTime")]
    public List<DateTime?> HasStartTime { get; set; }

    /// <summary>
    /// Gets or Sets HasStopTime
    /// </summary>
    [DataMember(Name="hasStopTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasStopTime")]
    public List<DateTime?> HasStopTime { get; set; }

    /// <summary>
    /// Gets or Sets HasUpdatedTime
    /// </summary>
    [DataMember(Name="hasUpdatedTime", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasUpdatedTime")]
    public List<DateTime?> HasUpdatedTime { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      var sb = new StringBuilder();
      sb.Append("class Event {\n");
      sb.Append("  Context: ").Append(Context).Append("\n");
      sb.Append("  Id: ").Append(Id).Append("\n");
      sb.Append("  Type: ").Append(Type).Append("\n");
      sb.Append("  Rdfslabel: ").Append(Rdfslabel).Append("\n");
      sb.Append("  EventMeasurementUnit: ").Append(EventMeasurementUnit).Append("\n");
      sb.Append("  EventQuantityKind: ").Append(EventQuantityKind).Append("\n");
      sb.Append("  HasCreatedTime: ").Append(HasCreatedTime).Append("\n");
      sb.Append("  HasDeletedTime: ").Append(HasDeletedTime).Append("\n");
      sb.Append("  HasDuration: ").Append(HasDuration).Append("\n");
      sb.Append("  HasObservationTime: ").Append(HasObservationTime).Append("\n");
      sb.Append("  HasPointInTime: ").Append(HasPointInTime).Append("\n");
      sb.Append("  HasReadTime: ").Append(HasReadTime).Append("\n");
      sb.Append("  HasStartTime: ").Append(HasStartTime).Append("\n");
      sb.Append("  HasStopTime: ").Append(HasStopTime).Append("\n");
      sb.Append("  HasUpdatedTime: ").Append(HasUpdatedTime).Append("\n");
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
