/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi.Samples.Shapes
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
                var colorString = WitResultUtilities.GetAllEntityValues(response, "color:color");
                var shapeString = WitResultUtilities.GetAllEntityValues(response, "shape:shape");

                if (colorString.Length != shapeString.Length)
                {
                    Debug.LogWarning("Mismatched entity pairings.");
                    return;
                }
                else
                {
                    for(var entity = 0; entity < shapeString.Length; entity++)
                    {
                        if (ColorUtility.TryParseHtmlString(colorString[entity], out var color))
                        {
                            if (string.IsNullOrEmpty(shapeString[entity]))
                            {
                                for (int i = 0; i < transform.childCount; i++)
                                {
                                    SetColor(transform.GetChild(i), color);
                                }
                            }
                            else
                            {
                                var shape = transform.Find(shapeString[entity]);
                                if (shape) SetColor(shape, color);
                            }
                        }
                    }
                }
            }
        }

        [MatchIntent("change_color")]
        public void OnColorIntent()
        {
            Debug.Log("OnColorIntent was triggered");
        }

        [MatchIntent("change_color")]
        public void OnHandleColorIntent(WitResponseNode node)
        {
            var intent = node.GetFirstIntentData();

            var color = node.GetFirstWitEntity("color:color");
            if (color == "red")
            {
                Debug.Log("The cube is red!");
            }

            Debug.Log("OnHandleColorIntent was triggered with color " +
                      color);
        }

        [MatchIntent("change_size")]
        public void OnHandleSizeIntent(WitResponseNode node)
        {
            var intent = node.GetFirstIntentData();

            var size = node.GetFirstWitIntValue("wit:number", 1);
            var shape = node.GetFirstWitEntity("shape:shape");

            if (shape.confidence > .5)
            {
                var shapeTransform = transform.Find(shape);
                if (shapeTransform)
                {
                    shapeTransform.localScale = Vector3.one * 10 / ((float) size);
                }
            }
        }

        private void FindShape(string shape)
        {

        }
    }
}
