using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Exceptions;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Pages
{
    public abstract class PageBase
    {
        private readonly ICollection<string> _images;
        private readonly ICollection<ImageFormatDescriptor> _formats;

        protected PageBase(ICollection<string> images, ICollection<ImageFormatDescriptor> formats)
        {
            _images = images ?? throw new ArgumentNullException(nameof(images));
            _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        }

        public virtual IList<ImageInstructionGroup> GetImageGroups()
        {
            EnsureValid(_images, _formats);

            var images = new Queue<string>(_images);
            var absoluteCount = _formats.Sum(x => x.AbsoluteCount);
            var remainingRelative = images.Count - absoluteCount;
            var ratioTotal = _formats.Sum(x => x.Ratio);
            var groups = new List<ImageInstructionGroup>(_formats.Count);
            
            foreach (var format in _formats)
            {
                var count = format.AbsoluteCount > 0 ? format.AbsoluteCount : (int)Math.Min(1.0, remainingRelative * (ratioTotal / format.Ratio));
                var formatImages = new List<string>(count);

                for (var i = 0; i < count; i++)
                {
                    formatImages.Add(images.Dequeue());
                }

                groups.Add(new ImageInstructionGroup
                {
                    Images = formatImages,
                    Instructions = GetResizeInstructions(format.Options)
                });
            }

            return groups;
        }

        protected virtual Instructions GetResizeInstructions(ResizeOptions options)
        {
            var collection = new NameValueCollection
            {
                { "quality", options.Quality.ToString() }
            };

            if (!string.IsNullOrEmpty(options.Format))
                collection.Add("format", options.Format);

            if (options.Mode != FitMode.None)
            {
                collection.Add("mode", options.Mode.ToString());

                if (options.Width.HasValue)
                    collection.Add("width", options.Width.ToString());

                if (options.Height.HasValue)
                    collection.Add("height", options.Height.ToString());
            }
            else
            {
                if (options.Width.HasValue)
                    collection.Add("maxwidth", options.Width.ToString());

                if (options.Height.HasValue)
                    collection.Add("maxheight", options.Height.ToString());
            }

            return new Instructions(collection);
        }

        protected virtual void EnsureValid(ICollection<string> images, ICollection<ImageFormatDescriptor> formats)
        {
            var absoluteCount = formats.Sum(x => x.AbsoluteCount);

            if (images.Count < absoluteCount)
                throw new ImageFormatValidationException("There are not enough images to fulfill the absolute counts in formats.");

            var remainingRelative = images.Count - absoluteCount;
            var requiredRatioCount = formats.Count(x => x.Ratio > 0);

            if (remainingRelative < requiredRatioCount)
                throw new ImageFormatValidationException("There are not enough images to fulfill the ratios in formats.");
        }
    }
}
