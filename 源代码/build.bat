@echo off
rem ? Windows ??? C# ??????? ClaudeDeskTopPet.exe(?????????)
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /win32icon:pet.ico /out:..\ClaudeDeskTopPet.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ClaudePet.cs
if errorlevel 1 (echo Build failed & pause) else (echo Done: ..\ClaudeDeskTopPet.exe)