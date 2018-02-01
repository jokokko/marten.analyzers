@echo off
cls
If Not Exist tools\FAKE\tools\fake.exe nuget.exe Install FAKE -Source "https://www.nuget.org/api/v2/" -OutputDirectory "tools" -ExcludeVersion
tools\FAKE\tools\fake.exe build.fsx %*
