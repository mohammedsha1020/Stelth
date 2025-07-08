# Stealth Development Instructions

## Project Overview
Building a C#/.NET remote control and screen sharing application with the following key components:
- **Stealth.Shared**: Common protocol, encryption, and trusted code logic
- **Stealth.PC**: Windows WPF application for PC-side functionality
- **Stealth.Mobile**: .NET MAUI Android application for mobile-side functionality

## Current Development Status
- ✅ Phase 1: Project Setup - COMPLETED
  - ✅ Created solution structure with 3 projects
  - ✅ Implemented shared protocol, encryption, and trusted code management
  - ✅ Created PC application with WPF UI and core functionality
  - ✅ Created Mobile application with MAUI framework
  - ✅ Basic connection management and screen capture foundation
- 🔄 Phase 2: Screen Sharing (PC → Mobile) - IN PROGRESS
  - ✅ PC screen capture implementation
  - ✅ Basic UI for connection management
  - ⏳ Network streaming optimization
  - ⏳ Mobile screen rendering
- ⏳ Phase 3: Remote Input Control (Mobile → PC) - READY TO START
- ⏳ Phase 4: Screen Sharing (Mobile → PC) - Pending
- ⏳ Phase 5: Remote Input (PC → Mobile) - Pending
- ⏳ Phase 6: Persistent Connection (Trusted Code) - Pending
- ⏳ Phase 7: Auto Start on Boot - Pending
- ⏳ Phase 8: Security Features - Pending
- ⏳ Phase 9: Optional UI - Pending
- ⏳ Phase 10: Testing & Optimization - Pending

## Build Commands
```bash
# Build the entire solution
dotnet build

# Build individual projects
cd Shared && dotnet build
cd PC && dotnet build  # Note: Requires Windows for WPF
cd Mobile && dotnet build -f net8.0-android  # Requires Android SDK

# Run PC Test Server (Cross-platform)
dotnet run --project PCTest/PCTest.csproj

# Run Mobile Simulator (Cross-platform)
dotnet run --project MobileTest/MobileTest.csproj simulator

# Run Mobile Tests
dotnet run --project MobileTest/MobileTest.csproj

# Run PC application (Windows only)
cd PC && dotnet run

# Run Mobile application (requires Android environment)
cd Mobile && dotnet build -f net8.0-android
```

## Quick Start Guide

### 🖥️ **PC Side**
```bash
# 1. Start PC server
dotnet run --project PCTest/PCTest.csproj

# 2. Note the displayed:
#    - Device ID
#    - Trusted Code (copy this for mobile)
#    - Server listening on port 7890
```

### 📱 **Mobile Side** 
```bash
# Option A: Mobile Simulator (for testing)
dotnet run --project MobileTest/MobileTest.csproj simulator

# Option B: Real Android app (requires Android SDK)
dotnet build Mobile/Stealth.Mobile.csproj -f net8.0-android
dotnet run --project Mobile/Stealth.Mobile.csproj -f net8.0-android
```

### 🔗 **Connection Process**
1. Start PC server → Get trusted code
2. Start mobile app/simulator
3. Enter PC IP address (127.0.0.1 for same machine)
4. Enter the trusted code from PC
5. Connect and test screen sharing

## Development Environment Setup
### For PC Development:
- Windows 10/11 (required for WPF)
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### For Mobile Development:
- .NET 8.0 SDK with MAUI workload
- Android SDK
- Visual Studio 2022 with MAUI workload or VS Code with .NET MAUI extension

### For Cross-Platform Development:
- The Shared project can be built on any platform
- Linux/macOS can be used for shared component development

## Prerequisites
- .NET 7.0 SDK
- Visual Studio or VS Code with C# extension
- Android SDK (for mobile development)
- Windows 10/11 (for PC testing)

## Architecture Notes
- Using TCP sockets for communication
- AES-256 encryption for all data transmission
- Shared protocol definitions in Stealth.Shared
- Modular design for easy testing and maintenance

## Security Considerations
- All communication encrypted with AES-256
- Trusted code authentication system
- No prompts for silent operation
- Secure storage of authentication codes

Last Updated: July 8, 2025
