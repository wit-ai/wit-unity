/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Configuration;
using System.Reflection;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitEndpointConfig))]
    public class WitEndpointConfigDrawer : WitPropertyDrawer
    {
        // Allow edit with lock
        protected override WitPropertyEditType GetEditType()
        {
            return WitPropertyEditType.LockEdit;
        }
        // Determine if should layout field
        protected override bool ShouldLayoutField(FieldInfo subfield)
        {
            switch (subfield.Name)
            {
                case "message":
                    return false;
            }
            return base.ShouldLayoutField(subfield);
        }
        // Get default fields
        protected override string GetDefaultFieldValue(FieldInfo subfield)
        {
            // Iterate options
            switch (subfield.Name)
            {
                case "uriScheme":
                    return WitRequest.URI_SCHEME;
                case "authority":
                    return WitRequest.URI_AUTHORITY;
                case "port":
                    return "80";
                case "witApiVersion":
                    return WitRequest.WIT_API_VERSION;
                case "speech":
                    return WitRequest.WIT_ENDPOINT_SPEECH;
            }

            // Return base
            return base.GetDefaultFieldValue(subfield);
        }
    }
}
