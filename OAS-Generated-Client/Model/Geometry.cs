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
  public class Geometry {
    /// <summary>
    /// Gets or Sets Context
    /// </summary>
    [DataMember(Name="@context", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "@context")]
    public GeometryContext Context { get; set; }

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
    /// Gets or Sets AsGML
    /// </summary>
    [DataMember(Name="asGML", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "asGML")]
    public List<string> AsGML { get; set; }

    /// <summary>
    /// Gets or Sets AsWKT
    /// </summary>
    [DataMember(Name="asWKT", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "asWKT")]
    public List<string> AsWKT { get; set; }

    /// <summary>
    /// Gets or Sets HasSerialization
    /// </summary>
    [DataMember(Name="hasSerialization", EmitDefaultValue=false)]
    [JsonProperty(PropertyName = "hasSerialization")]
    public List<string> HasSerialization { get; set; }


    /// <summary>
    /// Get the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()  {
      var sb = new StringBuilder();
      sb.Append("class Geometry {\n");
      sb.Append("  Context: ").Append(Context).Append("\n");
      sb.Append("  Id: ").Append(Id).Append("\n");
      sb.Append("  Type: ").Append(Type).Append("\n");
      sb.Append("  Rdfslabel: ").Append(Rdfslabel).Append("\n");
      sb.Append("  AsGML: ").Append(AsGML).Append("\n");
      sb.Append("  AsWKT: ").Append(AsWKT).Append("\n");
      sb.Append("  HasSerialization: ").Append(HasSerialization).Append("\n");
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
