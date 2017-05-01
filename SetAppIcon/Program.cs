using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SetAppIcon
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, LoadLibraryFlags dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumResourceNames(IntPtr hModule, uint lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumResourceLanguages(IntPtr hModule, IntPtr lpType, IntPtr lpName, EnumResLangProc lpEnumFunc, IntPtr lParam);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EnumResourceLanguages(IntPtr hModule, IntPtr lpType, [MarshalAs(UnmanagedType.LPTStr)] string lpName, EnumResLangProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumResLangProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, ushort wIDLanguage, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, [MarshalAs(UnmanagedType.LPTStr)] string lpName, ushort wLanguage, byte[] lpData, uint cbData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        private const uint ICON_BASE_ID = 1;

        private static string IconGroupName = null;
        private static IntPtr IconGroupId = IntPtr.Zero;

        private static List<ushort> IconGroupLanguages = new List<ushort> { 0 };

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                PrintUsage();
                return;
            }
            
            if (!File.Exists(args[0]) || !File.Exists(args[1]))
            {
                Console.WriteLine("Error: both files specified must exist.");
                PrintUsage();
                return;
            }

            DetermineIconGroupResource(args[0]);

            SetIcon(args[0], args[1]);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: " + Path.GetFileName(Assembly.GetExecutingAssembly().Location) + " <pe-file> <icon-file>");
        }

        private static void DetermineIconGroupResource(string pe)
        {
            var handle = LoadLibraryEx(pe, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE);

            if(handle == IntPtr.Zero)
            {
                Console.WriteLine("WARNING: Unable to load PE file for inspection");
                return;
            }

            EnumResourceNames(handle, (uint)ResourceType.RT_GROUP_ICON, new EnumResNameProc(EnumResNameCallback), IntPtr.Zero);

            DetermineResourceLanguages(handle);

            FreeLibrary(handle);
        }

        private static bool EnumResNameCallback(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam)
        {
            if (Helpers.IsIntResource(lpszName))
            {
                IconGroupId = lpszName;
                Console.WriteLine("First icon: " + IconGroupId);
            }
            else
            {
                IconGroupName = Helpers.GetResourceName(lpszName);
                Console.WriteLine($"First icon: \"{IconGroupName}\"");
            }
            return false;
        }

        private static void DetermineResourceLanguages(IntPtr handle)
        {
            bool result = true;
            if (IconGroupName != null)
            {
                IconGroupLanguages.Clear();
                result = EnumResourceLanguages(handle, new IntPtr((uint)ResourceType.RT_GROUP_ICON), IconGroupName, new EnumResLangProc(EnumResLangCallback), IntPtr.Zero);
            }
            else if (IconGroupId != IntPtr.Zero)
            {
                IconGroupLanguages.Clear();
                result = EnumResourceLanguages(handle, new IntPtr((uint)ResourceType.RT_GROUP_ICON), IconGroupId, new EnumResLangProc(EnumResLangCallback), IntPtr.Zero);
            }

            if (!result)
            {
                Console.WriteLine("Warning: Unable to determine icon resource languages: " + Marshal.GetLastWin32Error());
            }
        }

        private static bool EnumResLangCallback(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, ushort wIDLanguage, IntPtr lParam)
        {
            IconGroupLanguages.Add(wIDLanguage);
            return true;
        }

        private static void SetIcon(string pe, string ico)
        {
            var icon = IconFile.LoadFromFile(ico);
            Console.WriteLine($"Loaded icon with {icon.GetImageCount()} image(s).");

            var handle = BeginUpdateResource(pe, false);
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("Error: could not begin update: " + Marshal.GetLastWin32Error());
                return;
            }

            var iconData = icon.CreateIconGroupData(ICON_BASE_ID);

            foreach (var lang in IconGroupLanguages)
            {
                Console.WriteLine($"Updating for language {lang}...");

                bool res;
                if (IconGroupName != null)
                {
                    res = UpdateResource(handle, new IntPtr((int)ResourceType.RT_GROUP_ICON), IconGroupName, lang, iconData, (uint)iconData.Length);
                }
                else
                {
                    if (IconGroupId == IntPtr.Zero) IconGroupId = new IntPtr(1);
                    res = UpdateResource(handle, new IntPtr((int)ResourceType.RT_GROUP_ICON), IconGroupId, lang, iconData, (uint)iconData.Length);
                }

                if (!res) Console.Write("Warning: could not update group icon: " + Marshal.GetLastWin32Error());
                for (int i = 0; i < icon.GetImageCount(); i++)
                {
                    var image = icon.GetImageData(i);
                    res = UpdateResource(handle, new IntPtr((int)ResourceType.RT_ICON), new IntPtr(ICON_BASE_ID + i), lang, image, (uint)image.Length);
                    if (!res) Console.Write($"Warning: could not update image {i}: {Marshal.GetLastWin32Error()}");
                }
            }

            EndUpdateResource(handle, false);
            Console.WriteLine("Done.");
        }
    }
}
