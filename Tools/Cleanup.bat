@echo off

cd "%~dp0..\Code"

rmdir /Q /S ..\Build
For /F "tokens=*" %%a in ('dir /S /B /AD obj') do rmdir /Q /S "%%a"

pause