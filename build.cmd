@echo off
setlocal

echo Building MDEdit...
echo.

:: Clean previous builds
dotnet clean -c Release -v q

:: Build the solution (application + installer)
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Build failed!
    exit /b 1
)

:: Publish self-contained executable
dotnet publish src/MDEdit.csproj -c Release -r win-x64 --self-contained -o publish
if errorlevel 1 (
    echo.
    echo Publish failed!
    exit /b 1
)

echo.
echo Build completed successfully!
echo.
echo Outputs:
echo   Application: src\bin\Release\net8.0-windows\MDEdit.exe
echo   Self-contained: publish\MDEdit.exe
echo   MSI Installer: installer\bin\Release\MDEdit.Installer.msi
