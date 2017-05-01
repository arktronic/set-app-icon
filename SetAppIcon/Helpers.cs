using System;
using System.Runtime.InteropServices;

namespace SetAppIcon
{
    static class Helpers
    {
        public static bool IsIntResource(IntPtr value)
        {
            if (((uint)value.ToInt32()) > ushort.MaxValue)
                return false;

            return true;
        }

        public static uint GetResourceId(IntPtr value)
        {
            return (uint)value;
        }

        public static string GetResourceName(IntPtr value)
        {
            if (IsIntResource(value))
                return value.ToString();

            return Marshal.PtrToStringUni(value);
        }
    }
}
