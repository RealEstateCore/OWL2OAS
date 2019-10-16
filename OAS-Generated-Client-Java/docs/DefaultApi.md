# DefaultApi

All URIs are relative to *http://localhost:8080/*

Method | HTTP request | Description
------------- | ------------- | -------------
[**agentGet**](DefaultApi.md#agentGet) | **GET** /Agent | Get all &#x27;Agent&#x27; objects.
[**buildingComponentGet**](DefaultApi.md#buildingComponentGet) | **GET** /BuildingComponent | Get all &#x27;BuildingComponent&#x27; objects.
[**buildingGet**](DefaultApi.md#buildingGet) | **GET** /Building | Get all &#x27;Building&#x27; objects.
[**deviceGet**](DefaultApi.md#deviceGet) | **GET** /Device | Get all &#x27;Device&#x27; objects.
[**eventGet**](DefaultApi.md#eventGet) | **GET** /Event | Get all &#x27;Event&#x27; objects.
[**geoReferenceOrigoGet**](DefaultApi.md#geoReferenceOrigoGet) | **GET** /GeoReferenceOrigo | Get all &#x27;GeoReferenceOrigo&#x27; objects.
[**geometryGet**](DefaultApi.md#geometryGet) | **GET** /Geometry | Get all &#x27;Geometry&#x27; objects.
[**landGet**](DefaultApi.md#landGet) | **GET** /Land | Get all &#x27;Land&#x27; objects.
[**measurementUnitGet**](DefaultApi.md#measurementUnitGet) | **GET** /MeasurementUnit | Get all &#x27;MeasurementUnit&#x27; objects.
[**quantityKindGet**](DefaultApi.md#quantityKindGet) | **GET** /QuantityKind | Get all &#x27;QuantityKind&#x27; objects.
[**realEstateComponentGet**](DefaultApi.md#realEstateComponentGet) | **GET** /RealEstateComponent | Get all &#x27;RealEstateComponent&#x27; objects.
[**realEstateGet**](DefaultApi.md#realEstateGet) | **GET** /RealEstate | Get all &#x27;RealEstate&#x27; objects.
[**roomGet**](DefaultApi.md#roomGet) | **GET** /Room | Get all &#x27;Room&#x27; objects.

<a name="agentGet"></a>
# **agentGet**
> Agent agentGet()

Get all &#x27;Agent&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Agent result = apiInstance.agentGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#agentGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Agent**](Agent.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="buildingComponentGet"></a>
# **buildingComponentGet**
> BuildingComponent buildingComponentGet()

Get all &#x27;BuildingComponent&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    BuildingComponent result = apiInstance.buildingComponentGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#buildingComponentGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**BuildingComponent**](BuildingComponent.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="buildingGet"></a>
# **buildingGet**
> Building buildingGet()

Get all &#x27;Building&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Building result = apiInstance.buildingGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#buildingGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Building**](Building.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="deviceGet"></a>
# **deviceGet**
> Device deviceGet()

Get all &#x27;Device&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Device result = apiInstance.deviceGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#deviceGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Device**](Device.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="eventGet"></a>
# **eventGet**
> Event eventGet()

Get all &#x27;Event&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Event result = apiInstance.eventGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#eventGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Event**](Event.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="geoReferenceOrigoGet"></a>
# **geoReferenceOrigoGet**
> GeoReferenceOrigo geoReferenceOrigoGet()

Get all &#x27;GeoReferenceOrigo&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    GeoReferenceOrigo result = apiInstance.geoReferenceOrigoGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#geoReferenceOrigoGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**GeoReferenceOrigo**](GeoReferenceOrigo.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="geometryGet"></a>
# **geometryGet**
> Geometry geometryGet()

Get all &#x27;Geometry&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Geometry result = apiInstance.geometryGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#geometryGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Geometry**](Geometry.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="landGet"></a>
# **landGet**
> Land landGet()

Get all &#x27;Land&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Land result = apiInstance.landGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#landGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Land**](Land.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="measurementUnitGet"></a>
# **measurementUnitGet**
> MeasurementUnit measurementUnitGet()

Get all &#x27;MeasurementUnit&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    MeasurementUnit result = apiInstance.measurementUnitGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#measurementUnitGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**MeasurementUnit**](MeasurementUnit.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="quantityKindGet"></a>
# **quantityKindGet**
> QuantityKind quantityKindGet()

Get all &#x27;QuantityKind&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    QuantityKind result = apiInstance.quantityKindGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#quantityKindGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**QuantityKind**](QuantityKind.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="realEstateComponentGet"></a>
# **realEstateComponentGet**
> RealEstateComponent realEstateComponentGet()

Get all &#x27;RealEstateComponent&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    RealEstateComponent result = apiInstance.realEstateComponentGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#realEstateComponentGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**RealEstateComponent**](RealEstateComponent.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="realEstateGet"></a>
# **realEstateGet**
> RealEstate realEstateGet()

Get all &#x27;RealEstate&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    RealEstate result = apiInstance.realEstateGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#realEstateGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**RealEstate**](RealEstate.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

<a name="roomGet"></a>
# **roomGet**
> Room roomGet()

Get all &#x27;Room&#x27; objects.

### Example
```java
// Import classes:
//import io.swagger.client.ApiException;
//import io.swagger.client.api.DefaultApi;


DefaultApi apiInstance = new DefaultApi();
try {
    Room result = apiInstance.roomGet();
    System.out.println(result);
} catch (ApiException e) {
    System.err.println("Exception when calling DefaultApi#roomGet");
    e.printStackTrace();
}
```

### Parameters
This endpoint does not need any parameter.

### Return type

[**Room**](Room.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: Not defined
 - **Accept**: application/jsonld

