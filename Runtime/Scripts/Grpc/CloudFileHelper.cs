using Avn.Connect.V1;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Cryptography;
using UnityEngine;

using Authorization = Avn.Connect.V1.Authorization;

namespace ClassVR
{
    //TODO: rename
    public static class CloudFileHelper
    {
        // Maximum size in bytes that AVNFS will accept (5GB)
        private const long MaxSinglePartUploadSizeBytes = 5368709120;

        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
        /// </summary>
        /// <param name="filename">The name and extension of the file.</param>
        /// <param name="mediaType">The media (or MIME) type of the file.</param>
        /// <param name="data">The file contents as a byte array.</param>
        /// <param name="auth">The ClassVR Authentication to use for uploading.</param>
        /// <param name="endpointServer">The endpoint to use for communication, either Alpha or Production. Defaults to Production if not provided.</param>
        /// <returns>The Entity ID for the uploaded file, or -1 if operation failed.</returns>
        public static async Task<int> UploadToSharedCloud(string filename, string mediaType, byte[] data, Authorization auth, EndpointServer endpointServer = EndpointServer.Production)
        {
            var downloadUrl = await UploadtoAVNFS(filename, mediaType, data, auth, endpointServer);

            // Check if upload succeeded
            if (string.IsNullOrEmpty(downloadUrl))
            {
                return -1;
            }

            var orgId = ClassVRProperties.Instance.OrganizationInfo.Id;
            Debug.LogFormat("Assigning '{0}' to Shared Cloud of Organization with ID '{1}'", filename, orgId);

            try
            {
                // Construct a request using the organization ID the device is currently assigned to
                var addFilesRequest = new AddCloudFilesRequest
                {
                    Auth = auth,
                    OrganizationId = orgId,
                    FileUrls = { downloadUrl }
                };

                // Make the request
                var avnCloud = new CloudService.CloudServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
                var cloudFilesResponse = await avnCloud.AddCloudFilesAsync(addFilesRequest);

                // Check the response for EntityIds
                if(cloudFilesResponse.EntityIds.Count < 1)
                {
                    Debug.LogErrorFormat("Failed to assign '{0}' to Shared Cloud of Organization with ID '{1}'", filename, orgId);
                    return -1;
                }

                Debug.LogFormat("'{0}' successfully added to Shared Cloud of Organization with ID '{1}'", filename, orgId);
                return cloudFilesResponse.EntityIds[0];
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Exception thrown while assigning '{0}' to Shared Cloud of Organization with ID '{1}'", filename, orgId);
                Debug.LogException(ex);
                return -1;
            }
        }

        /// <summary>
        /// Uploads a file to AVNFS and returns the URL it can be accessed from.
        /// </summary>
        /// <param name="filename">The name and extension of the file.</param>
        /// <param name="mediaType">The media (or MIME) type of the file.</param>
        /// <param name="data">The file contents as a byte array.</param>
        /// <param name="auth">The ClassVR Authentication to use for uploading.</param>
        /// <param name="endpointServer">The endpoint to use for communication, either Alpha or Production. Defaults to Production if not provided.</param>
        /// <returns>The URL where the file can be downloaded from, or null if upload failed.</returns>
        public static async Task<string> UploadtoAVNFS(string filename, string mediaType, byte[] data, Authorization auth, EndpointServer endpointServer = EndpointServer.Production)
        {
            try
            {
                //TODO: Check file doesn't exceed upload size limit (not currently possible as max array length is 2GB)
                //TODO: enable streaming uploads

                Debug.LogFormat("Uploading '{0}' to ClassVR", filename);

                // Hash file contents
                byte[] hash;
                using (var sha256 = SHA256.Create())
                {
                    hash = sha256.ComputeHash(data);
                }

                // Encode hash as Base64URL
                var base64UrlHash = ToBase64Url(hash);

                var fileSignature = new GetFileUrlRequest
                {
                    Hash = base64UrlHash,
                    SizeBytes = data.Length,
                    MediaType = mediaType,
                    FileName = filename
                };

                // Check if file already exists
                var avnfs = new AvnfsService.AvnfsServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
                var getFileResponse = await avnfs.GetFileUrlAsync(fileSignature);
                if(getFileResponse.HasUrl)
                {
                    Debug.LogFormat("'{0}' has already been uploaded to ClassVR at {1}", filename, getFileResponse.Url);
                    return getFileResponse.Url;
                }

                // Request the URL to upload to
                var manifestRequest = new GetManifestRequest
                {
                    Auth = auth,
                    FileName = filename,
                    Hash = base64UrlHash,
                    MediaType = mediaType,
                    SizeBytes = data.Length
                };
                var uploadManifest = await avnfs.GetPostManifestAsync(manifestRequest);

                // Upload is done via a HTTP POST, construct a request with the required data
                var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uploadManifest.UploadUrl);
                request.Content = new ByteArrayContent(data);
                foreach (var field in uploadManifest.HeaderFields)
                {
                    // HTTPS headers are validated, so we have to assign the right ones to the request and content
                    if(field.Name.StartsWith("cache", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Headers.Add(field.Name, field.Value);
                    }
                    else
                    {
                        request.Content.Headers.Add(field.Name, field.Value);
                    }
                }

                // Post the request
                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogErrorFormat("Upload of '{0}' failed with code '{1}'", filename, response.StatusCode);
                    return null;
                }

                Debug.LogFormat("'{0}' uploaded successfully", filename);
                return uploadManifest.DownloadUrl;
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Exception thrown while uploading '{0}'", filename);
                Debug.LogException(ex);
                return null;
            }
        }

        static string ToBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
