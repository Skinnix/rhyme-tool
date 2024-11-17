@echo off
setlocal

cd "%~dp0..\Code"

set version=

for /f "usebackq delims=: tokens=1,*" %%a in (`"dotnet build RhymeTool\RhymeTool.csproj -c Release -t:VersionMessage"`) do (
    if "%%a" EQU "  Version" (
		set version=%%b
	)
)

if "%version%" == "" goto versionerror
set version=%version: =%
echo Version: %version%
echo.

dotnet restore ClientWeb\RhymeTool.Client.Web.csproj
dotnet publish ClientWeb\RhymeTool.Client.Web.csproj -c Release -o ..\Build\Publish\

dotnet restore App\RhymeTool.MauiBlazor.csproj
dotnet publish App\RhymeTool.MauiBlazor.csproj -c Release -f net9.0-android
dotnet publish App\RhymeTool.MauiBlazor.csproj -c Release -f net9.0-windows10.0.19041.0

copy /B /Y ..\Build\Release\net9.0-android\Net.Skinnix.RhymeTool.MauiBlazor-Signed.apk ..\Build\Publish\wwwroot\update\%version%\app.apk
tar -a -c -f ..\Build\Publish\wwwroot\update\%version%\app.zip -C ..\Build\Release\net9.0-windows10.0.19041.0\win10-x64 *

echo windows=app.zip > ..\Build\Publish\wwwroot\update\newest
echo android=app.apk >> ..\Build\Publish\wwwroot\update\newest
echo %version% >> ..\Build\Publish\wwwroot\update\newest

pause

explorer.exe "%~dp0..\Build\Publish\wwwroot\"
goto :eof


:versionerror
echo Fehler!
echo Konnte Version nicht lesen
echo.
pause
goto :eof