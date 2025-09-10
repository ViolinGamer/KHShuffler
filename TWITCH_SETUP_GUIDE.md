#  KHShuffler Twitch Integration Setup Guide

##  **NEW: No Code Editing Required!**

KHShuffler now lets you enter your Twitch app credentials directly in the program interface - **no need to edit source code!** This keeps your credentials private and makes the setup much easier.

##  Quick Setup Overview

1. **Create Twitch App** (one-time, 2 minutes)
2. **Enter Credentials in KHShuffler** (30 seconds)
3. **Connect and Test** (30 seconds)

**Total time: ~3 minutes** 

---

## ?? **Step-by-Step Setup**

### Step 1: Create Your Twitch Application

1. **Open KHShuffler**  Go to **"Test Effects"** Click **" Twitch Settings"**
2. **Click " Create Twitch App"** (this opens https://dev.twitch.tv/console)
3. **Click " Copy OAuth URL"** to copy the redirect URL to your clipboard
4. **Log in** with your Twitch account
5. **Click "Create an App"**
6. **Fill out the form**:
   - **Name**: `KHShuffler-YourUsername` (replace with your username)
   - **OAuth Redirect URLs**: **Paste the copied URL** (`http://localhost:3000/auth/callback`)
   - **Category**: `Game Integration`
7. **Click "Create"**

### Step 2: Get Your Credentials  

1. **Click on your newly created app** in the Twitch console
2. **Copy the Client ID** (long string like `abc123def456...`)
3. **Click "New Secret"** to generate a Client Secret
4. **Copy the Client Secret** (another long string)

### Step 3: Enter Credentials in KHShuffler

1. **Go back to KHShuffler**  **" Twitch Settings"**
2. **Check "Enable Twitch Integration"**
3. **Enter your channel name** (your Twitch username)
4. **Paste your Client ID** in the Client ID field
5. **Paste your Client Secret** in the Client Secret field
6. **You should see " Ready to connect"**

### Step 4: Connect and Test

1. **Click "Connect to Twitch"**
2. **Your browser opens**  **Click "Authorize"** on Twitch
3. **Copy the callback URL** and paste it back into KHShuffler
4. **You should see " Connected as YourUsername"**
5. **Click "Save Settings"**
6. **Test with the simulation buttons!**

---

##  **Security & Privacy Benefits**

###  **Your Credentials Stay Private**
- **No source code editing** required
- **Credentials stored locally** on your computer only
- **Not shared** when you share the program
- **Encrypted storage** in Windows Registry

###  **Easy Distribution**
- **Share KHShuffler** without exposing your credentials
- **Each user** enters their own Twitch app details
- **No security risks** from sharing executables
- **Professional approach** for public releases

---

##  **Available Features**

### **Current Features:**
- ** Secure OAuth Authentication** - Real Twitch login
- ** Effect Testing** - Simulate subs, bits, gift subs
- ** Full Configuration** - Enable/disable effects, set durations
- ** 7 Different Effects** - Chaos, images, sounds, HUD, etc.
- ** Multi-Effect Support** - Gift subs trigger multiple effects
---

##  **Troubleshooting**

### "Enter your Twitch app credentials above"
- **Solution**: Enter your Client ID and Client Secret in the text fields
- **Check**: Make sure you created a Twitch app first

### "Invalid credentials - check Client ID and Secret"  
- **Solution**: Double-check your Client ID and Secret for typos
- **Check**: Make sure there are no extra spaces
- **Try**: Generate a new Client Secret if needed

### "Need Credentials" (button disabled)
- **Solution**: Fill in both Client ID and Client Secret fields
- **Check**: Both fields must have values before connecting

### "Token exchange failed"
- **Solution**: Verify your redirect URI is exactly: `http://localhost:3000/auth/callback`
- **Check**: No trailing slashes or extra characters in Twitch app settings

### Browser doesn't open
- **Solution**: Copy the URL manually from any error dialog
- **Try**: Run KHShuffler as administrator
- **Check**: Windows firewall settings

---

##  **Pro Tips**

###  **For Streamers:**
- **Test everything** before going live
- **Configure effect durations** to match your stream style  
- **Enable variety** - mix different effect types
- **Save backup** of your Client ID (write it down)

###  **For Setup:**
- **Use descriptive app name** like `KHShuffler-YourChannel`
- **Keep credentials safe** - don't share screenshots with them visible
- **Test with small amounts** first (like 1 gift sub simulation)

###  **For Effects:**
- **Add custom images/sounds** to the effect folders
- **Adjust timing** based on your game speed
- **Consider your audience** - some effects might be too chaotic for certain game


---

Congratulations! Your Twitch integration is now properly configured and ready for streaming. Your viewers will love the interactive effects!