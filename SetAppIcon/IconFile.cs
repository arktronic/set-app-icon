using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SetAppIcon
{
    public class IconFile
    {
        private ICONDIR IconDir = new ICONDIR();
        private ICONDIRENTRY[] IconEntry;
        private byte[][] IconImage;

        private IconFile()
        {
        }

        public int GetImageCount()
        {
            return IconDir.idCount;
        }

        public byte[] GetImageData(int index)
        {
            return IconImage[index];
        }

        public static IconFile LoadFromFile(string filename)
        {
            var instance = new IconFile();

            var bytes = File.ReadAllBytes(filename);
            var pinnedBytes = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            instance.IconDir = Marshal.PtrToStructure<ICONDIR>(pinnedBytes.AddrOfPinnedObject());
            instance.IconEntry = new ICONDIRENTRY[instance.IconDir.idCount];
            instance.IconImage = new byte[instance.IconDir.idCount][];

            var offset = Marshal.SizeOf<ICONDIR>();
            var icondirentrySize = Marshal.SizeOf<ICONDIRENTRY>();
            for (int i = 0; i < instance.IconDir.idCount; i++)
            {
                var entry = Marshal.PtrToStructure<ICONDIRENTRY>(new IntPtr(pinnedBytes.AddrOfPinnedObject().ToInt64() + offset));
                instance.IconEntry[i] = entry;
                instance.IconImage[i] = new byte[entry.dwBytesInRes];
                Buffer.BlockCopy(bytes, (int)entry.dwImageOffset, instance.IconImage[i], 0, (int)entry.dwBytesInRes);

                offset += icondirentrySize;
            }

            pinnedBytes.Free();

            return instance;
        }

        public byte[] CreateIconGroupData(uint iconBaseID)
        {
            var igdSize = Marshal.SizeOf<ICONDIR>() + Marshal.SizeOf<GRPICONDIRENTRY>() * GetImageCount();
            var data = new byte[igdSize];
            var pinnedData = GCHandle.Alloc(data, GCHandleType.Pinned);
            Marshal.StructureToPtr(IconDir, pinnedData.AddrOfPinnedObject(), false);

            var offset = Marshal.SizeOf<ICONDIR>();
            for (int i = 0; i < GetImageCount(); i++)
            {
                var header = new BITMAPINFOHEADER();
                var pinnedHeader = GCHandle.Alloc(header, GCHandleType.Pinned);
                Marshal.Copy(GetImageData(i), 0, pinnedHeader.AddrOfPinnedObject(), Marshal.SizeOf<BITMAPINFOHEADER>());
                pinnedHeader.Free();

                var entry = new GRPICONDIRENTRY()
                {
                    bWidth = IconEntry[i].bWidth,
                    bHeight = IconEntry[i].bHeight,
                    bColorCount = IconEntry[i].bColorCount,
                    bReserved = IconEntry[i].bReserved,
                    wPlanes = header.biPlanes,
                    wBitCount = header.biBitCount,
                    dwBytesInRes = IconEntry[i].dwBytesInRes,
                    nID = (ushort)(iconBaseID + i)
                };

                Marshal.StructureToPtr(entry, new IntPtr(pinnedData.AddrOfPinnedObject().ToInt64() + offset), false);

                offset += Marshal.SizeOf<GRPICONDIRENTRY>();
            }

            pinnedData.Free();

            return data;
        }
    }
}
