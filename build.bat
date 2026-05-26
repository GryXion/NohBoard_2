@echo off
rem ============================================================================
rem  NohBoard build / test / publish helper
rem
rem  Run this from the repository root. All paths are RELATIVE so it works for
rem  anyone who clones the project, no matter where they put it.
rem
rem  Requirements:
rem    - .NET 10 SDK on PATH (https://dotnet.microsoft.com/download)
rem    - Windows 10 / 11 x64
rem
rem  Usage:
rem    build.bat                 - restore, build, run tests, publish both EXEs
rem    build.bat build           - restore + Release build (no tests, no publish)
rem    build.bat test            - restore + Release build + run unit tests
rem    build.bat publish         - publish self-contained AND framework-dependent
rem    build.bat publish-sc      - publish only the self-contained single-file EXE
rem    build.bat publish-fd      - publish only the framework-dependent EXE
rem    build.bat run             - run NohBoard from sources (dotnet run)
rem    build.bat clean           - remove bin/ and obj/ from every project
rem    build.bat help            - show this message
rem ============================================================================

setlocal EnableExtensions EnableDelayedExpansion

rem --- Anchor to the directory containing this script, no matter the cwd. ----
pushd "%~dp0" >nul

set "SOLUTION=NohBoard\NohBoard.sln"
set "APP_PROJECT=NohBoard\NohBoard\NohBoard.csproj"
set "TEST_PROJECT=NohBoard\NohBoard.Tests\NohBoard.Tests.csproj"
set "PUBLISH_OUT=NohBoard\NohBoard\bin\Release\publish"
set "CONFIG=Release"

rem --- Pre-flight: make sure dotnet is available. --------------------------
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] 'dotnet' was not found on PATH.
    echo         Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    goto :fail
)

set "TARGET=%~1"
if "%TARGET%"=="" set "TARGET=all"

if /i "%TARGET%"=="help"        goto :show_help
if /i "%TARGET%"=="-h"          goto :show_help
if /i "%TARGET%"=="--help"      goto :show_help
if /i "%TARGET%"=="/?"          goto :show_help

if /i "%TARGET%"=="all"         goto :do_all
if /i "%TARGET%"=="build"       goto :do_build
if /i "%TARGET%"=="test"        goto :do_test
if /i "%TARGET%"=="publish"     goto :do_publish
if /i "%TARGET%"=="publish-sc"  goto :do_publish_sc
if /i "%TARGET%"=="publish-fd"  goto :do_publish_fd
if /i "%TARGET%"=="run"         goto :do_run
if /i "%TARGET%"=="clean"       goto :do_clean

echo [ERROR] Unknown target '%TARGET%'.
echo.
goto :show_help

rem ============================================================================
:do_all
call :step "Restoring + building (%CONFIG%)"
dotnet build "%SOLUTION%" -c %CONFIG% --nologo || goto :fail

call :step "Running unit tests"
dotnet test "%SOLUTION%" -c %CONFIG% --nologo --no-build || goto :fail

call :step "Publishing self-contained single-file EXE"
call :publish_profile win-x64-selfcontained || goto :fail

call :step "Publishing framework-dependent single-file EXE"
call :publish_profile win-x64-framework-dependent || goto :fail

call :report_artifacts
goto :ok

rem ============================================================================
:do_build
call :step "Restoring + building (%CONFIG%)"
dotnet build "%SOLUTION%" -c %CONFIG% --nologo || goto :fail
goto :ok

rem ============================================================================
:do_test
call :step "Restoring + building (%CONFIG%)"
dotnet build "%SOLUTION%" -c %CONFIG% --nologo || goto :fail

call :step "Running unit tests"
dotnet test "%SOLUTION%" -c %CONFIG% --nologo --no-build || goto :fail
goto :ok

rem ============================================================================
:do_publish
call :step "Publishing self-contained single-file EXE"
call :publish_profile win-x64-selfcontained || goto :fail

call :step "Publishing framework-dependent single-file EXE"
call :publish_profile win-x64-framework-dependent || goto :fail

call :report_artifacts
goto :ok

rem ============================================================================
:do_publish_sc
call :step "Publishing self-contained single-file EXE"
call :publish_profile win-x64-selfcontained || goto :fail
call :report_artifacts
goto :ok

rem ============================================================================
:do_publish_fd
call :step "Publishing framework-dependent single-file EXE"
call :publish_profile win-x64-framework-dependent || goto :fail
call :report_artifacts
goto :ok

rem ============================================================================
:do_run
call :step "Running NohBoard from sources"
dotnet run --project "%APP_PROJECT%" -c %CONFIG% || goto :fail
goto :ok

rem ============================================================================
:do_clean
call :step "Cleaning bin/ and obj/ folders"
for /d /r "NohBoard" %%d in (bin obj) do (
    if exist "%%d" (
        echo   removing %%d
        rmdir /s /q "%%d"
    )
)
goto :ok

rem ============================================================================
rem  Helpers
rem ============================================================================

:publish_profile
set "PROFILE=%~1"
dotnet publish "%APP_PROJECT%" -p:PublishProfile=%PROFILE% --nologo
exit /b %ERRORLEVEL%

:step
echo.
echo === %~1 ===
echo.
exit /b 0

:report_artifacts
echo.
echo Build artifacts:
if exist "%PUBLISH_OUT%\win-x64-selfcontained\NohBoard.exe" (
    echo   self-contained        : %PUBLISH_OUT%\win-x64-selfcontained\NohBoard.exe
)
if exist "%PUBLISH_OUT%\win-x64-framework-dependent\NohBoard.exe" (
    echo   framework-dependent   : %PUBLISH_OUT%\win-x64-framework-dependent\NohBoard.exe
)
exit /b 0

rem ============================================================================
:show_help
echo.
echo NohBoard build helper
echo.
echo Usage:
echo   build.bat                 Restore, build, test, and publish both EXEs (default).
echo   build.bat build           Just restore and build in Release.
echo   build.bat test            Build and run the xUnit test suite.
echo   build.bat publish         Publish both self-contained and framework-dependent EXEs.
echo   build.bat publish-sc      Publish only the self-contained single-file EXE.
echo   build.bat publish-fd      Publish only the framework-dependent single-file EXE.
echo   build.bat run             Run NohBoard from source via 'dotnet run'.
echo   build.bat clean           Remove every bin/ and obj/ folder.
echo   build.bat help            Show this message.
echo.
echo Outputs land under %PUBLISH_OUT%\^<profile^>\NohBoard.exe.
echo.
goto :ok

rem ============================================================================
:ok
popd >nul
endlocal
exit /b 0

:fail
echo.
echo *** BUILD FAILED ***
popd >nul
endlocal
exit /b 1
