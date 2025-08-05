using System;
using UnityEngine;

// TODO: Refactor the API to be await based so this isn't callback hell.
namespace dev.interpause.sds_api
{
    /// <summary>
    /// Represents a pending generation task for an HDRI.
    /// </summary>
    public class GenHdriTask : MonoBehaviour
    {
        public float defaultPollRate = 1f;
        public string UserPrompt { get; private set; }
        public string TaskId { get; private set; }
        public string ResultUrl { get; private set; }
        public string Status { get; private set; }
        public bool IsBusy { get; private set; } = false;
        public Action<string> eventLogReceived;
        // Can i attach this to IsBusy somehow...
        public Action taskFinished;

        // TODO: Polling sucks is there better way?
        private float _pollRate;
        // NOTE: HDRI results could be loaded into a Skybox or used for environment lighting
        // but we don't automatically load them like we do with 3D objects
        private int _numEventsReceived;

        /// <summary>
        /// Reinitializing is possible.
        /// TODO: Set environment to placeholder while running.
        /// </summary>
        public void Initialize(
            string prompt,
            float pollRate = -1f
        )
        {
            if (IsBusy)
            {
                Debug.LogWarning("[HDRI Gen] Cannot reinitialize GenHdriTask while in progress.");
                return;
            }

            UserPrompt = prompt;
            if (pollRate < 0f)
                _pollRate = defaultPollRate;
            else
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
                Debug.LogWarning("[HDRI Gen] Cannot submit while in progress.");
                return;
            }
            IsBusy = true;

            GenHdriAPI.RequestHdriGeneration(
                (taskId) =>
                {
                    if (string.IsNullOrEmpty(taskId))
                    {
                        Debug.LogError("[HDRI Gen] Failed to get task ID from HDRI generation request.");
                        eventLogReceived?.Invoke("Failed to get task ID from HDRI generation request.");
                        Status = TaskStatus.FAILED;
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                    else
                    {
                        eventLogReceived?.Invoke($"HDRI generation request submitted successfully. Task ID: `{taskId}`");
                        TaskId = taskId;
                        _numEventsReceived = 0;
                        InvokeRepeating(nameof(CheckStatus), 0f, _pollRate);
                        Status = TaskStatus.IN_PROGRESS;
                    }
                },
                UserPrompt
            );
            eventLogReceived?.Invoke($"Starting new HDRI gen task with prompt: `{UserPrompt}`");
        }

        private void CheckStatus()
        {
            if (string.IsNullOrEmpty(TaskId))
            {
                Debug.LogWarning("[HDRI Gen] No current task ID to poll.");
                return;
            }

            GenHdriAPI.RequestGenerationEvents(
                (eventsData) =>
                {
                    if (eventsData == null)
                    {
                        Debug.LogWarning("[HDRI Gen] No events data received.");
                        return;
                    }
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

            GenHdriAPI.RequestGenerationStatus(
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
                        eventLogReceived?.Invoke($"HDRI generation task `{TaskId}` failed.");
                        Debug.LogWarning($"[HDRI Gen] HDRI generation task `{TaskId}` failed.");
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
                Debug.LogWarning("[HDRI Gen] No current task ID to check results.");
                return;
            }

            GenHdriAPI.RequestGenerationResults(
                (hdriUrl) =>
                {
                    if (string.IsNullOrEmpty(hdriUrl))
                    {
                        Debug.LogError("[HDRI Gen] Failed to get HDRI generation results URL.");
                        eventLogReceived?.Invoke("Failed to get HDRI generation results URL.");
                        Status = TaskStatus.FAILED;
                        IsBusy = false;
                        taskFinished?.Invoke();
                    }
                    else
                    {
                        ResultUrl = hdriUrl;
                        // NOTE: Unlike 3D objects, we don't automatically load HDRI into the scene
                        // The user can access ResultUrl to manually load it into skybox or lighting
                        // Debug.Log($"HDRI generation task `{TaskId}` completed successfully. Url: {hdriUrl}");
                        eventLogReceived?.Invoke($"HDRI generation task `{TaskId}` completed successfully. Url: {hdriUrl}");
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
