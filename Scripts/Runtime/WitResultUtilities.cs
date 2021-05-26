using com.facebook.witai.lib;

namespace com.facebook.witai
{
    public class WitResultUtilities
    {
        public static string GetFirstSlot(WitResponseNode witResponse, string name)
        {
            return witResponse?["entities"]?[name]?[0]?["value"]?.Value;
        }

        public static WitResponseNode GetFirstEntity(WitResponseNode witResponse, string name)
        {
            return witResponse?["entities"]?[name][0];
        }

        public static string GetIntentName(WitResponseNode witResponse)
        {
            return witResponse?["intents"]?[0]?["name"]?.Value;
        }

        public static WitResponseNode GetFirstIntent(WitResponseNode witResponse)
        {
            return witResponse?["intents"]?[0];
        }
    }
}
