@echo off

cd "%~dp0..\Code"

dotnet restore ClientWeb\RhymeTool.Client.Web.csproj
dotnet publish ClientWeb\RhymeTool.Client.Web.csproj -c Release -o ..\Build\Publish\

dotnet restore App\RhymeTool.MauiBlazor.csproj
dotnet publish App\RhymeTool.MauiBlazor.csproj -c Release -f net9.0-android

copy /B /Y ..\Build\Release\net9.0-android\Net.Skinnix.RhymeTool.MauiBlazor-Signed.apk ..\Build\Publish\wwwroot\app.apk

pause

explorer.exe "%~dp0..\Build\Publish\wwwroot\"