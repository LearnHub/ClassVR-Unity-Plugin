using System;
using System.IO;
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
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      byte[] byteData = Encoding.UTF8.GetBytes(data);
      return await UploadToSharedCloud(filename, mediaType, byteData, endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a file already on disk to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
    /// The display name is derived from the file path via <see cref="Path.GetFileName"/>.
    /// Note: the maximum file size for upload is 5GB.
    /// </summary>
    /// <param name="filePath">Local file path (not a URI — the method handles file:// prefixing).</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToSharedCloud(string filePath, string mediaType, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      // Validate the file path
      if (string.IsNullOrEmpty(filePath)) {
        Debug.LogError("FileUploader: filePath is null or empty.");
        return null;
      }
      if (!File.Exists(filePath)) {
        Debug.LogError($"FileUploader: file not found at '{filePath}'.");
        return null;
      }

      // Check file size doesn't exceed upload limit
      var fileInfo = new FileInfo(filePath);
      if (fileInfo.Length > MaxSinglePartUploadSizeBytes) {
        Debug.LogError($"FileUploader: file at '{filePath}' exceeds maximum upload size of {MaxSinglePartUploadSizeBytes} bytes (5GB). File size: {fileInfo.Length} bytes.");
        return null;
      }

      // Derive the display name from the file path (e.g. "/path/to/photo.png" -> "photo.png")
      var filename = Path.GetFileName(filePath);

      // Get authorization for upload
      var auth = GetAuthorizationForUpload(jwt, filename);
      if (auth == null) {
        return null;
      }

      // Upload the file to AVNFS and get the URL to download the file
      var downloadUrl = await UploadFileToAvnfs(filePath, filename, mediaType, auth, endpointServer);
      if (downloadUrl == null) {
        return null;
      }

      // Assign the file to the Shared Cloud area of the organization the device is currently registered to
      var addFileSuccess = await AddFileToSharedCloud(downloadUrl, auth, endpointServer);
      return addFileSuccess ? downloadUrl : null;
    }

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      //TODO: enable cancellation

      // No need to check file doesn't exceed upload size limit, as max array length is 2GB and upload limit is 5GB

      // Get authorization for upload
      var auth = GetAuthorizationForUpload(jwt, filename);
      if (auth == null) {
        return null;
      }

      // Upload the file to AVNFS and get the URL to download the file
      var downloadUrl = await UploadBytesToAvnfs(filename, mediaType, data, auth, endpointServer);
      // Check upload was successful
      if (downloadUrl == null) {
        return null;
      }

      // Assign the file to the Shared Cloud area of the organization the device is currently registered to
      var addFileSuccess = await AddFileToSharedCloud(downloadUrl, auth, endpointServer);

      // Return the URL if successful, null otherwise
      return addFileSuccess ? downloadUrl : null;
    }

    // Gets authorization for upload, using provided JWT or falling back to device JWT
    // Returns null if no valid JWT is available (error is logged)
    private static Authorization GetAuthorizationForUpload(string jwt, string filename) {
      var jwtToUse = jwt ?? CVRProperties.Instance.DeviceJWT;
      var auth = new Authorization { DeviceJwt = jwtToUse };
      if (string.IsNullOrEmpty(auth.DeviceJwt)) {
        Debug.LogError($"Couldn't retrieve device JWT for authorization. Upload of '{filename}' to ClassVR failed.");
        return null;
      }
      return auth;
    }

    // Uploads the provided file to AVNFS and provides the URL it can be downloaded from
    private static async Task<string> UploadBytesToAvnfs(
        string filename,
        string mediaType,
        byte[] data,
        Authorization auth,
        EndpointServer endpointServer) {

      Debug.Log($"Uploading '{filename}' to ClassVR");

#if !UNITY_EDITOR && UNITY_ANDROID
      // On Android, write data to a temp file and upload via the AVNFS ContentProvider
      var tempPath = Path.Combine(Application.temporaryCachePath, filename);
      try {
        File.WriteAllBytes(tempPath, data);
        return await UploadFileToAvnfs(tempPath, filename, mediaType, auth, endpointServer);
      } finally {
        try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
      }
#else
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
      using (var webRequest = ConstructFileUploadWebRequest(uploadManifest, data)) {
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
#endif
    }

    // Uploads a file from a local path to AVNFS via the on-device ContentProvider (Android) or via web request (other platforms)
    private static async Task<string> UploadFileToAvnfs(
        string filePath,
        string filename,
        string mediaType,
        Authorization auth,
        EndpointServer endpointServer) {

      Debug.Log($"Uploading '{filename}' to ClassVR from '{filePath}'");

#if !UNITY_EDITOR && UNITY_ANDROID
      var result = await AvnfsUploader.UploadAsync("file://" + filePath, mediaType, auth.DeviceJwt, filename);
      if (!result.Success) {
        Debug.LogError($"Upload of '{filename}' to AVNFS failed: {result.Error}");
        return null;
      }
      Debug.Log($"'{filename}' uploaded to AVNFS successfully");
      return result.AvnfsUrl;
#else
      try {
        var data = File.ReadAllBytes(filePath);
        return await UploadBytesToAvnfs(filename, mediaType, data, auth, endpointServer);
      } catch (Exception ex) {
        Debug.LogError($"Failed to read file at '{filePath}': {ex.Message}");
        return null;
      }
#endif
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

    // Hashes specified byte array using SHA256 then converts to Base64URL
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
    private static string ToBase64Url(byte[] bytes) {
      return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
  }
}
