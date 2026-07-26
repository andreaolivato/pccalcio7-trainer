@echo off
REM Build the trainer with the C# compiler that ships inside Windows.
REM No SDK, no Visual Studio, no NuGet, no internet. Produces a standalone .exe.

setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find csc.exe. This needs .NET Framework 4.x, which ships with
    echo Windows 8 and later. On Windows 7, install .NET Framework 4 first.
    exit /b 1
)

if not exist "%~dp0dist" mkdir "%~dp0dist"

set REFS=/reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll
REM Wildcards, so adding a translation file needs no change here.
set SRC="%~dp0src\*.cs" "%~dp0src\lang\*.cs"

REM /platform:x86 keeps the 32-bit MEMORY_BASIC_INFORMATION layout valid, and the
REM game is 32-bit anyway. /codepage:65001 so accented translations survive.
REM Both entry points are in the wildcard, so name the one we want each time.
"%CSC%" /nologo /codepage:65001 /target:winexe /platform:x86 /optimize+ ^
    /main:PcCalcio7Trainer.MainForm ^
    /out:"%~dp0dist\PcCalcio7Trainer.exe" %REFS% %SRC%
if errorlevel 1 goto fail

REM Console harness that runs the same searches, for checking against a live game.
"%CSC%" /nologo /codepage:65001 /target:exe /platform:x86 /optimize+ ^
    /main:PcCalcio7Trainer.SelfTest /out:"%~dp0dist\SelfTest.exe" %REFS% %SRC%
if errorlevel 1 goto fail

echo.
echo Built dist\PcCalcio7Trainer.exe   (the only file you need to ship)
echo Built dist\SelfTest.exe           (diagnostics, optional)
exit /b 0

:fail
echo.
echo BUILD FAILED
exit /b 1
