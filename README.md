# ReTrak for Emby

ReTrak for Emby is a plugin for Emby Server that automatically scrobbles media playback progress and synchronizes watched history and collections with your ReTrak profile.

## Features

- **Real-Time Scrobbles**: Automatically reports play, pause, resume, and stop events from any Emby client to ReTrak.
- **Library Synchronization**: Syncs movie and show watched history states between Emby and ReTrak.
- **Collection Sync**: Syncs your local digital or physical library to your ReTrak collection.

## Setup Guide

### 1. Build the Plugin
To compile the plugin from source, you can use the dotnet CLI or build with Visual Studio:
```bash
dotnet build ReTrak.sln -c Release
```
This produces a `ReTrak.dll` file in the build output directory (`ReTrak/bin/Release/`).

### 2. Install the Plugin
1. Copy the compiled `ReTrak.dll` file.
2. Paste it into the `plugins` folder inside your Emby Server data directory.
3. Restart your Emby Server to load the plugin.

### 3. Configuration
1. Open the Emby Server Dashboard.
2. Navigate to **Plugins** in the left sidebar and select the **ReTrak** plugin.
3. Enter your ReTrak server URL (defaults to `https://retrak.tv`).
4. To configure custom OAuth clients, replace the client credentials in `ReTrakURIs.cs` and `configPage.html` with your registered application credentials.
5. Click **Authenticate** to link your user profile.
