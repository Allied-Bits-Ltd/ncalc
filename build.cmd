@echo off

set MSBUILDRIR=C:\Program Files\Microsoft Visual Studio\2022\Professional\Msbuild\Current\Bin

"%MSBUILDRIR%\msbuild.exe" src\NCalc.Core\NCalc.Core.csproj  /p:Configuration=Release /p:TargetFramework=net462
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Core\NCalc.Core.csproj  /p:Configuration=Release /p:TargetFramework=netstandard2.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Core\NCalc.Core.csproj  /p:Configuration=Release /p:TargetFramework=net8.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Core\NCalc.Core.csproj  /p:Configuration=Release /p:TargetFramework=net9.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Sync\NCalc.Sync.csproj  /p:Configuration=Release /p:TargetFramework=net462
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Sync\NCalc.Sync.csproj  /p:Configuration=Release /p:TargetFramework=netstandard2.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Sync\NCalc.Sync.csproj  /p:Configuration=Release /p:TargetFramework=net8.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Sync\NCalc.Sync.csproj  /p:Configuration=Release /p:TargetFramework=net9.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Async\NCalc.Async.csproj /p:Configuration=Release /p:TargetFramework=net462
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Async\NCalc.Async.csproj /p:Configuration=Release /p:TargetFramework=netstandard2.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Async\NCalc.Async.csproj /p:Configuration=Release /p:TargetFramework=net8.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.Async\NCalc.Async.csproj /p:Configuration=Release /p:TargetFramework=net9.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.DependencyInjection\NCalc.DependencyInjection.csproj /p:Configuration=Release /p:TargetFramework=net462
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.DependencyInjection\NCalc.DependencyInjection.csproj /p:Configuration=Release /p:TargetFramework=netstandard2.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.DependencyInjection\NCalc.DependencyInjection.csproj /p:Configuration=Release /p:TargetFramework=net8.0
if errorlevel 1 goto error
"%MSBUILDRIR%\msbuild.exe" src\NCalc.DependencyInjection\NCalc.DependencyInjection.csproj /p:Configuration=Release /p:TargetFramework=net9.0
if errorlevel 1 goto error

nuget pack NCalc.nuspec

exit /b 0

:error
echo Error occurred!
pause
exit /b 1