using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avn.Connect.V1;
using UnityEngine;
using UnityEngine.Networking;
using Authorization = Avn.Connect.V1.Authorization;

namespace ClassVR.AvnCloud {
  public static class FileUploader {
    // Maximum size in bytes that AVNFS will accept (5GB)
    private const long MaxSinglePartUploadSizeBytes = 5368709120;

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a string. This will be encoded using UTF8.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production) {
      byte[] byteData = Encoding.UTF8.GetBytes(data);
      return await UploadToSharedCloud(filename, mediaType, byteData, endpointServer);
    }

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production) {
      //TODO: enable streaming uploads
      //TODO: enable cancellation

      // No need to check file doesn't exceed upload size limit, as max array length is 2GB and upload limit is 5GB

      // Use the device JWT for authentication
      var auth = new Authorization { DeviceJwt = CVRProperties.Instance.DeviceJWT };
      if (string.IsNullOrEmpty(auth.DeviceJwt)) {
        Debug.LogError($"Couldn't retreive device JWT for authorization. Upload of '{filename}' to ClassVR failed.");
        return null;
      }

      // Upload the file to AVNFS and get the URL to download the file
      var downloadUrl = await UploadToAvnfs(filename, mediaType, data, auth, endpointServer);
      // Check upload was successful
      if (downloadUrl == null) {
        return null;
      }

      // Assign the file to the Shared Cloud area of the organization the device is currently registered to
      var addFileSuccess = await AddFileToSharedCloud(downloadUrl, auth, endpointServer);

      // Return the URL if successful, null otherwise
      return addFileSuccess ? downloadUrl : null;
    }

    // Uploads the provided file to AVNFS and provides the URL it can be downloaded from
    private static async Task<string> UploadToAvnfs(
        string filename,
        string mediaType,
        byte[] data,
        Authorization auth,
        EndpointServer endpointServer) {

      Debug.Log($"Uploading '{filename}' to ClassVR");

      // Check whether the file has already been uploaded
      var base64UrlHash = Base64UrlHash(data);
      var existingUrl = await CheckFileAlreadyUploaded(filename, mediaType, base64UrlHash, data.Length, endpointServer);

      // Return the existing URL if file already exists in AVNFS
      if (!string.IsNullOrEmpty(existingUrl)) {
        Debug.Log($"'{filename}' has already been uploaded to ClassVR at {existingUrl}");
        return existingUrl;
      }

      // Files are uploaded via HTTP POST (not a gRPC call), so request the necessary data to make the HTTP request
      var uploadManifest = await GetUploadManifest(filename, mediaType, base64UrlHash, data.Length, auth, endpointServer);
      // Check we got the upload manifest successfully
      if (uploadManifest == null) {
        Debug.LogError($"Failed to retrieve upload manifest for '{filename}'. Aborting upload.");
        return null;
      }

      // Construct the UnityWebRequest and send it
      var webRequest = ConstructFileUploadWebRequest(uploadManifest, data);
      if (webRequest == null) {
        Debug.LogError($"Failed to construct upload request for '{filename}'. Aborting upload.");
        return null;
      }
      await webRequest.SendWebRequest();

      if (webRequest.result != UnityWebRequest.Result.Success) {
        Debug.LogError($"Upload of '{filename}' failed with error '{webRequest.error}'");
        return null;
      }

      Debug.Log($"'{filename}' uploaded to AVNFS successfully");
      return uploadManifest.DownloadUrl;
    }

    // Assigns the file with the specified URL to the Shared Cloud of the ClassVR Organization that the device is registered to
    // Returns true if successful, false otherwise
    private static async Task<bool> AddFileToSharedCloud(
        string downloadUrl,
        Authorization auth,
        EndpointServer endpointServer) {

      var orgId = CVRProperties.Instance.OrganizationInfo.Id;
      Debug.Log($"Assigning '{downloadUrl}' to Shared Cloud of Organization with ID '{orgId}'");

      // Construct a request using the organization ID the device is currently registered to
      var addFilesRequest = new AddCloudFilesRequest {
        Auth = auth,
        OrganizationId = orgId,
        FileUrls = { downloadUrl }
      };

      // Make the request
      var avnCloud = new CloudService.CloudServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));
      var cloudFilesResult = await avnCloud.AddCloudFilesAsync(addFilesRequest);

      // Check the response for EntityIds
      if (cloudFilesResult.EntityIds.Count < 1) {
        Debug.LogError($"Failed to assign '{downloadUrl}' to Shared Cloud of Organization with ID '{orgId}'");
        return false;
      }

      Debug.Log($"'{downloadUrl}' successfully added to Shared Cloud of Organization with ID '{orgId}'");
      return true;
    }

    // Checks if the file has been uploaded to AVNFS and provides its URL if so, otherwise null
    private static async Task<string> CheckFileAlreadyUploaded(
        string filename,
        string mediaType,
        string base64UrlHash,
        long sizeBytes,
        EndpointServer endpointServer) {

      // Construct a request to get the URL for file with specified hash
      var fileSignature = new GetFileUrlRequest {
        Hash = base64UrlHash,
        SizeBytes = sizeBytes,
        MediaType = mediaType,
        FileName = filename
      };

      // Send request
      var avnfs = new AvnfsService.AvnfsServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));
      var getFileResponse = await avnfs.GetFileUrlAsync(fileSignature);

      // If the request return a URL then the file has already been uploaded
      if (getFileResponse.HasUrl) {
        return getFileResponse.Url;
      }

      return null;
    }

    // Gets the manifest required to upload a file to AVNFS
    private static async Task<UploadManifest> GetUploadManifest(
        string filename,
        string mediaType,
        string base64UrlHash,
        long sizeBytes,
        Authorization auth,
        EndpointServer endpointServer) {

      // Request the data required to upload a file to AVNFS
      var avnfs = new AvnfsService.AvnfsServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));
      var manifestRequest = new GetManifestRequest {
        Auth = auth,
        FileName = filename,
        Hash = base64UrlHash,
        MediaType = mediaType,
        SizeBytes = sizeBytes
      };
      return await avnfs.GetPostManifestAsync(manifestRequest);
    }

    // Constructs a UnityWebRequest to upload the provided file to AVNFS
    private static UnityWebRequest ConstructFileUploadWebRequest(UploadManifest uploadManifest, byte[] data) {
      try {
        // Build a UnityWebRequest to POST the file
        var form = new WWWForm();
        foreach (var field in uploadManifest.HeaderFields) {
          form.AddField(field.Name, field.Value);
        }
        form.AddBinaryData("file", data);
        return UnityWebRequest.Post(uploadManifest.UploadUrl, form);
      } catch (Exception ex) {
        Debug.LogException(ex);
        return null;
      }
    }

    // Hashes specified byte array using SHA265 then converts to Base64URL
    private static string Base64UrlHash(byte[] data) {
      // Hash the array using SHA256
      byte[] hash;
      using (var sha256 = SHA256.Create()) {
        hash = sha256.ComputeHash(data);
      }

      // Encode hash as Base64URL
      return ToBase64Url(hash);
    }

    // Converts the given byte array to Base64URL - https://base64.guru/standards/base64url
    static string ToBase64Url(byte[] bytes) {
      return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
  }
}
