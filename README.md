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

This element has a list of attributes that can be provided for detailed control plugin behaviour.

| Parameter | Description | Default value |
| --------- | ----------- | ------------- |
| connectionName | Name of connection to Azure blob storage in `connectionStrings.config` | `"ResizerAzureBlobs"`
| containerName | Name of the container where cache blobs are to be stored | `"imagecache"` |
| timeoutSeconds | Seconds before attempt to fetch cache item is aborted | 5 |
| memoryStoreLimitMb | If provided, a memory cache is created in which recent cache items are kept (increased performance) |
| memoryStorePollingInterval | If a memory cache exists, the memory cache will be cleaned in the given interval | `"00:04:01"` |
| indexMaxSizeMb | If provided, the size of the index will be monitored and cleaned so not to exceed given size |
| indexMaxItems | If provided, the items in index will be monitored and cleaned so not to exceed given count |
| _(index database connection name)_ | (Not configurable) Index monitoring uses SQL, if index is set up, this is the name of the connection string to the created EF context | `"ResizerEFConnection"`

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

For this plugin to work with Optimizely (formerly EPiServer), at least version `7.3.0` of `ImageResizer.Plugins.EPiServerBlobReader` needs to be installed.

### Advanced topics

- [Index maintenance](/docs/indexmaintenance.md)

## Package maintainer

https://github.com/svenrog

## Changelog

[Changelog](CHANGELOG.md)