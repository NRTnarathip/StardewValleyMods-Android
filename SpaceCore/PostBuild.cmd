set AppName=abc.smapi.gameloader

adb shell am force-stop %AppName%

if errorlevel 1 (
    echo ADB is not connected.
    exit /b
)

adb push "bin/Release/SpaceCore.dll" "/storage/emulated/0/Android/data/%AppName%/files/Mods/SpaceCore"

adb shell am start %AppName%"/crc64e91f1276c636690c.LauncherActivity"
