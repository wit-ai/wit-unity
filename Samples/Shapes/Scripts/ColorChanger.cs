/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai.demo
{
    public class ColorChanger : MonoBehaviour
    {
        private void SetColor(Transform transform, Color color)
        {
            transform.GetComponent<Renderer>().material.color = color;
        }

        public void UpdateColor(WitResponseNode response)
        {
            var intent = WitResultUtilities.GetIntentName(response);
            if (intent == "change_color")
            {
                var colorString = WitResultUtilities.GetFirstEntityValue(response, "color:color");
                var shapeString = WitResultUtilities.GetFirstEntityValue(response, "shape:shape");

                if (ColorUtility.TryParseHtmlString(colorString, out var color))
                {
                    if (string.IsNullOrEmpty(shapeString))
                    {
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            SetColor(transform.GetChild(i), color);
                        }
                    }
                    else
                    {
                        var shape = transform.Find(shapeString);
                        if(shape) SetColor(shape, color);
                    }
                }
            }
        }
    }
}
