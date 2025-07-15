using System;
using GLTFast;
using UnityEngine;

// TODO: Refactor the API to be await based so this isn't callback hell.
namespace dev.interpause.sds_api
{
    public static class TaskStatus
    {
        public const string NOT_STARTED = "NOT_STARTED";
        public const string IN_PROGRESS = "IN_PROGRESS";
        public const string COMPLETED = "COMPLETED";
        public const string FAILED = "FAILED";
    }

    /// <summary>
    /// Represents a pending generation task for an object.
    /// </summary>
    public class GenObjectTask : MonoBehaviour
    {
        public string UserPrompt { get; private set; }
        public string ImagePath { get; private set; }
        public string TaskId { get; private set; }
        public string ResultUrl { get; private set; }
        public string Status { get; private set; }
        public bool IsBusy { get; private set; } = false;
        public Action<string> eventLogReceived;
        // Can i attach this to IsBusy somehow...
        public Action taskFinished;

        // TODO: Polling sucks is there better way?
        private float _pollRate;
        private GltfAsset _gltfAsset;
        private int _numEventsReceived;

        private void Awake()
        {
            _gltfAsset = gameObject.GetComponent<GltfAsset>();
            if (_gltfAsset == null)
                Debug.LogWarning("GltfAsset component is missing on the GameObject, results won't be displayed.");
        }

        /// <summary>
        /// Reinitializing is possible.
        /// TODO: Set gltf to placeholder object while running.
        /// </summary>
        public void Initialize(
            string prompt,
            string imgPath,
            float pollRate = 1f
        )
        {
            if (IsBusy)
            {
                Debug.LogWarning("Cannot reinitialize GenObjectTask while in progress.");
                return;
            }

            UserPrompt = prompt;
            ImagePath = imgPath;
            _pollRate = pollRate;
            TaskId = string.Empty;
            ResultUrl = string.Empty;
            Status = TaskStatus.NOT_STARTED;

            CancelInvoke(nameof(CheckStatus));
        }

        public void Submit()
        {
            if (IsBusy)
            {
                Debug.LogWarning("Cannot submit while in progress.");
                return;
            }
            IsBusy = true;

            GenObjectAPI.RequestObjectGeneration(
                (taskId) =>
                {
                    if (string.IsNullOrEmpty(taskId))
                    {
                        Debug.LogError("Failed to get task ID from object generation request.");
                        eventLogReceived?.Invoke("Failed to get task ID from object generation request.");
                        Status = TaskStatus.FAILED;
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                    else
                    {
                        eventLogReceived?.Invoke($"Object generation request submitted successfully. Task ID: `{taskId}`");
                        TaskId = taskId;
                        _numEventsReceived = 0;
                        InvokeRepeating(nameof(CheckStatus), 0f, _pollRate);
                        Status = TaskStatus.IN_PROGRESS;
                    }
                },
                UserPrompt,
                ImagePath
            );
            eventLogReceived?.Invoke($"Starting new gen task with prompt: `{UserPrompt}` and image: `{ImagePath}`");
        }

        private void CheckStatus()
        {
            if (string.IsNullOrEmpty(TaskId))
            {
                Debug.LogWarning("No current task ID to poll.");
                return;
            }

            GenObjectAPI.RequestGenerationEvents(
                (eventsData) =>
                {
                    var events = eventsData.Item1;
                    _numEventsReceived = eventsData.Item2;

                    foreach (var ev in events)
                    {
                        // Debug.Log($"Event: {ev}");
                        eventLogReceived?.Invoke(ev);
                    }
                },
                TaskId,
                _numEventsReceived
            );

            GenObjectAPI.RequestGenerationStatus(
                // See comfy.py for status values.
                (status) =>
                {
                    Status = status;
                    if (Status == TaskStatus.COMPLETED)
                    {
                        CancelInvoke(nameof(CheckStatus));
                        CheckResults();
                    }
                    else if (Status == TaskStatus.FAILED)
                    {
                        CancelInvoke(nameof(CheckStatus));
                        eventLogReceived?.Invoke($"Generation task `{TaskId}` failed.");
                        Debug.LogWarning($"Generation task `{TaskId}` failed.");
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                },
                TaskId
            );
        }

        private void CheckResults()
        {
            if (string.IsNullOrEmpty(TaskId))
            {
                Debug.LogWarning("No current task ID to check results.");
                return;
            }

            GenObjectAPI.RequestGenerationResults(
                async (glbUrl) =>
                {
                    if (string.IsNullOrEmpty(glbUrl))
                    {
                        Debug.LogError("Failed to get generation results URL.");
                        eventLogReceived?.Invoke("Failed to get generation results URL.");
                        Status = TaskStatus.FAILED;
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                    else
                    {
                        ResultUrl = glbUrl;
                        if (_gltfAsset != null)
                        {
                            _gltfAsset.Url = ResultUrl;
                            await _gltfAsset.Load(ResultUrl);
                        }
                        // Debug.Log($"Generation task `{TaskId}` completed successfully. Url: {glbUrl}");
                        eventLogReceived?.Invoke($"Generation task `{TaskId}` completed successfully. Url: {glbUrl}");
                        Status = TaskStatus.COMPLETED;
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                },
                TaskId
            );
        }

    }
}
