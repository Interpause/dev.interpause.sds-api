using System;
using System.Collections.Generic;

/// Refer to structs.py in the server, property names must match.
namespace dev.interpause.sds_api.structs
{
    [Serializable]
    class GenObjectResponse
    {
        public string task_id;
    }

    [Serializable]
    class GenEventsResponse
    {
        public List<string> events;
        public int n_received;
    }

    [Serializable]
    class GenStatusResponse
    {
        public string status;
    }

    [Serializable]
    class GenResultsResponse
    {
        public bool success;
        public string url;
    }
}
