# Blockbench Unity Bridge Plugin
This plugin enables automatic GLTF export from Blockbench when Unity imports `.bbmodel` files.
## Installation
### In Blockbench:
1. Open Blockbench
2. Go to **File > Plugins** (or press `Ctrl+Shift+P`)
3. Click **Load Plugin from File**
4. Navigate to and select `unity_bridge.js`
5. The plugin will automatically start a local server
## Usage
### Automatic Export (Unity triggers Blockbench)
1. Make sure Blockbench is running with the Unity Bridge plugin
2. Have your `.bbmodel` file open in Blockbench
3. When Unity imports the same `.bbmodel` file, Blockbench will auto-export a `.gltf`
### Manual Export
- **Blockbench**: Press `Ctrl+Shift+B` to force export current model as GLTF
- **Unity**: Press `Ctrl+Shift+B` or go to **Tools > Blockbench > Force Export GLTF**
## Configuration
- **Blockbench**: File > Export > Unity Bridge Settings (default port: 29405)
- **Unity**: Tools > Blockbench > Settings
## Troubleshooting
- Make sure Blockbench is running with the plugin loaded
- The bbmodel file must be open in Blockbench for auto-export to work
- Check port settings match between Blockbench and Unity
