using System;
using System.Diagnostics;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;

namespace OAS_Generated_Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var apiInstance = new DefaultApi();

            try
            {
                // Try fetching Device from endpoint
                Device result = apiInstance.DeviceGet();
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling DefaultApi.DeviceGet: " + e.Message);
            }
        }
    }
}
