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