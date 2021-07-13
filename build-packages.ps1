cd .\.nuget

.\nuget.exe pack ..\src\ImageResizer.Plugins.AzureBlobCache\ImageResizer.Plugins.AzureBlobCache.nuspec -Properties Configuration=Release
cd ..\