@echo off
echo ========================================
echo Hanime Provider Quick Test
echo ========================================
echo.
echo Building solution...
cd ..\..
dotnet clean > nul 2>&1
dotnet build --no-incremental > nul 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.
echo Running Hanime test (option 3)...
echo.

cd Test\NewScraperTest
echo 3| dotnet run

echo.
echo ========================================
echo Test completed
echo ========================================
pause
