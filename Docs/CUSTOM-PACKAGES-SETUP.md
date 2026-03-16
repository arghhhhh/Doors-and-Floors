# Project Setup

## Manual Package Installs

### App UI (com.unity.dt.app-ui 2.2.0-pre.6)

This pre-release version of App UI cannot be installed via UPM in Unity 2022.3. It must be manually downloaded and placed in the `Packages/` folder.

1. Download `com.unity.dt.app-ui-2.2.0-pre.6` from the [Unity Package Registry](https://packages.unity.com/)
2. Extract/copy it to `Packages/com.unity.dt.app-ui-2.2.0-pre.6/`
3. Unity will detect it as an embedded package on next editor reload

### ZED SDK Plugin

The ZED SDK plugin is installed via UPM from a custom fork: https://github.com/arghhhhh/zed-unity

This is already configured in `Packages/manifest.json` and will be resolved automatically.
