using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class PageInstructions : List<ImageInstructionGroup>
    {
        public PageInstructions(IList<ImageInstructionGroup> imageInstructionGroups) : base(imageInstructionGroups) { }

        public IEnumerable<(string, Instructions)> Flatten() {
            foreach (var group in this) {
                foreach (var image in group.Images)
                {
                    yield return (image, group.Instructions);
                }
            }
        }
    }
}
