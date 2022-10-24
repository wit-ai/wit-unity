/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Events;

namespace Facebook.WitAi.Windows
{
    public class WitUnderstandingViewerVoiceServiceAPI : WitUnderstandingViewerServiceAPI
    {
        private VoiceService _service;

        public WitUnderstandingViewerVoiceServiceAPI(VoiceService service)
        {
            _service = service;

            HasVoiceActivation = true;
            HasTextActivation = true;
        }

        public override VoiceEvents Events
        {
            get => _service.events;
        }

        public override string ServiceName
        {
            get
            {
                var configProvider = _service.GetComponent<IWitRuntimeConfigProvider>();
            
                if (configProvider != null)
                {
                    return $"{configProvider.RuntimeConfiguration.witConfiguration.name} [{_service.gameObject.name}]";
                }
            
                return _service.gameObject.name;
            }
        }
        
        public override bool Active
        {
            get=> _service.Active;
    }

        public override void Activate()
        {
            _service.Activate();
        }
        
        public override void Activate(string text)
        {
            _service.Activate(text);
        }

        public override void Deactivate()
        {
            _service.Deactivate();
        }

        public override void DeactivateAndAbortRequest()
        {
            _service.DeactivateAndAbortRequest();
        }
    }
}
