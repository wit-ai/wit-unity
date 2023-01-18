# wit-unity
`wit-unity` is a Unity C# based wrapper around the rest apis provided by [Wit.ai](http://wit.ai). It is meant to be used as a base library within Voice SDK. We have made it accessible here for contributions and early adoption testing. Wit-unity is ideal for developers looking to do early research with voice and potential expand the core capabilities of Voice SDK.

**NOTE** The wit-unity github repo is bleeding edge. Even the releases within this repo are early access. If you are looking for something stable to use in production you should use the release packages of Voice SDK instead.

It is strongly recommended if you are building anything in production you use Voice SDK instead of Wit-Unity.

## Voice SDK
Voice SDK is the primary release mechanism and use of the Wit-Unity library. You can learn more about Voice SDK in the Oculus Developer documentation under the [Voice SDK Overview](https://developer.oculus.com/documentation/unity/voice-sdk-overview/).

### Differences between Wit-Unity and Voice SDK
* **Quality** One of the core differences between wit-unity and Voice SDK is its QA process. Voice SDK goes through more rigorous testing before releases are published and is considered production ready. Changes published to wit-unity are engineer tested and then vetted by QA after checkin.
* **Completion** If you are working off of the main-line wit-unity branch you may clone work in progress features.
* **Platform Integration** Voice SDK offers extended platform specific implementations. Wit-Unity's purpose is to wrap the Wit.ai APIs. It does not deal with any platform specific optimizations. Voice SDK however provides platform integrations and additional optimizations for devices like the Oculus Quest.
* **Documentation** The documentation around wit-unity is somewhat limited. You will find more detailed documentation and tutorials on Voice SDK as a whole in the [Voice SDK documentation](https://developer.oculus.com/documentation/unity/voice-sdk-overview/).

## Installing Voice SDK
1. Download the repo. You can get it on the Unity Asset Store as part of the [Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) asset. Or you can download just the Voice SDK through the [Oculus developer website](https://developer.oculus.com/downloads/package/oculus-voice-sdk/).
2. Once you have imported you can find the initial setup in the Oculus menu under Oculus/Voice SDK/Settings button.

## Installing Wit-Unity without Voice SDK
There are a couple ways you can install this plugin in Unity.

1. You can download the repo and drop it in your Unity project's Assets directory
2. You can add the sdk via Unity's Package Manager.

### Installing Via Unity Package Manager
1. Open your unity project
2. Open the Package Manager by clicking on Window->Package Manager
3. Click the + dropdown in the upper left corner of the package manager window
4. Select "Add package from git url"
5. In the url box enter "https://github.com/wit-ai/wit-unity.git"

#### Post Install Setup
Once you have installed the Wit plugin you will need to add your project's server token to your project.
1. Open the Wit configuration window by clicking on Window->Wit->WitConfiguration in the menu bar.
2. Go to the [Wit.ai](http://wit.ai) website manually or by clicking on "Continue With Facebook"
<image src="https://user-images.githubusercontent.com/645359/125703060-59d62659-1dd6-442f-a92d-d8ec142c53d8.png" width=300>

4. Find your project and go to the project settings page
5. Copy the Server Token and past it in the box in the Wit Configuration window

## Samples
Samples using `wit-unity` can be found in the Samples directory or in the package manager's samples section. You will need to provide your own WitConfiguration for the sample scenes.

![image](https://user-images.githubusercontent.com/645359/121092454-6bab1100-c7a0-11eb-9cb1-87dd8ae6e875.png)


## License
The license for wit-unity can be found in [LICENSE](https://github.com/wit-ai/wit-unity/blob/main/LICENSE) file in the root directory of this source tree.

## Terms of Use
Our terms of use can be found at https://opensource.fb.com/legal/terms.

## Privacy Policy
Our privacy policy can be found at https://opensource.fb.com/legal/privacy.

Copyright © Meta Platforms, Inc
