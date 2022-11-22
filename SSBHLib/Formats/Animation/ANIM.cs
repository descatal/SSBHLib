using System.Collections.Generic;

namespace SSBHLib.Formats.Animation
{
    [SsbhFile("MINA")]
    public class Anim : SsbhFile
    {
        public uint Magic { get; set; } = 0x414E494D;

        public ushort VersionMajor { get; set; } = 0x0002;

        public ushort VersionMinor { get; set; } = 0x0000;

        public string Version { get; set; } // string version of version major and version minor, e.g. 1.2

        /// <summary>
        /// The <see cref="AnimTrack.FrameCount"/> will be in the inclusive range 1 to <see cref="FinalFrameIndex"/> + 1.
        /// </summary>
        public float FinalFrameIndex { get; set; }

        public ushort Unk1 { get; set; } = 1;

        public ushort Unk2 { get; set; } = 3;

        public string Name { get; set; }

        public AnimGroup[] Animations { get; set; }

        public byte[] Buffer { get; set; }

        // v1.2
        public ulong Unk_V12_1 { get; set; }
        public float Unk_V12_2 { get; set; }
        public float Unk_V12_3 { get; set; }
        public float Unk_V12_4 { get; set; }
        public AnimTrackV12[] Track_V12 { get; set; }
        public List<byte[]> Buffers_V12 { get; set; }
    }
}
