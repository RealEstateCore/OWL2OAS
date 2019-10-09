using System;
using System.Collections.Generic;
using RestSharp;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace IO.Swagger.Api
{
    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public interface IDefaultApi
    {
        /// <summary>
        /// Get all &#x27;Agent&#x27; objects. 
        /// </summary>
        /// <returns>Agent</returns>
        Agent AgentGet ();
        /// <summary>
        /// Get all &#x27;BuildingComponent&#x27; objects. 
        /// </summary>
        /// <returns>BuildingComponent</returns>
        BuildingComponent BuildingComponentGet ();
        /// <summary>
        /// Get all &#x27;Building&#x27; objects. 
        /// </summary>
        /// <returns>Building</returns>
        Building BuildingGet ();
        /// <summary>
        /// Get all &#x27;Device&#x27; objects. 
        /// </summary>
        /// <returns>Device</returns>
        Device DeviceGet ();
        /// <summary>
        /// Get all &#x27;Event&#x27; objects. 
        /// </summary>
        /// <returns>Event</returns>
        Event EventGet ();
        /// <summary>
        /// Get all &#x27;GeoReferenceOrigo&#x27; objects. 
        /// </summary>
        /// <returns>GeoReferenceOrigo</returns>
        GeoReferenceOrigo GeoReferenceOrigoGet ();
        /// <summary>
        /// Get all &#x27;Geometry&#x27; objects. 
        /// </summary>
        /// <returns>Geometry</returns>
        Geometry GeometryGet ();
        /// <summary>
        /// Get all &#x27;Land&#x27; objects. 
        /// </summary>
        /// <returns>Land</returns>
        Land LandGet ();
        /// <summary>
        /// Get all &#x27;MeasurementUnit&#x27; objects. 
        /// </summary>
        /// <returns>MeasurementUnit</returns>
        MeasurementUnit MeasurementUnitGet ();
        /// <summary>
        /// Get all &#x27;QuantityKind&#x27; objects. 
        /// </summary>
        /// <returns>QuantityKind</returns>
        QuantityKind QuantityKindGet ();
        /// <summary>
        /// Get all &#x27;RealEstateComponent&#x27; objects. 
        /// </summary>
        /// <returns>RealEstateComponent</returns>
        RealEstateComponent RealEstateComponentGet ();
        /// <summary>
        /// Get all &#x27;RealEstate&#x27; objects. 
        /// </summary>
        /// <returns>RealEstate</returns>
        RealEstate RealEstateGet ();
        /// <summary>
        /// Get all &#x27;Room&#x27; objects. 
        /// </summary>
        /// <returns>Room</returns>
        Room RoomGet ();
    }
  
    /// <summary>
    /// Represents a collection of functions to interact with the API endpoints
    /// </summary>
    public class DefaultApi : IDefaultApi
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultApi"/> class.
        /// </summary>
        /// <param name="apiClient"> an instance of ApiClient (optional)</param>
        /// <returns></returns>
        public DefaultApi(ApiClient apiClient = null)
        {
            if (apiClient == null) // use the default one in Configuration
                this.ApiClient = Configuration.DefaultApiClient; 
            else
                this.ApiClient = apiClient;
        }
    
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultApi"/> class.
        /// </summary>
        /// <returns></returns>
        public DefaultApi(String basePath)
        {
            this.ApiClient = new ApiClient(basePath);
        }
    
        /// <summary>
        /// Sets the base path of the API client.
        /// </summary>
        /// <param name="basePath">The base path</param>
        /// <value>The base path</value>
        public void SetBasePath(String basePath)
        {
            this.ApiClient.BasePath = basePath;
        }
    
        /// <summary>
        /// Gets the base path of the API client.
        /// </summary>
        /// <param name="basePath">The base path</param>
        /// <value>The base path</value>
        public String GetBasePath(String basePath)
        {
            return this.ApiClient.BasePath;
        }
    
        /// <summary>
        /// Gets or sets the API client.
        /// </summary>
        /// <value>An instance of the ApiClient</value>
        public ApiClient ApiClient {get; set;}
    
        /// <summary>
        /// Get all &#x27;Agent&#x27; objects. 
        /// </summary>
        /// <returns>Agent</returns>
        public Agent AgentGet ()
        {
    
            var path = "/Agent";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling AgentGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling AgentGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Agent) ApiClient.Deserialize(response.Content, typeof(Agent), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;BuildingComponent&#x27; objects. 
        /// </summary>
        /// <returns>BuildingComponent</returns>
        public BuildingComponent BuildingComponentGet ()
        {
    
            var path = "/BuildingComponent";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling BuildingComponentGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling BuildingComponentGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (BuildingComponent) ApiClient.Deserialize(response.Content, typeof(BuildingComponent), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Building&#x27; objects. 
        /// </summary>
        /// <returns>Building</returns>
        public Building BuildingGet ()
        {
    
            var path = "/Building";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling BuildingGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling BuildingGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Building) ApiClient.Deserialize(response.Content, typeof(Building), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Device&#x27; objects. 
        /// </summary>
        /// <returns>Device</returns>
        public Device DeviceGet ()
        {

            //var path = "/Device";
            string path = "https://localhost:44322/api/O2OEval";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling DeviceGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling DeviceGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Device) ApiClient.Deserialize(response.Content, typeof(Device), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Event&#x27; objects. 
        /// </summary>
        /// <returns>Event</returns>
        public Event EventGet ()
        {
    
            var path = "/Event";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling EventGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling EventGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Event) ApiClient.Deserialize(response.Content, typeof(Event), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;GeoReferenceOrigo&#x27; objects. 
        /// </summary>
        /// <returns>GeoReferenceOrigo</returns>
        public GeoReferenceOrigo GeoReferenceOrigoGet ()
        {
    
            var path = "/GeoReferenceOrigo";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling GeoReferenceOrigoGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling GeoReferenceOrigoGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (GeoReferenceOrigo) ApiClient.Deserialize(response.Content, typeof(GeoReferenceOrigo), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Geometry&#x27; objects. 
        /// </summary>
        /// <returns>Geometry</returns>
        public Geometry GeometryGet ()
        {
    
            var path = "/Geometry";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling GeometryGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling GeometryGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Geometry) ApiClient.Deserialize(response.Content, typeof(Geometry), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Land&#x27; objects. 
        /// </summary>
        /// <returns>Land</returns>
        public Land LandGet ()
        {
    
            var path = "/Land";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling LandGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling LandGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Land) ApiClient.Deserialize(response.Content, typeof(Land), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;MeasurementUnit&#x27; objects. 
        /// </summary>
        /// <returns>MeasurementUnit</returns>
        public MeasurementUnit MeasurementUnitGet ()
        {
    
            var path = "/MeasurementUnit";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling MeasurementUnitGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling MeasurementUnitGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (MeasurementUnit) ApiClient.Deserialize(response.Content, typeof(MeasurementUnit), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;QuantityKind&#x27; objects. 
        /// </summary>
        /// <returns>QuantityKind</returns>
        public QuantityKind QuantityKindGet ()
        {
    
            var path = "/QuantityKind";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling QuantityKindGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling QuantityKindGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (QuantityKind) ApiClient.Deserialize(response.Content, typeof(QuantityKind), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;RealEstateComponent&#x27; objects. 
        /// </summary>
        /// <returns>RealEstateComponent</returns>
        public RealEstateComponent RealEstateComponentGet ()
        {
    
            var path = "/RealEstateComponent";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling RealEstateComponentGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling RealEstateComponentGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (RealEstateComponent) ApiClient.Deserialize(response.Content, typeof(RealEstateComponent), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;RealEstate&#x27; objects. 
        /// </summary>
        /// <returns>RealEstate</returns>
        public RealEstate RealEstateGet ()
        {
    
            var path = "/RealEstate";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling RealEstateGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling RealEstateGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (RealEstate) ApiClient.Deserialize(response.Content, typeof(RealEstate), response.Headers);
        }
    
        /// <summary>
        /// Get all &#x27;Room&#x27; objects. 
        /// </summary>
        /// <returns>Room</returns>
        public Room RoomGet ()
        {
    
            var path = "/Room";
            path = path.Replace("{format}", "json");
                
            var queryParams = new Dictionary<String, String>();
            var headerParams = new Dictionary<String, String>();
            var formParams = new Dictionary<String, String>();
            var fileParams = new Dictionary<String, FileParameter>();
            String postBody = null;
    
                                                    
            // authentication setting, if any
            String[] authSettings = new String[] {  };
    
            // make the HTTP request
            IRestResponse response = (IRestResponse) ApiClient.CallApi(path, Method.GET, queryParams, postBody, headerParams, formParams, fileParams, authSettings);
    
            if (((int)response.StatusCode) >= 400)
                throw new ApiException ((int)response.StatusCode, "Error calling RoomGet: " + response.Content, response.Content);
            else if (((int)response.StatusCode) == 0)
                throw new ApiException ((int)response.StatusCode, "Error calling RoomGet: " + response.ErrorMessage, response.ErrorMessage);
    
            return (Room) ApiClient.Deserialize(response.Content, typeof(Room), response.Headers);
        }
    
    }
}
