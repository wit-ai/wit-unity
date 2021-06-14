using UnityEditor;

namespace com.facebook.witai.lib
{
    [CustomEditor(typeof(Mic))]
    public class MicEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var mic = (Mic) target;

            int index = EditorGUILayout.Popup("Input", mic.CurrentDeviceIndex, mic.Devices.ToArray());
            if (index != mic.CurrentDeviceIndex)
            {
                mic.ChangeDevice(index);
            }
        }
    }
}
