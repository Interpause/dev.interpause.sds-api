using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace dev.interpause.sds_api
{
    public class GenObjectResponse
    {
        public string task_id;
    }

    public static class GenObjectAPI
    {
        public static string BaseUrl { get; set; } = "http://nixrobo.home.arpa:3000";

        /// <summary>
        /// Requests object generation with the provided user prompt and image path.
        /// </summary>
        /// <param name="prompt">The user prompt for object generation.</param>
        /// <param name="imgPath">The path to the image used for object generation.</param>
        public static void RequestObjectGeneration(string prompt, string imgPath, Action<string> afterRequest)
        {
            Debug.Log($"Requesting object generation with prompt: \"{prompt}\" and image path: \"{imgPath}\"");

            var imageData = System.IO.File.ReadAllBytes(imgPath);
            var fileName = System.IO.Path.GetFileName(imgPath);

            var form = new WWWForm();
            form.AddField("client_id", "my_placeholder");
            form.AddField("prompt", prompt ?? "");
            form.AddBinaryData("image", imageData, fileName, "image/*");

            var url = $"{BaseUrl}/3d_obj/add_task";
            var req = UnityWebRequest.Post(url, form);

            req.SendWebRequest().completed += (asyncOperation) =>
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Object generation request successful.");
                    var resp = JsonUtility.FromJson<GenObjectResponse>(req.downloadHandler.text);
                    Debug.Log($"Task ID: {resp.task_id}");
                    afterRequest?.Invoke(resp.task_id);
                }
                else
                {
                    Debug.LogError($"Object generation request failed: {req.error}");
                    afterRequest?.Invoke(null);
                }
            };
        }
    }
}
