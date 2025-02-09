@echo off
setlocal

cd "%~dp0..\Code"

set version=
set assemblyVersion=

for /f "usebackq delims=: tokens=1,*" %%a in (`"dotnet build RhymeTool\RhymeTool.csproj -c Release -t:VersionMessage"`) do (
    if "%%a" EQU "  Version" (
		set version=%%b
	) else if "%%a" EQU "  AssemblyVersion" (
		set assemblyVersion=%%b
	)
)

if "%version%" == "" goto versionerror
set version=%version: =%
echo Version: %version%
if "%assemblyVersion%" == "" goto versionerror
set assemblyVersion=%assemblyVersion: =%
echo Assembly-Version: %assemblyVersion%
echo.

if exist ..\Build\Publish\wwwroot\ rmdir /Q /S ..\Build\Publish\wwwroot\

dotnet restore ClientWeb\RhymeTool.Client.Web.csproj
dotnet publish ClientWeb\RhymeTool.Client.Web.csproj -c Release -o ..\Build\Publish\

dotnet restore App\RhymeTool.MauiBlazor.csproj
dotnet publish App\RhymeTool.MauiBlazor.csproj -c Release -f net9.0-android
dotnet build App\RhymeTool.MauiBlazor.csproj -c Release -f net9.0-windows10.0.19041.0 -t:CreateInnoSetup

:pack
mkdir ..\Build\Publish\wwwroot\update\version\%assemblyVersion%
copy /B /Y ..\Build\Release\Skinnix.RhymeTool.MauiBlazor\net9.0-android\Net.Skinnix.RhymeTool.MauiBlazor-Signed.apk ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.apk
if exist ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.zip del ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.zip
if exist ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.exe del ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.exe
::tar -a -c -f ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.zip -C ..\Build\Release\Skinnix.RhymeTool.MauiBlazor\net9.0-windows10.0.19041.0\win10-x64 *
copy /B /Y ..\Build\Installer\win-x64\RhymeToolSetup.exe ..\Build\Publish\wwwroot\update\version\%assemblyVersion%\app.exe

echo [Platform:windows]>> ..\Build\Publish\wwwroot\update\check
echo Label=Windows>> ..\Build\Publish\wwwroot\update\check
echo Version=%version%>> ..\Build\Publish\wwwroot\update\check
echo URL=version/%assemblyVersion%/app.exe>> ..\Build\Publish\wwwroot\update\check
echo.>> ..\Build\Publish\wwwroot\update\check
echo [Platform:android]>> ..\Build\Publish\wwwroot\update\check
echo Label=Android>> ..\Build\Publish\wwwroot\update\check
echo Version=%version%>> ..\Build\Publish\wwwroot\update\check
echo URL=version/%assemblyVersion%/app.apk>> ..\Build\Publish\wwwroot\update\check

pause

explorer.exe "%~dp0..\Build\Publish\wwwroot\"
goto :eof


:versionerror
echo Fehler!
echo Konnte Version nicht lesen
echo.
pause
goto :eof