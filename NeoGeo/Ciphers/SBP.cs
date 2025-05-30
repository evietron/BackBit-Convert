using System;

public class SBP
{
    public static byte[] Decrypt(byte[] rom)
    {
        rom[0x2a6f8] = rom[0x2a6fa] = rom[0x2a6fc] = 0x71;
        rom[0x2a6f9] = rom[0x2a6fb] = rom[0x2a6fd] = 0x4e;
        rom[0x3ff2c] = 0x01;
        rom[0x3ff2d] = 0x70;

        byte[] buf = new byte[0x80000];
        Buffer.BlockCopy(rom, 0, buf, 0, 0x80000);

        for (int i = 0x200; i < 0x2000; i++)
        {
            if (i != 0xd5e && i != 0xd5f)
            {
                byte b = buf[i];
                buf[i] = (byte)((b >> 4) | (b << 4));
            }
        }

        rom = new byte[0x100000];
        Buffer.BlockCopy(buf, 0, rom, 0x000000, 0x80000);
        Buffer.BlockCopy(buf, 0, rom, 0x080000, 0x80000);

        return rom;
    }
}
