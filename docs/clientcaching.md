# Client caching and the async pipeline

As you may, or may not know, the async pipeline (required by this plugin) has [no client caching support (since 2016)](https://github.com/imazen/resizer/issues/166) due to the sunsetting of ImageResizer as an image processing module. This means that _Expires_ headers wouldn't normally not be set even though you have a `<clientcache>` element in your `web.config`. If this is a major hurdle for you to justify switching to async processing, read further...

## This plugin handles that

Until there is support for client caching for the async pipeline in ImageResizer while using `ImageResizer.AsyncInterceptModule` we've provided logic that adds those headers back in the meantime.