@echo off
REM Сборка: откройте "x64 Native Tools Command Prompt for VS" (Visual Studio Build Tools) и запустите этот файл.
cl /nologo /EHsc /O2 /std:c++17 /DUNICODE /D_UNICODE main.cpp /Fe:VideoWallpaper.exe ^
  /link /SUBSYSTEM:WINDOWS user32.lib shell32.lib gdi32.lib ole32.lib advapi32.lib comdlg32.lib mfplat.lib mfplay.lib mfuuid.lib
if %errorlevel% neq 0 (echo Сборка не удалась & exit /b 1)
echo Готово: VideoWallpaper.exe
