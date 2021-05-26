using System;
using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai.data
{
    [Serializable]
    public abstract class WitConfigurationData
    {
        [SerializeField] public WitConfiguration witConfiguration;

        public void Update()
        {
            var request = OnCreateRequest();
            request.onResponse += OnUpdate;
            request.Request();
        }

        protected abstract WitRequest OnCreateRequest();

        private void OnUpdate(WitRequest request)
        {
            request.onResponse -= OnUpdate;
            if (request.StatusCode == 200)
            {
                Update(request.ResponseData);
            }
            else
            {
                Debug.LogError(request.StatusDescription);
            }
        }

        protected abstract void Update(WitResponseNode data);
    }
}
