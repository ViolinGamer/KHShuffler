# ?? Twitch Settings Troubleshooting Guide

## ? **Issue Fixes Applied:**

### **1. Settings Not Persisting - FIXED**
- ? **Added auto-save** - Settings save immediately when you change them
- ? **Added SaveAllSettings()** method to force registry persistence  
- ? **Added real-time sync** - No need to manually save anymore

### **2. Max Effects Cap Removed - FIXED**
- ? **Increased maximum** from 20 to 1000 effects per event
- ? **Essentially unlimited** effects for large gift sub events

### **3. Test Gift Subs/Single Subs Not Working - FIXED**  
- ? **Fixed event routing logic** - Properly handles bits vs subs
- ? **Added enabled effects check** - Shows error if no effects enabled
- ? **Enhanced debug logging** - Shows exactly what's happening

### **4. Authentication Not Persisting - FIXED**
- ? **Auto-save credentials** - Client ID/Secret saved on text change
- ? **Auto-save tokens** - Access tokens persist between sessions
- ? **Force registry save** - Manual save button now works properly

---

## ?? **Testing Your Fixes:**

### **Step 1: Test Settings Persistence**
1. **Open Twitch Settings**
2. **Change any duration** (e.g., Tier 1 from 15 to 20 seconds)
3. **Close settings** without clicking Save
4. **Reopen Twitch Settings** 
5. **? Should show 20 seconds** (auto-saved)

### **Step 2: Test Max Effects**
1. **Set "Max effects per event"** to something high like 50
2. **Test Gift Subs** with count 10
3. **? Should trigger 10 effects** (no longer capped at 20)

### **Step 3: Test Gift Subs/Single Subs**
1. **Enable some effects** (check the boxes)
2. **Test Single Sub** 
3. **? Should trigger 1 effect**
4. **Test Gift Subs** with count 5
5. **? Should trigger 5 effects** with 500ms spacing

### **Step 4: Test Authentication Persistence**
1. **Enter Client ID and Secret**
2. **Close settings** 
3. **Reopen settings**
4. **? Credentials should still be there**

---

## ?? **Debugging Tools:**

### **Check Debug Output**
Look for these messages in Visual Studio Debug Output:
```
TwitchSettingsForm: Auto-saved settings to registry
EffectManager.HandleTwitchSubscription: User=TestUser, Tier=Tier1, Count=5
EffectManager: Duration=15s, Effects found=3
?? TestUser triggered 3 effects with 5x Tier1 sub(s)!
```

### **Registry Verification**
Settings are stored in Windows Registry at:
`HKEY_CURRENT_USER\Software\BetterGameShuffler`

You can check manually with `regedit` if needed.

### **Common Issues & Solutions**

#### **? "No enabled effects available"**
**Solution**: Go to Twitch Settings ? Enable some effect checkboxes

#### **? Settings reset on restart**  
**Solution**: The auto-save should fix this. If not, check Debug Output for registry errors.

#### **? Test buttons do nothing**
**Solution**: Check Debug Output. Likely no effects enabled or EffectManager not initialized.

#### **? Max effects still capped at 20**
**Solution**: The UI now allows up to 1000. If you had 20 before, change it to a higher number.

---

## ?? **Expected Behavior After Fixes:**

### **? Real-Time Auto-Save**
- **Duration changes** ? Saved immediately  
- **Effect enable/disable** ? Saved immediately
- **Credentials** ? Saved immediately
- **Authentication tokens** ? Saved immediately

### **? Unlimited Effects**
- **Max effects** ? Up to 1000 per event
- **Gift subs** ? Each sub triggers one effect (up to max)
- **Spacing** ? 500ms delay between effects (configurable)

### **? Working Test Buttons**
- **Test Single Sub** ? 1 effect, configured duration
- **Test Gift Subs** ? Multiple effects with spacing  
- **Test Bits** ? 1 effect, duration based on bits amount

### **? Persistent Authentication**
- **Client ID/Secret** ? Saved in registry, encrypted
- **Access tokens** ? Saved and restored on restart
- **Connection status** ? Maintained between sessions

---

## ?? **Summary of Changes:**

1. **Auto-save everything** - No more lost settings
2. **Unlimited effects** - No artificial caps  
3. **Fixed test routing** - Gift subs and single subs work
4. **Better debugging** - Clear error messages
5. **Persistent auth** - Credentials saved securely

Your Twitch integration should now work flawlessly! ??