Part of the Scope Workshop 25

Overview
The Project
Unity x Scope is a project that is both an immersive experience using real-time AI video generation via LongLive and Scope to create interactive worlds, as well as a toolbox for Unity to make it easier to create such worlds. The project captures game footage with depth and character data, streams it via WebRTC to RunPod's LongLive API, and displays the AI-transformed output live. The system supports dynamic prompts, NPC conversations, and location-based visual customisation. Full Projects can be exported as builds and let the user put in their own API Key and RunPod URL.

Core Features
Real-Time AI Video Streaming
WebRTC-based bidirectional streaming to RunPod. Sends gameplay video and receives AI-stylized output in real-time with configurable prompts, noise scale, and denoising parameters.


Depth Extraction with Character Masking
Dual-camera system that extracts depth information while isolating player characters on a separate layer. Outputs a three-color image: near depth, far depth, and character silhouette for better differentiation plus a OpenPose based Skeleton Visualisation to help guide the image generation.


Dynamic Prompt System
Prompts automatically update based on player state (standing, walking, sprinting, jumping). Combining prefix + action + suffix for flexible prompt construction with smooth noise scale transitions based on the input.


Location-Based Prompt Zones
Trigger colliders that modify the AI generation when players enter specific areas:

PrefixChanger – Modifies the prompt prefix as long as the player is within the collision box
SuffixChanger – Modifies the prompt suffix
PromptReplacer – Replaces the entire prompt with custom settings
Cache Reset – Resets the Cache when player steps into it
These areas also let the user change the noise scale and generation steps temporarily.


Vision-Based Prompt Injection
An optional secondary system allows users to tag and name "Visible Objects." When the player character approaches these items, the prompt automatically gets descriptions like "red house on the right” included.


Interactive NPC Chat
CharacterChat zones enable AI conversations powered by Google Gemini’s API. Each NPC possesses a unique personality, and image generation prompts are updated by the LLM to be dynamically based on the character’s emotions and gestures during dialogue. 


Minimap Mode
Toggle between normal third-person view and a top-down minimap perspective. Automatically adjusts depth settings and follows the player from above. It is depthmap based and adjusts automatically to any scene. It also shows the perspective of the camera.


UI Mode System
Four distinct UI states with smooth transitions:

Setup Screen – User can initialise build projects with their own API Keys
Start Screen – Initial launch screen with custom prompt
Gameplay – Normal gameplay with dynamic prompts
Chat Mode – NPC conversation interface with cursor unlock

Keyboard Controls
* 1: Toggle video input source

* 2: Toggle minimap mode

* 3: Toggle OpenPose skeleton

* 0: Reset API cache

* Tab: Toggle parameter UI

* Space: Start game (from start screen)

* F: Start NPC chat (when near character)

* Escape: Exit chat mode

* W A S D: Control the Character

* Q + E: Move the Camera left and right

* R: Reset Camera

Technical Requirements
Unity 6 with Universal Render Pipeline
Scope running in RunPod - LongLive model with VACE disabled (Ideally a RTX 5090 or 6000 Pro)
Google Gemini API Key for LLM text generation
URP Depth Texture: Must be enabled in URP Asset settings
Cyberpunk Demo
https://youtu.be/1gK8F9AoKnQ

Future Outlook
In the future, as real-time video generation improves, new models can be implemented into this toolbox to enhance the output and create more immersive worlds with clearer output. Also I plan to release the github repo once it is all cleaned up.

Tools I am using
In case this might help anyone some information about the tools and software im using to build.

For Coding: VS Code and Github Copilot -> usually Claude 4.5 for coding
Notion for Note Taking and Organisation
Pixelmator Pro, Affinity and Figma for Ui and Asset Design
Blender for 3D Modelling
Unity for all Engine work
Process
The original idea
I previously started working on a Daydream API integration into Unity. However, since the underlying models are image models, the output is creative yet very inconsistent. Changing the backend to Scope and its more consistent video models could improve that. From there, I would then try to incorporate more interactivity and events in Unity to drive prompt generation, etc.

My original Daydream API integration in Unity
Checkpoint 1: LongLive working in Unity
After some testing within the Scope interface itself, I have now implemented a first version of LongLive in Unity via WebRTC streaming from RunPod. It also, unlike the previous Daydream API version, supports widescreen.


Testing in Scope
Testing in Scope


Testing in Unity directly
Checkpoint 2: Building tools in Unity
After I got the scope integration running, I am now working to make tools inside the engine to feed back into the pipeline.

I have created these “Zones” that the player can walk in and which have an impact on the prompt and additional parameters from LongLive.


Types of Zones
Prefix Zone: Changes the prefix of the prompt.
Suffix Zone: Changes the suffix of the prompt.
Prompt Replace Zone: Replaces the entire prompt for complete changes of scenery.
Cache Reset Zone: Resets the cache of the image generation.
Checkpoint 3:
Most planned features are implemented on a technical level, and I have created an overview video (on the top of this page).

Now bug fixing, creating good prompts, adjusting parameters, and creating worlds are the focus.

Prototype of the Character Dialog System
Prototype of the Character Dialog System

Top down map with player and camera indicator
Top down map with player and camera indicator

Checkpoint 4:
I have implemented many more things, including OpenPose visualization, a setup screen, bug fixes, an interactive Cyberpunk demo, and some overall repository restructuring. This is my final submission for the workshop, so I have completely rewritten the overview at the top to now feature an overview of the entire project.



Licensing:
## 2. Instructions for the README

Add a section titled **"Installation & Setup"** to your `README.md` file. You can copy and paste the template below.

---

## 🛠 Installation & Setup

This project uses **Unity 6000.0.5812**. Follow these steps to set up the project locally:

1. **Clone the Repository**
2. **Open in Unity**
    - Open Unity Hub and add the cloned project folder.
    - Open the project. Unity will take a few minutes to regenerate the `Library` folder and download package dependencies (Cinemachine, AI Navigation, etc.) automatically.
3. **Install Third-Party Assets**
    - **Starter Assets - Third Person:** This project requires the [Starter Assets - Third Person Character Controller](https://assetstore.unity.com/packages/essentials/starter-assets-thirdperson-updates-in-new-charactercontroller-pa-196526).
        1. Open the link and add the asset to your Unity account.
        2. In the Unity Editor, go to **Window > Package Manager**.
        3. Select **Packages: My Assets**, find "Starter Assets - Third Person," and click **Download/Import**.
    - **TextMesh Pro:** If prompted by a pop-up regarding TMP Essentials, click **"Import TMP Essentials"**.
4. **Play**
    - Open the scene located at `Assets/Scenes/[YourSceneName].unity` and press Play.