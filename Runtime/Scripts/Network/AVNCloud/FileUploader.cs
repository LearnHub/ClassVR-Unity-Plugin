using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avn.Connect.V1;
using ClassVR.Platform.Android;
using UnityEngine;
using UnityEngine.Networking;
using Authorization = Avn.Connect.V1.Authorization;

namespace ClassVR.Network.AvnCloud {
  // Sends the Shared Cloud association request. The production implementation calls the gRPC client; tests
  // substitute a lambda so the entity ID extraction can be exercised without a network, a gRPC channel, or
  // the device-only CVRProperties singleton.
  internal delegate Task<AddCloudFilesResponse> AddCloudFilesFetch(AddCloudFilesRequest request);

  public static class FileUploader {
    // Maximum size in bytes that AVNFS will accept (5GB)
    private const long MaxSinglePartUploadSizeBytes = 5368709120;

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a string. This will be encoded using UTF8.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    [Obsolete("Use CloudFiles.Upload instead.")]
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      byte[] byteData = Encoding.UTF8.GetBytes(data);
      return (await UploadBytesToSharedCloud(filename, mediaType, byteData, endpointServer, jwt))?.FileUrl;
    }

    /// <summary>
    /// Uploads a file already on disk to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// The display name is derived from the file path via <see cref="Path.GetFileName"/>.
    /// Note: the maximum file size for upload is 5GB.
    /// </summary>
    /// <param name="filePath">Local file path (not a URI — the method handles file:// prefixing).</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    [Obsolete("Use CloudFiles.Upload instead.")]
    public static async Task<string> UploadToSharedCloud(string filePath, string mediaType, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return (await UploadFileToSharedCloud(filePath, mediaType, endpointServer, jwt))?.FileUrl;
    }

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    [Obsolete("Use CloudFiles.Upload instead.")]
    public static async Task<string> UploadToSharedCloud(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return (await UploadBytesToSharedCloud(filename, mediaType, data, endpointServer, jwt))?.FileUrl;
    }

    /// <summary>
    /// Uploads a file to AVNFS (the ClassVR file store) and returns its URL, WITHOUT associating it
    /// with any Organization. Use <see cref="CloudFiles.Upload(string, string, string, EndpointServer, string)"/>
    /// instead if you want the file to appear in the device Organization's Shared Cloud library.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a string. This will be encoded using UTF8.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToAvnfs(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      byte[] byteData = Encoding.UTF8.GetBytes(data);
      return await UploadToAvnfs(filename, mediaType, byteData, endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a file already on disk to AVNFS (the ClassVR file store) and returns its URL, WITHOUT
    /// associating it with any Organization. Use <see cref="CloudFiles.Upload(string, string, EndpointServer, string)"/>
    /// instead if you want the file to appear in the device Organization's Shared Cloud library.
    /// The display name is derived from the file path via <see cref="Path.GetFileName"/>.
    /// Note: the maximum file size for upload is 5GB.
    /// </summary>
    /// <param name="filePath">Local file path (not a URI — the method handles file:// prefixing).</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToAvnfs(string filePath, string mediaType, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
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
      return await UploadFileToAvnfs(filePath, filename, mediaType, auth, endpointServer);
    }

    /// <summary>
    /// Uploads a file to AVNFS (the ClassVR file store) and returns its URL, WITHOUT associating it
    /// with any Organization. Use <see cref="CloudFiles.Upload(string, string, byte[], EndpointServer, string)"/>
    /// instead if you want the file to appear in the device Organization's Shared Cloud library.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL where the file can be accessed. If the upload was unsuccessful, returns null.</returns>
    public static async Task<string> UploadToAvnfs(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      //TODO: enable cancellation

      // No need to check file doesn't exceed upload size limit, as max array length is 2GB and upload limit is 5GB

      // Get authorization for upload
      var auth = GetAuthorizationForUpload(jwt, filename);
      if (auth == null) {
        return null;
      }

      // Upload the file to AVNFS and get the URL to download the file
      return await UploadBytesToAvnfs(filename, mediaType, data, auth, endpointServer);
    }

    // Uploads a byte array to AVNFS and assigns it to the Shared Cloud of the Organization the device is
    // registered to. Returns everything known about the new cloud file, or null if any stage failed (the
    // failure is logged by whichever stage hit it).
    internal static async Task<CloudUploadResult> UploadBytesToSharedCloud(
        string filename,
        string mediaType,
        byte[] data,
        EndpointServer endpointServer,
        string jwt) {
      //TODO: enable cancellation

      // Upload the file to AVNFS and get the URL to download the file
      var downloadUrl = await UploadToAvnfs(filename, mediaType, data, endpointServer, jwt);
      // Check upload was successful
      if (downloadUrl == null) {
        return null;
      }

      // Assign the file to the Shared Cloud area of the organization the device is currently registered to
      var entityId = await AssociateWithOrg(downloadUrl, endpointServer, jwt, filename);
      if (!entityId.HasValue) {
        return null;
      }

      return new CloudUploadResult(entityId.Value, downloadUrl, filename, mediaType, data.Length);
    }

    // Uploads a file already on disk to AVNFS and assigns it to the Shared Cloud of the Organization the
    // device is registered to. Returns everything known about the new cloud file, or null if any stage
    // failed (the failure is logged by whichever stage hit it).
    internal static async Task<CloudUploadResult> UploadFileToSharedCloud(
        string filePath,
        string mediaType,
        EndpointServer endpointServer,
        string jwt) {

      // Read the size up front. UploadToAvnfs validates the path and logs the canonical error, and measuring
      // afterwards would let a file that vanished mid-upload turn a successful upload into a failure.
      var sizeBytes = TryGetFileLength(filePath);

      // Upload the file to AVNFS and get the URL to download the file
      var downloadUrl = await UploadToAvnfs(filePath, mediaType, endpointServer, jwt);
      if (downloadUrl == null) {
        return null;
      }

      // Assign the file to the Shared Cloud area of the organization the device is currently registered to.
      // The display name is derived from the file path (e.g. "/path/to/photo.png" -> "photo.png").
      var filename = Path.GetFileName(filePath);
      var entityId = await AssociateWithOrg(downloadUrl, endpointServer, jwt, filename);
      if (!entityId.HasValue) {
        return null;
      }

      return new CloudUploadResult(entityId.Value, downloadUrl, filename, mediaType, sizeBytes);
    }

    // The file's length, or null if it can't be determined. Deliberately silent.
    private static long? TryGetFileLength(string filePath) {
      try {
        return new FileInfo(filePath).Length;
      } catch {
        return null;
      }
    }

    // Associates an already-uploaded AVNFS file with the Shared Cloud of the Organization the device is
    // registered to. Returns the cloud file's entity ID on success, null otherwise.
    private static async Task<int?> AssociateWithOrg(string downloadUrl, EndpointServer endpointServer, string jwt, string filename) {
      var auth = GetAuthorizationForUpload(jwt, filename);
      if (auth == null) {
        return null;
      }

      return await AddFileToSharedCloud(downloadUrl, auth, endpointServer);
    }

    // Gets authorization for upload, using provided JWT or falling back to device JWT
    // Returns null if no valid JWT is available (error is logged)
    private static Authorization GetAuthorizationForUpload(string jwt, string filename) {
      var jwtToUse = jwt ?? CVRProperties.Instance.DeviceJWT;
      if (string.IsNullOrEmpty(jwtToUse)) {
        Debug.LogError($"Couldn't retrieve device JWT for authorization. Upload of '{filename}' to ClassVR failed.");
        return null;
      }
      return new Authorization { DeviceJwt = jwtToUse };
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
      // The provider drops the supplied name when the content already exists, so re-apply it
      return EnsureFileNameParameter(result.AvnfsUrl, filename);
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

    // Assigns the file with the specified URL to the Shared Cloud of the ClassVR Organization that the device is
    // registered to. Resolves the organization from the device, then defers to the overload below.
    // Returns the cloud file's entity ID if successful, null otherwise.
    private static async Task<int?> AddFileToSharedCloud(
        string downloadUrl,
        Authorization auth,
        EndpointServer endpointServer) {

      // OrganizationInfo is only populated on an enrolled Android device; off-device (e.g. in the Editor) it is
      // null, so guard against it rather than letting a NullReferenceException surface.
      var organizationInfo = CVRProperties.Instance.OrganizationInfo;
      if (organizationInfo == null) {
        Debug.LogError($"Couldn't retrieve Organization info. Failed to assign '{downloadUrl}' to Shared Cloud.");
        return null;
      }

      var avnCloud = new CloudService.CloudServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));
      return await AddFileToSharedCloud(downloadUrl, auth, organizationInfo.Id,
          request => avnCloud.AddCloudFilesAsync(request).ResponseAsync);
    }

    // Builds and sends the association request, and pulls the entity ID out of the response. The gRPC call sits
    // behind the AddCloudFilesFetch seam and the organization is passed in, so this logic is unit-testable
    // without a network or an enrolled device.
    // Returns the cloud file's entity ID if successful, null otherwise.
    internal static async Task<int?> AddFileToSharedCloud(
        string downloadUrl,
        Authorization auth,
        int organizationId,
        AddCloudFilesFetch fetch) {

      Debug.Log($"Assigning '{downloadUrl}' to Shared Cloud of Organization with ID '{organizationId}'");

      // Construct a request using the organization ID the device is currently registered to
      var addFilesRequest = new AddCloudFilesRequest {
        Auth = auth,
        OrganizationId = organizationId,
        FileUrls = { downloadUrl }
      };

      // Make the request
      var cloudFilesResult = await fetch(addFilesRequest);

      // Check the response for EntityIds
      if (cloudFilesResult.EntityIds.Count < 1) {
        Debug.LogError($"Failed to assign '{downloadUrl}' to Shared Cloud of Organization with ID '{organizationId}'");
        return null;
      }

      // Exactly one URL is sent per call, so more than one ID coming back means the cloud did something we
      // don't model. Surface it rather than silently discarding the extras.
      if (cloudFilesResult.EntityIds.Count > 1) {
        Debug.LogWarning($"Expected one entity ID for '{downloadUrl}' but the cloud returned {cloudFilesResult.EntityIds.Count}. Using the first.");
      }

      Debug.Log($"'{downloadUrl}' successfully added to Shared Cloud of Organization with ID '{organizationId}'");
      return cloudFilesResult.EntityIds[0];
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

    /// <summary>
    /// Ensures an AVNFS URL carries the display name as a <c>name</c> query parameter.
    /// </summary>
    /// <remarks>
    /// The on-device AVNFS ContentProvider applies the caller's <c>name</c> extra only when it
    /// performs a real upload. Re-uploading existing content would overwrite the name as a hash.
    ///
    /// Re-applying the parameter here makes a deduplicated upload produce the same URL shape
    /// as a fresh one.
    /// </remarks>
    /// <param name="avnfsUrl">The URL returned by the upload, which may lack the parameter.</param>
    /// <param name="filename">The display name the caller asked for.</param>
    /// <returns>The URL with a <c>name</c> parameter, or the input unchanged if it cannot be applied.</returns>
    internal static string EnsureFileNameParameter(string avnfsUrl, string filename) {
      if (string.IsNullOrEmpty(avnfsUrl) || string.IsNullOrEmpty(filename)) {
        return avnfsUrl;
      }

      // Already named — don't append a duplicate parameter
      if (avnfsUrl.Contains("?name=") || avnfsUrl.Contains("&name=")) {
        return avnfsUrl;
      }

      char separator = avnfsUrl.Contains('?') ? '&' : '?';
      return $"{avnfsUrl}{separator}name={Uri.EscapeDataString(filename)}";
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
