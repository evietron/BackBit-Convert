// Contains parts from
// https://raw.githubusercontent.com/mamedev/mame/master/src/devices/bus/neogeo/prot_kof98.cpp
// license:BSD-3-Clause
// copyright-holders:S. Smith,David Haywood,Fabio Priuli

using System;

public class ProtKOF98
{
    public static byte[] Decrypt(byte[] rom)
    {
        byte[] buf = new byte[0x500000];
        Buffer.BlockCopy(rom, 0, buf, 0, 0x100000);
        Buffer.BlockCopy(rom, 0x200000, buf, 0x100000, 0x400000);
        for (int i = 0; i < 0x100000; i++)
        {
            if ((i & 0xff800) != 0 && (i & 0xfe) != 0)
            {
                if ((i & 0xfc) == 0)
                {
                    buf[i] = rom[0x100000 | (i & 0xffffd)];
                } 
                else if ((i & 0x80000) != 0 && (((i >> 3) & 1) == ((i >> 1) & 1)))
                {
                    buf[i] = rom[i ^ ((i >> 10) & 0x100)];
                } 
                else
                {
                    buf[i] = rom[(((i << 17) ^ (i << 19)) & 0x100000) + ((i & 0xffffd) ^ 0x100) + ((i >> 2) & 2)];
                }
            } 
        }
        return buf;
    }
}
