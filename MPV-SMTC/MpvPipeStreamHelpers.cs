using System;
using Microsoft.CSharp.RuntimeBinder;
using Serilog;

namespace MPVSMTC
{
    class MpvPipeStreamHelpers
    {
        public static T GetPropertyDataFromResponse<T>(dynamic obj, T defautv)
        {
            
            string error = obj.GetType().GetProperty("error");
            if(!(error is null) && error != "success")
            {
                return defautv;
            }

            try
            {
                T ret = obj.data;
                if (ret is null)
                {
                    return defautv;
                }
                return ret;
            }
            catch (RuntimeBinderException)
            {
                Log.Verbose(
                    "Failed to extract data of type {0} from |{1}|, returning |{2}| instead",
                    defautv.GetType().FullName, obj, defautv.ToString());
                return defautv;
            }
        }

        public static T GetPropertyDataFromResponse<T>(dynamic obj) where T : new()
        {
            return GetPropertyDataFromResponse<T>(obj, new T());
        }
    }
}