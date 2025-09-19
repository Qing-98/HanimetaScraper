@echo off
echo 正在启动新架构抓取器测试...
echo.

REM 检查是否已安装 Playwright 浏览器
if not exist "bin\Debug\net8.0\playwright.ps1" (
    echo 首次运行，需要安装 Playwright 浏览器...
    dotnet build
    if exist "bin\Debug\net8.0\playwright.ps1" (
        echo 正在安装 Playwright 浏览器...
        powershell -ExecutionPolicy Bypass -File "bin\Debug\net8.0\playwright.ps1" install
    ) else (
        echo 错误：无法找到 Playwright 安装脚本
        pause
        exit /b 1
    )
)

echo 启动测试程序...
dotnet run

pause