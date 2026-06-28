# ReTrak for Emby

ReTrak for Emby is a plugin for Emby Server that automatically scrobbles media playback progress and synchronizes watched history and collections with your ReTrak profile.

## Features

- **Real-Time Scrobbles**: Automatically reports play, pause, resume, and stop events from any Emby client to ReTrak.
- **Library Synchronization**: Syncs movie and show watched history states between Emby and ReTrak.
- **Collection Sync**: Syncs your local digital or physical library to your ReTrak collection.

## Setup Guide

### 1. Get the Plugin

**Option A: Download the release (recommended)**

Download `retrak-emby.dll` from the [latest release](https://github.com/redeuxx/retrak-emby/releases/latest). No compilation required.

**Option B: Build from source**

To compile the plugin yourself, use the dotnet CLI or build with Visual Studio:
```bash
dotnet build ReTrak.sln -c Release
```
This produces a `retrak-emby.dll` file in the build output directory (`ReTrak/bin/Release/netstandard2.0/`).

### 2. Install the Plugin
1. Copy the `retrak-emby.dll` file.
2. Paste it into the `plugins` folder inside your Emby Server data directory.
3. Restart your Emby Server to load the plugin.

### 3. Configuration
1. Open the Emby Server Dashboard.
2. Navigate to **Plugins** in the left sidebar and select the **ReTrak** plugin.
3. Enter your ReTrak server URL (defaults to `https://retrak.tv`).
4. Click **Authenticate** to link your user profile.

### 4. Per-User Configuration (Non-Admin Settings)

Because Emby's core server binaries hardcode the sidebar navigation list, the ReTrak link does not appear directly under the user preferences sidebar. However, standard users can easily configure their own ReTrak settings and API keys.

Instruct your users to log in to Emby and navigate directly to this URL in their browser (replace `<your-emby-server>` with your actual server domain or IP address):

```text
http://<your-emby-server>/web/index.html#!/configurationpage?name=retrakuser
```

This direct link will open the ReTrak user settings interface, where they can configure their personal API keys and sync options independently.
