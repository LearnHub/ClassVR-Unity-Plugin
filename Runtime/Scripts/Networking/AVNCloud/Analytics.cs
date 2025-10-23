using System.Collections.Generic;
using System.Threading.Tasks;
using Avn.Connect.V1;
using ClassVR.AvnCloud;
using Grpc.Core;
using UnityEngine;

using WellKnownTypes = Google.Protobuf.WellKnownTypes;

namespace ClassVR {
  public static class Analytics {
    private static ClientCredentials clientCredentials;

    /// <summary>
    /// Sends an analytics event to AVN Cloud
    /// </summary>
    /// <param name="sourceId">Name of the source of the action. Should be in snake_case.</param>
    /// <param name="actionId">Name of the action taken. Should be in snake_case.</param>
    /// <param name="data">An optional collection of key-value pairs. Size must not exceed 2048.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <returns>A Task that can be awaited. To avoid awaiting use syntax <c>_ = SendEvent(...)</c></returns>
    public static async Task SendEvent(
      string sourceId,
      string actionId,
      Dictionary<string, string> data = null,
      EndpointServer endpointServer = EndpointServer.Production) {
      // Convert dictionary to WellKnownTypes.Struct
      var dataStruct = new WellKnownTypes.Struct();
      foreach (KeyValuePair<string, string> entry in data) {
        dataStruct.Fields.Add(entry.Key, WellKnownTypes.Value.ForString(entry.Value));
      }

      await SendEvent(actionId, sourceId, dataStruct, endpointServer);
    }

    /// <summary>
    /// Sends an analytics event to AVN Cloud. Use this overload if data contains values that aren't strings or numbers.
    /// </summary>
    /// <param name="sourceId">Name of the source of the action. Should be in snake_case.</param>
    /// <param name="actionId">Name of the action taken. Should be in snake_case.</param>
    /// <param name="data">An optional collection of key-value pairs. Size must not exceed 2048.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <returns>A Task that can be awaited. To avoid awaiting use syntax <c>_ = SendEvent(...)</c></returns>
    public static async Task SendEvent(
      string sourceId,
      string actionId,
      WellKnownTypes.Struct data = null,
      EndpointServer endpointServer = EndpointServer.Production) {
      // Construct a client - these are cheap and don't need to be cached
      var clientService = new ClientService.ClientServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));

      // Request and cache client credentials if not present
      if (clientCredentials == null) {
        try {
          var clientCredentialsResponse = await clientService.CreateClientCredentialsAsync(new CreateClientCredentialsRequest());
          clientCredentials = clientCredentialsResponse.ClientCredentials;
        } catch (RpcException ex) {
          Debug.LogError($"Analytics event send failed. Status code: {ex.Status.StatusCode}. Message: {ex.Status.Detail}");
          return;
        }
      }

      // Use the device JWT for auth
      var auth = new Authorization { DeviceJwt = CVRProperties.Instance.DeviceJWT };

      // Data when serialized cannot exceed 2048 chars
      if (data != null && data.CalculateSize() > 2048) {
        Debug.LogError($"Analytics event send failed. The data associated with analytics event action '{actionId}' and source '{sourceId}' cannot be sent as it exceeds 2048 chars.");
        return;
      }

      var request = new RecordActionRequest() {
        Client = clientCredentials,
        ActionId = actionId,
        SourceId = sourceId,
        HostId = Application.identifier,  // Package name
        Auth = auth,
        Data = data
      };

      try {
        // RecordAction doesn't return a response, but an exception will be thrown on failure
        await clientService.RecordActionAsync(request);
      } catch (RpcException ex) {
        Debug.LogError($"Analytics event send failed. Status code: {ex.Status.StatusCode}. Message: {ex.Status.Detail}");
        return;
      }
      Debug.Log($"Event '{sourceId}' - '{actionId}' sent");
    }
  }
}
