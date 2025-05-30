// Contains parts from
// https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/neogeo/prot_pvc.cpp
// license:BSD-3-Clause
// copyright-holders:S. Smith,David Haywood,Fabio Priuli

using System;

public class ProtPVC
{
    public static void SwapWords(byte[] rom, int romLength)
    {
        byte[] buf = new byte[romLength];
        Buffer.BlockCopy(rom, 0, buf, 0, romLength);
        for (int i = 0; i < romLength / 4; i++)
        {
            rom[i * 4 + 0] = buf[i * 2 + 0];
            rom[i * 4 + 1] = buf[i * 2 + 1];
            rom[i * 4 + 2] = buf[(romLength / 2) + i * 2 + 0];
            rom[i * 4 + 3] = buf[(romLength / 2) + i * 2 + 1];
        }
    }

    static readonly byte[] mslug5_xor1 = {
        0xc2, 0x4b, 0x74, 0xfd, 0x0b, 0x34, 0xeb, 0xd7, 0x10, 0x6d, 0xf9, 0xce, 0x5d, 0xd5, 0x61, 0x29,
        0xf5, 0xbe, 0x0d, 0x82, 0x72, 0x45, 0x0f, 0x24, 0xb3, 0x34, 0x1b, 0x99, 0xea, 0x09, 0xf3, 0x03 };
    static readonly byte[] mslug5_xor2 = {
        0x36, 0x09, 0xb0, 0x64, 0x95, 0x0f, 0x90, 0x42, 0x6e, 0x0f, 0x30, 0xf6, 0xe5, 0x08, 0x30, 0x64,
        0x08, 0x04, 0x00, 0x2f, 0x72, 0x09, 0xa0, 0x13, 0xc9, 0x0b, 0xa0, 0x3e, 0xc2, 0x00, 0x40, 0x2b };

    public static void MSlug5Decrypt(byte[] rom)
    {
        byte[] buf = new byte[rom.Length];
        SwapWords(rom, rom.Length);

        for (int i = 0; i < 0x100000; i++)
        {
            rom[i] ^= mslug5_xor1[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i++) {
            rom[i] ^= mslug5_xor2[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i += 4)
        {
            ushort rom16 = (ushort)(rom[i + 1] | (rom[i + 2] << 8));
            rom16 = (ushort)(
                (rom16 & 0xf00f) |
                ((rom16 & 0x0aa0) >> 1) |
                ((rom16 & 0x0550) << 1));
            rom[i + 1] = (byte)(rom16);
            rom[i + 2] = (byte)(rom16 >> 8);
        }

        Buffer.BlockCopy(rom, 0, buf, 0, rom.Length);
        for (int i = 0; i < 0x0100000 / 0x10000; i++)
        {
            int ofst = (i & 0xf0) |
                ((i & 0x0c) >> 2) |
                ((i & 0x03) << 2);
            Buffer.BlockCopy(buf, ofst * 0x10000, rom, i * 0x10000, 0x10000);
        }

        for (int i = 0x100000; i < 0x800000; i += 0x100)
        {
            int ofst = (i & 0xf000ff) |
                ((i & 0x000f00) ^ 0x00700) |
                ((i & 0x0cc000) >> 2) |
                ((i & 0x033000) << 2);
            Buffer.BlockCopy(buf, ofst, rom, i, 0x100);
        }
        Buffer.BlockCopy(rom, 0x000000, buf, 0x000000, rom.Length);
        Buffer.BlockCopy(buf, 0x700000, rom, 0x100000, 0x100000);
        Buffer.BlockCopy(buf, 0x100000, rom, 0x200000, 0x600000);
    }

    static readonly byte[] svc_xor1 = {
        0x3b, 0x6a, 0xf7, 0xb7, 0xe8, 0xa9, 0x20, 0x99, 0x9f, 0x39, 0x34, 0x0c, 0xc3, 0x9a, 0xa5, 0xc8,
        0xb8, 0x18, 0xce, 0x56, 0x94, 0x44, 0xe3, 0x7a, 0xf7, 0xdd, 0x42, 0xf0, 0x18, 0x60, 0x92, 0x9f };
    static readonly byte[] svc_xor2 = {
        0x69, 0x0b, 0x60, 0xd6, 0x4f, 0x01, 0x40, 0x1a, 0x9f, 0x0b, 0xf0, 0x75, 0x58, 0x0e, 0x60, 0xb4,
        0x14, 0x04, 0x20, 0xe4, 0xb9, 0x0d, 0x10, 0x89, 0xeb, 0x07, 0x30, 0x90, 0x50, 0x0e, 0x20, 0x26 };

    public static void SVCDecrypt(byte[] rom)
    {
        byte[] buf = new byte[rom.Length];
        SwapWords(rom, rom.Length);

        for (int i = 0; i < 0x100000; i++)
        {
            rom[i] ^= svc_xor1[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i++)
        {
            rom[i] ^= svc_xor2[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i += 4)
        {
            ushort rom16 = (ushort)(rom[i + 1] | (rom[i + 2] << 8));
            rom16 = (ushort)(
                (rom16 & 0xf00f) |
                ((rom16 & 0x0aa0) >> 1) |
                ((rom16 & 0x0550) << 1));
            rom[i + 1] = (byte)(rom16);
            rom[i + 2] = (byte)(rom16 >> 8);
        }

        Buffer.BlockCopy(rom, 0, buf, 0, rom.Length);
        for (int i = 0; i < 0x0100000 / 0x10000; i++)
        {
            int ofst = (i & 0xf0) |
                ((i & 0x0a) >> 1) |
                ((i & 0x05) << 1);
            Buffer.BlockCopy(buf, ofst * 0x10000, rom, i * 0x10000, 0x10000);
        }

        for (int i = 0x100000; i < 0x800000; i += 0x100)
        {
            int ofst = (i & 0xf000ff) |
                ((i & 0x000f00) ^ 0x00a00) |
                ((i & 0x080000) >> 3) |
                ((i & 0x040000) >> 1) |
                ((i & 0x020000) << 1) |
                ((i & 0x010000) << 3) |
                ((i & 0x00c000) >> 2) |
                ((i & 0x003000) << 2);
            Buffer.BlockCopy(buf, ofst, rom, i, 0x100);
        }
        Buffer.BlockCopy(rom, 0x000000, buf, 0x000000, rom.Length);
        Buffer.BlockCopy(buf, 0x700000, rom, 0x100000, 0x100000);
        Buffer.BlockCopy(buf, 0x100000, rom, 0x200000, 0x600000);
    }

    static readonly byte[] kof2003_xor1 = {
        0x3b, 0x6a, 0xf7, 0xb7, 0xe8, 0xa9, 0x20, 0x99, 0x9f, 0x39, 0x34, 0x0c, 0xc3, 0x9a, 0xa5, 0xc8,
        0xb8, 0x18, 0xce, 0x56, 0x94, 0x44, 0xe3, 0x7a, 0xf7, 0xdd, 0x42, 0xf0, 0x18, 0x60, 0x92, 0x9f };
    static readonly byte[] kof2003_xor2 = {
        0x2f, 0x02, 0x60, 0xbb, 0x77, 0x01, 0x30, 0x08, 0xd8, 0x01, 0xa0, 0xdf, 0x37, 0x0a, 0xf0, 0x65,
        0x28, 0x03, 0xd0, 0x23, 0xd3, 0x03, 0x70, 0x42, 0xbb, 0x06, 0xf0, 0x28, 0xba, 0x0f, 0xf0, 0x7a };

    public static byte[] KOF2003Decrypt(byte[] rom)
    {
        byte[] buf = new byte[rom.Length];
        SwapWords(rom, 0x800000);

        for (int i = 0; i < 0x100000; i++)
        {
            rom[0x800000 + i] ^= rom[0x100002 | i];
        }

        for (int i = 0; i < 0x100000; i++)
        {
            rom[i] ^= kof2003_xor1[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i++)
        {
            rom[i] ^= kof2003_xor2[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i += 4)
        {
            ushort rom16 = (ushort)(rom[i + 1] | (rom[i + 2] << 8));
            rom16 = (ushort)(
                (rom16 & 0xf00f) |
                ((rom16 & 0x0c00) >> 6) |
                ((rom16 & 0x0300) >> 2) |
                ((rom16 & 0x00c0) << 2) |
                ((rom16 & 0x0030) << 6));
            rom[i + 1] = (byte)(rom16);
            rom[i + 2] = (byte)(rom16 >> 8);
        }

        for (int i = 0; i < 0x0100000 / 0x10000; i++)
        {
            int ofst = (i & 0xf0) |
                ((i & 0x08) >> 3) |
                ((i & 0x04) >> 1) |
                ((i & 0x02) << 1) |
                ((i & 0x01) << 3);
            Buffer.BlockCopy(rom, ofst * 0x10000, buf, i * 0x10000, 0x10000);
        }

        for (int i = 0x100000; i < 0x900000; i += 0x100)
        {
            int ofst = (i & 0xf000ff) |
                ((i & 0x000f00) ^ 0x00800) |
                ((i & 0x080000) >> 3) |
                ((i & 0x040000) >> 1) |
                ((i & 0x020000) << 1) |
                ((i & 0x010000) << 3) |
                ((i & 0x00c000) >> 2) |
                ((i & 0x003000) << 2);
            Buffer.BlockCopy(rom, ofst, buf, i, 0x100);
        }
        Buffer.BlockCopy(buf, 0x000000, rom, 0x000000, 0x100000);
        Buffer.BlockCopy(buf, 0x800000, rom, 0x100000, 0x100000);
        Buffer.BlockCopy(buf, 0x100000, rom, 0x200000, 0x700000);
        buf = new byte[0x800000];
        Buffer.BlockCopy(rom, 0x000000, buf, 0x000000, 0x800000);
        return buf;
    }

    static readonly byte[] kof2003h_xor1 = {
        0xc2, 0x4b, 0x74, 0xfd, 0x0b, 0x34, 0xeb, 0xd7, 0x10, 0x6d, 0xf9, 0xce, 0x5d, 0xd5, 0x61, 0x29,
        0xf5, 0xbe, 0x0d, 0x82, 0x72, 0x45, 0x0f, 0x24, 0xb3, 0x34, 0x1b, 0x99, 0xea, 0x09, 0xf3, 0x03 };
    static readonly byte[] kof2003h_xor2 = {
        0x2b, 0x09, 0xd0, 0x7f, 0x51, 0x0b, 0x10, 0x4c, 0x5b, 0x07, 0x70, 0x9d, 0x3e, 0x0b, 0xb0, 0xb6,
        0x54, 0x09, 0xe0, 0xcc, 0x3d, 0x0d, 0x80, 0x99, 0x87, 0x03, 0x90, 0x82, 0xfe, 0x04, 0x20, 0x18 };

    public static byte[] KOF2003HDecrypt(byte[] rom)
    {
        byte[] buf = new byte[rom.Length];
        SwapWords(rom, 0x800000);

        for (int i = 0; i < 0x100000; i++)
        {
            rom[0x800000 + i] ^= rom[0x100002 | i];
        }

        for (int i = 0; i < 0x100000; i++)
        {
            rom[i] ^= kof2003h_xor1[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i++)
        {
            rom[i] ^= kof2003h_xor2[i % 0x20];
        }

        for (int i = 0x100000; i < 0x800000; i += 4)
        {
            ushort rom16 = (ushort)(rom[i + 1] | (rom[i + 2] << 8));
            rom16 = (ushort)(
                (rom16 & 0xf00f) |
                ((rom16 & 0x0aa0) >> 1) |
                ((rom16 & 0x0550) << 1));
            rom[i + 1] = (byte)(rom16);
            rom[i + 2] = (byte)(rom16 >> 8);
        }

        for (int i = 0; i < 0x0100000 / 0x10000; i++)
        {
            int ofst = (i & 0xf0) |
                ((i & 0x0c) >> 2) |
                ((i & 0x03) << 2);
            Buffer.BlockCopy(rom, ofst * 0x10000, buf, i * 0x10000, 0x10000);
        }

        for (int i = 0x100000; i < 0x900000; i += 0x100)
        {
            int ofst = (i & 0xf000ff) |
                ((i & 0x000f00) ^ 0x00400) |
                ((i & 0x0a4000) >> 1) |
                ((i & 0x052000) << 1) |
                ((i & 0x008000) >> 3) |
                ((i & 0x001000) << 3);
            Buffer.BlockCopy(rom, ofst, buf, i, 0x100);
        }
        Buffer.BlockCopy(buf, 0x000000, rom, 0x000000, 0x100000);
        Buffer.BlockCopy(buf, 0x800000, rom, 0x100000, 0x100000);
        Buffer.BlockCopy(buf, 0x100000, rom, 0x200000, 0x700000);
        buf = new byte[0x800000];
        Buffer.BlockCopy(rom, 0x000000, buf, 0x000000, 0x800000);
        return buf;
    }
}
