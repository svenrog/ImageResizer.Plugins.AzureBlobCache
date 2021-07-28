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

## Additional configuration

For detailed settings an `<azureBlobCache>` element can be provided inside `<resizer>`

```
<configuration>
    <resizer>
        ...
        <azureBlobCache connectionName="ResizerAzureBlobs" memoryStoreLimitMb="200" indexMaxItems="200000" />
        <plugins>
            ...
            <add name="AzureBlobCache" />
        </plugins>
    </resizer>    
</configuration>
```

This element has a list of attributes that can be provided for detailed control over plugin behaviour.

| Parameter | Description | Default value |
| --------- | ----------- | ------------- |
| connectionName | Name of connection to Azure blob storage in `connectionStrings.config`. | `"ResizerAzureBlobs"`
| containerName | Name of the container where cache blobs are to be stored. | `"imagecache"` |
| timeoutSeconds | Seconds before attempt to fetch cache item is aborted (after which the underlying image is returned normally). | 5 |
| logging | If logging should be enabled or not, set to either `true` or `false` (requires either `ImageResizer.Plugins.Logging` or [`ImageResizer.Plugins.Log4net`](https://github.com/svenrog/ImageResizer.Plugins.Log4net)). | `false` |
| memoryStoreLimitMb | If provided, a memory cache is created, where recent cache items are kept (for increased performance). |
| memoryStoreSlidingExpiration | The [sliding expiration](https://peterdaugaardrasmussen.com/2017/10/02/c-emorycache-absolute-expiration-vs-sliding-expiration/#slidingexpiration) time of a cache item in [`System.TimeSpan` format](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings) (`"HH:MM:SS"`). | `"00:30:00"` |
| memoryStoreAbsoluteExpiration | The [absolute expiration](https://peterdaugaardrasmussen.com/2017/10/02/c-emorycache-absolute-expiration-vs-sliding-expiration/#absoluteexpiration) time of a cache item (overrides sliding expiration if provided `"HH:MM:SS"`). |
| memoryStorePollingInterval | If a memory cache exists, the memory cache will be updated in the given interval (`"HH:MM:SS"`). Additional details about memory cache configuration can be found [here](https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/namedcaches-element-cache-settings). | `"00:04:01"` |
| indexMaxSizeMb | If provided, the size of the index will be monitored and cleaned as not to exceed given size (in MB). |
| indexMaxItems | If provided, the items in index will be monitored and cleaned as not to exceed given count (of items). |
| indexPollingInterval | The interval in which the index max size should be checked (`"HH:MM:SS"`). | `"00:05:00"`
| _indexDatabaseConnectionName_ | _Not configurable._ The index monitoring uses SQL, if index is set up, this is the name of the connection string to the created EntityFramework context. | `"ResizerEFConnection"`

## Important requirements

### Only compatible with the async pipeline

Requires ImageResizer to be configured with `ImageResizer.AsyncInterceptModule`.

If `ImageResizer.InterceptModule` is configured, replace all instances of this 

```
 <add name="ImageResizingModule" type="ImageResizer.InterceptModule" />
```
with this
```
 <add name="ImageResizingModule" type="ImageResizer.AsyncInterceptModule" />
```

### EPiServerBlobReader support

For this plugin to work with Optimizely (formerly EPiServer), at least version `7.3.1` of `ImageResizer.Plugins.EPiServerBlobReader` needs to be installed.

### Advanced topics

- [Index maintenance](/docs/indexmaintenance.md)
- [Logging](/docs/logging.md)
- [Client caching normally missing from async pipeline](/docs/clientcaching.md)

## Package maintainer

https://github.com/svenrog

## Changelog

[Changelog](CHANGELOG.md)