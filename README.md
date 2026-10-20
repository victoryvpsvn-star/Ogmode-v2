# OGMode
A tool to optimize Windows for gaming. One-click game mode for desktops and laptops.


[🇻🇳 Xem bản tiếng Việt](./README_VIE.md) | [🇬🇧 View English version](./README.md)


With just one click, OGMode applies pre-configured tweaks to make low-end PCs run games more smoothly. 🚀

**Main page:** [OGMode on Osteup](https://osteup.com/ogmode/)

Built by **Osteup** using **C#** and **Batch scripting** for system optimizations.

![OGMode Screenshot](https://github.com/SiroCandy06/OGMode/assets/101639160/e50f465b-47b9-4b1a-9c4d-c6fdafaff1c5)

## Features
- **One-click Game Mode**: disables visual effects, switches to a High performance power plan, enables Windows Game Mode, disables Game DVR, stops unneeded background services, frees RAM
- **Exact undo**: OGMode backs up original values (`%AppData%\OGMode\state.json`) and restores only what it changed, even after restarting the app
- **Junk cleaner**: scan first, see the size of each item, then clean (temp files, Windows Update cache, browser caches, crash dumps/error reports, thumbnail cache, shader caches, Recycle Bin)
- **Deep RAM clean**: trims working sets + purges the standby list
- **Quick tools**: flush DNS, restart Explorer, restore defaults

## Drawbacks
- UI and graphics may degrade (reduces system load for better performance)
- **Beta version** under testing

## Requirements
OGMode works best for low-end systems. Windows 10/11 64-bit.
- **[.NET Desktop Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** (.NET 8 reaches end of support on Nov 10, 2026)
- **[Download & Install OGMode](https://github.com/SiroCandy06/OGMode/releases)**

## How to Use
1. Download and install OGMode
2. Run OGMode (it requests **Administrator** automatically)
3. Use **Game Mode**, **Tools** and **Junk Cleaner** to optimize your system

## Build from source
```
dotnet build ogmm.csproj -c Release
```
Requires the .NET 10 SDK. If you open it in Visual Studio, run VS as Administrator (the app requires elevation).

### Run all tests
```
dotnet test OGMode.slnx -c Release
```
Locally, only the safe tests run: tests that change the real system (Game Mode on/off, registry, power plan, services, junk cleaning on real folders) are skipped. They run only on the throwaway CI VM (`OGMODE_INTEGRATION=1`).

### CI (GitHub Actions)
Go to **Actions -> Build & Test -> Run workflow**. It automatically restores, builds, runs every test, publishes a single-file `ogmm.exe`, smoke-tests it, and uploads the results as artifacts. Tick **release** (or push a `v*` tag) to also create a GitHub Release.

![OGMode Interface](https://github.com/SiroCandy06/OGMode/assets/101639160/445d2bcc-2220-4553-bc5f-73528130fd8f)

## Version History
| Version  | Date       | Updates |
|----------|------------|-----------------------------------------------------------------------------------------------------------------------------------|
| **v2.0** | 10/2026 | Upgraded to .NET 10, new Junk Cleaner page, deep RAM clean, power plan / Game Mode / Game DVR tweaks, exact backup & undo, safer service list (no longer stops Firewall/DNS/EventLog/Themes), many bug fixes (crash on Intro page, UI threading, leaked tasks). |
| **v1.4** | 23/06/2024 | Fixed slow loading, added update check, improved Game Mode, RAM cleanup tool, new system tweaks, explorer restart button. |
| **v1.3** | 06/03/2024 | Fixed Roblox crashes, better browser & YouTube performance, explorer fixes, new cleanup tool, Google DNS for lower ping. |
| **v1.2** | 19/01/2024 | Fixed missing images in Explorer, added RAM cleaner, Bluetooth disable option, improved UI, fixed YouTube lag. |
| **v1.1** | 12/01/2024 | Fade-in/out effects, performance optimizations, navigation bug fix, added Windows service optimizations. |
| **v1.0** | 03/01/2024 | First C# version, introduced Game Mode, increased FPS, upgraded from Batch to C#. |

For earlier batch script versions (v0.1 -> v0.5), see [here](https://osteup.com/tool-ogmode-fix-lag-windows-toi-uu-hoa-may-tinh/).

## Support
Join our **Discord community** or contact us via the "Contact" section.

![Support](https://github.com/SiroCandy06/OGMode/assets/101639160/504aeffc-4e15-4d63-8c31-ceeec8c700fe)
