# ?? KHShuffler Twitch Effects System

## ?? Overview

The Twitch Effects system adds viewer interactivity to your Kingdom Hearts shuffling streams! Viewers can trigger visual and gameplay effects through subscriptions and bit donations, making your stream more engaging and entertaining.

## ?? Quick Setup

1. **Add Media**: Place your files in the appropriate folders:
   - `images/` - For random moving images (PNG, JPG, GIF)
   - `sounds/` - For sound effects (WAV files recommended)
   - `hud/` - For static HUD overlays and custom HUD hide images
2. **Test Locally**: Use the "Test Effects" button to verify everything works
3. **Go Live**: Connect to Twitch and let viewers trigger effects!

##  Available Effects

###  **Chaos Shuffling** 
- **Effect**: Switches between games every 5 seconds

###  **Chaos Image**
- **Effect**: Random image bounces around the screen, trying to distract you from the gameplay

### **Game Ban**
- **Effect**: Temporarily removes a random game from rotation

###  **Color Chaos**
- **Effect**: Random color filter overlay

###  **Random Sound**
- **Effect**: Plays random sound with name display

###  **HUD Overlay**
- **Effect**: Shows random HUD image overlay

###  **Mirror Mode**
- **Effect**: Horizontal screen mirroring

##  Test Mode

Access test mode via the **"Test Effects"** button to:

- Test all effects locally without Twitch connection
- Adjust effect duration (1-300 seconds)
- Choose between **Stack Effects** (multiple simultaneous) or **Queue Effects** (one at a time)
- View real-time test logs
- Verify media files are working correctly

## Media File Setup

### Images Folder (`images/`)
- **Supported formats**: PNG, JPG, GIF
- **Purpose**: Random moving images for Chaos Emote effect
- **Recommended**: Emotes, memes, character images, animated reactions
- **Size**: Keep under 500x500px for best performance
- **Animation Support**: 
  - **GIF**: Full animation support while moving around screen
  - **PNG/JPG**: Static images with transparency (PNG) or photo quality (JPG)

### Sounds Folder (`sounds/`)
- **Supported formats**: WAV, MP3
- **Purpose**: Random sound effects for Random Sound
- **Recommended**: Short clips (1-10 seconds)
- **Format details**:
  - **WAV**: Best compatibility, uncompressed
  - **MP3**: Good compression, widely supported

### HUD Folder (`hud/`)
- **Supported formats**: PNG, JPG, GIF
- **Purpose**: Static overlays and HUD hiding
- **Animation Support**:
  - **GIF**: Full animation support during overlay duration
  - **PNG/JPG**: Standard overlay formats

## Effect Customization

### Effect Timing Configuration
Each effect has configurable:
- **Bits required**: Minimum bits for trigger
- **Subscriptions required**: Minimum subs for trigger  
- **Duration**: How long the effect lasts
- **Enabled state**: Can be disabled individually

## Technical Details

### Effect Stacking vs Queuing
- **Stack Mode**: Multiple effects can run simultaneously
- **Queue Mode**: Effects wait in line and run one at a time
- **Same Effect**: Never stacks (same effect type replaces previous)

## Troubleshooting

### Effects Not Working
1. Check that effect folders exist and contain files
2. Verify "Enable Effects" checkbox is checked
3. Test individual effects in Test Mode first
4. Check Debug Output for error messages

### Images Not Showing  
1. Ensure images are in supported formats (PNG, JPG, GIF)
2. Check file permissions (not read-only)
3. Try smaller image sizes if performance issues

### Sounds Not Playing
1. Use WAV format for best compatibility, MP3 for balance of quality/size, 
2. Check Windows audio settings
3. Verify sound files aren't corrupted
4. Test with simple/short sound files first
5. Supported formats: WAV, MP3
6. Check Debug Output for audio-specific error messages

