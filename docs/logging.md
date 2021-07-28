# Logging

There are multiple choices for logging with ImageResizer, here are some configuration details.

## Logging with Nlog
This is the default solution, more information can be found about setting it up [here](https://imageresizing.net/docs/v4/plugins/logging).

- `install-package ImageResizer.Plugins.Logging`

## Logging with Log4net

More information can be found about setting it up [here](https://github.com/svenrog/ImageResizer.Plugins.Log4net/blob/master/README.md).

- `install-package ImageResizer.Plugins.Log4net`

If you don't have log4net configured via `web.config` or need to initialize it differently provide:

`<log4net configFile="log4net.config" />` inside your `<resizer>` element.

### Capturing to a separate log file
To capture all events inside a separate log file the `log4net.config` needs to be set up. Using the name `ImageResizer.Plugins.AzureBlobCache` ensures all log events from the cache are captured, this can by appending the following to configuration.
```
<log4net>
  ...
  <appender name="blobCacheAppender" type="log4net.Appender.RollingFileAppender" >
    <file value="App_Data\blobCache.log" />
    <encoding value="utf-8" />
    <staticLogFileName value="true"/>
    <datePattern value=".yyyyMMdd.'log'" />
    <rollingStyle value="Date" />
    <lockingModel type="log4net.Appender.FileAppender+MinimalLock" />
    <appendToFile value="true" />
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date [%thread] %level %logger: %message%n" />
    </layout>
  </appender>
  ...
  <logger name="ImageResizer.Plugins.AzureBlobCache">
    <level value="Debug" />
    <appender-ref ref="blobCacheAppender" />
  </logger>
</log4net>
```
