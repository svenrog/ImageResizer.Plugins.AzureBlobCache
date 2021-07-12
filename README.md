# ImageResizer.Plugins.AzureBlobCache

## Description

Caching for resized images in blob storage

## How to get started?

Requires ImageResizer 4.0.1 or above

- `install-package ImageResizer.Plugins.AzureBlobCache`

The following will be added to `web.config`

```
<configuration>
    <resizer>
        <plugins>
            <add name="AzureBlobCache" />
        </plugins>
    </resizer>    
</configuration>
```

This configration element has a list of attributes that can be provided for additional control over the plugin behaviour.

| Parameter | Description | Default value |
| --------- | ----------- | ------------- |
| connectionName | Name of connection to Azure blob storage in `connectionStrings.config` | `"ResizerAzureBlobs"`
| containerName | Name of the container where cache blobs are to be stored | `"imagecache"` |
| timeoutSeconds | Seconds before attempt to fetch cache item is aborted | 5 |
| indexMaxSizeMb | If provided, the size of the index will be monitored and cleaned so not to exceed given size |
| indexMaxItems | If provided, the items in index will be monitored and cleaned so not to exceed given count |
| indexConnectionName | Index monitoring uses SQL, if index is set up, this is the name of the connection to the created EF context | `"ResizerEFConnection"`
| memoryStoreLimitMb | If provided, a memory cache is created in which recent cache items are kept (increased performance) |
| memoryStorePollingInterval | If a memory cache exists, the memory cache will be cleaned in the given interval | `"00:04:01"` |

## Important requirements

Requires ImageResizer to be configured with `ImageResizer.AsyncInterceptModule`.
If `ImageResizer.InterceptModule` is configured, replace all instances of this 
```
 <add name="ImageResizingModule" type="ImageResizer.InterceptModule" />
```
with this
```
 <add name="ImageResizingModule" type="ImageResizer.AsyncInterceptModule" />
```

## Package maintainer

https://github.com/svenrog

## Changelog

[Changelog](CHANGELOG.md)