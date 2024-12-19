using System;
using Avn.Connect.V1;
using System.Threading.Tasks;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

using Authorization = Avn.Connect.V1.Authorization;

namespace ClassVR
{
    //TODO: rename
    public static class CloudFileHelper
    {
        // Maximum size in bytes that AVNFS will accept (5GB)
        private const long MaxSinglePartUploadSizeBytes = 5368709120;

        // Enable coroutines from static context - https://discussions.unity.com/t/c-coroutines-in-static-functions/475291/26
        private class StaticMB : MonoBehaviour { }
        private static StaticMB mbInstance;

        private static void InitMonoBehaviour()
        {
            if (mbInstance == null)
            {
                var gameObject = new GameObject("ClassVRStatic");
                mbInstance = gameObject.AddComponent<StaticMB>();
            }
        }

        /// <summary>
        /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is assigned to.
        /// </summary>
        /// <param name="filename">The name and extension of the file.</param>
        /// <param name="mediaType">The media (or MIME) type of the file.</param>
        /// <param name="data">The file contents as a byte array.</param>
        /// <param name="auth">The ClassVR Authentication to use for uploading.</param>
        /// <param name="onComplete">Callback once the operation has completed. Bool parameter is true if the upload was successful, false otherwise.</param>
        /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
        public static void UploadToSharedCloud(string filename, string mediaType, byte[] data, Action<bool> onComplete, EndpointServer endpointServer = EndpointServer.Production)
        {
            //TODO: make this return a task so it can be awaited
            //TODO: Check file doesn't exceed upload size limit (not currently possible as max array length is 2GB and upload limit is 5GB)
            //TODO: enable streaming uploads
            //TODO: enable cancellation

            // Use the device JWT for authentication
            var auth = new Authorization { DeviceJwt = ClassVRProperties.Instance.DeviceJWT };
            if(string.IsNullOrEmpty(auth.DeviceJwt))
            {
                Debug.LogError("Couldn't retreive device JWT for authorization. Upload to ClassVR failed.");
                return;
            }

            // Create a MonoBehaviour instance so we can run coroutines
            InitMonoBehaviour();
            mbInstance.StartCoroutine(UploadAndAddToSharedCloud(filename, mediaType, data, auth, endpointServer, onComplete));

            // Ideally this method would be async, but the .net HttpClient doesn't work reliably for file uploads to Avantis cloud infrastructure
            // UnityWebRequest is working but isn't awaitable, so needs to be run in a coroutine to prevent blocking the main thread
        }

        // Uploads the provided file to AVNFS and adds to the Shared Cloud area of the organization the device is registered to. Indicates success via the callback.
        private static IEnumerator UploadAndAddToSharedCloud(
            string filename,
            string mediaType,
            byte[] data,
            Authorization auth,
            EndpointServer endpointServer,
            Action<bool> onComplete)
        {
            InitMonoBehaviour();

            // Upload the file to AVNFS and get the URL to download the file
            string downloadUrl = null;
            yield return mbInstance.StartCoroutine(UploadToAvnfs(filename, mediaType, data, auth, endpointServer, (returnVal) => { downloadUrl = returnVal; }));

            if (downloadUrl == null)
            {
                onComplete(false);
                yield break;
            }

            // Assign the file to the Shared Cloud area of the organization the device is currently registered to
            bool addFileSuccess = false;
            yield return mbInstance.StartCoroutine(AddFileToSharedCloud(downloadUrl, auth, endpointServer, (returnVal) => { addFileSuccess = returnVal; }));
            onComplete(addFileSuccess);
        }

        // Uploads the provided file to AVNFS and provides the URL it can be downloaded from via the callback
        private static IEnumerator UploadToAvnfs(
            string filename,
            string mediaType,
            byte[] data,
            Authorization auth,
            EndpointServer endpointServer,
            Action<string> onComplete)
        {
            Debug.LogFormat("Uploading '{0}' to ClassVR", filename);

            InitMonoBehaviour();

            // Check whether the file has already been uploaded
            var base64UrlHash = Base64UrlHash(data);
            string existingUrl = null;
            yield return mbInstance.StartCoroutine(CheckFileAlreadyUploaded(filename, mediaType, base64UrlHash, data.Length, endpointServer, (returnVal) => { existingUrl = returnVal; }));

            if (!string.IsNullOrEmpty(existingUrl))
            {
                Debug.LogFormat("'{0}' has already been uploaded to ClassVR at {1}", filename, existingUrl);
                onComplete(existingUrl);
                yield break;
            }

            // Files are uploaded via HTTP POST (not a gRPC call), so request the necessary data to make the HTTP request
            UploadManifest uploadManifest = null;
            yield return mbInstance.StartCoroutine(GetUploadManifest(filename, mediaType, base64UrlHash, data.Length, auth, endpointServer, (returnVal) => { uploadManifest = returnVal; }));

            if (uploadManifest == null)
            {
                Debug.LogErrorFormat("Failed to retrieve upload manifest for '{0}'. Aborting upload.", filename);
                onComplete(null);
                yield break;
            }

            // Construct the UnityWebRequest and send it
            var webRequest = ConstructFileUploadWebRequest(uploadManifest, data);
            if (webRequest == null)
            {
                Debug.LogErrorFormat("Failed to construct upload request for '{0}'", filename);
                onComplete(null);
                yield break;
            }
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogErrorFormat("Upload of '{0}' failed with error '{1}'", filename, webRequest.error);
                onComplete(null);
                yield break;
            }

            Debug.LogFormat("'{0}' uploaded successfully", filename);
            onComplete(uploadManifest.DownloadUrl);
        }

        // Assigns the file with the specified URL to the Shared Cloud of the ClassVR Organization that the device is registered to
        private static IEnumerator AddFileToSharedCloud(
            string downloadUrl,
            Authorization auth,
            EndpointServer endpointServer,
            Action<bool> onComplete)
        {
            var orgId = ClassVRProperties.Instance.OrganizationInfo.Id;
            Debug.LogFormat("Assigning '{0}' to Shared Cloud of Organization with ID '{1}'", downloadUrl, orgId);

            // Construct a request using the organization ID the device is currently registered to
            var addFilesRequest = new AddCloudFilesRequest
            {
                Auth = auth,
                OrganizationId = orgId,
                FileUrls = { downloadUrl }
            };

            // Make the request and yield until complete
            var avnCloud = new CloudService.CloudServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
            var cloudFilesTask = avnCloud.AddCloudFilesAsync(addFilesRequest).ResponseAsync;
            while (!cloudFilesTask.IsCompleted) { yield return null; }

            // Check the response for EntityIds
            if (cloudFilesTask.Result.EntityIds.Count < 1)
            {
                Debug.LogErrorFormat("Failed to assign '{0}' to Shared Cloud of Organization with ID '{1}'", downloadUrl, orgId);
                onComplete(false);
                yield break;
            }

            Debug.LogFormat("'{0}' successfully added to Shared Cloud of Organization with ID '{1}'", downloadUrl, orgId);
            onComplete(true);
        }

        // Hashes specified byte array using SHA265 then converts to Base64URL
        private static string Base64UrlHash(byte[] data)
        {
            // Hash the array using SHA256
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(data);
            }

            // Encode hash as Base64URL
            return ToBase64Url(hash);
        }

        // Checks if the file has been uploaded to AVNFS and provides its URL in the callback if so, otherwise null
        private static IEnumerator CheckFileAlreadyUploaded(
            string filename,
            string mediaType,
            string base64UrlHash,
            long sizeBytes,
            EndpointServer endpointServer,
            Action<string> onComplete)
        {
            // Construct a request to get the URL for file with specified hash
            var fileSignature = new GetFileUrlRequest
            {
                Hash = base64UrlHash,
                SizeBytes = sizeBytes,
                MediaType = mediaType,
                FileName = filename
            };

            // Send request and yield until complete
            var avnfs = new AvnfsService.AvnfsServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
            var getFileResponseTask = avnfs.GetFileUrlAsync(fileSignature).ResponseAsync;
            while (!getFileResponseTask.IsCompleted) { yield return null; }

            // If the request return a URL then the file has already been uploaded
            if (getFileResponseTask.IsCompletedSuccessfully && getFileResponseTask.Result.HasUrl)
            {
                onComplete(getFileResponseTask.Result.Url);
            }

            onComplete(null);
        }

        // Gets the manifest required to upload a file to AVNFS and provides via the callback
        private static IEnumerator GetUploadManifest(
            string filename,
            string mediaType,
            string base64UrlHash,
            long sizeBytes,
            Authorization auth,
            EndpointServer endpointServer,
            Action<UploadManifest> onComplete)
        {
            // Request the data required to upload a file to AVNFS and yield until complete
            var avnfs = new AvnfsService.AvnfsServiceClient(AvnCloudChannel.ChannelForServer(endpointServer));
            var manifestRequest = new GetManifestRequest
            {
                Auth = auth,
                FileName = filename,
                Hash = base64UrlHash,
                MediaType = mediaType,
                SizeBytes = sizeBytes
            };
            var uploadManifestTask = avnfs.GetPostManifestAsync(manifestRequest).ResponseAsync;
            while (!uploadManifestTask.IsCompleted) { yield return null; }
            onComplete(uploadManifestTask.Result);
        }

        // Constructs a UnityWebRequest to upload the provided file to AVNFS
        private static UnityWebRequest ConstructFileUploadWebRequest(UploadManifest uploadManifest, byte[] data)
        {
            try
            {
                // Build a UnityWebRequest to POST the file
                var form = new WWWForm();
                foreach (var field in uploadManifest.HeaderFields)
                {
                    form.AddField(field.Name, field.Value);
                }
                form.AddBinaryData("file", data);
                return UnityWebRequest.Post(uploadManifest.UploadUrl, form);
            }
            catch (Exception ex)
            {
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
