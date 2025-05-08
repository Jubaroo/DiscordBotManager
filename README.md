# Discord Bot Manager

A simple WPF desktop application for Windows that allows you to manage multiple Discord bots (Node.js) from a single interface. Features include starting, stopping, automatic restarting on crash, log viewing with ANSI color support, and running in the system tray.

## Features
- Add any folder containing a Discord.js (or similar) bot  
- Start and stop individual bots or all bots at once  
- Automatic restart when a bot crashes 🔄  
- Real-time log display with ANSI color parsing for Info, Error, Debug, Warning, etc. :contentReference[oaicite:0]{index=0}:contentReference[oaicite:1]{index=1}  
- Minimizes to system tray with Show/Hide and Exit options :contentReference[oaicite:2]{index=2}:contentReference[oaicite:3]{index=3}  
- Configuration is persisted between runs (`bots.json` in `%APPDATA%`) :contentReference[oaicite:4]{index=4}:contentReference[oaicite:5]{index=5}  

## Prerequisites
- [.NET SDK 8.0](https://dotnet.microsoft.com/download) (configured via `global.json`)
- [Node.js](https://nodejs.org/) (for running your Discord bots)

## Installation
1. Clone this repository:  
   ```bash
   git clone https://github.com/yourusername/DiscordBotManager.git
   cd DiscordBotManager

2. Build the project in Visual Studio 2022 (or later) or via the .NET CLI:
   ```bash
   dotnet build

3. (Optional) Publish a self-contained executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true   

## Usage 
1. Launch the DiscordBotManager.exe.
2. Click Add Bot and select the folder that contains your bot (must have index.js or a package.json with a start script).
3. Use Start, Stop, or Start All/Stop All to control your bots.
4. View logs in real-time with colored output.
5. Close the window to minimize to tray. Right-click the tray icon to Show/Hide or Exit.

## Configuration
The list of added bot folders is saved in %APPDATA%\DiscordBotManager\bots.json. You can manually edit this file if needed 
