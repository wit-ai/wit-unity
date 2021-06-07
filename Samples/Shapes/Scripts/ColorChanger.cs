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
                    if (!string.IsNullOrEmpty(shapeString))
                    {
                        var shape = transform.Find(shapeString);
                        if(shape) SetColor(shape, color);
                    }
                    else
                    {
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            SetColor(transform.GetChild(i), color);
                        }
                    }
                }
            }
        }
    }
}
