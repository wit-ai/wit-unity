# wit-unity
`wit-unity` is the Unity SDK for [Wit.ai](http://wit.ai).

## Install
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
Our terms of use can be found at https://opensource.facebook.com/legal/terms.

## Privacy Policy
Our privacy policy can be found at https://opensource.facebook.com/legal/privacy.
