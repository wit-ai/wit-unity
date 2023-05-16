/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi;
using Meta.Conduit;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Voice.Samples.WitShapes
{
    public class ColorChanger : MonoBehaviour
    {
        private void SetColor(Transform transform, UnityEngine.Color color)
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
                    VLog.W("Mismatched entity pairings.");
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

        [HandleEntityResolutionFailure]
        public void OnConduitFailed(string intent, Exception ex)
        {
            VLog.E(ex.Message);
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
            if (color == null)
            {
                return;
            }

            if (color == "red")
            {
                Debug.Log("The cube is red!");
            }

            Debug.Log("OnHandleColorIntent was triggered with color " +
                      color);
        }

        [MatchIntent("change_color")]
        public void OnHandleColorIntentWithConduit(Color color, Shape shape)
        {
            Debug.Log($"OnHandleColorIntent was triggered via Conduit with color {color} {color.ToString()} and shape {shape} {shape.ToString()}");

            var shapeTransform = transform.Find(shape.ToString());
            if (shapeTransform)
            {
                if (ColorUtility.TryParseHtmlString(color.ToString(), out var unityColor)) {
                    SetColor(shapeTransform, unityColor);
                }
            }
        }

        [MatchIntent("change_size")]
        public void OnHandleSizeIntentWithConduit(int size, Shape shape)
        {
            Debug.Log($"OnHandleSizeIntent was triggered via Conduit with size {size} and shape {shape} {shape.ToString()}");

            var shapeTransform = transform.Find(shape.ToString());
            if (shapeTransform)
            {
                shapeTransform.localScale = Vector3.one * 10 / ((float) size);
            }
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
    }
}
