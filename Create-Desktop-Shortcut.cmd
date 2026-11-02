@echo off
setlocal
set "APP=%~dp0WmsPackingAssistant.exe"
if not exist "%APP%" (
  echo Khong tim thay WmsPackingAssistant.exe trong thu muc hien tai.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$exe=$env:APP; $desk=[Environment]::GetFolderPath('Desktop'); if(!(Test-Path $desk)) { throw 'DESKTOP_NOT_FOUND' }; $w=New-Object -ComObject WScript.Shell; $s=$w.CreateShortcut((Join-Path $desk 'WMS In tem chan.lnk')); $s.TargetPath=$exe; $s.WorkingDirectory=[System.IO.Path]::GetDirectoryName($exe); $s.IconLocation=$exe+',0'; $s.Description='WMS Packing & Productivity Assistant - In tem chan'; $s.Save()"
if errorlevel 1 (
  echo Tao shortcut Desktop that bai.
  pause
  exit /b 1
)
echo Da tao shortcut WMS In tem chan tren Desktop voi icon cua EXE.
pause
