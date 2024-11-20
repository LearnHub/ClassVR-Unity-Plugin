using Avn.Connect.V1;
using Grpc.Net.Client;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace ClassVR
{
    //TODO: rename
    public static class CloudFileHelper
    {
        // Maximum size in bytes that AVNFS will accept (5GB)
        private const long MaxSinglePartUploadSizeBytes = 5368709120;

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

            try
            {
                // Construct a request using the organization ID the device is currently assigned to
                var addFilesRequest = new AddCloudFilesRequest
                {
                    Auth = auth,
                    OrganizationId = ClassVRProperties.Instance.OrganizationInfo.Id,
                    FileUrls = { downloadUrl }
                };

                // Make the request
                var avnCloud = new CloudService.CloudServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
                var cloudFilesResponse = await avnCloud.AddCloudFilesAsync(addFilesRequest);

                // Check the response for EntityIds
                if(cloudFilesResponse.EntityIds.Count < 1)
                {
                    Debug.LogErrorFormat("Failed to add '{0}' to Shared Cloud", filename);
                    return -1;
                }

                return cloudFilesResponse.EntityIds[0];
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Exception thrown while adding '{0}' to Shared Cloud", filename);
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

                // Upload is done via HTTP, build a form with the required data
                var formData = new WWWForm();
                foreach (var field in uploadManifest.HeaderFields)
                {
                    formData.AddField(field.Name, field.Value);
                }
                formData.AddBinaryData("file", data);

                // Post the form to the upload URL
                using var webRequest = UnityWebRequest.Post(uploadManifest.UploadUrl, formData);
                await webRequest.SendWebRequest();

                // Check upload success
                if (webRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(webRequest.error);
                    return null;
                }

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
