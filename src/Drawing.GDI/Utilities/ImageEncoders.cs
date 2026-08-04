using System.Collections.Concurrent;
using System.Drawing.Imaging;

namespace Regira.Drawing.GDI.Utilities;

public class ImageEncoders
{
    //http://stackoverflow.com/questions/249587/high-quality-image-scaling-library#353222
    private static IDictionary<string, ImageCodecInfo>? _encoders;
    private static readonly object EncodersLock = new();
    private static IDictionary<string, ImageCodecInfo> Data
    {
        //get accessor that creates the dictionary on demand
        get
        {
            //if the quick lookup isn't initialised, initialise it
            if (_encoders == null)
            {
                //protect against concurrency issues
                lock (EncodersLock)
                {
                    //check again, we might not have been the first person to acquire the lock (see the double checked lock pattern)
                    if (_encoders == null)
                    {
                        _encoders = new ConcurrentDictionary<string, ImageCodecInfo>(StringComparer.InvariantCultureIgnoreCase);

                        //get all the codecs
                        foreach (var codec in ImageCodecInfo.GetImageEncoders())
                        {
                            //add each codec to the quick lookup
                            _encoders.Add(codec.MimeType!.ToLower(), codec);
                        }
                    }
                }
            }

            //return the lookup
            return _encoders;
        }
    }

    public ImageCodecInfo this[string key]
    {
        get
        {
            lock (EncodersLock)
            {
                return Data[key];
            }
        }
    }

    public bool ContainsKey(string key)
    {
        lock (EncodersLock)
        {
            return Data.ContainsKey(key);
        }
    }
    public bool TryGetValue(string key, out ImageCodecInfo? value)
    {
        lock (EncodersLock)
        {
            return Data.TryGetValue(key, out value);
        }
    }
}