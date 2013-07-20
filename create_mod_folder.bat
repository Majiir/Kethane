@echo off

REM ======== Cleanup ========

REM Start off by cleaning the ModFolder by delteing it
echo Deleting ModFolder...
rmdir ModFolder /S /Q

REM ======== Sanity Check ========

REM Check if the KethaneReleaseFolder folder is created...
IF EXIST KethaneReleaseFolder (GOTO :RELEASESUCCESS)
:RELEASEERROR
echo ERROR: KethaneReleaseFolder not found, check build instructions in README.md
GOTO :EXIT
:RELEASESUCCESS

REM Check if the project has been built as Release...
IF EXIST Plugin\bin\Release\MMI_Kethane.dll (GOTO :BUILDSUCCESS)
echo ERROR: MMI_Kethane.dll not found, check build instructions in README.md
GOTO :EXIT
:BUILDSUCCESS

REM ======== Project File Copying ========

REM And now create the folder again!
echo.
echo Creating ModFolder...
mkdir ModFolder

REM Now copy the Parts folder into it...
echo.
echo Copying Parts...
xcopy Parts ModFolder\Parts /e /i /q

REM Now the DLL...
echo.
echo Copying DLL...
mkdir ModFolder\Plugins
copy Plugin\bin\Release\MMI_Kethane.dll /B ModFolder\Plugins\MMI_Kethane.dll

REM And a few Misc files...
echo.
echo Copying Misc Files...
xcopy Resources ModFolder\Resources /e /i /q
copy Grid.cfg ModFolder\Grid.cfg
copy mote.png ModFolder\mote.png
copy smoke.jpg ModFolder\smoke.jpg

REM ======== Additional File Copying ========

REM And the files not in the KethaneReleaseFolder...
echo.
echo Copying Additional Files from KethaneReleaseFolder...
xcopy KethaneReleaseFolder\*.wav ModFolder /e /i /q
xcopy KethaneReleaseFolder\*.mu ModFolder /e /i /q
xcopy KethaneReleaseFolder\*.mbm ModFolder /e /i /q

REM ======== Finished Message ========

REM Let the user know nothing went wrong!
echo.
echo If no errors occured the Mod Folder has been successfully created!
GOTO :EXIT

REM ======== End of Script ========

:EXIT
REM Make sure the script never ends abruptly.
pause