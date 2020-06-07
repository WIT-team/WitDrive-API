using MDBFS.Filesystem.Models;
using MDBFS.Misc;

namespace MDBFS.Filesystem.Streams
{
    static class ElementExtFile
    {
        internal static void IncreaseLength(this Element elem, long count)
        {
            if (!elem.Metadata.ContainsKey(nameof(EMatadataKeys.Length)))
            {
                elem.Metadata[nameof(EMatadataKeys.Length)] = count;
            }
            else
            {
                elem.Metadata[nameof(EMatadataKeys.Length)] =((long) elem.Metadata[nameof(EMatadataKeys.Length)]) + count;
            }
        }
        internal static void IncreaseLength(this Element elem, int count)
        {
            if (!elem.Metadata.ContainsKey(nameof(EMatadataKeys.Length)))
            {
                elem.Metadata[nameof(EMatadataKeys.Length)] = (long) count;
            }
            else
            {
                elem.Metadata[nameof(EMatadataKeys.Length)] =((long) elem.Metadata[nameof(EMatadataKeys.Length)]) + count;
            }
        }
        internal static long GetLength(this Element elem)
        {
            if (!elem.Metadata.ContainsKey(nameof(EMatadataKeys.Length)))
            {
                return 0L;
            }
            else
            {
                return (long) elem.Metadata[nameof(EMatadataKeys.Length)];
            }
        }
    }
}