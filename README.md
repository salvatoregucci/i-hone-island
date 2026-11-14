# iLand

A small WPF Dynamic Island-style desktop widget for Windows. The compact capsule expands on hover and provides clock/date, system utilization, and available hardware sensor readings. Sensor availability depends on the hardware and drivers.

## Requirements

- Windows 10/11
- .NET 8 SDK

## Run locally

```powershell
dotnet restore
dotnet run
```

## Build

```powershell
dotnet build -c Release -p:Platform=x64
dotnet build -c Release -p:Platform=x86
```

## Publish self-contained single-file executables

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PlatformTarget=x64 -o publish/win-x64
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PlatformTarget=x86 -o publish/win-x86
```

The executable is written to the corresponding `publish/win-x64` or `publish/win-x86` directory. Run as administrator if your hardware sensors require elevated access.
