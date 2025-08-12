using System.Threading.Tasks;
using Avn.Connect.V1;
using ClassVR.AvnCloud;
using Grpc.Core;
using UnityEngine;

namespace ClassVR {
  public static class Analytics {
    private static ClientCredentials clientCredentials;

    public static async Task SendEvent(
      string actionId,
      string sourceId,
      Google.Protobuf.WellKnownTypes.Struct data = null,
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
      Debug.Log($"ADMDBG: sending record action");
      Debug.Log($"ADMDBG: clientId = {clientCredentials.ClientId}");
      Debug.Log($"ADMDBG: ActionId = {actionId}");
      Debug.Log($"ADMDBG: SourceId = {sourceId}");
      Debug.Log($"ADMDBG: HostId = {Application.identifier}");
      Debug.Log($"ADMDBG: DeviceJwt = {auth.DeviceJwt}");

      try {
        // RecordAction doesn't return a response, but an exception will be thrown on failure
        await clientService.RecordActionAsync(request);
      } catch (RpcException ex) {
        Debug.LogError($"Analytics event send failed. Status code: {ex.Status.StatusCode}. Message: {ex.Status.Detail}");
        return;
      }
      Debug.Log($"ADMDBG: send event completed");
    }
  }
}
