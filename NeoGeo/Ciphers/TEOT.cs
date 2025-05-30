using System;

public class TEOT
{
    public static byte[] Pack(byte[] rom)
    {
        byte[] buf = new byte[0x800000];
        Buffer.BlockCopy(rom, 0x000000, buf, 0x000000, 0x100000);
        Buffer.BlockCopy(rom, 0x100000, buf, 0x100000, 0x0e0000);
        Buffer.BlockCopy(rom, 0x200000, buf, 0x1e0000, 0x1e0000);
        Buffer.BlockCopy(rom, 0x400000, buf, 0x3c0000, 0x280000);
        Buffer.BlockCopy(rom, 0x700000, buf, 0x640000, 0x0e0000);
        Buffer.BlockCopy(rom, 0x800000, buf, 0x720000, 0x0e0000);
        return buf;
    }
}
