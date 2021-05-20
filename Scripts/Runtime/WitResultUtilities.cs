using com.facebook.witai.lib;

namespace com.facebook.witai
{
    public class WitResultUtilities
    {
        public static string GetFirstSlot(JSONNode witResponse, string name)
        {
            return witResponse?["entities"]?[name]?[0]?["value"]?.Value;
        }

        public static JSONNode GetFirstEntity(JSONNode witResponse, string name)
        {
            return witResponse?["entities"]?[name][0];
        }

        public static string GetIntentName(JSONNode witResponse)
        {
            return witResponse?["intents"]?[0]?["name"]?.Value;
        }

        public static JSONNode GetFirstIntent(JSONNode witResponse)
        {
            return witResponse?["intents"]?[0];
        }
    }
}
