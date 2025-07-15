using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace dev.interpause.sds_api
{
    // NOTE: All the callbacks will invoke with null if the request fails.
    public static class GenObjectAPI
    {
        public static string BaseUrl { get; set; } = "http://nixrobo.home.arpa:3000";
        public static string ClientId { get; set; } = "my_placeholder";

        /// <summary>
        /// Requests object generation with the provided user prompt and image path.
        /// </summary>
        /// <param name="callback">Callback invoked with the task ID as string.</param>
        /// <param name="prompt">The user's description of their sketch.</param>
        /// <param name="imgPath">The path to the sketch image file.</param>
        public static void RequestObjectGeneration(Action<string> callback, string prompt, string imgPath)
        {
            var url = $"{BaseUrl}/3d_obj/add_task";

            Debug.Log($"Requesting object generation with prompt: `{prompt}` and image path: `{imgPath}`");

            var imageData = System.IO.File.ReadAllBytes(imgPath);
            var fileName = System.IO.Path.GetFileName(imgPath);

            var form = new WWWForm();
            form.AddField("client_id", ClientId);
            form.AddField("prompt", prompt ?? "");
            form.AddBinaryData("image", imageData, fileName, "image/*");

            var req = UnityWebRequest.Post(url, form);

            req.SendWebRequest().completed += (asyncOperation) =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Object generation request successful.");
                    var res = JsonUtility.FromJson<structs.GenObjectResponse>(req.downloadHandler.text);
                    Debug.Log($"Task ID: `{res.task_id}`");
                    callback.Invoke(res.task_id);
                }
                else
                {
                    Debug.LogError($"Object generation request failed: `{req.error}`");
                    callback.Invoke(null);
                }
            };
        }

        /// <summary>
        /// Requests generation log events for a specific task ID.
        /// </summary>
        /// <param name="callback">Callback invoked with the list of events and the number of events total.</param>
        /// <param name="taskId">Task ID of the generation request.</param>
        /// <param name="received">Number of events received so far (to avoid sending all events).</param>
        public static void RequestGenerationEvents(Action<Tuple<List<string>, int>> callback, string taskId, int received = 0)
        {
            var url = $"{BaseUrl}/3d_obj/get_events";

            var form = new WWWForm();
            form.AddField("client_id", ClientId);
            form.AddField("task_id", taskId);
            form.AddField("n_received", received);

            var req = UnityWebRequest.Post(url, form);

            req.SendWebRequest().completed += (asyncOperation) =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<structs.GenEventsResponse>(req.downloadHandler.text);
                    callback.Invoke(new Tuple<List<string>, int>(res.events, res.n_received));
                }
                else
                {
                    Debug.LogError($"Failed to get generation events: `{req.error}`");
                    callback.Invoke(null);
                }
            };
        }

        /// <summary>
        /// Requests the status of a generation task.
        /// </summary>
        /// <param name="callback">Callback invoked with the status as string.</param>
        /// <param name="taskId">Task ID of the generation request.</param>
        public static void RequestGenerationStatus(Action<string> callback, string taskId)
        {
            var url = $"{BaseUrl}/3d_obj/get_status";

            var form = new WWWForm();
            form.AddField("client_id", ClientId);
            form.AddField("task_id", taskId);

            var req = UnityWebRequest.Post(url, form);

            req.SendWebRequest().completed += (asyncOperation) =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<structs.GenStatusResponse>(req.downloadHandler.text);
                    callback.Invoke(res.status);
                }
                else
                {
                    Debug.LogError($"Failed to get generation status: `{req.error}`");
                    callback.Invoke(null);
                }
            };
        }

        /// <summary>
        /// Requests the results of a generation task.
        /// </summary>
        /// <param name="callback">Callback invoked with the .glb 3D model URL as string.</param>
        /// <param name="taskId">Task ID of the generation request.</param>
        public static void RequestGenerationResults(Action<string> callback, string taskId)
        {
            var url = $"{BaseUrl}/3d_obj/get_result";

            var form = new WWWForm();
            form.AddField("client_id", ClientId);
            form.AddField("task_id", taskId);

            var req = UnityWebRequest.Post(url, form);

            req.SendWebRequest().completed += (asyncOperation) =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<structs.GenResultsResponse>(req.downloadHandler.text);
                    if (res.success)
                    {
                        Debug.Log($"Generation results URL: `{res.url}`");
                        callback.Invoke(res.url);
                    }
                    else
                    {
                        Debug.LogError("Generation failed or no results available.");
                        callback.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"Failed to get generation results: `{req.error}`");
                    callback.Invoke(null);
                }
            };
        }
    }
}
