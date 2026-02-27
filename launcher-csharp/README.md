# N14 C# Launcher

Native Windows launcher (C# WinForms) to register accounts against your N14 API and open the website login.

Features:
- Register account via `POST /api/register`
- `Use PC User` button to autofill username from Windows username
- Open website button

## Build (developer machine)

Requirements:
- .NET SDK 8+

Commands:
```powershell
cd launcher-csharp
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output exe path:
`launcher-csharp\bin\Release\net8.0-windows\win-x64\publish\N14Launcher.exe`

This publish mode creates a standalone exe so end users do not need Node.js or .NET runtime installed.
